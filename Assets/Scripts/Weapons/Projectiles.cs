using System.Collections.Generic;
using UnityEngine;

/// Flecha (projétil por raio que crava), granada (corpo rígido com espoleta), C4 (gruda e detona) e explosões.
public class Arrow : MonoBehaviour
{
    static readonly Vector3 GRAV = new Vector3(0, -9.8f, 0);
    public Vector3 vel;
    float v0 = 1f, energy = 100f, life = 90f;
    public bool stuck;
    List<Collider> exclude;
    Prof prof;
    Transform spinner;      // faca: gira no ar em torno do eixo lateral
    float spinRate;
    public bool tether;     // arpão: ao cravar num boneco prende uma corda que o jogador pode puxar

    void Awake() { Game.Track(gameObject); }

    /// Modelo com a ponta na origem e o corpo para trás (+Z): flecha ou adaga (de pé em +Y no arquivo).
    void BuildModel(string model)
    {
        spinner = new GameObject("gira").transform;
        spinner.SetParent(transform, false);
        bool knife = model == "dagger";
        var rot = knife ? U.Axis(Vector3.right, -Mathf.PI / 2) : U.Axis(Vector3.up, Mathf.PI);
        float len = knife ? 0.28f : model == "bolt" ? 0.5f : model == "harpoon_bolt" ? 1.15f : 0.75f;
        var m = Game.Model(model, rot, len, out var sz);
        if (sz != Vector3.zero)
        {
            m.transform.SetParent(spinner, false);
            m.transform.localPosition = new Vector3(0, 0, sz.z * 0.5f);
        }
        else
        {
            Destroy(m);
            var shaft = Prims.Visual("shaft", Prims.Cylinder(0.0045f, 0.0045f, 0.72f, 6), Mats.Mat(new Color(0.55f, 0.4f, 0.22f), 0.7f), spinner);
            shaft.transform.localRotation = U.Euler(Mathf.PI / 2, 0, 0);
            shaft.transform.localPosition = new Vector3(0, 0, 0.36f);
        }
        if (knife)
        {
            // gira em torno do centro da faca, não da ponta
            spinner.localPosition = new Vector3(0, 0, sz.z * 0.5f);
            m.transform.localPosition = Vector3.zero;
            spinRate = U.RandRange(-1f, 1f) > 0 ? 22f : -22f;
        }
    }

    public void Launch(Vector3 pos, Vector3 velocity, float e, List<Collider> ex, Prof p = null, string model = "arrow")
    {
        BuildModel(model);
        prof = p ?? Weapons.P_ARROW;
        transform.position = pos;
        vel = velocity;
        v0 = Mathf.Max(velocity.magnitude, 0.1f);
        energy = e;
        exclude = ex;
        Orient();
    }
    void Orient()
    {
        if (vel.magnitude > 0.1f)
            transform.rotation = U.LookingAt(vel, Mathf.Abs(vel.normalized.y) < 0.99f ? Vector3.up : Vector3.right);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (stuck)
        {
            life -= dt;
            if (life <= 0f) Destroy(gameObject);
            return;
        }
        vel += GRAV * dt;
        if (spinRate != 0f) spinner.localRotation = U.Axis(Vector3.right, spinRate * dt) * spinner.localRotation;
        var from = transform.position;
        var to = from + vel * dt;
        var h = Phys.Ray(from, to, Game.L_WORLD | Game.L_DUMMY | Game.L_PROPS, exclude);
        if (!h.hit)
        {
            transform.position = to;
            Orient();
            life -= dt;
            if (life <= 0f || to.y < -50f) Destroy(gameObject);
            return;
        }
        var dir = vel.normalized;
        float depth = 0.06f;
        float ratio = Mathf.Clamp(vel.magnitude / v0, 0.2f, 1f);
        var part = h.part;
        if (part != null)
        {
            var res = part.dummy.ApplyPenetration(part, h.position, dir, energy * ratio * ratio, prof, h.normal);
            depth = Mathf.Clamp(!res.stopped ? Vector3.Distance(h.position, res.exit) : 0.18f * ratio, 0.05f, 0.32f);
            if (!res.stopped) depth = Mathf.Min(Vector3.Distance(h.position, res.exit) + 0.08f, 0.4f);
            if (!part.Freeze) part.rb.AddForceAtPosition(dir * 5f * ratio, h.position, ForceMode.Impulse);
            if (Game.hud) Game.hud.Hitmarker(part.dummy, part);
            Game.sfx.Play("thunk", h.position, -4f, 0.8f);
            if (tether && Game.player != null) new GameObject("corda").AddComponent<HarpoonRope>().Init(this, part);
        }
        else
        {
            Game.sfx.Play("thunk", h.position, -2f);
            if (h.body != null) h.body.AddForceAtPosition(dir * 3f, h.position, ForceMode.Impulse);
        }
        if (spinRate != 0f)
        {
            // faca: crava de ponta (quanto entra depende da energia)
            depth = Mathf.Min(depth, 0.09f);
            spinner.localRotation = Quaternion.identity;
        }
        transform.position = h.position + dir * depth;
        transform.rotation = U.LookingAt(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right);
        stuck = true;
        if (h.body != null) transform.SetParent(h.body.transform, true);
    }
}

