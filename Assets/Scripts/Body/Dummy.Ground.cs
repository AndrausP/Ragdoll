using System.Collections.Generic;
using UnityEngine;

/// No chão: agonia, rolar, rastejar, pressionar a ferida, torniquete em si mesmo, espasmos.
/// Os músculos são torques PD em cada junta em direção a uma pose (ragdoll ativo).
public partial class Dummy
{
    // Poses (rotação relativa ao pai, radianos, ordem YXZ). X+ no quadril = coxa para trás; joelho X+ = flexão;
    // cotovelo X- = flexão; ombro X- = braço para frente; Z+ (esq.) = abrir para o lado.
    static Dictionary<string, Vector3> P(params object[] kv)
    {
        var d = new Dictionary<string, Vector3>();
        for (int i = 0; i < kv.Length; i += 2) d[(string)kv[i]] = (Vector3)kv[i + 1];
        return d;
    }
    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static readonly Dictionary<string, Dictionary<string, Vector3>> POSES = new Dictionary<string, Dictionary<string, Vector3>>
    {
        { "fetal", P("abdomen", V(0.45f, 0, 0), "chest", V(0.4f, 0, 0), "neck", V(0.5f, 0, 0), "head", V(0.25f, 0, 0),
            "thigh_l", V(-1.1f, 0, 0), "shin_l", V(1.9f, 0, 0), "thigh_r", V(-1.1f, 0, 0), "shin_r", V(1.9f, 0, 0),
            "upper_arm_l", V(-1.1f, 0, -0.3f), "forearm_l", V(-1.9f, 0, 0), "upper_arm_r", V(-1.1f, 0, 0.3f), "forearm_r", V(-1.9f, 0, 0),
            "foot_l", V(0.3f, 0, 0), "foot_r", V(0.3f, 0, 0)) },
        { "writhe_a", P("abdomen", V(-0.35f, 0.2f, 0.2f), "chest", V(-0.3f, 0.3f, 0), "neck", V(-0.5f, 0.2f, 0), "head", V(-0.3f, 0, 0),
            "thigh_l", V(-0.9f, 0, 0.1f), "shin_l", V(1.6f, 0, 0), "thigh_r", V(0.15f, 0, -0.1f), "shin_r", V(0.2f, 0, 0),
            "upper_arm_l", V(-0.6f, 0, 0.9f), "forearm_l", V(-1.4f, 0, 0), "upper_arm_r", V(-1.6f, 0, -0.4f), "forearm_r", V(-0.4f, 0, 0),
            "foot_l", V(-0.4f, 0, 0), "foot_r", V(0.4f, 0, 0)) },
        { "writhe_b", P("abdomen", V(0.35f, -0.2f, -0.25f), "chest", V(0.25f, -0.3f, 0), "neck", V(0.3f, -0.4f, 0), "head", V(0.2f, 0, 0),
            "thigh_l", V(0.1f, 0, 0.1f), "shin_l", V(0.3f, 0, 0), "thigh_r", V(-1.1f, 0, -0.1f), "shin_r", V(1.9f, 0, 0),
            "upper_arm_l", V(-1.6f, 0, 0.4f), "forearm_l", V(-0.4f, 0, 0), "upper_arm_r", V(-0.6f, 0, -0.9f), "forearm_r", V(-1.4f, 0, 0),
            "foot_l", V(0.4f, 0, 0), "foot_r", V(-0.4f, 0, 0)) },
        { "clutch", P("abdomen", V(0.35f, 0, 0), "chest", V(0.25f, 0, 0), "neck", V(0.6f, 0, 0), "head", V(0.3f, 0, 0),
            "thigh_l", V(-0.8f, 0, 0), "shin_l", V(1.3f, 0, 0), "thigh_r", V(-0.5f, 0, 0), "shin_r", V(0.9f, 0, 0),
            "upper_arm_l", V(-0.8f, 0, 0.2f), "forearm_l", V(-1.2f, 0, 0), "upper_arm_r", V(-0.8f, 0, -0.2f), "forearm_r", V(-1.2f, 0, 0)) },
        { "crawl_l", P("neck", V(-0.6f, 0, 0), "head", V(-0.3f, 0, 0), "chest", V(-0.15f, 0, 0),
            "upper_arm_l", V(-2.1f, 0, 0.1f), "forearm_l", V(-0.25f, 0, 0), "upper_arm_r", V(-0.5f, 0, -0.2f), "forearm_r", V(-1.6f, 0, 0),
            "thigh_l", V(-0.3f, 0, 0.15f), "shin_l", V(0.6f, 0, 0), "thigh_r", V(0.0f, 0, -0.1f), "shin_r", V(0.2f, 0, 0)) },
        { "crawl_r", P("neck", V(-0.6f, 0, 0), "head", V(-0.3f, 0, 0), "chest", V(-0.15f, 0, 0),
            "upper_arm_l", V(-0.5f, 0, 0.2f), "forearm_l", V(-1.6f, 0, 0), "upper_arm_r", V(-2.1f, 0, -0.1f), "forearm_r", V(-0.25f, 0, 0),
            "thigh_l", V(0.0f, 0, 0.1f), "shin_l", V(0.2f, 0, 0), "thigh_r", V(-0.3f, 0, -0.15f), "shin_r", V(0.6f, 0, 0)) },
        // postura flexora (decorticação): braços fletidos junto ao peito, pernas estendidas, pés em ponta;
        // aparece nas convulsões de quem está morrendo por falta de oxigênio no cérebro
        { "arch", P("abdomen", V(-0.55f, 0, 0), "chest", V(-0.5f, 0, 0), "neck", V(-0.85f, 0, 0), "head", V(-0.55f, 0, 0),
            "thigh_l", V(0.25f, 0, 0.12f), "shin_l", V(0.05f, 0, 0), "thigh_r", V(0.25f, 0, -0.12f), "shin_r", V(0.05f, 0, 0),
            "upper_arm_l", V(-0.7f, 0, 0.35f), "forearm_l", V(-1.9f, 0, 0), "upper_arm_r", V(-0.7f, 0, -0.35f), "forearm_r", V(-1.9f, 0, 0),
            "foot_l", V(-0.7f, 0, 0), "foot_r", V(-0.7f, 0, 0)) },
        // suspiro agônico: cabeça e pescoço jogados para trás, peito sobe
        { "gasp", P("chest", V(-0.22f, 0, 0), "neck", V(-0.75f, 0, 0), "head", V(-0.5f, 0, 0), "abdomen", V(-0.12f, 0, 0)) },
        { "limp", new Dictionary<string, Vector3>() },
    };

