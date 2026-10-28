using System.Collections.Generic;
using UnityEngine;

/// Armadilhas e máquinas para testar o dano nos bonecos: guilhotina, prensa hidráulica, serra circular, pêndulo,
/// espinhos e lança-chamas. Ficam na "zona de testes" ao fundo do galpão (z ≈ 24). Tecla G aciona a que o jogador mira.
/// Todas usam a API de dano existente (ApplyPenetration, SeverPart, DestroyPart, Flames), então ferimentos, sangue,
/// amputações e raio-X funcionam como nos tiros.
public abstract class Hazard : MonoBehaviour
{
    public string label = "armadilha";
    public bool Busy { get; protected set; }
    public int hitsDealt;                       // golpes de dano aplicados (usado pelo --hazardtest)
    public static readonly List<Hazard> All = new List<Hazard>();
    static readonly Collider[] buf = new Collider[64];
    readonly Dictionary<DummyPart, float> lastHit = new Dictionary<DummyPart, float>();

    protected virtual void Awake() { All.Add(this); }
    void OnDestroy() { All.Remove(this); }

    public abstract void Trigger();

    protected static void Overlap(Vector3 c, Vector3 half, Quaternion rot, List<DummyPart> res)
    {
        res.Clear();
        int n = Physics.OverlapBoxNonAlloc(c, half, buf, rot, Game.L_DUMMY, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var p = buf[i].GetComponent<DummyPart>();
            if (p && !p.destroyed && !res.Contains(p)) res.Add(p);
        }
    }

    /// Dano com intervalo mínimo por parte (evita aplicar o mesmo golpe a cada passo de física).
    protected bool Strike(DummyPart p, Vector3 point, Vector3 dir, float energy, Prof prof, float cooldown, bool sever = false)
    {
        if (lastHit.TryGetValue(p, out var t) && Time.time - t < cooldown) return false;
        lastHit[p] = Time.time;
        if (!p || p.destroyed) return false;
        p.dummy.ApplyPenetration(p, point, dir.normalized, energy, prof, -dir.normalized);
        if (sever && p && p.def.severable && !p.severed && !p.cutHere) p.dummy.SeverPart(p, dir);
        hitsDealt++;
        return true;
    }

    protected static void Blood(Vector3 pos, Vector3 dir, DummyPart p, int n = 10, float speed = 4f)
    {
        if (Game.blood != null && p != null) Game.blood.Spray(pos, dir, n, speed, 0.9f, p.col);
    }

    // ------------------------------------------------------------------ construção
    protected static GameObject Solid(Transform parent, string name, Vector3 size, Vector3 localPos, Material mat, bool collider = true)
    {
        var go = Prims.Visual(name, Prims.Box(size), mat, parent);
        go.transform.localPosition = localPos;
        if (collider)
        {
            go.layer = Game.LAYER_WORLD;
            go.AddComponent<BoxCollider>().size = size;
        }
        return go;
    }

    protected static Rigidbody Kinematic(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        return rb;
    }

    protected static Material Steel(float dark = 0f) => Mats.Mat(new Color(0.42f - dark, 0.44f - dark, 0.48f - dark), 0.38f, 0.6f);
    protected static Material Paint(Color c) => Mats.Mat(c, 0.55f, 0.25f);

    protected static GameObject Sign(Transform parent, string text, Vector3 localPos)
    {
        var go = new GameObject("placa");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var plate = Prims.Visual("p", Prims.Box(new Vector3(1.0f, 0.22f, 0.03f)), Mats.Emissive(new Color(1f, 0.75f, 0.1f), 1.6f), go.transform, false);
        plate.transform.localPosition = Vector3.zero;
        return go;
    }

