using System.Collections.Generic;
using UnityEngine;

public class BoneState { public string name; public float hp, max; public bool fractured; public BoneDef def; public Material mat; }
public class OrganState { public string name, vital; public float hp, max, pneumo; public bool destroyed; public OrganDef def; public GameObject mesh; public Material mat; }
public class VesselState { public string name; public bool cut; public VesselDef def; public Material mat; }

/// Uma parte do corpo do boneco: corpo rígido + estado anatômico (ossos, órgãos, vasos, feridas).
public class DummyPart : MonoBehaviour
{
    public static readonly Color SKIN = new Color(0.80f, 0.62f, 0.52f);
    public static readonly Color BLOOD_DARK = new Color(0.30f, 0.015f, 0.02f);

    public string partId = "";
    public PartDef def;
    public Dummy dummy;
    public DummyPart parentPart;
    public readonly List<DummyPart> childrenParts = new List<DummyPart>();
    public Joint joint;
    public Rigidbody rb;
    public Collider col;

    public float hp = 100f, maxHp = 100f;
    public readonly List<BoneState> bones = new List<BoneState>();
    public readonly List<OrganState> organs = new List<OrganState>();
    public readonly List<VesselState> vessels = new List<VesselState>();
    public readonly List<Wound> wounds = new List<Wound>();

    public bool severed, cutHere, destroyed, tourniquet;
    // corte em qualquer ponto (Dummy.Cut.cs): fragmento = pedaço distal com corpo próprio, mesmo referencial da parte de origem;
    // meshDetached = a pele desta parte segue o próprio corpo (não colapsa no coto como no corte na junta)
    public bool fragment, meshDetached, segInit;
    public int boneIndex;
    public float segLo, segHi;                  // extensão da peça ao longo do eixo do membro (espaço de repouso)
    public float bandage, pressure, bloodiness, limbBlood, drainBlood, impactGrace;
    public Vector3 lastVel;
    public Wound stumpParentWound;
    // "metadados" do Godot
    public float wall; public int guts;
    public bool hasStump; public Vector3 stumpLocal, stumpNormal;
    public Material bandageMat;
    public float burn, onFire, burnWoundAt = 0.15f;     // fogo: dose acumulada, segundos ainda queimando, próxima queimadura visível

    public Material skinMat;
    public GameObject bodyMesh, xrayRoot, deco, hairMesh;
    public bool realModel;
    Color skinTint = SKIN;

    public Xf Xform => new Xf(transform.rotation, transform.position);
    public float mass => rb.mass;
    public bool Freeze
    {
        get => rb.isKinematic;
        set { rb.isKinematic = value; }
    }

    public void Setup(Dummy d, string id, PartDef pd)
    {
        dummy = d; partId = id; def = pd; name = id;
        boneIndex = Anatomy.OrderIndex(id);
        maxHp = pd.hp; hp = maxHp;
        gameObject.layer = Game.LAYER_DUMMY;
        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = pd.mass;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 1.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxAngularVelocity = 40f;
        rb.solverIterations = 16;
        rb.solverVelocityIterations = 6;
        limbBlood = pd.mass * 55f;
        BuildShape();
        BuildState();
        BuildXray();
    }