    // ---- agonia em camadas: pose-base + ruído por junta + suspiros + espasmos (ver docs/agonia.md)
    float agonyClock, gaspT, gaspCd = 1.5f, spasmT, spasmDur, spasmCd = 3f, spasmAmp, spasmPhase, groanCd = 2f;
    public int spasmCount, gaspCount;
    /// Gancho de teste: força uma ação de agonia (writhe, fetal, clutch...).
    public void DebugAction(string act, string pose, float dur) { action = act; curPose = pose; actionDur = dur; actionTime = 0f; }
    public string AgonyDebug => $"ação={action} pose={curPose} spasmos={spasmCount} suspiros={gaspCount}";
    float writhePhase, writheW;
    readonly Dictionary<string, Vector3> poseBuf = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Vector3> spasmDir = new Dictionary<string, Vector3>();
    static readonly string[] SPINE = { "abdomen", "chest", "neck", "head" };

    /// 0 = longe da morte, 1 = nos últimos instantes (perda de sangue, falta de oxigênio, parada cardíaca).
    float Dying()
    {
        float bf = blood / Anatomy.BLOOD_TOTAL;
        float d = Mathf.Max((0.72f - bf) / 0.3f, hypoxiaTime / 18f);
        if (cardiacArrest) d = 1f;
        return Mathf.Clamp01(d);
    }

    static float Wander(float t, float freq, float seed) => Mathf.PerlinNoise(t * freq + seed * 13.7f, seed * 3.1f) * 2f - 1f;