    // ------------------------------------------------------------------ gerenciamento
    /// Aciona a armadilha mais próxima da mira (a menos de 26° e 14 m). Devolve a escolhida.
    public static Hazard ActivateAimed(Vector3 from, Vector3 fwd)
    {
        Hazard best = null; float bestAng = 26f;
        foreach (var h in All)
        {
            var to = h.transform.position + Vector3.up * 1.2f - from;
            if (to.magnitude > 14f) continue;
            float ang = Vector3.Angle(fwd, to);
            if (ang < bestAng) { bestAng = ang; best = h; }
        }
        if (best != null)
        {
            if (best.Busy) { if (Game.hud) Game.hud.Toast(best.label + ": em funcionamento"); }
            else { best.Trigger(); if (Game.hud) Game.hud.Toast(best.label + " acionada"); Game.LogEvent(best.label + " acionada", new Color(1f, 0.8f, 0.4f)); }
        }
        else if (Game.hud) Game.hud.Toast("Mire em uma armadilha (zona de testes ao fundo, z ≈ 24) e aperte G");
        return best;
    }

    /// Monta a zona de testes inteira. `root` é o transform do mapa.
    public static void BuildZone(Transform root)
    {
        float z = 24f;
        Guillotine.Build(root, new Vector3(-26f, 0, z), 0f);
        HydraulicPress.Build(root, new Vector3(-18f, 0, z));
        CircularSaw.Build(root, new Vector3(-10f, 0, z));
        SwingingHammer.Build(root, new Vector3(10f, 0, z));
        SpikeTrap.Build(root, new Vector3(18f, 0, z));
        FlameJet.Build(root, new Vector3(26f, 0, z));
        // faixa de piso e placa da zona
        var stripe = Prims.Visual("faixa", Prims.Box(new Vector3(62f, 0.012f, 0.25f)), Mats.Mat(new Color(0.95f, 0.68f, 0.08f), 0.6f), root, false);
        stripe.transform.localPosition = new Vector3(0f, 0.007f, z - 3.6f);
    }
}

/// Guilhotina: a lâmina cai por gravidade sobre o cepo e corta o que estiver no meio.
public class Guillotine : Hazard
{
    Rigidbody blade; float y, vy; int phase; float hold;
    float topY = 3.15f, bottomY = 1.35f;
    readonly HashSet<DummyPart> cutNow = new HashSet<DummyPart>();
    readonly List<DummyPart> tmp = new List<DummyPart>();
    Vector3 basePos;