    void BuildShape()
    {
        // sola agarra o piso, mão e cabeça meio termo, tronco e membros deslizam (roupa/pele)
        float fr = partId.StartsWith("foot") ? 1.1f : partId.StartsWith("hand") ? 0.85f : partId == "head" ? 0.7f : 0.6f;
        var pm = new PhysicsMaterial { dynamicFriction = fr, staticFriction = fr + 0.1f, bounciness = 0.02f };
        Mesh prim;
        switch (def.shape)
        {
            case "box":
                var b = gameObject.AddComponent<BoxCollider>(); b.size = def.size; col = b;
                prim = Prims.Box(def.size);
                break;
            case "capsule":
                var c = gameObject.AddComponent<CapsuleCollider>(); c.radius = def.r; c.height = def.len; c.direction = 1; col = c;
                prim = Prims.Capsule(def.r, def.len, 20, 6);
                break;
            default:
                var s = gameObject.AddComponent<SphereCollider>(); s.radius = def.r; col = s;
                prim = Prims.Sphere(def.r, def.r * 2f);
                break;
        }
        col.sharedMaterial = pm;
        skinMat = Mats.Mat(SKIN, 0.55f);
        var skin = MeshStore.Human("skin_" + partId);
        Mesh m = prim;
        if (skin != null)
        {
            realModel = true;
            m = skin.mesh;
            if (dummy.look != null && skin.hasColors)
            {
                m = Object.Instantiate(skin.mesh);         // cópia própria: cores e formato deste boneco
                dummy.look.ShapePart(partId, m);
                m.colors = dummy.look.Recolor(m.colors, m.vertices, def.pos);
            }
            Mats.SetRough(skinMat, 0.6f);
            if (skin.hasColors)
            {
                // roupa/pele nos vértices; a cor do material vira só o tom de sangue por cima
                skinMat.SetFloat("_UseVC", 1);
                skinTint = Color.white;
                Mats.SetColor(skinMat, Color.white);
            }
            var hm = MeshStore.Human("hair_" + partId);
            if (hm != null && !(dummy.look != null && dummy.look.bald))
                hairMesh = Prims.Visual("hair", hm.mesh, Mats.Mat(dummy.look != null ? dummy.look.hair : new Color(0.1f, 0.07f, 0.05f), 0.9f), transform);
        }
        bodyMesh = Prims.Visual("body", m, skinMat, transform);
        if (partId == "head")
        {
            deco = new GameObject("deco");
            deco.transform.SetParent(transform, false);
            foreach (float sx in new[] { 1f, -1f })
            {
                var eye = Prims.Visual(sx > 0 ? "eye_l" : "eye_r", Prims.Sphere(0.016f, 0.022f), Mats.Mat(new Color(0.05f, 0.05f, 0.07f), 0.3f), deco.transform);
                eye.transform.localPosition = new Vector3(0.035f * sx, 0.005f, 0.098f);
            }
            // o modelo já tem olhos; as esferas ficam só como estado (abertos/fechados) do código
            deco.SetActive(!realModel);
        }
    }

    void BuildState()
    {
        foreach (var b in def.bones) bones.Add(new BoneState { name = b.name, hp = b.hp, max = b.hp, def = b });
        if (def.skull) bones.Add(new BoneState { name = "Crânio", hp = 90, max = 90, def = new BoneDef { name = "Crânio", shell = true, chance = 1f } });
        if (def.ribs)
        {
            bones.Add(new BoneState { name = "Costelas esquerdas", hp = 60, max = 60, def = new BoneDef { name = "Costelas esquerdas", shell = true, chance = 0.5f, side = 1f } });
            bones.Add(new BoneState { name = "Costelas direitas", hp = 60, max = 60, def = new BoneDef { name = "Costelas direitas", shell = true, chance = 0.5f, side = -1f } });
        }
        foreach (var o in def.organs) organs.Add(new OrganState { name = o.name, hp = o.hp, max = o.hp, vital = o.vital, def = o });
        foreach (var v in def.vessels) vessels.Add(new VesselState { name = v.name, def = v });
    }

    void BuildXray()
    {
        xrayRoot = new GameObject("xray");
        xrayRoot.transform.SetParent(transform, false);
        RealXray("bones", new Color(0.95f, 0.93f, 0.85f), 1);
        RealXray("organs", Color.white, 1);
        foreach (var o in organs)
        {
            o.mat = Mats.XRay(o.def.color, 2);
            var mi = Prims.Visual("organ", Prims.Sphere(0.5f, 1f, 12, 6), o.mat, xrayRoot.transform, false);
            mi.transform.localScale = o.def.s;
            mi.transform.localPosition = o.def.c;
            // com o modelo real, a elipse do órgão só aparece quando ele está lesionado
            mi.SetActive(!realModel);
            o.mesh = mi;
        }
        foreach (var b in bones)
        {
            if (b.def.shell) continue;
            b.mat = SegmentMesh(b.def.a, b.def.b, b.def.r, new Color(0.95f, 0.93f, 0.85f));
        }
        foreach (var v in vessels)
        {
            var c = v.def.artery ? new Color(0.9f, 0.05f, 0.05f) : new Color(0.15f, 0.2f, 0.85f);
            v.mat = SegmentMesh(v.def.a, v.def.b, v.def.r * 0.7f, c);
        }
        xrayRoot.SetActive(Game.xray);
    }

    /// Malha anatômica real (ossos / órgãos) no raio-X, com cor por sistema
    void RealXray(string kind, Color c, int prio)
    {
        var sm = MeshStore.Human(kind + "_" + partId);
        if (sm == null) return;
        var go = Prims.Visual("xray_" + kind, sm.mesh, null, xrayRoot.transform, false);
        var mats = new Material[sm.materialNames.Count];
        for (int i = 0; i < mats.Length; i++)
            mats[i] = Mats.XRay(kind == "organs" ? sm.materialColors[i].gamma : c, prio);
        go.GetComponent<MeshRenderer>().sharedMaterials = mats;
    }