/// Corda do arpão: liga o cano ao arpão cravado no boneco. Segurar o botão direito puxa o corpo até o jogador; Q solta.
public class HarpoonRope : MonoBehaviour
{
    Arrow harpoon; DummyPart part; LineRenderer line; float life = 25f;

    public void Init(Arrow a, DummyPart p)
    {
        harpoon = a; part = p;
        Game.Track(gameObject);
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 2; line.startWidth = 0.012f; line.endWidth = 0.012f;
        line.material = Mats.Mat(new Color(0.06f, 0.05f, 0.04f), 0.9f);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void FixedUpdate()
    {
        life -= Time.fixedDeltaTime;
        if (life <= 0f || harpoon == null || part == null || part.destroyed || Game.player == null) { Destroy(gameObject); return; }
        var from = Game.player.RopeOrigin;
        var to = harpoon.transform.position;
        var diff = from - to;
        float dist = diff.magnitude;
        if (InputX.Down(InputX.A.Reload) && Game.player.Weapon().id == "harpoon") { Destroy(gameObject); return; }
        if (InputX.Alt && InputX.Captured && dist > 1.4f)
        {
            if (part.Freeze) part.dummy.Collapse();
            // puxa o corpo inteiro: força no pedaço atingido, proporcional à massa
            float m = Mathf.Max(part.mass, 3f);
            part.rb.AddForce(diff.normalized * Mathf.Min(90f * m, 700f), ForceMode.Force);
            foreach (var q in part.dummy.ValidParts()) if (q != part && !q.Freeze) q.rb.AddForce(diff.normalized * q.mass * 14f, ForceMode.Force);
        }
    }

    void LateUpdate()
    {
        if (harpoon == null || line == null || Game.player == null) return;
        line.SetPosition(0, Game.player.RopeOrigin); line.SetPosition(1, harpoon.transform.position);
    }
}

public class Grenade : MonoBehaviour
{
    public float fuse = 3.2f;
    float beep;
    Light lt;
    public Rigidbody rb;

    void Awake()
    {
        gameObject.layer = Game.LAYER_PROPS;
        var sc = gameObject.AddComponent<SphereCollider>(); sc.radius = 0.045f;
        sc.sharedMaterial = new PhysicsMaterial { bounciness = 0.35f, dynamicFriction = 0.7f, staticFriction = 0.7f };
        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.45f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Prims.Visual("body", Prims.Sphere(0.045f, 0.1f), Mats.Mat(new Color(0.25f, 0.3f, 0.18f), 0.6f), transform);
        var lever = Prims.Visual("lever", Prims.Box(new Vector3(0.015f, 0.06f, 0.012f)), Mats.Mat(new Color(0.5f, 0.5f, 0.5f), 0.4f, 0.7f), transform);
        lever.transform.localPosition = new Vector3(0.03f, 0.04f, 0);
        var lgo = new GameObject("luz"); lgo.transform.SetParent(transform, false);
        lt = lgo.AddComponent<Light>(); lt.type = LightType.Point; lt.color = new Color(1, 0.2f, 0.1f); lt.range = 0.6f; lt.intensity = 0f;
        Game.Track(gameObject);
    }

    void OnCollisionEnter(Collision c)
    {
        if (rb.linearVelocity.magnitude > 2f) Game.sfx.Play("impact", transform.position, -10f, 0.6f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        fuse -= dt; beep -= dt;
        lt.intensity = Mathf.Max(0f, lt.intensity - dt * 20f);
        if (beep <= 0f) { beep = Mathf.Clamp(fuse * 0.25f, 0.08f, 0.5f); lt.intensity = 3f; }
        if (fuse <= 0f)
        {
            Explosions.Explode(transform.position, 1f, 7f, 70, "Granada");
            Destroy(gameObject);
        }
    }
}

public class C4 : MonoBehaviour
{
    float blink;
    Light lt;
    GameObject led;