    public static Guillotine Build(Transform root, Vector3 pos, float yaw)
    {
        var go = new GameObject("Guilhotina");
        go.transform.SetParent(root, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var g = go.AddComponent<Guillotine>(); g.label = "Guilhotina"; g.basePos = pos;
        var wood = Mats.Mat(new Color(0.32f, 0.2f, 0.1f), 0.8f);
        var steel = Steel();
        Solid(go.transform, "poste_l", new Vector3(0.14f, 3.7f, 0.22f), new Vector3(-0.62f, 1.85f, 0), wood);
        Solid(go.transform, "poste_r", new Vector3(0.14f, 3.7f, 0.22f), new Vector3(0.62f, 1.85f, 0), wood);
        Solid(go.transform, "viga", new Vector3(1.5f, 0.2f, 0.3f), new Vector3(0, 3.7f, 0), wood);
        // cepo (mesa de apoio do pescoço) e tábua inferior
        Solid(go.transform, "cepo", new Vector3(1.5f, 0.14f, 2.7f), new Vector3(0, 1.05f, 1.15f), wood);
        foreach (var lx in new[] { -0.65f, 0.65f }) foreach (var lz in new[] { 0.1f, 2.2f })
            Solid(go.transform, "perna", new Vector3(0.14f, 0.98f, 0.14f), new Vector3(lx, 0.49f, lz), wood);
        // lâmina
        var b = Solid(go.transform, "lamina", new Vector3(1.0f, 0.46f, 0.045f), new Vector3(0, g.topY, 0), steel);
        var bevel = Prims.Visual("fio", Prims.Box(new Vector3(1.0f, 0.04f, 0.055f)), Mats.Mat(new Color(0.8f, 0.82f, 0.85f), 0.2f, 0.9f), b.transform);
        bevel.transform.localPosition = new Vector3(0, -0.21f, 0);
        var weight = Prims.Visual("peso", Prims.Box(new Vector3(1.0f, 0.2f, 0.12f)), Mats.Mat(new Color(0.15f, 0.15f, 0.17f), 0.5f, 0.6f), b.transform);
        weight.transform.localPosition = new Vector3(0, 0.3f, 0);
        g.blade = Kinematic(b);
        g.y = g.topY;
        Sign(go.transform, "GUILHOTINA", new Vector3(0, 2.2f, -0.5f));
        Game.MakeLight(LightType.Point, go.transform, new Vector3(0, 3.0f, -1.5f), Quaternion.identity, new Color(1f, 0.85f, 0.6f), 1.6f, 7f);
        return g;
    }

    public override void Trigger()
    {
        if (Busy) return;
        Busy = true; phase = 1; vy = 0f; cutNow.Clear();
        Game.sfx.Play("draw_blade", transform.position + Vector3.up * 3f, -2f, 0.7f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (phase == 1)
        {
            vy += 16f * dt; y -= vy * dt;
            if (y <= bottomY) { y = bottomY; phase = 2; hold = 2.5f; Game.sfx.Play("thunk", transform.position + Vector3.up, 4f, 0.6f); }
            Cut(vy);
        }
        else if (phase == 2) { hold -= dt; if (hold <= 0f) phase = 3; }
        else if (phase == 3)
        {
            y += 0.9f * dt;
            if (y >= topY) { y = topY; phase = 0; Busy = false; cutNow.Clear(); }
        }
        blade.MovePosition(transform.TransformPoint(new Vector3(0, y, 0)));
    }

    void Cut(float speed)
    {
        if (speed < 1.2f) return;
        var c = transform.TransformPoint(new Vector3(0, y - 0.12f, 0));
        Overlap(c, new Vector3(0.5f, 0.16f, 0.03f), transform.rotation, tmp);      // fatia fina: só o que cruza o plano da lâmina
        foreach (var p in tmp)
        {
            if (cutNow.Contains(p) || !p.dummy) continue;
            cutNow.Add(p);
            Strike(p, p.transform.position, Vector3.down, 3500f, Weapons.P_CLAYMORE, 0f, true);
            Blood(p.transform.position, Vector3.up, p, 14, 5f);
        }
    }
}

/// Prensa hidráulica: o êmbolo desce devagar, segura e sobe; o que estiver embaixo é esmagado.
public class HydraulicPress : Hazard
{
    Rigidbody platen; float y; int phase; float hold;
    const float TOP = 2.7f, BED = 0.36f, HALF = 0.23f;
    readonly List<DummyPart> tmp = new List<DummyPart>();
    readonly HashSet<DummyPart> gibbed = new HashSet<DummyPart>();

    public static HydraulicPress Build(Transform root, Vector3 pos)
    {
        var go = new GameObject("Prensa hidráulica");
        go.transform.SetParent(root, false); go.transform.localPosition = pos;
        var p = go.AddComponent<HydraulicPress>(); p.label = "Prensa hidráulica";
        var blue = Paint(new Color(0.12f, 0.3f, 0.55f)); var steel = Steel(0.08f); var yellow = Paint(new Color(0.95f, 0.68f, 0.08f));
        Solid(go.transform, "leito", new Vector3(1.9f, BED, 1.9f), new Vector3(0, BED / 2, 0), steel);
        foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            Solid(go.transform, "coluna", new Vector3(0.16f, 3.7f, 0.16f), new Vector3(sx * 0.95f, 1.85f, sz * 0.95f), blue);
        Solid(go.transform, "cabeçote", new Vector3(2.2f, 0.7f, 2.2f), new Vector3(0, 3.55f, 0), blue);
        var cyl = Prims.Visual("cilindro", Prims.Cylinder(0.26f, 0.26f, 0.9f, 20), steel, go.transform);
        cyl.transform.localPosition = new Vector3(0, 3.05f, 0);
        var pl = Solid(go.transform, "êmbolo", new Vector3(1.55f, HALF * 2, 1.55f), new Vector3(0, TOP, 0), Paint(new Color(0.6f, 0.15f, 0.1f)));
        var stripe = Prims.Visual("faixa", Prims.Box(new Vector3(1.56f, 0.05f, 1.56f)), yellow, pl.transform);
        stripe.transform.localPosition = new Vector3(0, -HALF + 0.03f, 0);
        p.platen = Kinematic(pl); p.y = TOP;
        Sign(go.transform, "PRENSA", new Vector3(0, 1.7f, -1.1f));
        Game.MakeLight(LightType.Point, go.transform, new Vector3(0, 3.2f, -2.2f), Quaternion.identity, new Color(1f, 0.85f, 0.6f), 1.6f, 7f);
        return p;
    }

    public override void Trigger() { if (!Busy) { Busy = true; phase = 1; gibbed.Clear(); Game.sfx.Play("rack", transform.position + Vector3.up * 2f, -2f, 0.5f); } }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float minY = BED + HALF + 0.02f;
        if (phase == 1) { y -= 0.55f * dt; if (y <= minY) { y = minY; phase = 2; hold = 1.6f; Game.sfx.Play("thunk", transform.position + Vector3.up, 6f, 0.5f); } }
        else if (phase == 2) { hold -= dt; if (hold <= 0f) phase = 3; }
        else if (phase == 3) { y += 0.9f * dt; if (y >= TOP) { y = TOP; phase = 0; Busy = false; } }
        platen.MovePosition(transform.TransformPoint(new Vector3(0, y, 0)));
        if (phase == 1 || phase == 2) Crush();
    }

    void Crush()
    {
        float bottom = y - HALF;                                   // face de baixo do êmbolo (local)
        float clearance = bottom - BED;
        var c = transform.TransformPoint(new Vector3(0, bottom - 0.06f, 0));
        Overlap(c, new Vector3(0.78f, 0.1f, 0.78f), transform.rotation, tmp);
        foreach (var p in tmp)
        {
            if (!p.dummy) continue;
            float thick = Mathf.Max(0.06f, Mathf.Min(p.col.bounds.size.y, 0.4f));
            if (clearance > thick * 0.95f) continue;
            float squeeze = Mathf.Clamp01((thick - clearance) / thick);
            // esmagamento contundente: osso quebra, órgão rompe; sangue esguicha para os lados
            if (Strike(p, p.transform.position, Vector3.down, 90f + squeeze * 1600f, Weapons.P_HAMMER, 0.22f))
            {
                var side = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward;
                Blood(p.transform.position, side + Vector3.up * 0.3f, p, 12, 3.5f);
                Game.sfx.Play("bone", p.transform.position, 2f, Random.Range(0.85f, 1.1f));
            }
            // quase fechado: membros e cabeça viram papa
            if (clearance < 0.075f && p.def.severable && p.def.leaf && !gibbed.Contains(p))
            {
                gibbed.Add(p);
                p.dummy.DestroyPart(p, Vector3.down);
            }
        }
    }
}

/// Serra circular: o carro corre por um trilho, cruzando a mesa; o disco gira e corta o que estiver no caminho.
public class CircularSaw : Hazard
{
    Rigidbody carriage; Transform disc; float x, dir = 1f, runT; int phase;
    const float RANGE = 1.15f, DISC_R = 0.45f;
    readonly List<DummyPart> tmp = new List<DummyPart>();
    readonly Dictionary<DummyPart, int> cuts = new Dictionary<DummyPart, int>();