    Material SegmentMesh(Vector3 a, Vector3 b, float r, Color c)
    {
        float length = Mathf.Max(Vector3.Distance(a, b), 0.01f);
        var m = Mats.XRay(c, 3);
        var go = Prims.Visual("seg", Prims.Cylinder(r, r, length, 8), m, xrayRoot.transform, false);
        var dir = length > 0.011f ? (b - a).normalized : Vector3.up;
        go.transform.localRotation = U.BasisY(dir);
        go.transform.localPosition = (a + b) * 0.5f;
        return m;
    }

    // ---------------------------------------------------------------- estado

    public void SetXray(bool on)
    {
        xrayRoot.SetActive(on);
        if (hairMesh) hairMesh.SetActive(!on);
        Mats.SetTransparent(skinMat, on);
        var c = Mats.GetColor(skinMat);
        c.a = on ? 0.22f : 1f;
        Mats.SetColor(skinMat, c);
    }

    public void AddBloodiness(float v)
    {
        bloodiness = Mathf.Clamp(bloodiness + v, 0f, 0.92f);
        float a = Mats.GetColor(skinMat).a;
        var c = Color.Lerp(skinTint, BLOOD_DARK, bloodiness);
        c.a = a;
        Mats.SetColor(skinMat, c);
        Mats.SetRough(skinMat, Mathf.Lerp(0.55f, 0.25f, bloodiness));
        if (dummy) dummy.SetPartBlood(partId, bloodiness);
    }

    public void RefreshOrganVisual(OrganState o)
    {
        if (o.mesh == null) return;
        float f = Mathf.Clamp01(o.hp / o.max);
        var c = Color.Lerp(new Color(0.08f, 0f, 0f), o.def.color, f);
        if (realModel && f < 1f)
        {
            // lesão destacada sobre a anatomia real
            o.mesh.SetActive(true);
            c = Color.Lerp(new Color(1f, 0.1f, 0.05f, 0.75f), new Color(0.15f, 0f, 0f, 0.75f), 1f - f);
        }
        o.mat.SetColor("_Color", c);
        if (o.destroyed) o.mesh.transform.localScale = o.def.s * 0.6f;
    }

    public void RefreshBoneVisual(BoneState b)
    {
        if (b.mat) b.mat.SetColor("_Color", b.fractured ? new Color(1f, 0.25f, 0.1f) : new Color(0.95f, 0.93f, 0.85f));
    }

    public void RefreshVesselVisual(VesselState v)
    {
        if (v.mat && v.cut) v.mat.SetColor("_Color", new Color(1, 1, 0.2f));
    }

    public bool IsLimb() => def.limb.StartsWith("arm") || def.limb.StartsWith("leg");

    public bool AnyFracture() { foreach (var b in bones) if (b.fractured) return true; return false; }
    public bool MainBoneFractured() { foreach (var b in bones) if (b.fractured && !b.def.shell) return true; return false; }

    public OrganState Organ(string vital) { foreach (var o in organs) if (o.vital == vital) return o; return null; }

    /// Ancestral (dentro do mesmo membro) ou ela mesma tem torniquete?
    public bool HasTourniquetAbove()
    {
        var p = this;
        while (p != null)
        {
            if (p.tourniquet) return true;
            if (!p.IsLimb()) return false;
            p = p.parentPart;
        }
        return false;
    }

    public float TreatmentFactor(Wound w)
    {
        float f = 1f;
        if (HasTourniquetAbove()) f *= 0.02f;
        if (bandage > 0f) f *= Mathf.Lerp(1f, w.arterial ? 0.55f : 0.12f, bandage);
        if (pressure > 0f) f *= Mathf.Lerp(1f, 0.3f, pressure);
        return f;
    }

    public float TotalBleed() { float t = 0; foreach (var w in wounds) t += w.ExternalRate() + w.InternalRate(); return t; }