    /// Reflexos do tronco cerebral, independentes de consciência: suspiros e espasmos. Só impulsos (sem PD), então
    /// funcionam também inconsciente e depois de morto. `scale` apaga os efeitos com o tempo.
    void Reflexes(float dt, float dying, float scale)
    {
        if (scale <= 0.01f || Game.legacyAgony) return;
        // --- suspiro agônico: irregular, cada vez mais espaçado
        gaspCd -= dt;
        if (gaspT > 0f) gaspT -= dt;
        if (gaspCd <= 0f && (dying > 0.2f || consciousness < 0.45f))
        {
            gaspCd = Mathf.Lerp(2.2f, 7f, dying) * U.RandRange(0.7f, 1.5f);
            gaspT = 0.7f; gaspCount++;
            if (parts.TryGetValue("head", out var hd) && hd)
            {
                Game.sfx.Play("gasp", hd.transform.position, -9f * (2f - scale), U.RandRange(0.85f, 1.1f));
                if (hd.parentPart != null && hd.parentPart)
                    hd.rb.AddTorque(-hd.parentPart.transform.right * 6f * scale, ForceMode.VelocityChange);   // joga a cabeça para trás (~6 rad/s, independe da inércia)
            }
            if (parts.TryGetValue("chest", out var ch0) && ch0) ch0.rb.AddForce(Vector3.up * U.RandRange(2f, 5f) * scale, ForceMode.Impulse);
        }
        // --- espasmo: rajada clônica (≈5 Hz) que decai, com extensão em arco no começo
        spasmCd -= dt;
        if (spasmT > 0f)
        {
            spasmT -= dt;
            spasmPhase += dt * U.TAU * U.RandRange(4.2f, 5.6f);
            float env = Mathf.Pow(Mathf.Clamp01(spasmT / spasmDur), 0.7f) * spasmAmp * scale;
            float s = Mathf.Sin(spasmPhase);
            foreach (var p in AttachedLimbs())
            {
                if (!spasmDir.TryGetValue(p.partId, out var dir)) spasmDir[p.partId] = dir = U.RandomUnit();
                p.rb.AddForce(dir * s * env * p.mass * 14f);
            }
            if (parts.TryGetValue("chest", out var ch) && ch && parts.TryGetValue("pelvis", out var pv) && pv)
            {
                ch.rb.AddTorque(ch.transform.right * s * env * 7f);
                pv.rb.AddTorque(pv.transform.right * -s * env * 4f);
            }
        }
        else if (spasmCd <= 0f && dying > 0.3f)
        {
            spasmCd = U.RandRange(2.5f, 6f) * (1.4f - dying);
            spasmDur = U.RandRange(0.9f, 2.2f); spasmT = spasmDur; spasmAmp = Mathf.Lerp(0.7f, 1.2f, dying);
            spasmPhase = 0f; spasmCount++; spasmDir.Clear();
        }
    }

