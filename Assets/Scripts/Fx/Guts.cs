using System.Collections.Generic;
using UnityEngine;

/// Evisceração: alças de intestino (corda Verlet presa na ferida) e pedaços de órgão que saem do corpo.
public static class Guts
{
    public static readonly Color C_GUT = new Color(0.86f, 0.55f, 0.5f), C_GUT_DARK = new Color(0.6f, 0.22f, 0.22f);
    public const int MAX_PER_PART = 2, MAX_CHAINS = 24;
    static readonly List<GutRope> chains = new List<GutRope>();

    public static void Spill(DummyPart part, Vector3 localPos, Vector3 localNormal, float severity)
    {
        if (part.guts >= MAX_PER_PART || part.destroyed) return;
        part.guts++;
        var xf = part.Xform;
        var posW = xf.Apply(localPos);
        var nrmW = (xf.q * localNormal).normalized;
        bool chest = part.partId == "chest";
        if (!chest) Intestine(part, localPos, posW, nrmW, (int)Mathf.Clamp(8f + severity * 6f, 8f, 16f));
        int chunks = chest ? 1 + (severity > 1.2f ? 1 : 0) : (severity > 0.8f ? 1 : 0) + (U.Randf() < 0.4f ? 1 : 0);
        for (int i = 0; i < chunks; i++) Chunk(posW + nrmW * 0.03f, nrmW, chest, part);
        Game.blood.Spray(posW, nrmW, 30, 2.2f, 0.6f, part.col);
        Game.blood.Mist(posW, nrmW, 1.5f);
        Game.sfx.Play("gib", posW, -3f, U.RandRange(0.8f, 1f));
        part.AddBloodiness(0.25f);
        Game.LogEvent($"[b]EVISCERAÇÃO:[/b] vísceras expostas — {part.def.name}", new Color(1, 0.35f, 0.3f));
    }

    static void Intestine(DummyPart part, Vector3 localPos, Vector3 posW, Vector3 nrmW, int segs)
    {
        var go = new GameObject("intestino");
        var rope = go.AddComponent<GutRope>();
        rope.Setup(part, localPos, posW, nrmW, segs);
        Game.Track(go);
        chains.Add(rope);
        while (chains.Count > MAX_CHAINS)
        {
            if (chains[0]) Object.Destroy(chains[0].gameObject);
            chains.RemoveAt(0);
        }
    }

    static void Chunk(Vector3 posW, Vector3 nrmW, bool chest, DummyPart part)
    {
        var go = new GameObject("viscera");
        go.layer = Game.LAYER_PROPS;
        var sz = new Vector3(U.RandRange(0.05f, 0.08f), U.RandRange(0.03f, 0.05f), U.RandRange(0.04f, 0.07f));
        var sc = go.AddComponent<SphereCollider>(); sc.radius = sz.y;
        var colr = chest ? Anatomy.C_LUNG : U.Pick(new[] { Anatomy.C_LIVER, Anatomy.C_STOMACH, Anatomy.C_SPLEEN, Anatomy.C_GUT });
        var vis = Prims.Visual("m", Prims.Sphere(0.5f, 1f, 12, 6), Mats.Wet(colr), go.transform);
        vis.transform.localScale = sz * 2f;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        go.transform.position = posW;
        rb.linearVelocity = (nrmW + U.RandomUnit() * 0.4f).normalized * U.RandRange(0.8f, 2f);
        rb.angularVelocity = U.RandomUnit() * 6f;
        Physics.IgnoreCollision(sc, part.col);
        Game.Track(go);
    }
}

/// Alça de intestino: corda Verlet presa na ferida (sem juntas físicas, que travam a física ao serem criadas).
public class GutRope : MonoBehaviour
{
    const float SEG = 0.055f, R = 0.017f;
    DummyPart part;
    Vector3 anchorLocal;
    Vector3[] pts, old;
    float[] floorY;
    Transform[] segs;
    int tick;

    public void Setup(DummyPart p, Vector3 lpos, Vector3 posW, Vector3 nrmW, int n)
    {
        part = p; anchorLocal = lpos;
        pts = new Vector3[n + 1]; old = new Vector3[n + 1]; floorY = new float[n + 1];
        var dir = (nrmW + Vector3.down * 0.6f + U.RandomUnit() * 0.25f).normalized;
        var at = posW;
        for (int i = 0; i <= n; i++)
        {
            pts[i] = at;
            old[i] = at - dir * U.RandRange(0.004f, 0.012f);   // velocidade inicial para fora
            floorY[i] = -1e9f;
            at += dir * SEG;
            dir = (dir + U.RandomUnit() * 0.5f + Vector3.down * 0.25f).normalized;
        }
        var mesh = Prims.Capsule(R, SEG + R * 2f, 10, 3);
        segs = new Transform[n];
        for (int i = 0; i < n; i++)
        {
            var c = Color.Lerp(Guts.C_GUT, Guts.C_GUT_DARK, i % 3 == 2 ? 0.4f : U.Randf() * 0.15f);
            segs[i] = Prims.Visual("seg", mesh, Mats.Wet(c), transform).transform;
        }
        GroundAll();
        Draw();
    }

    void GroundAll()
    {
        for (int i = 0; i < pts.Length; i++)
        {
            var h = Phys.Ray(pts[i] + Vector3.up * 0.4f, pts[i] + Vector3.down * 3f, Game.L_WORLD);
            floorY[i] = (h.hit ? h.position.y : -1e9f) + R;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool attached = part && !part.destroyed;
        tick++;
        if (tick % 15 == 0) GroundAll();
        var g = new Vector3(0, -9.8f, 0) * dt * dt;
        for (int i = 0; i < pts.Length; i++)
        {
            if (i == 0 && attached) continue;
            var p = pts[i];
            var v = (p - old[i]) * 0.985f;
            old[i] = p;
            p += v + g;
            if (p.y < floorY[i]) { p.y = floorY[i]; old[i] = Vector3.Lerp(old[i], p, 0.6f); }   // atrito no chão
            pts[i] = p;
        }
        if (attached) pts[0] = part.Xform.Apply(anchorLocal);
        for (int k = 0; k < 6; k++)
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var d = b - a;
                float l = d.magnitude;
                if (l < 1e-6f) continue;
                var corr = d * ((l - SEG) / l);
                if (i == 0 && attached) pts[i + 1] = b - corr;
                else { pts[i] = a + corr * 0.5f; pts[i + 1] = b - corr * 0.5f; }
            }
    }

    void LateUpdate() => Draw();

    void Draw()
    {
        for (int i = 0; i < segs.Length; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            var dir = b - a;
            if (dir.magnitude < 1e-5f) dir = Vector3.down;
            segs[i].SetPositionAndRotation((a + b) * 0.5f, U.BasisY(dir));
        }
    }
}