    public static CircularSaw Build(Transform root, Vector3 pos)
    {
        var go = new GameObject("Serra circular");
        go.transform.SetParent(root, false); go.transform.localPosition = pos;
        var s = go.AddComponent<CircularSaw>(); s.label = "Serra circular";
        var steel = Steel(0.05f); var orange = Paint(new Color(0.9f, 0.4f, 0.05f));
        // mesa comprida (corpo deitado ao longo do eixo Z) com fenda para o disco
        Solid(go.transform, "mesa", new Vector3(2.0f, 0.12f, 2.6f), new Vector3(0, 0.9f, 0), steel);
        foreach (var sx in new[] { -0.85f, 0.85f }) foreach (var sz in new[] { -1.15f, 1.15f })
            Solid(go.transform, "perna", new Vector3(0.12f, 0.84f, 0.12f), new Vector3(sx, 0.42f, sz), steel);
        // trilho e pórtico do carro
        Solid(go.transform, "trilho", new Vector3(3.4f, 0.1f, 0.16f), new Vector3(0, 2.0f, 0), orange);
        foreach (var sx in new[] { -1.7f, 1.7f }) Solid(go.transform, "suporte", new Vector3(0.14f, 2.0f, 0.2f), new Vector3(sx, 1.0f, 0), orange);
        var car = new GameObject("carro"); car.transform.SetParent(go.transform, false); car.transform.localPosition = new Vector3(0, 1.45f, 0);
        var arm = Prims.Visual("braço", Prims.Box(new Vector3(0.14f, 0.7f, 0.18f)), orange, car.transform);
        arm.transform.localPosition = new Vector3(0, 0.3f, 0);
        var motor = Prims.Visual("motor", Prims.Box(new Vector3(0.3f, 0.26f, 0.3f)), steel, car.transform);
        motor.transform.localPosition = new Vector3(0, -0.05f, 0.0f);
        // disco (normal em Z, gira em torno de Z): cilindro girado
        var d = Prims.Visual("disco", Prims.Cylinder(DISC_R, DISC_R, 0.025f, 40), Mats.Mat(new Color(0.75f, 0.77f, 0.8f), 0.25f, 0.9f), car.transform);
        d.transform.localRotation = Quaternion.Euler(90, 0, 0);
        d.transform.localPosition = new Vector3(0, -0.45f, 0);
        for (int i = 0; i < 24; i++)                                    // dentes
        {
            float a = i * Mathf.PI * 2f / 24f;
            var t = Prims.Visual("dente", Prims.Box(new Vector3(0.05f, 0.07f, 0.03f)), Mats.Mat(new Color(0.9f, 0.9f, 0.92f), 0.2f, 0.9f), d.transform, false);
            t.transform.localPosition = new Vector3(Mathf.Cos(a) * DISC_R, 0f, Mathf.Sin(a) * DISC_R);
            t.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
        }
        s.disc = d.transform; s.carriage = Kinematic(car);
        car.layer = Game.LAYER_WORLD;
        var bc = car.AddComponent<BoxCollider>(); bc.size = new Vector3(0.3f, 0.3f, 0.3f);
        s.x = -RANGE;
        Sign(go.transform, "SERRA", new Vector3(0, 2.6f, -1.5f));
        Game.MakeLight(LightType.Point, go.transform, new Vector3(0, 2.8f, -1.8f), Quaternion.identity, new Color(1f, 0.9f, 0.7f), 1.6f, 7f);
        return s;
    }