    /// Mistura pose-base com o suspiro e o arco (espasmo) e soma ruído por junta (tremor + vagar lento).
    Dictionary<string, Vector3> ComposePose(Dictionary<string, Vector3> a, Dictionary<string, Vector3> b, float blend, float intensity, float dying)
    {
        poseBuf.Clear();
        if (Game.legacyAgony) { foreach (var kv in a) poseBuf[kv.Key] = kv.Value; return poseBuf; }
        float gaspW = gaspT > 0f ? Mathf.Sin(Mathf.Clamp01(gaspT / 0.7f) * Mathf.PI) : 0f;
        float archW = spasmT > 0f ? Mathf.Clamp01(Mathf.Min(spasmDur - spasmT, spasmT) / 0.25f) * spasmAmp : 0f;
        float tremor = Mathf.Clamp01(pain * consciousness * 0.6f + dying * 0.5f);          // tremor de choque
        int k = 0;
        foreach (var id in Anatomy.ORDER)
        {
            k++;
            Vector3 v = Vector3.zero; bool has = false;
            if (a.TryGetValue(id, out var va)) { v = va; has = true; }
            if (b != null && b.TryGetValue(id, out var vb)) { v = has ? Vector3.Lerp(va, vb, blend) : vb * blend; has = true; }
            if (POSES["gasp"].TryGetValue(id, out var vg)) { v = Vector3.Lerp(v, vg, gaspW); has = true; }
            if (archW > 0f && POSES["arch"].TryGetValue(id, out var vr)) { v = Vector3.Lerp(v, vr, Mathf.Clamp01(archW)); has = true; }
            if (!has && intensity > 0.2f && id != "pelvis") { v = Vector3.zero; has = true; }
            if (!has) continue;
            bool limb = id.StartsWith("upper") || id.StartsWith("fore") || id.StartsWith("thigh") || id.StartsWith("shin") || id.StartsWith("hand") || id.StartsWith("foot");
            float amp = (limb ? 0.20f : 0.07f) * Mathf.Clamp01(intensity);
            var n = new Vector3(Wander(agonyClock, 0.9f, k), Wander(agonyClock, 0.8f, k + 40f), Wander(agonyClock, 0.7f, k + 80f)) * amp;
            n += new Vector3(Mathf.Sin(agonyClock * 47f + k), Mathf.Sin(agonyClock * 53f + k * 2f), 0f) * 0.03f * tremor;
            poseBuf[id] = v + n;
        }
        return poseBuf;
    }

    void RagdollStep(float dt)
    {
        foreach (var p in ValidParts())
        {
            if (p.Freeze) continue;
            var v = p.rb.linearVelocity;
            float drop = p.lastVel.magnitude - v.magnitude;
            p.impactGrace -= dt;
            if (drop > 8.5f && p.impactGrace <= 0f && !p.severed) Impact(p, drop);
            p.lastVel = v;
        }
        foreach (var p in ValidParts()) p.pressure = U.MoveToward(p.pressure, 0f, dt * 1.5f);
        agonyClock += dt;
        if (dead) { DeathTwitch(dt); return; }
        if (state == State.UNCONSCIOUS) { muscleLevel = 0f; Twitch(dt, 0.15f); Reflexes(dt, Mathf.Max(0.5f, Dying()), 0.8f); return; }
        Agony(dt);
    }

    List<DummyPart> AttachedLimbs()
    {
        var arr = new List<DummyPart>();
        foreach (var p in ValidParts()) if (!p.severed && p.IsLimb()) arr.Add(p);
        return arr;
    }

    void DeathTwitch(float dt)
    {
        float since = timeAlive - deathTime;
        Reflexes(dt, 1f, Mathf.Clamp01(1f - since / 14f));
        if (since > 7f) return;
        twitchCd -= dt;
        if (twitchCd <= 0f)
        {
            twitchCd = U.RandRange(0.25f, 0.9f) + since * 0.3f;
            var arr = AttachedLimbs();
            if (arr.Count > 0)
            {
                var p = U.Pick(arr);
                p.rb.AddForce(U.RandomUnit() * U.RandRange(0.4f, 1.6f) * (1f - since / 7f) * p.mass, ForceMode.Impulse);
            }
            if (parts.TryGetValue("chest", out var ch) && ch && since < 4f && U.Randf() < 0.4f)
                ch.rb.AddForce(Vector3.up * U.RandRange(2f, 6f), ForceMode.Impulse);   // respiração agônica
        }
    }

    void Twitch(float dt, float amount)
    {
        twitchCd -= dt;
        if (twitchCd <= 0f)
        {
            twitchCd = U.RandRange(1.5f, 4f);
            var arr = AttachedLimbs();
            if (arr.Count > 0)
            {
                var p = U.Pick(arr);
                p.rb.AddForce(U.RandomUnit() * amount * 4f * p.mass, ForceMode.Impulse);
            }
        }
    }

    public bool LimbUsable(string side)
    {
        if (paralysis == "tetraplegia") return false;
        foreach (var id in new[] { "upper_arm_" + side, "forearm_" + side, "hand_" + side })
        {
            if (!parts.TryGetValue(id, out var p) || !p || p.severed || p.destroyed || p.MainBoneFractured()) return false;
        }
        return true;
    }