    public List<string> ConditionLines()
    {
        var o = new List<string>();
        if (destroyed) { o.Add("[color=#ff3030]DESTRUÍDA[/color]"); return o; }
        if (cutHere) o.Add("[color=#ff3030]AMPUTADA[/color]");
        else if (severed) o.Add("[color=#ff6060]separada do corpo[/color]");
        foreach (var b in bones)
        {
            if (b.fractured) o.Add($"[color=#ffb070]{b.name}: FRATURADO[/color]");
            else if (b.hp < b.max * 0.5f) o.Add($"{b.name}: trincado");
        }
        foreach (var g in organs)
        {
            if (g.destroyed) o.Add($"[color=#ff4040]{g.name}: DESTRUÍDO[/color]");
            else if (g.hp < g.max) o.Add($"[color=#ff9a9a]{g.name}: {(int)(100f * g.hp / g.max)}%[/color]");
        }
        foreach (var v in vessels)
            if (v.cut) o.Add(v.def.artery ? $"[color=#ff2a2a]{v.name}: ROMPIDA[/color]" : $"[color=#ff7a7a]{v.name}: rompida[/color]");
        if (wounds.Count > 0) o.Add($"{wounds.Count} ferida(s), {TotalBleed():F1} ml/s");
        if (tourniquet) o.Add("[color=#80c0ff]TORNIQUETE[/color]");
        if (bandage > 0f) o.Add("[color=#c0e0ff]Bandagem[/color]");
        return o;
    }

    // ---------------------------------------------------------------- feridas

    public void AddWound(Wound w)
    {
        w.part = this;
        wounds.Add(w);
        WoundVisual(w);
    }

    void WoundVisual(Wound w)
    {
        var colr = new Color(0.12f, 0f, 0f);
        float size = 0.016f;
        switch (w.kind)
        {
            case "saída": size = 0.03f; colr = new Color(0.35f, 0.02f, 0.03f); break;
            case "corte": case "machado": size = 0.02f; break;
            case "queimadura": size = 0.05f; colr = new Color(0.06f, 0.03f, 0.02f); break;
            case "estilhaço": size = 0.012f; break;
            case "toco": size = 0.001f; break;
        }
        if (realModel && w.kind != "toco")
        {
            if (MeshStore.ProjectOnSkin(partId, w.localPos, w.localNormal, out var pp, out var pn))
            { w.localPos = pp; w.localNormal = pn; }
        }
        if (w.kind != "toco" && WoundFx.Create(this, w))
        {
            AddBloodiness(0.035f);
            return;
        }
        var basis = U.BasisY(w.localNormal);
        Vector3 scale = Vector3.one;
        if (w.kind == "corte" || w.kind == "machado")
        {
            scale = new Vector3(w.kind == "corte" ? 3.5f : 4.5f, 1f, 0.8f);
            basis = U.Axis(w.localNormal.normalized, U.Randf() * U.TAU) * basis;
        }
        else if (w.kind == "saída") scale = new Vector3(U.RandRange(0.8f, 1.3f), 1f, U.RandRange(0.8f, 1.3f));
        if (w.kind != "toco")
        {
            var mi = Prims.Visual("wound", Prims.Sphere(size, size * 0.6f, 10, 4), Mats.Mat(colr, 0.2f), transform, false);
            mi.transform.localRotation = basis;
            mi.transform.localPosition = w.localPos + w.localNormal.normalized * 0.002f;
            mi.transform.localScale = scale;
            w.visual = mi;
            // borda escura/vermelha em volta
            Prims.Visual("ring", Prims.Sphere(size * 1.9f, size * 0.25f, 12, 4), Mats.Mat(new Color(0.45f, 0.03f, 0.04f), 0.3f), mi.transform, false);
        }
        AddBloodiness(0.07f);
    }

