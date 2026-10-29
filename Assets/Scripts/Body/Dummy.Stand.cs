using System.Collections.Generic;
using UnityEngine;

/// Em pé: equilíbrio, reação por região, passo de recuperação (ponto de captura), locomoção procedural
/// (andar/correr/mancar com IK nas pernas) e a "mente" do boneco: objetivo, medo, fuga, segurar a ferida.
public partial class Dummy
{
    // ---- pose em pé / equilíbrio (igual ao Godot)
    Xf standRoot = Xf.Identity;
    readonly Dictionary<string, Vector3> prevPos = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Quaternion> prevRot = new Dictionary<string, Quaternion>();
    float standTime, collapseTime = -1f;
    Vector3 flinch, flinchVel;
    public float balance = 1f;
    Vector3 stagger, staggerVel;
    float holdWound;
    public bool gettingUp;
    readonly Dictionary<string, Vector3> kicks = new Dictionary<string, Vector3>(), kickVel = new Dictionary<string, Vector3>();
    float buckle, painOver;
    public string fallReason = "";
    float stepT = -1f;
    Vector3 stepD;
    string stepFirst = "l";
    float getUpT, getUpCd = 3f;
    readonly Dictionary<string, Quaternion> getUpQ = new Dictionary<string, Quaternion>();
    Xf getUpRoot;

    // ---- mente: objetivo, medo, locomoção
    public float fear;                 // 0..1
    public float aimedAt;              // >0 enquanto o jogador mira nele
    public string mind = "parado";     // texto para o painel
    Vector3 goal;
    bool hasGoal, escaping, escaped;
    float idleTimer = 3f, clutchTimer, nextPainStop = 4f, struggle;
    float speed, gaitPhase, gaitAmp, heading;
    float breathPh, turnRate, lastHeading, gaitPh;
    string swingSide = "l";
    string clutchSide = "";
    Vector3 lookAt;
    bool looking;
    const float WALK_SPEED = 1.3f, RUN_SPEED = 3.2f, LIMP_SPEED = 0.55f;

    void InitBrain()
    {
        var fwd = standRoot.q * Vector3.forward;
        heading = Mathf.Atan2(fwd.x, fwd.z);
        idleTimer = U.RandRange(2f, 5f);
        nextPainStop = U.RandRange(3f, 5f);
    }

    static Xf RotAbout(Xf xf, Vector3 pivot, Quaternion b) => U.RotAbout(xf, pivot, b);

    /// Perna com ferida séria (artéria, machado, flecha ou sangrando muito)
    bool LegHurt(string side)
    {
        foreach (var id in new[] { "thigh_" + side, "shin_" + side, "foot_" + side })
        {
            if (!parts.TryGetValue(id, out var p) || !p) continue;
            foreach (var w in p.wounds)
                if (w.arterial || w.bleed > 3f || w.kind == "machado" || w.kind == "flecha") return true;
        }
        return false;
    }

    /// Ferida mais séria na perna (para segurar com as duas mãos)
    Wound WorstLegWound(out string side)
    {
        side = ""; Wound best = null; float br = 0.3f;
        foreach (var s in new[] { "l", "r" })
            foreach (var id in new[] { "thigh_" + s, "shin_" + s, "foot_" + s })
            {
                if (!parts.TryGetValue(id, out var p) || !p || p.severed) continue;
                foreach (var w in p.wounds)
                {
                    float r = w.ExternalRate() + w.pain * 6f + (w.arterial ? 4f : 0f);
                    if (r > br) { br = r; best = w; side = s; }
                }
            }
        return best;
    }

    /// Paredes do tronco acumulam dano; passando do limite, as vísceras saem.
    void WallDamage(DummyPart p, float amount, Vector3 lpos, Vector3 lnormal)
    {
        if (!(p.partId == "abdomen" || p.partId == "pelvis" || p.partId == "chest") || amount <= 0f) return;
        p.wall += amount;
        float thr = p.partId == "chest" ? 75f : 38f;
        if (p.wall >= thr * (p.guts + 1) && p.guts < Guts.MAX_PER_PART)
        {
            Guts.Spill(p, lpos, lnormal, p.wall / thr - p.guts);
            DamageOrgan(p, p.partId == "chest" ? "lung" : "gut", 35f);
            acutePain += 0.5f;
            balance -= 0.5f;
        }
    }

    // ------------------------------------------------------------------ IK