    public override void Trigger() { if (!Busy) { Busy = true; phase = 1; runT = 0f; cuts.Clear(); } }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (phase == 1)
        {
            runT += dt;
            x += dir * 0.55f * dt;                                      // avanço lento, como serra de bancada
            if (Mathf.Abs(x) >= RANGE) { dir = -dir; x = Mathf.Clamp(x, -RANGE, RANGE); if (runT > 4.5f) { phase = 2; } }
        }
        else if (phase == 2)
        {
            // volta ao ponto de partida e para
            x = Mathf.MoveTowards(x, -RANGE, 1.4f * dt);
            if (Mathf.Abs(x + RANGE) < 0.01f) { phase = 0; Busy = false; dir = 1f; }
        }
        carriage.MovePosition(transform.TransformPoint(new Vector3(x, 1.45f, 0)));
        if (phase != 0) { disc.Rotate(0, 0, -1500f * dt, Space.Self); Cut(); }
    }

    void Cut()
    {
        var c = transform.TransformPoint(new Vector3(x, 1.0f, 0));
        Overlap(c, new Vector3(DISC_R * 0.95f, DISC_R * 0.95f, 0.035f), transform.rotation, tmp);
        foreach (var p in tmp)
        {
            if (!p.dummy) continue;
            cuts.TryGetValue(p, out int n);
            // 1.º e 2.º cortes ferem; a partir do 3.º atravessa o membro
            if (Strike(p, p.transform.position, transform.right * dir, 380f, Weapons.P_SWORD, 0.12f, n >= 2))
            {
                cuts[p] = n + 1;
                Blood(p.transform.position, Vector3.up + transform.right * dir * 0.6f, p, 10, 5f);
                if (n % 2 == 0) Game.sfx.Play("slash", p.transform.position, 0f, Random.Range(1.2f, 1.6f));
            }
        }
    }
}