    /// Visual do coto (carne + osso) num ponto local com normal
    public void AddStumpVisual(Vector3 localPos, Vector3 normal, float radius)
    {
        var root = new GameObject("stump");
        root.transform.SetParent(transform, false);
        root.transform.localRotation = U.BasisY(normal);
        root.transform.localPosition = localPos;
        var flesh = Prims.Visual("flesh", Prims.Cylinder(radius * 0.95f, radius, 0.025f, 16), Mats.Mat(new Color(0.55f, 0.04f, 0.05f), 0.25f), root.transform);
        flesh.transform.localPosition = new Vector3(0, 0.005f, 0);
        var inner = Prims.Visual("inner", Prims.Cylinder(radius * 0.75f, radius * 0.75f, 0.03f, 16), Mats.Mat(new Color(0.32f, 0.01f, 0.02f), 0.15f), root.transform);
        inner.transform.localPosition = new Vector3(0, 0.01f, 0);
        var bone = Prims.Visual("bone", Prims.Cylinder(radius * 0.28f, radius * 0.32f, 0.05f, 12), Mats.Mat(new Color(0.93f, 0.9f, 0.8f), 0.5f), root.transform);
        bone.transform.localPosition = new Vector3(0, 0.022f, 0);
        var marrow = Prims.Visual("marrow", Prims.Cylinder(radius * 0.12f, radius * 0.12f, 0.052f, 10), Mats.Mat(new Color(0.5f, 0.1f, 0.08f), 0.5f), root.transform);
        marrow.transform.localPosition = new Vector3(0, 0.023f, 0);
        for (int i = 0; i < 5; i++)
        {
            var sb = Prims.Box(new Vector3(radius * 0.35f, U.RandRange(0.01f, 0.035f), radius * 0.2f));
            var shred = Prims.Visual("shred", sb, Mats.Mat(Color.Lerp(new Color(0.6f, 0.06f, 0.07f), SKIN, U.Randf() * 0.4f), 0.35f), root.transform);
            float ang = U.Randf() * U.TAU;
            shred.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius * 0.85f, 0.01f, Mathf.Sin(ang) * radius * 0.85f);
            shred.transform.localRotation = U.Euler(U.RandRange(-0.6f, 0.6f), ang, U.RandRange(-0.6f, 0.6f));
        }
        AddBloodiness(0.25f);
    }

    public float PartRadius()
    {
        switch (def.shape)
        {
            case "capsule": return def.r;
            case "sphere": return def.r * 0.6f;
            default: return Mathf.Min(def.size.x, def.size.z) * 0.5f;
        }
    }

    // ---------------------------------------------------------------- geometria (espaço local)

    /// (t_in, t_out) da reta origem+dir*t dentro da forma, ou (-1,-1)
    public Vector2 RaySpan(Vector3 o, Vector3 d)
    {
        switch (def.shape)
        {
            case "box": return Slab(o, d, -def.size * 0.5f, def.size * 0.5f);
            case "sphere": return SphereSpan(o, d, Vector3.zero, def.r);
        }
        float r = def.r, hh = def.len * 0.5f;
        float t0 = -1e9f, t1 = 1e9f;
        float a = d.x * d.x + d.z * d.z;
        float bq = 2f * (o.x * d.x + o.z * d.z);
        float c = o.x * o.x + o.z * o.z - r * r;
        if (a > 1e-8f)
        {
            float disc = bq * bq - 4f * a * c;
            if (disc < 0f) return new Vector2(-1, -1);
            float sq = Mathf.Sqrt(disc);
            t0 = (-bq - sq) / (2f * a);
            t1 = (-bq + sq) / (2f * a);
        }
        else if (c > 0f) return new Vector2(-1, -1);
        if (Mathf.Abs(d.y) > 1e-8f)
        {
            float ty0 = (-hh - o.y) / d.y, ty1 = (hh - o.y) / d.y;
            t0 = Mathf.Max(t0, Mathf.Min(ty0, ty1));
            t1 = Mathf.Min(t1, Mathf.Max(ty0, ty1));
        }
        else if (Mathf.Abs(o.y) > hh) return new Vector2(-1, -1);
        if (t1 < t0) return new Vector2(-1, -1);
        return new Vector2(t0, t1);
    }

    public static Vector2 Slab(Vector3 o, Vector3 d, Vector3 mn, Vector3 mx)
    {
        float t0 = -1e9f, t1 = 1e9f;
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(d[i]) < 1e-8f)
            {
                if (o[i] < mn[i] || o[i] > mx[i]) return new Vector2(-1, -1);
            }
            else
            {
                float a = (mn[i] - o[i]) / d[i], b = (mx[i] - o[i]) / d[i];
                t0 = Mathf.Max(t0, Mathf.Min(a, b));
                t1 = Mathf.Min(t1, Mathf.Max(a, b));
            }
        }
        if (t1 < t0) return new Vector2(-1, -1);
        return new Vector2(t0, t1);
    }

    static Vector2 SphereSpan(Vector3 o, Vector3 d, Vector3 c, float r)
    {
        var oc = o - c;
        float b = Vector3.Dot(oc, d);
        float cc = Vector3.Dot(oc, oc) - r * r;
        float disc = b * b - cc;
        if (disc < 0f) return new Vector2(-1, -1);
        float s = Mathf.Sqrt(disc);
        return new Vector2(-b - s, -b + s);
    }

    /// Distância mínima entre segmentos p1-q1 e p2-q2; retorna (dist, s no seg1 [0..1])
    public static Vector2 SegSeg(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s = 0f, t = 0f;
        if (a <= 1e-9f && e <= 1e-9f) return new Vector2(Vector3.Distance(p1, p2), 0f);
        if (a <= 1e-9f) t = Mathf.Clamp01(f / e);
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 1e-9f) s = Mathf.Clamp01(-c / a);
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                if (denom != 0f) s = Mathf.Clamp01((b * f - c * e) / denom);
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        var c1 = p1 + d1 * s; var c2 = p2 + d2 * t;
        return new Vector2(Vector3.Distance(c1, c2), s);
    }
}