    string WoundSide(Wound w)
    {
        string id = w.part.partId;
        if (id.StartsWith("upper_arm") || id.StartsWith("forearm") || id.StartsWith("hand")) return id.Substring(id.Length - 1);
        return "";
    }

    public Wound WorstWound()
    {
        Wound best = null;
        float bestRate = 0.6f;
        foreach (var p in ValidParts())
        {
            if (p.severed) continue;
            foreach (var w in p.wounds)
            {
                float r = w.ExternalRate() * (w.arterial ? 2f : 1f);
                if (r > bestRate)
                {
                    string side = WoundSide(w);
                    string other = side == "l" ? "r" : "l";
                    if (side == "" && !(LimbUsable("l") || LimbUsable("r"))) continue;
                    if (side != "" && !LimbUsable(other)) continue;
                    bestRate = r; best = w;
                }
            }
        }
        return best;
    }

    void ChooseAction()
    {
        actionTime = 0f; reachTimer = 0f; pressTimer = 0f;
        float pf = pain * consciousness;
        var w = WorstWound();
        bool legsBad = paralysis != "" || LegBad("l") || LegBad("r");
        if (w != null && U.Randf() < 0.8f)
        {
            action = "reach";
            reachWound = w;
            string side = WoundSide(w);
            var wp = w.part.Xform.Apply(w.localPos);
            if (side != "") reachHand = parts["hand_" + (side == "l" ? "r" : "l")];
            else
            {
                var cand = new List<DummyPart>();
                foreach (var s in new[] { "l", "r" }) if (LimbUsable(s)) cand.Add(parts["hand_" + s]);
                cand.Sort((a, b) => Vector3.Distance(a.transform.position, wp).CompareTo(Vector3.Distance(b.transform.position, wp)));
                reachHand = cand[0];
            }
            actionDur = U.RandRange(5f, 8f);
            curPose = "clutch";
        }
        else if (pf > 0.45f && U.Randf() < 0.75f) { action = "writhe"; actionDur = U.RandRange(2.5f, 5f); curPose = "writhe_a"; }
        // já tentou levantar sem a perna e não conseguiu: arrastar-se com os braços vira a saída
        else if (legsBad && consciousness > 0.6f && paralysis != "tetraplegia" && U.Randf() < (legTries > 0 ? 0.9f : 0.6f)) { action = "crawl"; actionDur = U.RandRange(5f, 9f); curPose = "crawl_l"; }
        else if (pf > 0.2f) { action = "fetal"; actionDur = U.RandRange(2.5f, 4.5f); curPose = "fetal"; }
        else { action = "rest"; actionDur = U.RandRange(2f, 4f); curPose = "limp"; }
    }

    bool LegBad(string side)
    {
        foreach (var id in new[] { "thigh_" + side, "shin_" + side, "foot_" + side })
            if (!parts.TryGetValue(id, out var p) || !p || p.severed || p.destroyed || p.MainBoneFractured()) return true;
        return false;
    }

