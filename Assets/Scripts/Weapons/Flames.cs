using System.Collections.Generic;
using UnityEngine;

/// Lança-chamas: o jato é feito de "bolhas" de combustível em chamas simuladas no código (sobem um pouco, perdem
/// velocidade, colidem por raio). Bolha que acerta o boneco incendeia a parte (Dummy.Burn); a que acerta o chão
/// vira uma poça queimando por alguns segundos. O visual é um único ParticleSystem de fogo e outro de fumaça
/// (partículas emitidas na posição de cada bolha a cada quadro).
public class Flames : MonoBehaviour
{
    class Blob { public Vector3 pos, vel; public float life, max; }
    class Pool { public Vector3 pos, n; public float life; }

    static Flames inst;
    readonly List<Blob> blobs = new List<Blob>();
    readonly List<Pool> pools = new List<Pool>();
    ParticleSystem fire, smoke;
    Light glow;
    public static float sprayGlow;       // o bico está soltando fogo (luz no jogador)

    public static Flames Get()
    {
        if (inst) return inst;
        inst = new GameObject("fogo").AddComponent<Flames>();
        return inst;
    }

    void Awake()
    {
        fire = MakePs("chamas", true, 2500);
        smoke = MakePs("fumaça", false, 1500);
        var lg = new GameObject("luz do fogo");
        lg.transform.SetParent(transform, false);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = new Color(1f, 0.55f, 0.2f); glow.range = 7f; glow.intensity = 0f;
        glow.shadows = LightShadows.None;
    }

    ParticleSystem MakePs(string name, bool additive, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        var em = ps.emission; em.rateOverTime = 0;
        var sh = ps.shape; sh.enabled = false;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, additive ? AnimationCurve.Linear(0, 0.6f, 1, 1.5f) : AnimationCurve.Linear(0, 0.5f, 1, 2.2f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        if (additive)
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.35f), new GradientColorKey(new Color(0.8f, 0.15f, 0.02f), 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        else
            g.SetKeys(new[] { new GradientColorKey(new Color(0.18f, 0.16f, 0.15f), 0f), new GradientColorKey(new Color(0.3f, 0.29f, 0.28f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.Unlit(Color.white, Mats.SoftDot, additive);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    /// Solta combustível em chamas do bico (chamado a cada quadro enquanto o gatilho está apertado).
    public static void Spray(Vector3 from, Vector3 dir, Vector3 inherit, float dt)
    {
        var f = Get();
        int n = Mathf.Max(1, Mathf.RoundToInt(dt * 70f));
        for (int i = 0; i < n; i++)
        {
            var d = (dir + U.RandomUnit() * 0.07f).normalized;
            float life = U.RandRange(0.55f, 0.85f);
            f.blobs.Add(new Blob { pos = from + d * U.RandRange(0f, 0.15f), vel = d * U.RandRange(13f, 16f) + inherit, life = life, max = life });
        }
        sprayGlow = 0.12f;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        for (int i = blobs.Count - 1; i >= 0; i--)
        {
            var b = blobs[i];
            b.life -= dt;
            if (b.life <= 0f) { blobs.RemoveAt(i); continue; }
            b.vel += new Vector3(0, 1.6f, 0) * dt;                 // o fogo sobe
            b.vel *= 1f - Mathf.Min(0.9f, dt * 1.3f);
            var next = b.pos + b.vel * dt;
            var h = Phys.Ray(b.pos, next, Game.L_WORLD | Game.L_DUMMY | Game.L_PROPS, Game.player != null ? Game.player.SelfColliders : null);
            if (h.hit)
            {
                float heat = b.life / b.max;
                if (h.part != null) h.part.dummy.Burn(h.part, h.position, 0.05f * heat);
                else if (U.Randf() < 0.08f && pools.Count < 40) pools.Add(new Pool { pos = h.position + h.normal * 0.02f, n = h.normal, life = U.RandRange(3f, 6f) });
                blobs.RemoveAt(i);
                continue;
            }
            b.pos = next;
        }
        for (int i = pools.Count - 1; i >= 0; i--)
        {
            var p = pools[i];
            p.life -= dt;
            if (p.life <= 0f)
            {
                if (Game.blood) Game.blood.Scorch(p.pos, p.n, 0.6f);
                pools.RemoveAt(i);
                continue;
            }
            // queima o que estiver por cima
            if (U.Randf() < dt * 6f)
                foreach (var c in Physics.OverlapSphere(p.pos, 0.35f, Game.L_DUMMY))
                {
                    var part = c.GetComponent<DummyPart>();
                    if (part != null && !part.destroyed) part.dummy.Burn(part, p.pos, 0.02f);
                }
        }
    }

    void Update()
    {
        // visual: cada bolha deixa um rastro de chama; perto do fim, fumaça
        var ep = new ParticleSystem.EmitParams();
        foreach (var b in blobs)
        {
            float age = 1f - b.life / b.max;
            ep.position = b.pos + U.RandomUnit() * 0.04f;
            ep.velocity = b.vel * 0.15f + Vector3.up * 0.4f;
            ep.startSize = Mathf.Lerp(0.08f, 0.55f, age);
            ep.startLifetime = U.RandRange(0.12f, 0.22f);
            fire.Emit(ep, 1);
            if (age > 0.6f && U.Randf() < 0.35f)
            {
                ep.startSize = Mathf.Lerp(0.3f, 0.7f, age);
                ep.startLifetime = U.RandRange(0.8f, 1.4f);
                ep.velocity = Vector3.up * 0.8f + b.vel * 0.05f;
                smoke.Emit(ep, 1);
            }
        }
        foreach (var p in pools) EmitFire(p.pos, 0.18f, Time.deltaTime * 30f);
        // luz: no bico do jogador e na região do jato
        sprayGlow -= Time.deltaTime;
        if (blobs.Count > 0)
        {
            var c = Vector3.zero;
            foreach (var b in blobs) c += b.pos;
            glow.transform.position = c / blobs.Count;
        }
        glow.intensity = Mathf.MoveTowards(glow.intensity, blobs.Count > 0 || pools.Count > 0 ? 2.5f + Mathf.PerlinNoise(Time.time * 9f, 0f) * 1.5f : 0f, Time.deltaTime * 20f);
    }

    /// Labaredas num ponto (parte do corpo pegando fogo, poça). rate = partículas neste quadro (fração vira chance).
    public static void EmitFire(Vector3 pos, float radius, float rate)
    {
        var f = Get();
        int n = Mathf.FloorToInt(rate) + (U.Randf() < rate - Mathf.Floor(rate) ? 1 : 0);
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < n; i++)
        {
            ep.position = pos + U.RandomUnit() * radius;
            ep.velocity = new Vector3(U.RandRange(-0.2f, 0.2f), U.RandRange(0.8f, 1.6f), U.RandRange(-0.2f, 0.2f));
            ep.startSize = U.RandRange(0.08f, 0.2f);
            ep.startLifetime = U.RandRange(0.25f, 0.5f);
            f.fire.Emit(ep, 1);
            if (U.Randf() < 0.25f)
            {
                ep.startSize = U.RandRange(0.15f, 0.3f);
                ep.startLifetime = U.RandRange(1f, 1.8f);
                ep.velocity = Vector3.up * U.RandRange(0.5f, 0.9f);
                f.smoke.Emit(ep, 1);
            }
        }
    }

    public static void ClearAll()
    {
        if (!inst) return;
        inst.blobs.Clear(); inst.pools.Clear();
        inst.fire.Clear(); inst.smoke.Clear();
    }
}