    void Awake()
    {
        var body = Prims.Visual("body", Prims.Box(new Vector3(0.16f, 0.05f, 0.1f)), Mats.Mat(new Color(0.72f, 0.65f, 0.45f), 0.9f), transform);
        body.transform.localPosition = new Vector3(0, 0.025f, 0);
        var cols = new[] { Color.red, new Color(0.1f, 0.3f, 1), Color.yellow };
        for (int k = 0; k < 3; k++)
        {
            var w = Prims.Visual("fio", Prims.Box(new Vector3(0.006f, 0.006f, 0.11f)), Mats.Mat(cols[k], 0.5f), transform);
            w.transform.localPosition = new Vector3(-0.05f + k * 0.05f, 0.053f, 0);
        }
        led = Prims.Visual("led", Prims.Sphere(0.008f, 0.016f), Mats.Emissive(new Color(1, 0.1f, 0.05f), 4f), transform, false);
        led.transform.localPosition = new Vector3(0.06f, 0.055f, 0.03f);
        var lgo = new GameObject("luz"); lgo.transform.SetParent(transform, false);
        lgo.transform.localPosition = new Vector3(0, 0.1f, 0);
        lt = lgo.AddComponent<Light>(); lt.type = LightType.Point; lt.color = new Color(1, 0.15f, 0.1f); lt.range = 0.8f;
        Game.Track(gameObject);
        Game.c4s.Add(this);
    }

    void Update()
    {
        blink += Time.deltaTime;
        bool on = (blink % 0.8f) < 0.12f;
        led.SetActive(on);
        lt.intensity = on ? 2f : 0f;
    }

    public void Detonate()
    {
        Explosions.Explode(transform.position, 2.3f, 9f, 25, "C4");
        Game.c4s.Remove(this);
        Destroy(gameObject);
    }
}

public static class Explosions
{
    public static void Explode(Vector3 pos, float power, float radius, int shrapnel, string nm)
    {
        ExplosionFx(pos, power);
        Game.sfx.Play("explosion", pos, 4f + power * 3f, U.RandRange(0.85f, 1.05f));
        if (Game.player != null)
        {
            float dist = Vector3.Distance(Game.player.transform.position, pos);
            Game.player.AddShake(Mathf.Clamp(power * 1.2f - dist * 0.06f, 0.05f, 1.5f));
        }
        var h = Phys.Ray(pos + Vector3.up * 0.2f, pos + Vector3.down * 2.5f, Game.L_WORLD);
        if (h.hit) Game.blood.Scorch(h.position, h.normal, 1.6f * power + 0.8f);
        foreach (var d in new List<Dummy>(Game.dummies))
        {
            if (!d) continue;
            foreach (var p in d.ValidParts())
            {
                if (p.destroyed) continue;
                float dist2 = Vector3.Distance(p.transform.position, pos) - p.PartRadius() * 0.5f;
                if (dist2 > radius) continue;
                float los = Phys.Ray(pos, p.transform.position, Game.L_WORLD).hit ? 0.3f : 1f;
                float inten = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Max(dist2, 0f) / radius), 2f) * los;
                float rb = radius * 0.45f;
                float intenDmg = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Max(dist2, 0f) / rb), 2.5f) * los;
                if (inten < 0.015f) continue;
                d.ApplyBlast(p, pos, intenDmg, inten, power, nm);
            }
        }
        // empurra objetos soltos
        foreach (var c in Physics.OverlapSphere(pos, radius * 0.7f, Game.L_PROPS))
        {
            var b = c.attachedRigidbody;
            if (b == null || b.isKinematic || b.GetComponent<DummyPart>()) continue;
            var dv = b.position - pos;
            float f = Mathf.Clamp01(1f - dv.magnitude / (radius * 0.7f));
            b.AddForce((dv.normalized + Vector3.up * 0.4f) * f * power * 6f * Mathf.Min(b.mass, 20f), ForceMode.Impulse);
        }
        for (int i = 0; i < shrapnel; i++)
        {
            var dir = new Vector3(U.RandRange(-1, 1), U.RandRange(-0.25f, 1), U.RandRange(-1, 1)).normalized;
            Weapons.FireRay(pos + dir * 0.06f, dir, U.RandRange(35f, 90f), Weapons.P_SHRAPNEL, null, radius * 1.6f, false);
        }
    }

    static void ExplosionFx(Vector3 pos, float power)
    {
        Fx.FlashLight(pos + Vector3.up * 0.4f, new Color(1f, 0.65f, 0.3f), 10f * power, 8f, 0.45f);
        Fx.Burst(pos, 50, 0.45f, 9f * Mathf.Sqrt(power), new Color(1f, 0.75f, 0.3f), new Color(0.9f, 0.2f, 0f, 0f), 0.5f * power, true, new Vector3(0, 1, 0), Vector3.up, 180f, 0.3f, 9f);
        Fx.Burst(pos, 40, 2.8f, 2.5f * Mathf.Sqrt(power), new Color(0.25f, 0.22f, 0.2f, 0.75f), new Color(0.35f, 0.33f, 0.3f, 0f), 1.4f * power, false, new Vector3(0, 0.8f, 0), Vector3.up, 180f, 0.3f, 2f);
        Fx.Burst(pos, 60, 0.9f, 16f, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.1f, 0f), 0.05f, true, new Vector3(0, -9.8f, 0), Vector3.up, 180f, 0.3f, 4f);
    }
}