    void Agony(float dt)
    {
        float vigor = Mathf.Clamp((blood / Anatomy.BLOOD_TOTAL - 0.45f) / 0.4f, 0.08f, 1f);
        muscleLevel = consciousness * vigor * (cardiacArrest ? 0.25f : 1f);
        float pf = pain * consciousness;
        float intensity = Mathf.Clamp(0.35f + pf, 0.35f, 1.3f) * muscleLevel;
        float dying = Dying();
        Reflexes(dt, dying, 1f);
        groanCd -= dt;
        if (groanCd <= 0f)
        {
            groanCd = U.RandRange(3f, 7f) * Mathf.Lerp(1f, 1.8f, dying);
            if (!Game.legacyAgony && pf > 0.45f && consciousness > 0.35f && parts.TryGetValue("head", out var gh) && gh)
                Game.sfx.Play("groan", gh.transform.position, -10f, U.RandRange(0.85f, 1.12f));
        }
        actionTime += dt;
        if (actionTime > actionDur) ChooseAction();
        var skip = new List<string>();
        switch (action)
        {
            case "reach":
                if (!reachHand || reachHand.severed || reachWound == null || !reachWound.part || reachWound.part.severed) { ChooseAction(); return; }
                string side = reachHand.partId.Substring(reachHand.partId.Length - 1);
                skip.AddRange(new[] { "upper_arm_" + side, "forearm_" + side, "hand_" + side });
                var wx = reachWound.part.Xform;
                var wp = wx.Apply(reachWound.localPos);
                var wn = (wx.q * reachWound.localNormal).normalized;
                var target = wp + wn * 0.045f;
                var err = target - reachHand.transform.position;
                var f = err * 900f * muscleLevel - reachHand.rb.linearVelocity * 30f;
                f = U.LimitLength(f, 200f * muscleLevel);
                reachHand.rb.AddForce(f);
                parts["forearm_" + side].rb.AddForce(f * 0.5f);
                if (parts.TryGetValue("chest", out var ch) && ch) ch.rb.AddForce(-f * 1.2f);
                reachTimer += dt;
                if (err.magnitude < 0.11f)
                {
                    pressTimer += dt;
                    reachWound.part.pressure = 1f;
                    if (pressTimer > 0.3f && (pressTimer % 1f) < dt)
                    {
                        reachWound.part.AddBloodiness(0.02f);
                        reachHand.AddBloodiness(0.08f);
                    }
                    if (pressTimer > 2.6f) TrySelfTourniquet(reachWound);
                }
                else if (reachTimer > 6f && pressTimer < 0.1f) actionTime = actionDur;
                break;
            case "writhe":
                if (Game.legacyAgony)
                {
                    poseTimer -= dt;
                    if (poseTimer <= 0f)
                    {
                        curPose = curPose == "writhe_a" ? "writhe_b" : "writhe_a";
                        poseTimer = U.RandRange(0.45f, 1.1f) / Mathf.Max(0.4f, intensity);
                        rollSign = -rollSign;
                    }
                    Roll(rollSign * 55f * intensity);
                }
                else
                {
                    // vai e volta entre as duas poses de forma contínua (seno), com a velocidade ligada à dor/força
                    writhePhase += dt * Mathf.Max(1.1f, 4.2f * intensity) * (1f + 0.25f * Wander(agonyClock, 0.6f, 5f));
                    writheW = 0.5f + 0.5f * Mathf.Sin(writhePhase);
                    Roll(Mathf.Cos(writhePhase) * 62f * intensity);
                }
                if (U.Randf() < dt * 2f * intensity)
                {
                    var arr = AttachedLimbs();
                    if (arr.Count > 0) { var p = U.Pick(arr); p.rb.AddForce(U.RandomUnit() * 1.2f * intensity * p.mass, ForceMode.Impulse); }
                }
                break;
            case "fetal":
                Roll(Mathf.Sin(actionTime * 2.2f) * 25f * intensity);
                break;
            case "crawl":
                Crawl(dt);
                break;
        }
        bool blendW = action == "writhe" && !Game.legacyAgony;
        var baseA = POSES[blendW ? "writhe_a" : curPose];
        var baseB = blendW ? POSES["writhe_b"] : null;
        ApplyPose(ComposePose(baseA, baseB, blendW ? writheW : 0f, intensity, dying), skip, 1f);
    }

    void Roll(float torque)
    {
        if (!parts.TryGetValue("pelvis", out var pel) || !pel || !parts.TryGetValue("chest", out var ch) || !ch) return;
        var axis = (ch.transform.position - pel.transform.position).normalized;
        ch.rb.AddTorque(axis * torque);
        pel.rb.AddTorque(axis * torque * 0.8f);
    }