    /// IK de dois ossos: raiz s, junta do meio e, ponta w (pose de repouso) -> alvo. Retorna (rot 1º osso, rot extra do 2º).
    static (Quaternion qs, Quaternion qe) TwoBone(Vector3 s, Vector3 e, Vector3 w, Vector3 target, Vector3 pole, float extra = 0f)
    {
        float l1 = Vector3.Distance(s, e), l2 = Vector3.Distance(e, w) + extra;
        var to = target - s;
        float d = Mathf.Clamp(to.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.004f);
        var dir = to.normalized;
        var n = Vector3.Cross(dir, pole).normalized;
        if (n.magnitude < 0.5f) n = Vector3.right;
        float a1 = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f));
        var upDir = U.Axis(n, a1) * dir;
        var alt = U.Axis(n, -a1) * dir;
        if (Vector3.Dot(upDir, pole) < Vector3.Dot(alt, pole)) upDir = alt;
        var qs = Quaternion.FromToRotation((e - s).normalized, upDir);
        var mid = s + upDir * l1;
        var qe = Quaternion.FromToRotation(qs * (w - e).normalized, (s + dir * d - mid).normalized);
        return (qs, qe);
    }

    (Quaternion, Quaternion) ArmIK(string side, Vector3 target)
    {
        var defs = Anatomy.Parts();
        float sx = side == "l" ? 1f : -1f;
        return TwoBone(defs["upper_arm_" + side].joint.anchor, defs["forearm_" + side].joint.anchor,
            defs["hand_" + side].joint.anchor, target, new Vector3(sx, -0.2f, -0.6f).normalized, 0.07f);
    }

    // ------------------------------------------------------------------ reação por região

    /// Um golpe "chuta" a junta da parte atingida (mola que volta sozinha).
    void HitKick(DummyPart p, Vector3 dirW, float imp)
    {
        var dl = Quaternion.Inverse(standRoot.q) * dirW;
        string id = p.partId;
        if (id == "pelvis" || id == "abdomen") id = "abdomen";
        if (id.StartsWith("hand") || id.StartsWith("forearm")) id = "upper_arm" + id.Substring(id.Length - 2);
        if (id.StartsWith("thigh") || id.StartsWith("shin") || id.StartsWith("foot"))
        {
            buckle = Mathf.Min(buckle + ImpactConfig.LEG_BUCKLE * Mathf.Clamp(imp / 8f, 0.4f, 2f), 0.12f);
            // a dor na perna faz parar e segurar
            clutchTimer = Mathf.Max(clutchTimer, U.RandRange(1.8f, 3.2f));
            clutchSide = id.Substring(id.Length - 1);
            return;
        }
        var axis = Vector3.Cross(Vector3.up, new Vector3(dl.x, 0, dl.z)).normalized;
        float mag = Mathf.Clamp(imp * ImpactConfig.KICK_PER_IMPULSE, 0f, ImpactConfig.KICK_MAX);
        var kick = axis * mag;
        if (id == "chest")
        {
            var inv = standRoot.Inverse();
            float hitSide = inv.Apply(p.transform.position).x - inv.Apply(entryLast).x;
            kick += Vector3.up * Mathf.Sign(hitSide) * mag * 0.8f * Mathf.Sign(Mathf.Abs(dl.z) > 0.1f ? dl.z : 1f);
        }
        kickVel.TryGetValue(id, out var kv);
        kickVel[id] = kv + kick * 14f;
    }

    void UpdateKicks(float dt)
    {
        foreach (var id in new List<string>(kickVel.Keys))
        {
            kicks.TryGetValue(id, out var k);
            var v = kickVel[id];
            v += (-k * ImpactConfig.KICK_STIFF - v * ImpactConfig.KICK_DAMP) * dt;
            k = U.LimitLength(k + v * dt, ImpactConfig.KICK_MAX);
            kicks[id] = k; kickVel[id] = v;
            if (k.magnitude < 1e-4f && v.magnitude < 1e-3f) { kicks.Remove(id); kickVel.Remove(id); }
        }
        buckle = U.MoveToward(buckle, 0f, dt * 0.12f);
    }

    // ------------------------------------------------------------------ passo de recuperação (ponto de captura)

    void UpdateStep(float dt)
    {
        if (stepT < 0f)
        {
            // ponto de captura do pêndulo invertido: x + v/ω, ω = sqrt(g / altura do centro de massa)
            const float omega = 3.13f;
            var capture = stagger + staggerVel / omega;
            if (capture.magnitude > ImpactConfig.STEP_TRIGGER && !LegHurt("l") && !LegHurt("r"))
            {
                var dir = new Vector3(capture.x, 0, capture.z).normalized;
                stepD = dir * Mathf.Clamp(capture.magnitude * 1.6f, ImpactConfig.STEP_MIN, ImpactConfig.STEP_MAX);
                stepFirst = stepD.x > 0.02f ? "l" : (stepD.x < -0.02f ? "r" : (stepFirst == "l" ? "r" : "l"));
                stepT = 0f;
                stagger *= 0.35f;
                staggerVel *= 0.3f;
            }
            return;
        }
        stepT += dt / ImpactConfig.STEP_TIME;
        if (stepT >= 1f)
        {
            standRoot.p += standRoot.q * stepD;
            stepT = -1f;
            stepD = Vector3.zero;
        }
    }

    class Steps { public Vector3 root, l, r; public float liftL, liftR; }

    Steps StepOffsets()
    {
        var o = new Steps();
        if (stepT < 0f) return o;
        string second = stepFirst == "l" ? "r" : "l";
        float a = U.Smoothstep(0, 1, Mathf.Clamp01(stepT / 0.5f));
        float b = U.Smoothstep(0, 1, Mathf.Clamp01((stepT - 0.5f) / 0.5f));
        if (stepFirst == "l") { o.l = stepD * a; o.r = stepD * b; o.liftL = Mathf.Sin(a * Mathf.PI) * ImpactConfig.STEP_LIFT; o.liftR = Mathf.Sin(b * Mathf.PI) * ImpactConfig.STEP_LIFT; }
        else { o.r = stepD * a; o.l = stepD * b; o.liftR = Mathf.Sin(a * Mathf.PI) * ImpactConfig.STEP_LIFT; o.liftL = Mathf.Sin(b * Mathf.PI) * ImpactConfig.STEP_LIFT; }
        o.root = stepD * (a + b) * 0.5f;
        return o;
    }

    // ------------------------------------------------------------------ mente

    /// Decide o que fazer em pé: tarefa, medo, fuga, segurar a perna. Retorna a velocidade desejada.
    float Think(float dt, out bool clutch, out bool handsUp)
    {
        clutch = false; handsUp = false;
        aimedAt -= dt;
        if (holdStill) { mind = "parado"; return 0f; }
        bool aimed = aimedAt > 0f;
        bool hurt = false;
        foreach (var p in parts.Values) if (p && p.wounds.Count > 0) { hurt = true; break; }
        bool legL = LegHurt("l"), legR = LegHurt("r");
        bool leg = legL || legR;
        fear = Mathf.Clamp01(fear + (aimed ? 0.5f * dt : 0f) - 0.03f * dt + (hurt ? 0.05f * dt : 0f));
        if (Game.player != null && (fear > 0.2f || aimed))
        {
            lookAt = Game.player.HeadPosition;
            looking = true;
        }
        else looking = false;

        // a dor na perna interrompe de tempos em tempos: ele para e segura com as duas mãos
        var legW = WorstLegWound(out var lside);
        if (legW != null)
        {
            nextPainStop -= dt * (0.6f + pain);
            if (nextPainStop <= 0f && clutchTimer <= 0f)
            {
                clutchTimer = U.RandRange(1.6f, 3.4f);
                clutchSide = lside;
                nextPainStop = U.RandRange(2.5f, 5f);
                struggle += 1f;
                if (struggle == 1f || U.Randf() < 0.3f)
                    Game.LogEvent($"{dummyName} tenta continuar, mas a perna não aguenta", new Color(1f, 0.85f, 0.6f));
            }
        }
        if (clutchTimer > 0f)
        {
            clutchTimer -= dt;
            if (legW != null) { clutch = true; mind = "segurando a perna ferida"; return 0f; }
        }

        if (escaped) { mind = "a salvo (escapou)"; return 0f; }

        // mirado e ainda inteiro: mãos para cima, recua devagar olhando para o jogador
        if (aimed && fear > 0.35f && !hurt)
        {
            handsUp = true;
            mind = "com medo — mãos para cima";
            if (Game.player != null)
            {
                var away = standRoot.p - Game.player.transform.position; away.y = 0;
                var toP = -away;
                heading = Mathf.Atan2(toP.x, toP.z);          // de frente para o jogador
                walkBackward = true;
            }
            return 0.45f;
        }
        walkBackward = false;

        // ferido ou com muito medo: foge para a saída
        if (hurt || fear > 0.55f)
        {
            if (!escaping)
            {
                escaping = true;
                Game.LogEvent($"{dummyName} tenta fugir para a saída", new Color(1f, 0.9f, 0.6f));
            }
            goal = Game.main ? Game.main.ExitPoint : standRoot.p;
            hasGoal = true;
            mind = leg ? "fugindo, mancando" : "fugindo";
            return leg ? LIMP_SPEED * (legL && legR ? 0.6f : 1f) : (fear > 0.4f ? RUN_SPEED : WALK_SPEED);
        }

        // calmo: faz a tarefa (vai de ponto em ponto, para um pouco em cada)
        if (!hasGoal)
        {
            idleTimer -= dt;
            mind = "esperando";
            if (idleTimer <= 0f && Game.main)
            {
                goal = Game.main.TaskPoint(this);
                hasGoal = true;
                mind = "indo fazer a tarefa";
            }
            return 0f;
        }
        mind = "fazendo a tarefa";
        return WALK_SPEED;
    }

    bool walkBackward;
    float debugSpeed = -1f;

    /// Gancho de teste (--gaittest): anda em linha reta à velocidade dada (m/s); negativo desliga.
    public void DebugWalk(float spd)
    {
        debugSpeed = spd;
        if (spd >= 0f) { goal = standRoot.p + new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading)) * 40f; hasGoal = true; }
    }
    public float GaitSpeed => speed;

    /// Reposiciona o boneco em pé (testes): move a raiz da pose, que reescreve as partes a cada passo de física.
    public void PlaceStanding(Vector3 pos, float yawDeg)
    {
        standRoot = new Xf(Quaternion.Euler(0, yawDeg, 0), pos);
        heading = yawDeg * Mathf.Deg2Rad; lastHeading = heading;
    }
    public bool holdStill;     // capturas de tela: fica parado no lugar

    /// Avança a raiz em direção ao objetivo, seguindo o chão e desviando de obstáculos
    void Navigate(float dt, ref float want)
    {
        if (!hasGoal && !walkBackward) { want = 0f; return; }
        if (hasGoal && !walkBackward)
        {
            var to = goal - standRoot.p; to.y = 0;
            if (to.magnitude < 0.5f)
            {
                if (escaping && !escaped)
                {
                    escaped = true;
                    Game.LogEvent($"{dummyName} chegou à saída", new Color(0.6f, 1f, 0.6f));
                }
                hasGoal = false;
                idleTimer = U.RandRange(2f, 4f);
                want = 0f;
                return;
            }
            float desired = Mathf.Atan2(to.x, to.z);
            // obstáculo à frente: vira
            var fwdW = new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading));
            var o = standRoot.p + Vector3.up * 0.5f;
            var h = Phys.Ray(o, o + fwdW * 0.8f, Game.L_WORLD | Game.L_PROPS);
            if (h.hit) desired = heading + 1.2f;
            float turn = (want > 2f ? 4f : 2.5f) * dt;
            heading += Mathf.Clamp(Mathf.DeltaAngle(heading * Mathf.Rad2Deg, desired * Mathf.Rad2Deg) * Mathf.Deg2Rad, -turn, turn);
        }
        else
        {
            float turn = 3f * dt;
            float cur = Mathf.Atan2((standRoot.q * Vector3.forward).x, (standRoot.q * Vector3.forward).z);
            heading = cur + Mathf.Clamp(Mathf.DeltaAngle(cur * Mathf.Rad2Deg, heading * Mathf.Rad2Deg) * Mathf.Deg2Rad, -turn, turn);
        }
    }

    void AdvanceRoot(float dt)
    {
        standRoot.q = Quaternion.Euler(0, heading * Mathf.Rad2Deg, 0);
        if (speed < 0.01f) return;
        var dir = new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading)) * (walkBackward ? -1f : 1f);
        var next = standRoot.p + dir * speed * dt;
        // segue o chão; borda alta (plataforma) é obstáculo
        var g = Phys.Ray(next + Vector3.up * 0.6f, next + Vector3.down * 1.5f, Game.L_WORLD);
        if (!g.hit || standRoot.p.y - g.position.y > 0.35f)
        {
            speed = 0f;
            hasGoal = false;
            idleTimer = U.RandRange(2f, 4f);
            return;
        }
        // não atravessa paredes
        var o = standRoot.p + Vector3.up * 0.5f;
        if (Phys.Ray(o, o + dir * 0.35f, Game.L_WORLD).hit) { speed = 0f; return; }
        next.y = Mathf.MoveTowards(standRoot.p.y, g.position.y + 0.005f, dt * 1.5f);
        standRoot.p = next;
    }

    /// Ciclo de passada: em espaço local, o pé de apoio corre para trás na mesma velocidade do corpo (fica parado no chão)
    /// e o pé de balanço vai de trás para a frente em arco (estilo Kenwright).
    Steps Gait(float dt, out float bob, out float armPhase, bool leftHurt, bool rightHurt)
    {
        var o = new Steps();
        // girando parado: passos curtos no lugar (sem isso os pés deslizavam no giro)
        bool turning = speed < 0.1f && turnRate > 0.7f;
        float target = speed > 0.05f || turning ? 1f : 0f;
        gaitAmp = U.MoveToward(gaitAmp, target, dt * 3f);
        float run = Mathf.Clamp01((speed - WALK_SPEED) / (RUN_SPEED - WALK_SPEED));
        float L = turning ? 0.06f : Mathf.Clamp(0.22f + speed * 0.3f, 0.2f, 1.05f) * (walkBackward ? 0.5f : 1f);
        float ts = turning ? 0.32f : Mathf.Max(L / Mathf.Max(speed, 0.15f), 0.25f);
        // mancando: o apoio na perna ferida é mais curto
        bool stanceHurt = (swingSide == "l" && rightHurt) || (swingSide == "r" && leftHurt);
        float rate = stanceHurt ? 1.7f : 1f;
        if (gaitAmp > 0.01f)
        {
            gaitPhase += dt / ts * rate * (look != null ? look.gaitMul : 1f);
            if (gaitPhase >= 1f)
            {
                gaitPhase -= 1f;
                swingSide = swingSide == "l" ? "r" : "l";
                Game.sfx.Play("step", standRoot.p, -16f + speed * 1.5f, U.RandRange(0.85f, 1.15f));
            }
        }
        float ph = gaitPhase;
        gaitPh = ph;
        float sw = U.Smoothstep(0, 1, ph);
        float zSwing = (-L * 0.5f + sw * L) * gaitAmp;
        float zStance = (L * 0.5f - ph * L) * gaitAmp;
        float dirSign = walkBackward ? -1f : 1f;
        float lift = Mathf.Sin(ph * Mathf.PI) * (0.06f + run * 0.1f) * gaitAmp;
        if (swingSide == "l") { o.l = new Vector3(0, 0, zSwing * dirSign); o.liftL = lift; o.r = new Vector3(0, 0, zStance * dirSign); }
        else { o.r = new Vector3(0, 0, zSwing * dirSign); o.liftR = lift; o.l = new Vector3(0, 0, zStance * dirSign); }
        // o corpo sobe no meio do apoio; afunda quando apoia na perna ferida
        bob = (Mathf.Cos(ph * U.TAU) * (0.012f + run * 0.03f) - (stanceHurt ? 0.04f : 0f)) * gaitAmp - run * 0.04f * gaitAmp;
        armPhase = (swingSide == "l" ? ph : ph + 1f) * Mathf.PI;   // braço oposto à perna
        return o;
    }

    // ------------------------------------------------------------------ animação em pé

    void AnimateStanding(float dt)
    {
        standTime += dt;
        flinchVel += (-flinch * 70f - flinchVel * 10f) * dt;
        flinch += flinchVel * dt;
        balance = U.MoveToward(balance, BalanceCap(), dt * ImpactConfig.BALANCE_RECOVER);
        staggerVel += (-stagger * 22f - staggerVel * 6f) * dt;
        stagger = U.LimitLength(stagger + staggerVel * dt, 0.22f);
        UpdateKicks(dt);
        UpdateStep(dt);
        painOver = pain * consciousness > ImpactConfig.PAIN_COLLAPSE ? painOver + dt : Mathf.Max(0f, painOver - dt * 0.5f);

        // mente e locomoção (sem andar durante o passo de recuperação)
        float want = Think(dt, out bool clutch, out bool handsUp);
        if (stepT >= 0f) want = 0f;
        if (debugSpeed >= 0f) { hasGoal = true; want = debugSpeed; }
        Navigate(dt, ref want);
        speed = U.MoveToward(speed, want, dt * (want > speed ? 2.5f : 4f));
        AdvanceRoot(dt);
        bool lh = LegHurt("l"), rh = LegHurt("r");
        var gait = Gait(dt, out float bob, out float armPh, lh, rh);
        var steps = StepOffsets();
        steps.l += gait.l; steps.r += gait.r; steps.liftL += gait.liftL; steps.liftR += gait.liftR;
        float run = Mathf.Clamp01((speed - WALK_SPEED) / (RUN_SPEED - WALK_SPEED));
        // velocidade de giro (para os passos no lugar)
        turnRate = Mathf.Lerp(turnRate, Mathf.Abs(Mathf.DeltaAngle(lastHeading * Mathf.Rad2Deg, heading * Mathf.Rad2Deg)) * Mathf.Deg2Rad / Mathf.Max(dt, 1e-4f), Mathf.Clamp01(dt * 8f));
        lastHeading = heading;
        // respiração no ritmo da frequência respiratória (mais funda com dor/medo/perda de sangue)
        breathPh = (breathPh + dt * Mathf.Max(respRate, 4f) / 60f * U.TAU) % U.TAU;
        float breath = Mathf.Sin(breathPh) * (0.010f + Mathf.Clamp01(respRate / 40f) * 0.018f) * consciousness;
        // quadril: gira com a perna que avança, o tronco gira ao contrário; o peso passa para o pé de apoio
        float walkAmt = gaitAmp * Mathf.Clamp01(speed / WALK_SPEED + 0.3f);
        float pelvisYaw = Mathf.Cos(armPh) * (0.08f + run * 0.06f) * walkAmt;
        float stanceX = (swingSide == "l" ? -1f : 1f) * Mathf.Sin(gaitPh * Mathf.PI) * (0.018f - run * 0.008f) * walkAmt;
        // parado: troca o peso de uma perna para a outra de tempos em tempos
        float stillAmt = 1f - gaitAmp;
        float shiftX = stanceX + Mathf.Sin(standTime * 0.33f + dummyName.Length) * 0.022f * stillAmt * (1f - fear * 0.5f);
        var hipQ = U.Axis(Vector3.up, pelvisYaw) * U.Axis(new Vector3(0, 0, 1), -shiftX * 1.6f);
        var chestQ = U.Axis(Vector3.up, -pelvisYaw * 1.6f);
        var chestA = Anatomy.Parts()["chest"].joint.anchor;
        var chestFullQ = chestQ * U.Axis(Vector3.right, -breath);
        chestXfCur = new Xf(chestFullQ, chestA - chestFullQ * chestA);

        float unsteady = Mathf.Clamp01(1f - balance);
        float wob = 1f + unsteady * 5f + fear * 2f;
        var sway = U.Euler(Mathf.Sin(standTime * 0.5f) * 0.012f * wob, 0, Mathf.Sin(standTime * 0.7f + 0.4f * unsteady) * 0.015f * wob);
        if (stagger.magnitude > 1e-4f) sway = U.Axis(Vector3.Cross(Vector3.up, stagger).normalized, stagger.magnitude * 1.1f) * sway;
        var fl = Quaternion.identity;
        if (flinch.magnitude > 1e-5f) fl = U.Axis(flinch.normalized, Mathf.Clamp(flinch.magnitude, 0f, 0.6f));
        // curva o tronco com dor/desequilíbrio; inclina para a frente ao correr; dobra ao segurar a perna
        float bend = Mathf.Clamp(pain * 0.22f + unsteady * 0.2f + holdWound * 0.12f + run * 0.22f, 0f, 0.5f) + (clutch ? 0.65f : 0f)
                     + (look != null ? look.slouch : 0f)         // postura própria: uns andam curvados
                     + Mathf.Clamp01(speed / WALK_SPEED) * 0.055f * gaitAmp;   // quem anda inclina o tronco ~3° para a frente
        fl = fl * U.Axis(Vector3.right, bend);

        // mãos: segurar a ferida do tronco / as duas mãos na perna / mãos para cima
        string holdSide = "";
        Vector3 holdTarget = Vector3.zero;
        var w = WorstWound();
        if (!clutch && !handsUp && w != null && w.part && (w.part.partId == "abdomen" || w.part.partId == "chest" || w.part.partId == "pelvis"))
        {
            holdSide = w.localPos.x >= 0f ? "l" : "r";
            if (!LimbUsable(holdSide)) holdSide = holdSide == "l" ? "r" : "l";
            if (LimbUsable(holdSide))
            {
                var lp = w.localPos; var ln = w.localNormal.normalized;
                if (ln.z < -0.2f) { lp.z = Mathf.Abs(lp.z); ln = new Vector3(ln.x, ln.y, -ln.z); }
                holdTarget = w.part.def.pos + lp + ln * 0.035f;
            }
            else holdSide = "";
        }
        holdWound = U.MoveToward(holdWound, holdSide != "" ? 1f : 0f, dt * 2f);
        clutchBlend = U.MoveToward(clutchBlend, clutch ? 1f : 0f, dt * 3f);
        handsUpBlend = U.MoveToward(handsUpBlend, handsUp ? 1f : 0f, dt * 3f);

        var defs = Anatomy.Parts();
        var pelvisPivot = defs["abdomen"].joint.anchor;
        // agachado ao segurar a perna (os joelhos dobram pelo IK)
        var rootOff = steps.root + new Vector3(shiftX, -(unsteady * 0.035f + buckle) + bob - clutchBlend * 0.28f - Mathf.Abs(shiftX) * 0.5f, 0);

        // olhar com medo: cabeça (e um pouco do pescoço) viram para o jogador
        Quaternion lookQ = Quaternion.identity;
        if (looking)
        {
            var headW = standRoot.Apply(defs["head"].pos);
            var dl = Quaternion.Inverse(standRoot.q) * (lookAt - headW);
            float yaw = Mathf.Clamp(Mathf.Atan2(dl.x, dl.z), -1.3f, 1.3f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(dl.y, new Vector2(dl.x, dl.z).magnitude), -0.6f, 0.6f);
            lookQ = U.Euler(pitch, yaw, 0);
        }
        lookBlend = U.MoveToward(lookBlend, looking ? 1f : 0f, dt * 2.5f);

        var pose = new Dictionary<string, Xf>();
        // primeiro tudo menos os braços que seguem alvos; as pernas vêm do IK
        foreach (var kv in parts)
        {
            string id = kv.Key; var p = kv.Value;
            if (!p || p.severed || p.destroyed) continue;
            prevPos[id] = p.transform.position; prevRot[id] = p.transform.rotation;
            var xf = Xf.At(p.def.pos);
            foreach (var kk in kicks)
                if (id == kk.Key || IsDescendant(id, kk.Key)) xf = RotAbout(xf, defs[kk.Key].joint.anchor, U.Euler(kk.Value));
            if (id == "head")
            {
                var idle = U.Axis(Vector3.up, Mathf.Sin(standTime * 0.31f) * 0.25f * (1f - lookBlend)) * U.Axis(Vector3.right, Mathf.Sin(standTime * 0.23f) * 0.06f + pain * 0.2f + (look != null ? look.slouch * 0.7f : 0f) - bend * 0.55f)
                           * U.Axis(Vector3.forward, look != null ? look.headTilt : 0f);
                xf = RotAbout(xf, defs["head"].joint.anchor, Quaternion.Slerp(Quaternion.identity, lookQ, lookBlend * 0.65f) * idle);
            }
            if (id == "neck" || id == "head")
                xf = RotAbout(xf, defs["neck"].joint.anchor, Quaternion.Slerp(Quaternion.identity, lookQ, lookBlend * 0.35f));
            // respirar: o peito sobe (ombros e braços juntos); a cabeça compensa para o olhar não balançar
            if (id == "chest" || IsDescendant(id, "chest"))
                xf = RotAbout(xf, defs["chest"].joint.anchor, U.Axis(Vector3.right, -breath));
            if (id == "head") xf = RotAbout(xf, defs["head"].joint.anchor, U.Axis(Vector3.right, breath * 0.8f));
            // contra-rotação dos ombros ao andar
            if (id == "chest" || IsDescendant(id, "chest"))
                xf = RotAbout(xf, defs["chest"].joint.anchor, chestQ);
            pose[id] = xf;
        }
        // pernas (em repouso) e transformação do tronco
        var hipPivot = defs["pelvis"].pos;
        var hipXf = new Xf(hipQ, hipPivot - hipQ * hipPivot);
        var upperXf = new Xf(Quaternion.identity, rootOff) * new Xf(sway, Vector3.zero) * hipXf * new Xf(fl, pelvisPivot - fl * pelvisPivot);
        var lowerXf = new Xf(Quaternion.identity, rootOff) * new Xf(sway, Vector3.zero) * hipXf;
        foreach (var id in new List<string>(pose.Keys))
            pose[id] = (UPPER.Contains(id) ? upperXf : lowerXf) * pose[id];
        if (pose.ContainsKey("pelvis"))
        {
            LegPose(pose, "l", steps.l, steps.liftL);
            LegPose(pose, "r", steps.r, steps.liftR);
        }
        // braços: dependem das pernas já posicionadas (segurar a perna)
        foreach (var side in new[] { "l", "r" })
            ArmPose(pose, side, upperXf, holdSide, holdTarget, armPh, run, unsteady, wob);
        foreach (var kv in pose)
        {
            var p = parts[kv.Key];
            var wx = standRoot * kv.Value;
            p.rb.position = wx.p; p.rb.rotation = wx.q;
            p.transform.SetPositionAndRotation(wx.p, wx.q);
        }
    }

    float clutchBlend, handsUpBlend, lookBlend;
    Xf chestXfCur = Xf.Identity;     // respiração + contra-rotação do peito (os braços acompanham)

    void ArmPose(Dictionary<string, Xf> pose, string side, Xf upperXf, string holdSide, Vector3 holdTarget, float armPh, float run, float unsteady, float wob)
    {
        var defs = Anatomy.Parts();
        string ua = "upper_arm_" + side, fa = "forearm_" + side, ha = "hand_" + side;
        if (!pose.ContainsKey(ua)) return;
        float sgn = side == "l" ? 1f : -1f;
        var shoulder = defs[ua].joint.anchor;
        var elbowRest = defs[fa].joint.anchor;
        // base: balanço do braço (parado) ou oposto à perna (andando/correndo)
        float walkSwing = Mathf.Sin(armPh + (side == "l" ? 0f : Mathf.PI)) * (0.36f + run * 0.5f) * gaitAmp;
        var swing = U.Axis(Vector3.right, Mathf.Sin(standTime * 0.9f + sgn) * 0.05f * wob - walkSwing)
                  * U.Axis(new Vector3(0, 0, 1), sgn * (0.04f + unsteady * 0.25f));
        // cotovelo meio flexionado ao andar e muito ao correr; estica um pouco no balanço para trás
        float swingPhase = Mathf.Sin(armPh + (side == "l" ? 0f : Mathf.PI));
        var elbowBend = U.Axis(Vector3.right, -0.12f - 0.04f * Mathf.Sin(standTime * 0.9f) - unsteady * 0.4f - run * 1.1f * gaitAmp
                                               - gaitAmp * (0.2f + 0.12f * swingPhase));
        Quaternion qs = swing, qe = elbowBend;
        // alvos de IK (em espaço de repouso do tronco)
        Vector3? target = null;
        float blend = 0f;
        if (clutchBlend > 0.01f)
        {
            var lw = WorstLegWound(out var ls);
            if (lw != null && pose.ContainsKey(lw.part.partId) && LimbUsable(side))
            {
                // as duas mãos apertam a ferida da perna (cada mão de um lado)
                var wp = pose[lw.part.partId].Apply(lw.localPos + lw.localNormal.normalized * 0.04f);
                var offs = pose[lw.part.partId].Rotate(new Vector3(0.035f * sgn, 0.02f * sgn, 0));
                target = upperXf.Inverse().Apply(wp + offs);
                blend = clutchBlend;
            }
        }
        if (target == null && handsUpBlend > 0.01f && LimbUsable(side))
        {
            // rendição: mãos na altura da cabeça e cotovelos dobrados (não estica a axila da pele)
            target = shoulder + new Vector3(0.15f * sgn, 0.24f, 0.14f);
            blend = handsUpBlend;
        }
        if (target == null && holdSide == side)
        {
            target = holdTarget;
            blend = holdWound;
        }
        if (target != null)
        {
            var ik = ArmIK(side, target.Value);
            qs = Quaternion.Slerp(qs, ik.Item1, blend);
            qe = Quaternion.Slerp(qe, ik.Item2, blend);
            // ik.Item2 é aplicada no cotovelo já girado pelo ombro
        }
        var elbow = shoulder + qs * (elbowRest - shoulder);
        foreach (var id in new[] { ua, fa, ha })
        {
            if (!pose.ContainsKey(id)) continue;
            var xf = Xf.At(defs[id].pos);
            foreach (var kk in kicks)
                if (id == kk.Key || IsDescendant(id, kk.Key)) xf = RotAbout(xf, defs[kk.Key].joint.anchor, U.Euler(kk.Value));
            xf = RotAbout(xf, shoulder, qs);
            if (id != ua) xf = RotAbout(xf, elbow, qe);
            pose[id] = upperXf * chestXfCur * xf;
        }
    }

    bool IsDescendant(string id, string anc)
    {
        var p = parts[id].parentPart;
        while (p != null) { if (p.partId == anc) return true; p = p.parentPart; }
        return false;
    }

    void LegPose(Dictionary<string, Xf> pose, string side, Vector3 stepOff, float lift)
    {
        var defs = Anatomy.Parts();
        string th = "thigh_" + side, sh = "shin_" + side, ft = "foot_" + side;
        if (!(pose.ContainsKey(th) && pose.ContainsKey(sh) && pose.ContainsKey(ft))) return;
        var pel = pose["pelvis"];
        var hipRest = defs[th].joint.anchor;
        var kneeRest = defs[sh].joint.anchor;
        var ankleRest = defs[ft].joint.anchor;
        var hip = pel.Apply(hipRest - defs["pelvis"].pos);
        var ankleT = ankleRest + stepOff + new Vector3(0, lift, 0);
        // o IK trabalha com o quadril na posição de repouso: desloca o alvo pelo movimento do quadril
        var ik = TwoBone(hipRest, kneeRest, ankleRest, ankleT - (hip - hipRest), new Vector3(0, 0.1f, 1f));
        var bt = ik.qs;
        var bs = ik.qe * bt;
        var knee = hip + bt * (kneeRest - hipRest);
        var ankle = knee + bs * (ankleRest - kneeRest);
        pose[th] = new Xf(bt, hip - bt * (hipRest - defs[th].pos));
        pose[sh] = new Xf(bs, knee - bs * (kneeRest - defs[sh].pos));
        // pé plano no apoio; no balanço sai com a ponta para baixo e chega com o calcanhar (ponta para cima)
        float roll = lift > 1e-3f ? (stepOff.z > 0f ? 0.25f : -0.45f) * Mathf.Clamp01(lift / 0.05f) : 0f;
        var bf = U.Axis(Vector3.right, -lift * 2.5f + roll);
        pose[ft] = new Xf(bf, ankle - bf * (ankleRest - defs[ft].pos));
    }

    // ------------------------------------------------------------------ levantar

    /// Caído mas em condições: tenta se levantar.
    void TryGetUp(float dt)
    {
        getUpCd -= dt;
        if (state != State.DOWN || dead || paralysis != "" || getUpCd > 0f) return;
        if (timeAlive - collapseTime < 3.5f || consciousness < 0.8f || pain > 0.75f) return;
        float b = balance; balance = 1f;
        string why = CollapseReason();
        balance = b;
        // sem um pé/perna mas forte: tenta assim mesmo (até 2 vezes), não aguenta o peso e cai; depois rasteja
        bool legTry = why == "perdeu a perna" && legTries < 2 && consciousness > 0.85f && pain < 0.6f && blood > Anatomy.BLOOD_TOTAL * 0.7f;
        if (why != "" && !legTry || !legTry && BalanceCap() < 0.4f) { getUpCd = 2f; return; }
        if (Game.player != null && Game.player.grabBody != null && Game.player.grabBody.GetComponent<DummyPart>() is DummyPart gp && gp.dummy == this) return;
        getUpCd = 4f;
        if (!legTry && U.Randf() < 0.35f) return;
        StartGetUp();
        if (legTry && gettingUp)
        {
            legTries++;
            getUpWillFail = true;
            Game.LogEvent($"{dummyName} tenta levantar sem a perna", new Color(1f, 0.85f, 0.6f));
        }
    }

    int legTries;
    public int LegTries => legTries;

    /// Começa a levantar (também usado pelas capturas de tela).
    public void StartGetUp()
    {
        if (state == State.STANDING || gettingUp || dead) return;
        var pel = parts["pelvis"]; var ch = parts["chest"];
        // de bruços ou de costas? (frente do peito virada para o chão ou para cima)
        var front = ch.transform.rotation * Vector3.forward;
        var headDir = ch.transform.rotation * Vector3.up; headDir.y = 0;
        if (headDir.magnitude < 0.2f) { headDir = pel.transform.rotation * Vector3.up; headDir.y = 0; }
        if (headDir.magnitude < 0.01f) headDir = Vector3.forward;
        headDir.Normalize();
        getUpProne = front.y < 0f;
        // de bruços levanta para onde a cabeça aponta; de costas, para o lado dos pés
        var fwd = getUpProne ? headDir : -headDir;
        var ground = pel.transform.position;
        var h = Phys.Ray(ground + Vector3.up * 0.6f, ground + Vector3.down * 3f, Game.L_WORLD);
        ground.y = (h.hit ? h.position.y : ground.y - 0.15f) + 0.005f;
        // a raiz final fica onde o corpo vai terminar em pé (sobre os pés), não onde a pelve está deitada
        var seq = getUpProne ? PRONE_SEQ : SUPINE_SEQ;
        ground -= fwd * seq[0].dz;
        standRoot = new Xf(U.LookingAt(-fwd, Vector3.up), ground);
        heading = Mathf.Atan2(fwd.x, fwd.z);
        getUpRoot = pel.Xform;
        getUpQ.Clear();
        foreach (var id in Anatomy.ORDER)
        {
            var p = parts[id];
            if (!p || p.severed || p.parentPart == null) continue;
            getUpQ[id] = Quaternion.Inverse(p.parentPart.transform.rotation) * p.transform.rotation;
        }
        foreach (var p in parts.Values)
        {
            if (!p || p.severed) continue;
            p.rb.linearVelocity = Vector3.zero; p.rb.angularVelocity = Vector3.zero;
            p.Freeze = true;
        }
        // fraco (dor, sangue, perna ferida): mais lento, treme e pode não conseguir
        getUpWeak = Mathf.Clamp01(pain * 0.5f + (1f - blood / Anatomy.BLOOD_TOTAL) * 2f + (LegHurt("l") || LegHurt("r") ? 0.35f : 0f) + (1f - consciousness));
        getUpWillFail = U.Randf() < getUpWeak * 0.45f;
        gettingUp = true;
        getUpT = 0f;
        getUpSeg = 0;
        BuildGetUpTimeline();
        muscleLevel = 0f;
        action = "none";
        Game.LogEvent($"{dummyName} tenta se levantar ({(getUpProne ? "de bruços" : "de costas")})", new Color(0.8f, 0.9f, 1f));
    }

    /// Etapa da animação de levantar: pelve (altura h, inclinação p para a frente em rad, avanço dz na raiz) e
    /// rotações locais das partes (Euler em rad, igual ao resto do código: +x no tronco = inclina para a frente,
    /// -x na coxa = flexiona o quadril, +x na canela = dobra o joelho, -x no braço = braço para a frente).
    class GetUpKey
    {
        public float h, p, dz, dur;
        public Dictionary<string, Vector3> j = new Dictionary<string, Vector3>();
        public GetUpKey(float h, float p, float dz, float dur, params (string, Vector3)[] js)
        {
            this.h = h; this.p = p; this.dz = dz; this.dur = dur;
            foreach (var (k, v) in js) j[k] = v;
        }
    }

    static Vector3 Rot(float x, float y = 0, float z = 0) => new Vector3(x, y, z);

    // ajoelhado com um pé na frente (comum às duas sequências)
    static GetUpKey Lunge(float dur) => new GetUpKey(0.5f, 0.3f, 0f, dur,
        ("abdomen", Rot(0.15f)), ("neck", Rot(-0.15f)), ("head", Rot(-0.15f)),
        ("thigh_l", Rot(-1.6f, 0, 0.05f)), ("shin_l", Rot(1.6f)), ("foot_l", Rot(0.05f)),
        ("thigh_r", Rot(0.2f, 0, -0.04f)), ("shin_r", Rot(1.1f)), ("foot_r", Rot(0.9f)),
        ("upper_arm_l", Rot(-0.6f, 0, 0.1f)), ("forearm_l", Rot(-0.55f)),          // mão no joelho da frente
        ("upper_arm_r", Rot(-0.25f, 0, -0.15f)), ("forearm_r", Rot(-0.35f)));

    static GetUpKey Standing(float dur) => new GetUpKey(0.92f, 0f, 0f, dur);

    // de bruços: flexão -> quatro apoios -> um joelho -> em pé (dz relativo à raiz final)
    static readonly GetUpKey[] PRONE_SEQ =
    {
        new GetUpKey(0.17f, 1.4f, 0.32f, 0.75f,
            ("neck", Rot(-0.35f)), ("head", Rot(-0.3f)),
            ("upper_arm_l", Rot(-1.45f, 0, 0.2f)), ("upper_arm_r", Rot(-1.45f, 0, -0.2f)), ("forearm_l", Rot(-0.15f)), ("forearm_r", Rot(-0.15f)),
            ("hand_l", Rot(0.9f)), ("hand_r", Rot(0.9f)), ("shin_l", Rot(0.15f)), ("shin_r", Rot(0.15f)), ("foot_l", Rot(0.6f)), ("foot_r", Rot(0.6f))),
        new GetUpKey(0.55f, 1.45f, 0.1f, 0.8f,
            ("neck", Rot(-0.45f)), ("head", Rot(-0.35f)),
            ("thigh_l", Rot(-1.45f, 0, 0.05f)), ("thigh_r", Rot(-1.45f, 0, -0.05f)), ("shin_l", Rot(1.5f)), ("shin_r", Rot(1.5f)), ("foot_l", Rot(0.6f)), ("foot_r", Rot(0.6f)),
            ("upper_arm_l", Rot(-1.45f, 0, 0.1f)), ("upper_arm_r", Rot(-1.45f, 0, -0.1f)), ("hand_l", Rot(0.9f)), ("hand_r", Rot(0.9f))),
        Lunge(0.85f),
        Standing(0.9f),
    };

    // de costas: senta com as mãos apoiadas atrás -> recolhe os joelhos -> um joelho -> em pé
    static readonly GetUpKey[] SUPINE_SEQ =
    {
        new GetUpKey(0.14f, -0.3f, -0.38f, 0.8f,
            ("abdomen", Rot(0.15f)), ("head", Rot(0.1f)),
            ("thigh_l", Rot(-1.35f, 0, 0.05f)), ("thigh_r", Rot(-1.35f, 0, -0.05f)), ("shin_l", Rot(0.1f)), ("shin_r", Rot(0.1f)),
            ("upper_arm_l", Rot(0.6f, 0, 0.25f)), ("upper_arm_r", Rot(0.6f, 0, -0.25f)), ("forearm_l", Rot(-0.2f)), ("forearm_r", Rot(-0.2f)),
            ("hand_l", Rot(-0.9f)), ("hand_r", Rot(-0.9f))),
        new GetUpKey(0.15f, 0.2f, -0.25f, 0.7f,
            ("abdomen", Rot(0.4f)), ("chest", Rot(0.2f)), ("neck", Rot(-0.2f)), ("head", Rot(-0.2f)),
            ("thigh_l", Rot(-2.1f, 0, 0.1f)), ("thigh_r", Rot(-2.1f, 0, -0.1f)), ("shin_l", Rot(2.2f)), ("shin_r", Rot(2.2f)), ("foot_l", Rot(0.3f)), ("foot_r", Rot(0.3f)),
            ("upper_arm_l", Rot(-0.9f, 0, 0.15f)), ("upper_arm_r", Rot(-0.9f, 0, -0.15f)), ("forearm_l", Rot(-0.3f)), ("forearm_r", Rot(-0.3f))),
        Lunge(0.85f),
        Standing(0.9f),
    };

    bool getUpProne, getUpWillFail;
    float getUpWeak;
    int getUpSeg;

    // ---- linha do tempo contínua (pendência 08): as poses-chave viram uma curva de Hermite (a velocidade não zera em
    // cada pose), cada região do corpo tem seu atraso (cabeça e tronco puxam, braços empurram, pernas vêm depois) e mãos
    // e pés que tocam o chão ficam presos no ponto de apoio (IK de dois ossos) até saírem dele.
    readonly List<float> gkT = new List<float>();
    readonly List<Vector3> gkRootP = new List<Vector3>(), gkRootR = new List<Vector3>();
    readonly List<Dictionary<string, Vector3>> gkJ = new List<Dictionary<string, Vector3>>();
    float getUpClock, getUpEnd, getUpSlow = 1f;
    int getUpLungeKey = -1;
    class GetUpPin { public Vector3 at, lastFk; public float w, lift; public bool on, has; }
    readonly Dictionary<string, GetUpPin> getUpPins = new Dictionary<string, GetUpPin>();
    public float GetUpElapsed => getUpClock;      // testes

    static Vector3 LogQ(Quaternion q)
    {
        q.ToAngleAxis(out float a, out Vector3 ax);
        if (float.IsNaN(ax.x) || float.IsInfinity(ax.x) || Mathf.Abs(a) < 1e-4f) return Vector3.zero;
        if (a > 180f) a -= 360f;
        return ax.normalized * (a * Mathf.Deg2Rad);
    }

    static Quaternion ExpQ(Vector3 v)
    {
        float a = v.magnitude;
        return a < 1e-6f ? Quaternion.identity : Quaternion.AngleAxis(a * Mathf.Rad2Deg, v / a);
    }

    /// Hermite cúbica por chaves com tempos próprios; tangente zero nas pontas (sai e chega parado).
    Vector3 Curve(List<Vector3> k, float t)
    {
        int n = gkT.Count;
        if (t <= gkT[0]) return k[0];
        if (t >= gkT[n - 1]) return k[n - 1];
        int i = 0;
        while (i < n - 2 && t > gkT[i + 1]) i++;
        float t0 = gkT[i], t1 = gkT[i + 1], dur = Mathf.Max(t1 - t0, 1e-4f);
        float u = (t - t0) / dur;
        Vector3 Tan(int j) => j <= 0 || j >= n - 1 ? Vector3.zero : (k[j + 1] - k[j - 1]) / Mathf.Max(gkT[j + 1] - gkT[j - 1], 1e-4f) * 0.85f;
        float u2 = u * u, u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * k[i] + (u3 - 2f * u2 + u) * dur * Tan(i) + (-2f * u3 + 3f * u2) * k[i + 1] + (u3 - u2) * dur * Tan(i + 1);
    }

    /// Atraso (s) de cada região em relação ao relógio: negativo = sai na frente.
    float GetUpDelay(string id)
    {
        float d;
        if (id == "head" || id == "neck") d = -0.12f;
        else if (id == "chest") d = -0.05f;
        else if (id.StartsWith("upper_arm") || id.StartsWith("forearm") || id.StartsWith("hand")) d = getUpProne ? -0.06f : 0.02f;
        else if (id.StartsWith("thigh")) d = 0.08f;
        else if (id.StartsWith("shin")) d = 0.12f;
        else if (id.StartsWith("foot")) d = 0.16f;
        else d = 0f;
        // lados levemente defasados: o corpo não é simétrico
        if (id.EndsWith("_r")) d += 0.04f;
        return d * getUpSlow;
    }

    void BuildGetUpTimeline()
    {
        var seq = getUpProne ? PRONE_SEQ : SUPINE_SEQ;
        gkT.Clear(); gkRootP.Clear(); gkRootR.Clear(); gkJ.Clear(); getUpPins.Clear();
        var invStand = Quaternion.Inverse(standRoot.q);
        void Add(float t, Xf root, Dictionary<string, Vector3> j)
        {
            gkT.Add(t); gkRootP.Add(root.p); gkRootR.Add(LogQ(invStand * root.q)); gkJ.Add(j);
        }
        var j0 = new Dictionary<string, Vector3>();
        foreach (var kv in getUpQ) j0[kv.Key] = LogQ(kv.Value);
        Add(0f, getUpRoot, j0);
        getUpSlow = 1f + getUpWeak * 1.3f;
        float t = 0f;
        getUpLungeKey = -1;
        for (int i = 0; i < seq.Length; i++)
        {
            var k = seq[i];
            var jk = new Dictionary<string, Vector3>();
            foreach (var id in getUpQ.Keys) jk[id] = LogQ(KeyQ(k, id));
            t += k.dur * getUpSlow;
            Add(t, KeyRoot(k), jk);
            if (i == seq.Length - 2)
            {
                getUpLungeKey = gkT.Count - 1;
                // fraco: para ajoelhado, respira com a mão no joelho, e só então tenta ficar de pé
                if (getUpWeak > 0.15f)
                {
                    t += Mathf.Lerp(0.3f, 1.3f, getUpWeak) * U.RandRange(0.8f, 1.2f);
                    Add(t, KeyRoot(k), jk);
                }
            }
        }
        getUpEnd = t;
        getUpClock = 0f;
    }

    /// IK de dois ossos no mundo: leva o pivô da ponta (pulso/tornozelo) até `target` com peso `w`. A ponta mantém a
    /// rotação do mundo (palma/sola apoiada no chão continua apoiada).
    void PinChain(Dictionary<string, Xf> xf, string a, string b, string c, Vector3 target, float w)
    {
        if (!xf.ContainsKey(a) || !xf.ContainsKey(b) || !xf.ContainsKey(c)) return;
        var pa = parts[a]; var pb = parts[b]; var pc = parts[c];
        var A = xf[a].Apply(pa.def.joint.anchor - pa.def.pos);
        var B = xf[a].Apply(pb.def.joint.anchor - pa.def.pos);
        var C = xf[b].Apply(pc.def.joint.anchor - pb.def.pos);
        var T = Vector3.Lerp(C, target, w);
        var mid = (A + C) * 0.5f;
        var pole = B - mid; if (pole.sqrMagnitude < 1e-6f) pole = Vector3.forward;
        var (qs, qe) = TwoBone(A, B, C, T, pole.normalized);
        var xa = xf[a]; var xb = xf[b]; var xc = xf[c];
        xf[a] = new Xf(qs * xa.q, A + qs * (xa.p - A));
        var B2 = A + qs * (B - A);
        var qb = qe * qs;
        xf[b] = new Xf(qb * xb.q, B2 + qb * (xb.p - B));
        var C2 = B2 + qb * (C - B);
        xf[c] = new Xf(xc.q, C2 + (xc.p - C));
    }

    /// Levantar contínuo: amostra a curva, aplica os atrasos, encosta no chão, prende os apoios e escreve a pose.
    void GetUpStep(float dt)
    {
        var seq = getUpProne ? PRONE_SEQ : SUPINE_SEQ;
        getUpClock += dt;
        float tr = Mathf.Min(getUpClock, getUpEnd);
        var root = new Xf(standRoot.q * ExpQ(Curve(gkRootR, tr)), Curve(gkRootP, tr));
        var q = new Dictionary<string, Quaternion>();
        var tmp = new List<Vector3>(gkJ.Count);
        foreach (var id in getUpQ.Keys)
        {
            tmp.Clear();
            foreach (var j in gkJ) tmp.Add(j[id]);
            q[id] = ExpQ(Curve(tmp, Mathf.Clamp(getUpClock - GetUpDelay(id), 0f, getUpEnd)));
        }
        // fase atual (para a falha e para o texto)
        getUpSeg = 0;
        while (getUpSeg < gkT.Count - 1 && tr > gkT[getUpSeg + 1]) getUpSeg++;
        // respiração de esforço e tremor quando fraco
        float effort = getUpClock < getUpEnd ? 1f : 0.4f;
        float br = Mathf.Sin(timeAlive * (2.2f + getUpWeak * 2f)) * 0.025f * effort * (1f + getUpWeak);
        if (q.ContainsKey("chest")) q["chest"] = q["chest"] * U.Euler(-br, 0, 0);
        if (q.ContainsKey("abdomen")) q["abdomen"] = q["abdomen"] * U.Euler(br * 0.5f, 0, 0);
        if (getUpWeak > 0.05f && getUpClock < getUpEnd)
        {
            float trm = getUpWeak * 0.035f;
            foreach (var id in new[] { "upper_arm_l", "upper_arm_r", "thigh_l", "thigh_r", "abdomen" })
                if (q.ContainsKey(id)) q[id] = q[id] * U.Euler(Mathf.Sin(timeAlive * 31f + id.Length) * trm, 0, Mathf.Sin(timeAlive * 23f + id.Length * 2f) * trm);
        }
        var xf = FK(root, q);
        // o ponto mais baixo encosta no chão (mãos, joelhos ou pés de apoio): nada atravessa nem flutua
        float lowest = float.MaxValue;
        foreach (var kv in xf) lowest = Mathf.Min(lowest, kv.Value.p.y - parts[kv.Key].PartRadius() * 0.9f);
        float lift = standRoot.p.y + 0.005f - lowest;
        if (getUpClock < 0.3f) lift = Mathf.Max(lift, 0f);
        foreach (var k in new List<string>(xf.Keys)) xf[k] = xf[k].Translated(new Vector3(0, lift, 0));
        // apoios: mão/pé que chega ao chão fica preso até a curva tirá-lo de lá; nos últimos 0,35 s soltam para a
        // pose em pé (sem pulo na passagem para a animação de andar)
        float release = Mathf.Clamp01((getUpEnd - getUpClock) / 0.6f);
        foreach (var (a, b, c) in new[] { ("upper_arm_l", "forearm_l", "hand_l"), ("upper_arm_r", "forearm_r", "hand_r"), ("thigh_l", "shin_l", "foot_l"), ("thigh_r", "shin_r", "foot_r") })
        {
            if (!xf.ContainsKey(c)) continue;
            if (!getUpPins.TryGetValue(c, out var pin)) getUpPins[c] = pin = new GetUpPin();
            var pc = parts[c]; var pb = parts[b];
            float h = xf[c].p.y - pc.PartRadius() * 0.9f - standRoot.p.y;
            var wrist = xf[b].Apply(pc.def.joint.anchor - pb.def.pos);
            var flat = wrist - pin.lastFk; flat.y = 0f;
            float fkSpeed = pin.has ? flat.magnitude / dt : 0f;
            pin.lastFk = wrist; pin.has = true;
            // apoio que a curva leva para longe (> 18 cm) solta: o membro é reposicionado com um passo, não arrastado
            if (pin.on && h > 0.08f) pin.on = false;
            else if (pin.on && Vector3.Distance(new Vector3(pin.at.x, 0, pin.at.z), new Vector3(wrist.x, 0, wrist.z)) > 0.12f)
            {
                pin.on = false;
                pin.lift = Mathf.Max(pin.lift, 0.06f);      // sai do apoio pelo ar
            }
            else if (!pin.on && h < 0.05f && fkSpeed < 0.35f && getUpClock > 0.1f) { pin.on = true; pin.at = wrist; }
            pin.w = U.MoveToward(pin.w, pin.on ? 1f : 0f, dt / 0.12f);
            // passo: rente ao chão e se deslocando, o membro sobe num arco em vez de raspar o piso
            float wantLift = !pin.on && h < 0.09f && fkSpeed > 0.15f ? Mathf.Clamp(fkSpeed * 0.07f, 0.035f, 0.1f) : 0f;
            pin.lift = wantLift > pin.lift ? wantLift : U.MoveToward(pin.lift, wantLift, dt * 0.5f);   // sobe já, desce devagar
            // alvo = do apoio preso até a pose da curva erguida pelo passo
            var free = wrist + Vector3.up * pin.lift;
            float w = Mathf.Max(pin.w * release, pin.lift > 1e-3f ? 1f : 0f);
            if (w > 1e-3f) PinChain(xf, a, b, c, Vector3.Lerp(free, pin.at, pin.w * release), w);
        }
        foreach (var kv in xf)
        {
            var p = parts[kv.Key];
            prevPos[kv.Key] = p.transform.position; prevRot[kv.Key] = p.transform.rotation;
            // limite de giro por quadro nos membros: a IK pode trocar o cotovelo/joelho de lado num quadro
            var qn = kv.Value.q;
            if (getUpClock > 0.05f && kv.Key != "pelvis") qn = Quaternion.RotateTowards(p.transform.rotation, qn, 11f * Mathf.Rad2Deg * dt);
            p.rb.position = kv.Value.p; p.rb.rotation = qn;
            p.transform.SetPositionAndRotation(kv.Value.p, qn);
        }
        // fraco demais: ajoelhado, não aguenta e desaba
        if (getUpWillFail && getUpLungeKey > 0 && getUpClock >= gkT[getUpLungeKey] + 0.15f * getUpSlow)
        {
            fallReason = "não conseguiu se levantar";
            Game.LogEvent($"{dummyName} não aguenta e cai de novo", new Color(1f, 0.75f, 0.5f));
            getUpCd = 3f;
            Collapse();
            return;
        }
        float maxDelay = 0.16f * getUpSlow + 0.04f * getUpSlow;
        if (getUpClock < getUpEnd + maxDelay) return;
        gettingUp = false;
        state = State.STANDING;
        standTime = 0f;
        balance = BalanceCap() * 0.6f;
        stagger = flinch = flinchVel = Vector3.zero;
        // chega meio desequilibrado: um pequeno ajuste de passo
        staggerVel = new Vector3(U.RandRange(-0.3f, 0.3f), 0, U.RandRange(0.1f, 0.4f)) * (0.5f + getUpWeak);
        hasGoal = false;
        idleTimer = 0.5f;
        Game.LogEvent($"{dummyName} ficou de pé de novo ({getUpClock:F1} s)", new Color(0.7f, 1f, 0.7f));
    }

    /// Cinemática direta: a partir da pelve e das rotações locais, posiciona cada parte presa à junta do pai.
    Dictionary<string, Xf> FK(Xf root, Dictionary<string, Quaternion> q)
    {
        var o = new Dictionary<string, Xf> { { "pelvis", root } };
        foreach (var id in Anatomy.ORDER)
        {
            var p = parts[id];
            if (id == "pelvis" || !p || p.severed || !o.ContainsKey(p.parentPart.partId)) continue;
            var par = o[p.parentPart.partId];
            var b = par.q * (q.TryGetValue(id, out var qq) ? qq : Quaternion.identity);
            var anc = p.def.joint.anchor;
            var pivot = par.Apply(anc - p.parentPart.def.pos);
            o[id] = new Xf(b, pivot - b * (anc - p.def.pos));
        }
        return o;
    }

    Xf KeyRoot(GetUpKey k)
    {
        var pp = parts["pelvis"].def.pos;
        return standRoot * new Xf(U.Axis(Vector3.right, k.p), new Vector3(pp.x, k.h, pp.z + k.dz));
    }

    static Quaternion KeyQ(GetUpKey k, string id) => k.j.TryGetValue(id, out var e) ? U.Euler(e) : Quaternion.identity;

    // ------------------------------------------------------------------ cair

    void FlinchFrom(Vector3 dirW, float strength)
    {
        var dl = Quaternion.Inverse(standRoot.q) * dirW;
        dl.y = 0;
        if (dl.magnitude < 1e-4f) return;
        flinchVel += Vector3.Cross(Vector3.up, dl.normalized) * strength;
    }

    public bool ShouldCollapse() => CollapseReason() != "";

    /// Por que ele não aguenta ficar em pé ("" = aguenta)
    public string CollapseReason()
    {
        if (dead || paralysis != "") return paralysis != "" ? "paralisia" : "morte";
        if (gettingUp) return "";
        int badLegs = 0;
        foreach (var side in new[] { "l", "r" })
        {
            foreach (var id in new[] { "thigh_" + side, "shin_" + side, "foot_" + side })
            {
                if (!parts.TryGetValue(id, out var p) || !p || p.severed || p.destroyed) return "perdeu a perna";
                if (!id.StartsWith("foot") && p.MainBoneFractured()) return "perna fraturada";
            }
            if (LegHurt(side)) badLegs++;
        }
        if (badLegs >= 2) return "as duas pernas feridas";
        if (!parts.TryGetValue("head", out var h) || !h || h.severed) return "cabeça";
        var br = h.Organ("brain");
        if (br != null && br.hp < br.max * ImpactConfig.BRAIN_HP_COLLAPSE) return "lesão cerebral";
        if (concussion > ImpactConfig.CONCUSSION_COLLAPSE) return "concussão";
        if (consciousness < ImpactConfig.CONSCIOUS_COLLAPSE) return "quase desmaiando";
        if (painOver > ImpactConfig.PAIN_HOLD_TIME) return "dor insuportável";
        if (1f - blood / Anatomy.BLOOD_TOTAL > ImpactConfig.BLOOD_LOSS_COLLAPSE) return "perda de sangue";
        return balance <= 0f ? "perdeu o equilíbrio" : "";
    }

    public float BalanceCap()
    {
        float c = 1f;
        foreach (var side in new[] { "l", "r" }) if (LegHurt(side)) c -= 0.4f;
        c -= Mathf.Clamp(pain - 0.4f, 0f, 1.1f) * 0.35f;
        c -= (1f - blood / Anatomy.BLOOD_TOTAL) * 1.2f;
        c -= (1f - consciousness) * 0.8f;
        // correr mancando cansa o equilíbrio
        return Mathf.Clamp(c, 0.05f, 1f);
    }

    void Stagger(Vector3 dirW, float amount)
    {
        balance -= amount;
        var dl = Quaternion.Inverse(standRoot.q) * dirW;
        dl.y = 0;
        staggerVel += U.LimitLength(dl, 1f) * Mathf.Min(amount, 0.8f) * ImpactConfig.STAGGER_PUSH;
    }

    public void Collapse()
    {
        if (state != State.STANDING && !gettingUp) return;
        gettingUp = false;
        state = State.DOWN;
        collapseTime = timeAlive;
        var why = CollapseReason();
        fallReason = why != "" ? why : "golpe forte";
        if (why != "" && !dead) Game.LogEvent($"{dummyName} caiu: {why}", new Color(1f, 0.8f, 0.5f));
        float dt = Time.fixedDeltaTime;
        foreach (var kv in parts)
        {
            var p = kv.Value;
            if (!p || p.destroyed) continue;
            var pp = prevPos.TryGetValue(kv.Key, out var v0) ? v0 : p.transform.position;
            var v = (p.transform.position - pp) / dt;
            p.Freeze = false;
            p.rb.linearVelocity = U.LimitLength(v, 4f);
            // velocidade angular da pose: a queda herda o giro em vez de partir do zero
            if (prevRot.TryGetValue(kv.Key, out var q0))
            {
                (p.transform.rotation * Quaternion.Inverse(q0)).ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                if (!float.IsInfinity(axis.x) && !float.IsNaN(axis.x))
                    p.rb.angularVelocity = U.LimitLength(axis * (ang * Mathf.Deg2Rad / dt), 8f);
            }
            p.lastVel = p.rb.linearVelocity;
            p.impactGrace = 0.5f;
            p.rb.WakeUp();
        }
        // joelhos cedem
        foreach (var side in new[] { "l", "r" })
            if (parts.TryGetValue("shin_" + side, out var shin) && shin && !shin.severed)
                shin.rb.AddForce(transform.rotation * new Vector3(0, 0, -0.8f), ForceMode.Impulse);
        ChooseAction();
    }
}
