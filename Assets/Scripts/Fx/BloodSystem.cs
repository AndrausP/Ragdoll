using System.Collections.Generic;
using UnityEngine;

/// Gotas de sangue com física, respingos (quads no lugar dos Decals do Godot), poças que crescem, névoa e pedaços (gibs).
public class BloodSystem : MonoBehaviour
{
    const int MAX_DROPS = 1600, MAX_SPLATS = 700, MAX_POOLS = 120, MAX_HOLES = 250, MAX_GIBS = 160;
    static readonly Vector3 GRAV = new Vector3(0, -9.8f, 0);

    readonly List<Vector3> dPos = new List<Vector3>(), dVel = new List<Vector3>();
    readonly List<float> dLife = new List<float>(), dMl = new List<float>();
    readonly List<Collider> dSrc = new List<Collider>();

    Mesh dropMesh;
    Material dropMat;
    readonly Matrix4x4[] mats = new Matrix4x4[1023];
    readonly List<Material> splatMats = new List<Material>();
    Material poolMat, holeMat, scorchMat;
    readonly List<Material> gibMats = new List<Material>();

    readonly List<GameObject> splats = new List<GameObject>();
    int splatI;
    class Pool { public GameObject go; public Vector3 center, normal; public float volume, radius, target, age, rot; public MaterialPropertyBlock mpb; }
    readonly List<Pool> pools = new List<Pool>();
    readonly List<GameObject> holes = new List<GameObject>();
    class Gib { public Rigidbody body; public float bleed, acc; }
    readonly List<Gib> gibList = new List<Gib>();
    readonly Dictionary<DummyPart, float> pointAcc = new Dictionary<DummyPart, float>();
    float simAcc;
    public int totalDropsSpawned, totalSplats;
    public int ActiveDrops => dPos.Count;
    public int PoolCount => pools.Count;
    public int GibCount => gibList.Count;
    readonly List<Collider> one = new List<Collider>(1);

    void Awake()
    {
        dropMesh = Prims.Sphere(1f, 2f, 6, 3);
        dropMat = Mats.Mat(new Color(0.42f, 0.01f, 0.02f), 0.12f);
        var rng = new System.Random(1337);
        for (int k = 0; k < 6; k++)
        {
            var m = Mats.FadeMat(Color.white, 0.25f);
            m.SetTexture("_MainTex", SplatImage(rng, 128));
            splatMats.Add(m);
        }
        poolMat = Mats.FadeMat(Color.white, 0.12f);
        poolMat.SetTexture("_MainTex", PoolImage(256));
        holeMat = Mats.FadeMat(Color.white, 0.8f);
        holeMat.SetTexture("_MainTex", HoleImage());
        scorchMat = Mats.FadeMat(Color.white, 0.9f);
        scorchMat.SetTexture("_MainTex", ScorchImage(rng));
        foreach (var c in new[] { new Color(0.5f, 0.04f, 0.05f), new Color(0.32f, 0.01f, 0.02f), new Color(0.62f, 0.12f, 0.12f),
                                   new Color(0.85f, 0.82f, 0.88f), new Color(0.92f, 0.9f, 0.82f), new Color(0.9f, 0.6f, 0.65f) })
            gibMats.Add(Mats.Mat(c, 0.3f));
    }

    // ------------------------------------------------------------------ texturas procedurais

    static float Noise(float x, float y, float f, int seed) => (Mathf.PerlinNoise(x * f + seed * 13.1f, y * f + seed * 7.7f) - 0.5f) * 2f;