    void Crawl(float dt)
    {
        var pel = parts["pelvis"]; var ch = parts["chest"];
        var front = ch.transform.rotation * Vector3.forward;
        var lng = (ch.transform.position - pel.transform.position).normalized;
        if (Vector3.Dot(front, Vector3.down) < 0.35f)
        {
            // vira de bruços primeiro
            float s = Mathf.Sign(Vector3.Dot(Vector3.Cross(front, Vector3.down), lng));
            if (s == 0f) s = 1f;
            Roll(s * 90f * muscleLevel);
            curPose = "crawl_l";
            return;
        }
        float phase = actionTime % 1.5f;
        curPose = phase < 0.75f ? "crawl_l" : "crawl_r";
        var fwd = new Vector3(lng.x, 0, lng.z);
        if (fwd.magnitude < 0.1f) return;
        fwd.Normalize();
        float pull = Mathf.Sin((phase % 0.75f) / 0.75f * Mathf.PI);
        ch.rb.AddForce(fwd * 520f * pull * muscleLevel + Vector3.up * 120f * pull * muscleLevel);
        pel.rb.AddForce(fwd * 160f * pull * muscleLevel);
    }

    void ApplyPose(Dictionary<string, Vector3> pose, List<string> skip, float scale)
    {
        foreach (var kv in pose)
        {
            if (skip.Contains(kv.Key)) continue;
            if (!parts.TryGetValue(kv.Key, out var p) || !p || p.severed || p.destroyed) continue;
            Drive(p, kv.Value, scale);
        }
    }

    float JointStrength(DummyPart p)
    {
        float s = muscleLevel;
        string id = p.partId;
        if (paralysis == "tetraplegia" && !(id == "head" || id == "neck")) return 0f;
        if (paralysis == "paraplegia" && (id.StartsWith("thigh") || id.StartsWith("shin") || id.StartsWith("foot"))) return 0f;
        if (p.MainBoneFractured()) s *= 0.12f;
        else if (p.parentPart != null && p.IsLimb() && p.parentPart.MainBoneFractured()) s *= 0.25f;
        if (p.HasTourniquetAbove()) s *= 0.6f;
        return s;
    }

    /// Torque PD da junta para levar a parte à rotação alvo relativa ao pai (o "músculo")
    void Drive(DummyPart p, Vector3 target, float scale)
    {
        var par = p.parentPart;
        if (par == null || !par || p.joint == null) return;
        float strength = JointStrength(p) * scale;
        if (strength <= 0.01f) return;
        float maxT = p.def.joint.muscle * strength;
        var qd = par.transform.rotation * U.Euler(target);
        var qc = p.transform.rotation;
        var qe = qd * Quaternion.Inverse(qc);
        if (qe.w < 0f) { qe.x = -qe.x; qe.y = -qe.y; qe.z = -qe.z; qe.w = -qe.w; }
        float angle = 2f * Mathf.Acos(Mathf.Clamp(qe.w, -1f, 1f));
        var axis = new Vector3(qe.x, qe.y, qe.z);
        var err = axis.magnitude > 1e-6f ? axis.normalized * angle : Vector3.zero;
        var relW = p.rb.angularVelocity - par.rb.angularVelocity;
        float ie = chainInertia.TryGetValue(p.partId, out var ii) ? ii : 0.1f;
        var tq = U.LimitLength((err * 60f - relW * 12f) * ie, maxT);
        p.rb.AddTorque(tq);
        par.rb.AddTorque(-tq);
    }

    void Impact(DummyPart p, float dv)
    {
        p.impactGrace = 0.25f;
        var main = new List<BoneState>();
        foreach (var b0 in p.bones) if (!b0.def.shell || b0.name == "Crânio") main.Add(b0);
        if (main.Count == 0) return;
        var b = U.Pick(main);
        float dmg = (dv - 8.5f) * 14f;
        b.hp -= dmg;
        acutePain += 0.08f + dv * 0.01f;
        if (b.hp <= 0f && !b.fractured)
        {
            b.fractured = true; b.hp = 0f;
            p.RefreshBoneVisual(b);
            acutePain += 0.3f;
            Game.sfx.Play("bone", p.transform.position, 0f);
            Game.LogEvent($"Impacto ({dv:F0} m/s) → {p.def.name}: {b.name} FRATURADO", new Color(1, 0.7f, 0.4f));
            if (b.name == "Crânio") DamageOrgan(p, "brain", 25f);
            if (b.name == "Vértebras cervicais") DamageOrgan(p, "spinal_c", 60f);
        }
        if (p.partId == "head" && dv > 14f)
        {
            concussion = Mathf.Min(1f, concussion + (dv - 14f) * 0.08f);
            DamageOrgan(p, "brain", (dv - 14f) * 3f);
        }
        if (p.wounds.Count > 0) Game.blood.Spray(p.transform.position, Vector3.up, 6, 1.5f, 0.9f, p.col);
    }