/// Pêndulo com martelo: balança três vezes e perde amplitude; o impacto é contundente e arremessa.
public class SwingingHammer : Hazard
{
    Rigidbody head; float angle, t; int phase; float amp;
    const float ARM = 3.1f, PIVOT_Y = 4.2f;
    readonly List<DummyPart> tmp = new List<DummyPart>();
    Vector3 lastPos;

    public static SwingingHammer Build(Transform root, Vector3 pos)
    {
        var go = new GameObject("Martelo pendular");
        go.transform.SetParent(root, false); go.transform.localPosition = pos;
        var h = go.AddComponent<SwingingHammer>(); h.label = "Martelo pendular";
        var steel = Steel(0.08f); var red = Paint(new Color(0.55f, 0.1f, 0.08f));
        foreach (var sx in new[] { -1.2f, 1.2f }) Solid(go.transform, "poste", new Vector3(0.2f, PIVOT_Y + 0.3f, 0.3f), new Vector3(sx, (PIVOT_Y + 0.3f) / 2, 0), steel);
        Solid(go.transform, "viga", new Vector3(2.6f, 0.25f, 0.3f), new Vector3(0, PIVOT_Y + 0.15f, 0), steel);
        var hh = new GameObject("martelo"); hh.transform.SetParent(go.transform, false);
        var arm = Prims.Visual("haste", Prims.Box(new Vector3(0.1f, ARM, 0.1f)), steel, hh.transform);
        arm.transform.localPosition = new Vector3(0, ARM / 2, 0);
        var mass = Prims.Visual("cabeça", Prims.Box(new Vector3(0.9f, 0.5f, 0.5f)), red, hh.transform);
        mass.transform.localPosition = new Vector3(0, 0.0f, 0);
        hh.layer = Game.LAYER_WORLD;
        var bc = hh.AddComponent<BoxCollider>(); bc.size = new Vector3(0.9f, 0.5f, 0.5f);
        h.head = Kinematic(hh);
        h.Place(0f);
        Sign(go.transform, "MARTELO", new Vector3(0, 2.0f, -1.2f));
        Game.MakeLight(LightType.Point, go.transform, new Vector3(0, 3.0f, -2.5f), Quaternion.identity, new Color(1f, 0.85f, 0.6f), 1.6f, 7f);
        return h;
    }

    void Place(float ang)
    {
        angle = ang;
        var pivot = transform.TransformPoint(new Vector3(0, PIVOT_Y, 0));
        var rot = transform.rotation * Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
        head.MovePosition(pivot + rot * new Vector3(0, -ARM, 0));
        head.MoveRotation(rot);
    }

    public override void Trigger() { if (!Busy) { Busy = true; phase = 1; t = 0f; amp = 1.25f; lastPos = head.position; } }