    static Texture2D Tex(int s, Color[] px)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, true);
        t.SetPixels(px); t.Apply(true);
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static Texture2D SplatImage(System.Random rng, int s)
    {
        float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        var circles = new List<(Vector2, float)>();
        var c = new Vector2(s, s) * 0.5f;
        int n1 = rng.Next(5, 10);
        for (int i = 0; i < n1; i++) circles.Add((c + new Vector2(R(-14, 14), R(-14, 14)), R(9, 22)));
        int n2 = rng.Next(10, 23);
        for (int i = 0; i < n2; i++)
        {
            float ang = (float)rng.NextDouble() * U.TAU, dist = R(26, 58);
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            circles.Add((c + dir * dist, R(1.2f, 5f)));
            if (rng.NextDouble() < 0.4)
                for (int j = 0; j < 4; j++) circles.Add((c + dir * (dist - j * 4f), 2.5f - j * 0.4f));
        }
        var alpha = new float[s * s];
        foreach (var (cp, cr) in circles)
        {
            int x0 = Mathf.Clamp((int)(cp.x - cr - 3), 0, s - 1), x1 = Mathf.Clamp((int)(cp.x + cr + 3), 0, s - 1);
            int y0 = Mathf.Clamp((int)(cp.y - cr - 3), 0, s - 1), y1 = Mathf.Clamp((int)(cp.y + cr + 3), 0, s - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), cp) - cr;
                    float a = Mathf.Clamp01(1f - d * 0.6f);
                    if (a > alpha[y * s + x]) alpha[y * s + x] = a;
                }
        }
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float a2 = alpha[y * s + x];
                float core = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), c) / 40f);
                var col = Color.Lerp(new Color(0.55f, 0.02f, 0.03f), new Color(0.3f, 0f, 0.01f), core * 0.7f);
                col.a = a2;
                px[y * s + x] = col;
            }
        return Tex(s, px);
    }

    static Texture2D PoolImage(int s)
    {
        var px = new Color[s * s];
        var c = new Vector2(s, s) * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (s * 0.5f);
                float edge = 0.82f + Noise(x, y, 0.02f, 7) * 0.22f;
                float a = Mathf.Clamp01((edge - d) * 14f);
                float tone = 0.5f + Noise(x * 3f, y * 3f, 0.02f, 7) * 0.3f;
                var col = Color.Lerp(new Color(0.42f, 0.01f, 0.02f), new Color(0.25f, 0f, 0.01f), tone);
                col.a = a;
                px[y * s + x] = col;
            }
        return Tex(s, px);
    }

    static Texture2D HoleImage()
    {
        var px = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = new Vector2(x - 15.5f, y - 15.5f).magnitude;
                float a = Mathf.Clamp01(1f - (d - 4f) / 9f);
                var col = d < 4.5f ? new Color(0.03f, 0.03f, 0.03f) : new Color(0.2f, 0.18f, 0.16f);
                col.a = a * (d < 4.5f ? 1f : 0.6f);
                px[y * 32 + x] = col;
            }
        return Tex(32, px);
    }

    static Texture2D ScorchImage(System.Random rng)
    {
        int s = 128, seed = rng.Next(1000);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64f;
                float a = Mathf.Clamp01((1f - d) * 1.6f + Noise(x, y, 0.05f, seed) * 0.6f);
                px[y * s + x] = new Color(0.02f, 0.018f, 0.015f, a * 0.92f);
            }
        return Tex(s, px);
    }

    // ------------------------------------------------------------------ gotas

    public void SpawnDrop(Vector3 pos, Vector3 vel, float ml, Collider src)
    {
        if (dPos.Count >= MAX_DROPS) return;
        dPos.Add(pos); dVel.Add(vel); dLife.Add(4f); dMl.Add(ml); dSrc.Add(src);
        totalDropsSpawned++;
    }

    void RemoveDrop(int i)
    {
        int last = dPos.Count - 1;
        if (i != last)
        {
            dPos[i] = dPos[last]; dVel[i] = dVel[last]; dLife[i] = dLife[last]; dMl[i] = dMl[last]; dSrc[i] = dSrc[last];
        }
        dPos.RemoveAt(last); dVel.RemoveAt(last); dLife.RemoveAt(last); dMl.RemoveAt(last); dSrc.RemoveAt(last);
    }

    public void EmitWound(Wound w, float rate, float dt, bool arterial, float pulse)
    {
        var part = w.part;
        if (part == null) return;
        float mlpd = Mathf.Max(0.9f, rate / 45f);
        w.emitAcc = Mathf.Min(w.emitAcc + rate * dt, mlpd * 10f);
        int n = 0;
        var xf = part.Xform;
        while (w.emitAcc >= mlpd && n < 8)
        {
            w.emitAcc -= mlpd;
            n++;
            var nw = (xf.q * w.localNormal).normalized;
            var pos = xf.Apply(w.localPos + w.localNormal.normalized * 0.012f);
            var baseV = part.rb.linearVelocity;
            Vector3 vel;
            if (arterial)
            {
                float speed = 0.5f + 4.6f * pulse;
                vel = (nw + U.RandomUnit() * 0.13f).normalized * speed * U.RandRange(0.85f, 1.15f);
            }
            else vel = nw * U.RandRange(0.04f, 0.35f) + U.RandomUnit() * 0.08f;
            SpawnDrop(pos, vel + baseV, mlpd, part.col);
            part.AddBloodiness(0.0012f * mlpd);
        }
    }

    public void EmitPoint(DummyPart part, Vector3 local, Vector3 normal, float rate, float dt, float pulse)
    {
        var key = part;
        pointAcc.TryGetValue(key, out float acc);
        acc += rate * dt;
        float mlpd = Mathf.Max(0.9f, rate / 30f);
        int n = 0;
        var xf = part.Xform;
        while (acc >= mlpd && n < 5)
        {
            acc -= mlpd; n++;
            var nw = (xf.q * normal).normalized;
            var pos = xf.Apply(local + normal * 0.015f);
            SpawnDrop(pos, nw * U.RandRange(0.1f, 0.8f) * pulse + U.RandomUnit() * 0.1f + part.rb.linearVelocity, mlpd, part.col);
        }
        pointAcc[key] = Mathf.Min(acc, mlpd * 6f);
    }

    public void Spray(Vector3 pos, Vector3 dir, int count, float speed, float spread, Collider src)
    {
        for (int i = 0; i < count; i++)
        {
            var d = (dir + U.RandomUnit() * spread * U.Randf()).normalized;
            SpawnDrop(pos, d * speed * U.RandRange(0.35f, 1.25f), U.RandRange(0.4f, 1.4f), src);
        }
    }

    public void Mist(Vector3 pos, Vector3 dir, float amount)
    {
        Fx.Burst(pos, Mathf.Clamp((int)(10 * amount) + 6, 4, 80), 0.55f, 3.2f * amount, new Color(0.6f, 0.03f, 0.04f, 0.85f),
            new Color(0.35f, 0f, 0.01f, 0f), 0.07f, false, new Vector3(0, -3.5f, 0), dir.sqrMagnitude > 1e-4f ? dir : Vector3.up,
            40f, 0.2f, 3f, 0.6f, 2.2f + amount * 0.5f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        simAcc += dt;
        if (simAcc < 1f / 60f) { UpdateGibs(dt); return; }
        float sdt = simAcc;
        simAcc = 0f;
        int mask = Game.L_WORLD | Game.L_DUMMY | Game.L_PROPS;
        for (int i = dPos.Count - 1; i >= 0; i--)
        {
            var v = dVel[i] + GRAV * sdt;
            v *= 1f - 0.25f * sdt;
            var p0 = dPos[i];
            var p1 = p0 + v * sdt;
            dLife[i] -= sdt;
            one.Clear();
            if (dSrc[i]) one.Add(dSrc[i]);
            var hit = Phys.Ray(p0, p1, mask, one);
            if (hit.hit) { DropHit(hit, v, dMl[i]); RemoveDrop(i); }
            else if (dLife[i] <= 0f || p1.y < -30f) RemoveDrop(i);
            else { dPos[i] = p1; dVel[i] = v; }
        }
        UpdateGibs(sdt);
        UpdatePools(sdt);
    }

    void Update()
    {
        // desenha as gotas instanciadas (alongadas na direção do movimento)
        int n = dPos.Count;
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            var v = dVel[i];
            float r = Mathf.Clamp(0.004f + Mathf.Sqrt(dMl[i]) * 0.0045f, 0.004f, 0.02f);
            float sp = v.magnitude;
            Matrix4x4 m;
            if (sp > 0.05f) m = Matrix4x4.TRS(dPos[i], U.BasisY(v / sp), new Vector3(r, r * (1f + Mathf.Min(sp * 0.25f, 4f)), r));
            else m = Matrix4x4.TRS(dPos[i], Quaternion.identity, Vector3.one * r);
            mats[k++] = m;
            if (k == mats.Length)
            {
                Graphics.DrawMeshInstanced(dropMesh, 0, dropMat, mats, k, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
                k = 0;
            }
        }
        if (k > 0) Graphics.DrawMeshInstanced(dropMesh, 0, dropMat, mats, k, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }

    void DropHit(RayHit hit, Vector3 vel, float ml)
    {
        var part = hit.part;
        if (part != null) { part.AddBloodiness(0.012f * ml); return; }
        if (hit.body != null) return;
        var n = hit.normal;
        float speed = vel.magnitude;
        if (n.y > 0.75f && speed < 3.6f) AddToPool(hit.position, n, ml);
        else Splat(hit.position, n, vel, ml);
    }

    // ------------------------------------------------------------------ marcas

    static Quaternion DecalBasis(Vector3 n, Vector3 tangent)
    {
        var y = n.normalized;
        var x = tangent - y * Vector3.Dot(tangent, y);
        if (x.magnitude < 0.01f)
        {
            var rf = Mathf.Abs(Vector3.Dot(y, Vector3.right)) < 0.9f ? Vector3.right : new Vector3(0, 0, -1);
            x = rf - y * Vector3.Dot(rf, y);
            x = Quaternion.AngleAxis(U.Randf() * 360f, y) * x;
        }
        x = x.normalized;
        var z = Vector3.Cross(x, y).normalized;
        return U.FromBasis(x, y, z);
    }

    GameObject Mark(Material m, Transform parent)
    {
        var go = Prims.Visual("mark", Prims.FloorQuad(), m, parent, false);
        go.GetComponent<MeshRenderer>().receiveShadows = true;
        return go;
    }

    public void Splat(Vector3 pos, Vector3 n, Vector3 vel, float ml)
    {
        GameObject d;
        if (splats.Count < MAX_SPLATS) { d = Mark(splatMats[0], transform); splats.Add(d); }
        else { d = splats[splatI]; splatI = (splatI + 1) % MAX_SPLATS; }
        totalSplats++;
        float speed = vel.magnitude;
        var vt = vel - n * Vector3.Dot(vel, n);
        float stretch = 1f + Mathf.Clamp(vt.magnitude * 0.18f, 0f, 2.8f);
        float size = Mathf.Clamp(0.035f + Mathf.Sqrt(ml) * 0.05f * (0.6f + speed * 0.1f), 0.03f, 0.45f);
        d.GetComponent<MeshRenderer>().sharedMaterial = U.Pick(splatMats);
        var at = vt.magnitude > 0.01f ? pos + vt.normalized * size * stretch * 0.25f : pos;
        d.transform.SetPositionAndRotation(at + n * (0.002f + (totalSplats % 7) * 0.0002f), DecalBasis(n, vt));
        d.transform.localScale = new Vector3(size * stretch, 1, size);
    }

    public void AddToPool(Vector3 pos, Vector3 n, float ml)
    {
        Pool best = null;
        float bestD = 1e9f;
        foreach (var pl in pools)
        {
            if (Vector3.Dot(pl.normal, n) < 0.9f) continue;
            float dd = Vector3.Distance(pl.center, pos);
            if (dd < pl.radius + 0.14f && dd < bestD) { best = pl; bestD = dd; }
        }
        if (best != null)
        {
            best.volume += ml;
            best.center = Vector3.Lerp(best.center, pos, Mathf.Clamp(ml / best.volume, 0f, 0.15f));
            best.target = PoolRadius(best.volume);
            best.age = Mathf.Min(best.age, 4f);
            return;
        }
        if (pools.Count >= MAX_POOLS)
        {
            int smallest = 0;
            for (int k = 0; k < pools.Count; k++) if (pools[k].volume < pools[smallest].volume) smallest = k;
            Destroy(pools[smallest].go);
            pools.RemoveAt(smallest);
        }
        var p2 = new Pool { go = Mark(poolMat, transform), center = pos, normal = n, volume = ml, radius = 0.02f, target = PoolRadius(ml), rot = U.Randf() * U.TAU, mpb = new MaterialPropertyBlock() };
        pools.Add(p2);
        PlacePool(p2);
    }

    static float PoolRadius(float v) => Mathf.Min(0.03f + Mathf.Sqrt(v) * 0.0158f, 1.9f);

    void PlacePool(Pool pl)
    {
        float r = pl.radius;
        var b = DecalBasis(pl.normal, Quaternion.AngleAxis(pl.rot * Mathf.Rad2Deg, Vector3.up) * Vector3.right);
        pl.go.transform.SetPositionAndRotation(pl.center + pl.normal * 0.0015f, b);
        pl.go.transform.localScale = new Vector3(r * 2.1f, 1, r * 2.1f);
        float t = Mathf.Clamp01(pl.age / 90f);
        pl.mpb.SetColor("_Color", Color.Lerp(Color.white, new Color(0.55f, 0.45f, 0.45f, 1f), t));
        pl.go.GetComponent<MeshRenderer>().SetPropertyBlock(pl.mpb);
    }

    void UpdatePools(float dt)
    {
        foreach (var pl in pools)
        {
            pl.age += dt;
            if (Mathf.Abs(pl.radius - pl.target) > 0.002f || (pl.age % 2f) < dt)
            {
                pl.radius = U.MoveToward(pl.radius, pl.target, dt * 0.12f);
                PlacePool(pl);
            }
        }
    }

    public void BulletHole(Vector3 pos, Vector3 n)
    {
        var d = Mark(holeMat, transform);
        d.transform.SetPositionAndRotation(pos + n * 0.0025f, DecalBasis(n, Vector3.zero));
        d.transform.localScale = new Vector3(0.05f, 1, 0.05f);
        holes.Add(d);
        if (holes.Count > MAX_HOLES) { Destroy(holes[0]); holes.RemoveAt(0); }
    }

    public void Scorch(Vector3 pos, Vector3 n, float size)
    {
        var d = Mark(scorchMat, transform);
        d.transform.SetPositionAndRotation(pos + n * 0.003f, DecalBasis(n, Vector3.zero));
        d.transform.localScale = new Vector3(size, 1, size);
        holes.Add(d);
    }

    // ------------------------------------------------------------------ pedaços

    public void Gibs(Vector3 pos, int count, Vector3 dir, bool isHead)
    {
        for (int i = 0; i < count; i++)
        {
            var g = new GameObject("gib");
            g.layer = Game.LAYER_PROPS;
            g.transform.SetParent(transform, false);
            var sz = new Vector3(U.RandRange(0.015f, 0.06f), U.RandRange(0.01f, 0.045f), U.RandRange(0.015f, 0.06f));
            var bc = g.AddComponent<BoxCollider>(); bc.size = sz;
            var pm = new PhysicsMaterial { dynamicFriction = 0.9f, staticFriction = 0.9f, bounciness = 0.1f };
            bc.sharedMaterial = pm;
            int k = U.RandInt(0, 5);
            if (isHead && U.Randf() < 0.35f) k = U.Randf() < 0.6f ? 5 : 4;
            else if (k >= 4 && U.Randf() < 0.6f) k = U.RandInt(0, 2);
            var mesh = U.Randf() < 0.5f ? Prims.Sphere(sz.x * 0.6f, sz.y * 1.2f, 8, 4) : Prims.Box(sz);
            Prims.Visual("m", mesh, gibMats[k], g.transform);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.03f, sz.x * sz.y * sz.z * 1100f);
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            g.transform.position = pos + U.RandomUnit() * 0.06f;
            rb.linearVelocity = dir * U.RandRange(1f, 4f) + U.RandomUnit() * U.RandRange(1.5f, 5f) + Vector3.up * U.RandRange(1f, 3.5f);
            rb.angularVelocity = U.RandomUnit() * U.RandRange(4f, 18f);
            gibList.Add(new Gib { body = rb, bleed = U.RandRange(1f, 3.5f) });
        }
        while (gibList.Count > MAX_GIBS)
        {
            if (gibList[0].body) Destroy(gibList[0].body.gameObject);
            gibList.RemoveAt(0);
        }
    }

    void UpdateGibs(float dt)
    {
        foreach (var gi in gibList)
        {
            if (gi.bleed <= 0f || !gi.body) continue;
            gi.bleed -= dt;
            gi.acc += dt;
            if (gi.acc > 0.06f)
            {
                gi.acc = 0f;
                SpawnDrop(gi.body.position, gi.body.linearVelocity * 0.4f + U.RandomUnit() * 0.2f, 0.6f, gi.body.GetComponent<Collider>());
            }
        }
    }

    // ------------------------------------------------------------------ limpeza

    public void ClearAll()
    {
        dPos.Clear(); dVel.Clear(); dLife.Clear(); dMl.Clear(); dSrc.Clear();
        foreach (var d in splats) if (d) Destroy(d);
        splats.Clear(); splatI = 0;
        foreach (var pl in pools) if (pl.go) Destroy(pl.go);
        pools.Clear();
        foreach (var h in holes) if (h) Destroy(h);
        holes.Clear();
        foreach (var gi in gibList) if (gi.body) Destroy(gi.body.gameObject);
        gibList.Clear();
        pointAcc.Clear();
    }

    public float PoolVolume() { float t = 0; foreach (var pl in pools) t += pl.volume; return t; }
}