    public void DamageOrgan(DummyPart p, string vital, float dmg)
    {
        var o = p.Organ(vital);
        if (o == null) return;
        o.hp = Mathf.Max(0f, o.hp - dmg);
        if (o.hp <= 0f && !o.destroyed)
        {
            o.destroyed = true;
            Game.LogEvent($"{p.def.name}: {o.name} DESTRUÍDO", new Color(1, 0.3f, 0.3f));
        }
        p.RefreshOrganVisual(o);
    }

    void TrySelfTourniquet(Wound w)
    {
        if (tourniquetsLeft <= 0) return;
        string site = Anatomy.TourniquetSite(w.part.partId);
        if (site == "") return;
        var sp = parts[site];
        if (!sp || sp.severed || sp.tourniquet) return;
        bool heavy = w.arterial || w.ExternalRate() > 3f;
        if (!heavy) return;
        if (ApplyTourniquet(sp, true))
        {
            tourniquetsLeft--;
            actionTime = actionDur - 0.6f;
        }
    }

    // ------------------------------------------------------------------ tratamento

    public bool ApplyTourniquet(DummyPart p, bool bySelf = false)
    {
        if (p.partId.StartsWith("hand") || p.partId.StartsWith("foot")) p = p.parentPart;
        if (p == null || !p || !p.IsLimb() || p.severed || p.tourniquet || p.destroyed) return false;
        p.tourniquet = true;
        float r = p.PartRadius();
        float y = p.def.shape == "capsule" ? p.def.len * 0.28f : 0f;
        var band = Prims.Visual("tourniquet", Prims.Torus(r * 0.9f, r * 1.22f, 20), Mats.Mat(new Color(0.08f, 0.08f, 0.09f), 0.6f), p.transform);
        band.transform.localPosition = new Vector3(0, y, 0);
        band.transform.localScale = new Vector3(1, 1.6f, 1);
        var tag = Prims.Visual("tag", Prims.Box(new Vector3(0.025f, 0.02f, 0.012f)), Mats.Mat(new Color(1f, 0.45f, 0.05f), 0.5f), p.transform);
        tag.transform.localPosition = new Vector3(0, y, r * 1.25f);
        acutePain += 0.25f;
        Game.sfx.Play("tourniquet", p.transform.position, -2f);
        Game.LogEvent($"Torniquete aplicado: {p.def.name}{(bySelf ? " (pelo próprio boneco)" : "")}", new Color(0.5f, 0.8f, 1f));
        return true;
    }

    public bool ApplyBandage(DummyPart p)
    {
        if (p == null || !p || p.destroyed || p.bandage > 0f) return false;
        p.bandage = 1f;
        var mat = Mats.Mat(new Color(0.95f, 0.95f, 0.92f), 0.9f);
        Mesh m;
        switch (p.def.shape)
        {
            case "capsule": m = Prims.Cylinder(p.def.r * 1.08f, p.def.r * 1.08f, p.def.len * 0.45f); break;
            case "box": m = Prims.Box(Vector3.Scale(p.def.size, new Vector3(1.04f, 0.5f, 1.06f))); break;
            default: m = Prims.Sphere(p.def.r * 1.04f, p.def.r * 1.2f); break;
        }
        Prims.Visual("bandage", m, mat, p.transform);
        p.bandageMat = mat;
        Game.LogEvent($"Bandagem aplicada: {p.def.name}", new Color(0.7f, 0.9f, 1f));
        Game.sfx.Play("tourniquet", p.transform.position, -8f, 1.4f);
        return true;
    }
}