    void FixedUpdate()
    {
        if (phase != 1) return;
        float dt = Time.fixedDeltaTime;
        t += dt;
        float w = 2.1f;                                                  // ~ pêndulo de 3 m
        const float RAISE = 1.4f;
        float a;
        if (t < RAISE) a = amp * Mathf.SmoothStep(0f, 1f, t / RAISE);     // levanta o martelo devagar
        else { float tt = t - RAISE; a = amp * Mathf.Cos(w * tt) * Mathf.Exp(-tt * 0.28f); }
        Place(a);
        var vel = (head.position - lastPos) / dt; lastPos = head.position;
        Overlap(head.position, new Vector3(0.5f, 0.3f, 0.3f), head.rotation, tmp);
        foreach (var p in tmp)
        {
            if (!p.dummy || vel.magnitude < 1.5f) continue;
            if (Strike(p, p.transform.position, vel, 120f + vel.sqrMagnitude * 40f, Weapons.P_HAMMER, 0.18f))
            {
                p.rb.AddForce(vel * p.mass * 0.9f, ForceMode.Impulse);
                Game.sfx.Play("impact", p.transform.position, 3f, Random.Range(0.7f, 0.9f));
                Blood(p.transform.position, vel.normalized, p, 6, 3f);
            }
        }
        if (t > 14f) { phase = 0; Busy = false; Place(0f); }
    }
}

/// Espinhos: sobem da placa em uma fração de segundo, seguram e recolhem.
public class SpikeTrap : Hazard
{
    Rigidbody spikes; float y; int phase; float hold, t;
    const float DEPTH = 0.62f;
    readonly Dictionary<DummyPart, int> stabs = new Dictionary<DummyPart, int>();
    readonly List<DummyPart> tmp = new List<DummyPart>();

    public static SpikeTrap Build(Transform root, Vector3 pos)
    {
        var go = new GameObject("Armadilha de espinhos");
        go.transform.SetParent(root, false); go.transform.localPosition = pos;
        var s = go.AddComponent<SpikeTrap>(); s.label = "Espinhos";
        var steel = Steel(0.1f);
        // moldura com furo (4 lados)
        Solid(go.transform, "borda_n", new Vector3(2.6f, 0.08f, 0.3f), new Vector3(0, 0.04f, 1.15f), steel);
        Solid(go.transform, "borda_s", new Vector3(2.6f, 0.08f, 0.3f), new Vector3(0, 0.04f, -1.15f), steel);
        Solid(go.transform, "borda_l", new Vector3(0.3f, 0.08f, 2.0f), new Vector3(1.15f, 0.04f, 0), steel);
        Solid(go.transform, "borda_o", new Vector3(0.3f, 0.08f, 2.0f), new Vector3(-1.15f, 0.04f, 0), steel);
        var grp = new GameObject("espinhos"); grp.transform.SetParent(go.transform, false);
        grp.transform.localPosition = new Vector3(0, -DEPTH, 0);
        var sm = Mats.Mat(new Color(0.7f, 0.72f, 0.75f), 0.25f, 0.9f);
        for (int ix = -2; ix <= 2; ix++)
            for (int iz = -2; iz <= 2; iz++)
            {
                var sp = Prims.Visual("espinho", Prims.Cylinder(0.004f, 0.045f, DEPTH + 0.05f, 10), sm, grp.transform);
                sp.transform.localPosition = new Vector3(ix * 0.36f, (DEPTH + 0.05f) / 2f, iz * 0.36f);
                sp.layer = Game.LAYER_WORLD;
                var cc = sp.AddComponent<CapsuleCollider>(); cc.radius = 0.03f; cc.height = DEPTH + 0.05f; cc.direction = 1;
            }
        s.spikes = Kinematic(grp); s.y = -DEPTH;
        Sign(go.transform, "ESPINHOS", new Vector3(0, 1.0f, -1.6f));
        Game.MakeLight(LightType.Point, go.transform, new Vector3(0, 2.8f, -1.8f), Quaternion.identity, new Color(1f, 0.85f, 0.6f), 1.4f, 7f);
        return s;
    }

    public override void Trigger() { if (!Busy) { Busy = true; phase = 1; t = 0f; stabs.Clear(); Game.sfx.Play("rack", transform.position, 1f, 1.8f); } }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (phase == 1) { y = Mathf.MoveTowards(y, 0f, 6f * dt); if (y >= 0f) { phase = 2; hold = 2.8f; } }
        else if (phase == 2) { hold -= dt; if (hold <= 0f) phase = 3; }
        else if (phase == 3) { y = Mathf.MoveTowards(y, -DEPTH, 0.8f * dt); if (y <= -DEPTH) { phase = 0; Busy = false; } }
        spikes.MovePosition(transform.TransformPoint(new Vector3(0, y, 0)));
        if (phase == 1 || phase == 2)
        {
            Overlap(transform.TransformPoint(new Vector3(0, 0.3f, 0)), new Vector3(1.1f, 0.3f, 1.1f), transform.rotation, tmp);
            foreach (var p in tmp)
            {
                if (!p.dummy) continue;
                stabs.TryGetValue(p, out int n);
                if (n >= 2) continue;                                   // no máximo dois furos por parte e por acionamento
                if (Strike(p, p.transform.position - Vector3.up * 0.04f, Vector3.up, 55f, Weapons.P_SPEAR, 0.6f))
                {
                    stabs[p] = n + 1;
                    Blood(p.transform.position, Vector3.up, p, 6, 2f);
                }
            }
        }
    }
}

/// Lança-chamas de parede: bico fixo, jato horizontal por 4 s.
public class FlameJet : Hazard
{
    float left; Vector3 nozzle, dir; Light glow;

    public static FlameJet Build(Transform root, Vector3 pos)
    {
        var go = new GameObject("Lança-chamas de parede");
        go.transform.SetParent(root, false); go.transform.localPosition = pos;
        var f = go.AddComponent<FlameJet>(); f.label = "Lança-chamas";
        var steel = Steel(0.1f);
        // painel de fundo, tubo e bico
        Solid(go.transform, "painel", new Vector3(3.0f, 3.0f, 0.2f), new Vector3(0, 1.5f, 1.6f), steel);
        var pipe = Prims.Visual("tubo", Prims.Cylinder(0.09f, 0.09f, 0.9f, 14), steel, go.transform);
        pipe.transform.localRotation = Quaternion.Euler(90, 0, 0); pipe.transform.localPosition = new Vector3(0, 1.0f, 1.0f);
        var tank = Prims.Visual("tanque", Prims.Cylinder(0.35f, 0.35f, 1.0f, 20), Paint(new Color(0.6f, 0.1f, 0.08f)), go.transform);
        tank.transform.localPosition = new Vector3(1.0f, 0.5f, 1.2f);
        f.nozzle = go.transform.TransformPoint(new Vector3(0, 1.0f, 0.5f));
        f.dir = go.transform.TransformDirection(Vector3.back);
        f.glow = Game.MakeLight(LightType.Point, go.transform, new Vector3(0, 1.2f, 0.0f), Quaternion.identity, new Color(1f, 0.55f, 0.15f), 0f, 8f);
        Sign(go.transform, "LANÇA-CHAMAS", new Vector3(0, 2.4f, 1.45f));
        return f;
    }

    public override void Trigger() { if (!Busy) { Busy = true; left = 4f; } }

    void Update()
    {
        if (left <= 0f) { if (glow) glow.intensity = 0f; return; }
        left -= Time.deltaTime;
        Flames.Spray(nozzle, dir, Vector3.zero, Time.deltaTime);
        if (glow) glow.intensity = Random.Range(2.5f, 4f);
        if (left <= 0f) Busy = false;
    }
}
