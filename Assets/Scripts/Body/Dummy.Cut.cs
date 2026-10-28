using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Corte em qualquer ponto de um membro: a pele (e a roupa) é partida por um plano, cada lado ganha uma tampa de
/// carne/osso, o pedaço distal vira um corpo rígido novo (mesmo referencial da parte de origem) e as partes
/// penduradas nele são religadas ao fragmento.
public partial class Dummy
{
    MeshData bodyData, clothData;
    List<Transform> skinBones;
    List<Matrix4x4> skinBind;
    readonly Dictionary<DummyPart, Transform> fragBone = new Dictionary<DummyPart, Transform>();
    int fragCount;

    /// Direção "para a ponta" do membro no espaço de repouso (a mesma dos colisores: Y, ou Z no pé).
    static Vector3 LimbAxis(PartDef d)
    {
        if (d.id.StartsWith("foot")) return Vector3.forward;
        return d.pos.y >= d.joint.anchor.y ? Vector3.up : Vector3.down;
    }

    static int AuxIndex(string id)
    {
        int h = System.Array.IndexOf(HELPER_JOINTS, id);
        return h < 0 ? -1 : Anatomy.ORDER.Length + h;
    }

    /// Pode ser cortada em qualquer ponto? (membros e pescoço; a cabeça é só destruída)
    public bool Cuttable(DummyPart p) =>
        skinBody != null && p != null && !p.destroyed && p.def.joint != null && p.def.severable && p.def.shape != "sphere";

    /// Ponta distal (mundo) de uma peça cortável; usada para cortar o pulso/tornozelo/pescoço quando a mão/pé/cabeça é arrancada.
    Vector3 DistalTip(DummyPart p, float back)
    {
        var axis = LimbAxis(p.def);
        float half = (p.def.shape == "capsule" ? p.def.len : Vector3.Dot(p.def.size, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)))) * 0.5f;
        float aC = Vector3.Dot(p.def.pos, axis);
        float hi = p.segInit ? p.segHi : aC + half;
        return p.Xform.Apply(axis * (hi - back - aC));
    }

    /// Amputação pelo golpe: corta onde bateu (membros), no pulso/tornozelo (mão/pé) ou no pescoço (cabeça).
    bool TryMeshCut(DummyPart p, Vector3 dirW)
    {
        if (skinBody == null || p.cutHere || p.destroyed) return false;
        if (p.meshDetached) return true;                // pedaço já solto: não há o que amputar de novo
        var target = p;
        Vector3 pt = cutHint ?? p.transform.position;
        if (p.partId == "head")
        {
            if (!parts.TryGetValue("neck", out var neck) || !neck || neck.severed) return false;
            target = neck; pt = DistalTip(neck, 0.035f);
        }
        else if (p.def.leaf && p.parentPart != null && !p.parentPart.severed)
        {
            target = p.parentPart; pt = DistalTip(target, 0.02f);
        }
        if (!Cuttable(target) || target.cutHere) return false;
        bool ok = CutAt(target, pt, dirW, cutRagged);
        cutHint = null;
        return ok;
    }

    static float[] CutField(MeshData d, HashSet<int> region, Vector3 axis, float s, float ragged, Vector3 center, float seed)
    {
        var f = new float[d.Count];
        var ex = Vector3.Cross(axis, Vector3.up); if (ex.sqrMagnitude < 1e-4f) ex = Vector3.Cross(axis, Vector3.forward);
        ex.Normalize();
        var ez = Vector3.Cross(ex, axis);
        for (int i = 0; i < f.Length; i++)
        {
            var b = d.bw[i];
            float share = 0f;
            if (region.Contains(b.boneIndex0)) share += b.weight0;
            if (region.Contains(b.boneIndex1)) share += b.weight1;
            if (region.Contains(b.boneIndex2)) share += b.weight2;
            if (region.Contains(b.boneIndex3)) share += b.weight3;
            if (share < 0.5f) { f[i] = -1f; continue; }
            float ax = Vector3.Dot(d.v[i], axis) - s;
            if (ragged > 0f)
            {
                var rel = d.v[i] - center;
                float ang = Mathf.Atan2(Vector3.Dot(rel, ex), Vector3.Dot(rel, ez));
                float nz = Mathf.PerlinNoise(Mathf.Cos(ang) * 1.6f + seed, Mathf.Sin(ang) * 1.6f + 3.1f) - 0.5f;
                ax -= ragged * 0.04f * nz;
            }
            f[i] = Mathf.Abs(ax) < 1e-6f ? 1e-6f : ax;
        }
        return f;
    }

    static bool Crosses(MeshData d, float[] f)
    {
        var t = d.tris;
        for (int i = 0; i < t.Count; i += 3)
        {
            bool a = f[t[i]] > 0f, b = f[t[i + 1]] > 0f, c = f[t[i + 2]] > 0f;
            if (a != b || b != c) return true;
        }
        return false;
    }

    static void ResizeCollider(Collider col, PartDef def, Vector3 axis, float aC, float a, float b)
    {
        float len = Mathf.Max(b - a, 0.02f);
        var center = axis * ((a + b) * 0.5f - aC);
        if (col is CapsuleCollider cc)
        {
            cc.radius = Mathf.Min(def.r, Mathf.Max(len * 0.5f, 0.012f));
            cc.height = Mathf.Max(len, cc.radius * 2f);
            cc.center = center;
        }
        else if (col is BoxCollider bc)
        {
            var sz = def.size;
            if (Mathf.Abs(axis.y) > 0.5f) sz.y = len; else if (Mathf.Abs(axis.z) > 0.5f) sz.z = len; else sz.x = len;
            bc.size = sz;
            bc.center = center;
        }
    }

    /// Corta a parte `p` no ponto do mundo `worldPoint` (projetado no eixo do membro). `ragged` 0..1: corte irregular
    /// (machado, escopeta, explosão); 0 = lâmina limpa. Devolve false se não for possível cortar ali.
    public bool CutAt(DummyPart p, Vector3 worldPoint, Vector3 dirW, float ragged = 0f)
    {
        if (!Cuttable(p) || p.cutHere || p.parentPart == null && !p.fragment) return false;
        var def = p.def;
        var axis = LimbAxis(def);
        float half = (def.shape == "capsule" ? def.len : Vector3.Dot(def.size, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)))) * 0.5f;
        float aC = Vector3.Dot(def.pos, axis);
        if (!p.segInit) { p.segLo = aC - half; p.segHi = aC + half; p.segInit = true; }
        if (p.segHi - p.segLo < 0.09f) return false;

        var rest = p.Xform.Inverse().Apply(worldPoint) + def.pos;
        float s = Mathf.Clamp(Vector3.Dot(rest, axis), p.segLo + 0.035f, p.segHi - 0.035f);
        var centerRest = def.pos + axis * (s - aC);

        // ---- pele e roupa
        EnsureData();
        var chain = Chain(p);
        var region = new HashSet<int>();
        foreach (var c in chain)
        {
            region.Add(c.boneIndex);
            if (c != p) { int ax = AuxIndex(c.partId); if (ax >= 0) region.Add(ax); }
        }
        float seed = Random.value * 50f;
        var f = CutField(bodyData, region, axis, s, ragged, centerRest, seed);
        if (!Crosses(bodyData, f)) return false;

        int newBone = skinBones.Count;
        var distMap = new int[newBone];
        for (int i = 0; i < distMap.Length; i++) distMap[i] = -1;
        distMap[p.boneIndex] = newBone;
        if (!p.fragment) { int ax = AuxIndex(p.partId); if (ax >= 0) distMap[ax] = newBone; }

        var opt = new CutOptions { capNormal = axis, distMap = distMap, capDepth = 0.014f };
        var info = MeshCut.Cut(bodyData, f, opt, out _);
        if (clothData != null)
        {
            var fc = CutField(clothData, region, axis, s, ragged, centerRest, seed);
            if (Crosses(clothData, fc))
            {
                var oc = new CutOptions { capNormal = axis, distMap = distMap, cap = false };
                MeshCut.Cut(clothData, fc, oc, out _);
            }
        }
        skinBind.Add(Matrix4x4.Translate(-def.pos));
        var nbT = new GameObject($"bone_{p.partId}_cut{fragCount + 1}").transform;
        nbT.SetParent(transform, false);
        nbT.localPosition = def.pos;
        skinBones.Add(nbT);
        var bind = skinBind.ToArray();
        bodyData.Apply(skinBody.sharedMesh, bind);
        if (clothData != null) clothData.Apply(skinCloth.sharedMesh, bind);
        meshVertsDirty = meshTopoDirty = false;
        skinBody.bones = skinBones.ToArray();
        if (skinCloth) skinCloth.bones = skinBones.ToArray();

        // ---- física: o corpo cai e o fragmento ganha vida própria
        if (state == State.STANDING) Collapse();
        bool wasAttached = !p.severed;
        float lo = p.segLo, hi = p.segHi;
        float frac = (hi - s) / (hi - lo);

        var go = new GameObject($"{p.partId}_corte{++fragCount}");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(p.transform.position, p.transform.rotation);
        var frag = go.AddComponent<DummyPart>();
        frag.Setup(this, p.partId, def);
        frag.fragment = true; frag.boneIndex = newBone; frag.segInit = true; frag.segLo = s; frag.segHi = hi;
        frag.bodyMesh.SetActive(false);
        if (frag.hairMesh) frag.hairMesh.SetActive(false);
        parts[p.partId + "#" + fragCount] = frag;
        fragBone[frag] = nbT;

        float m0 = p.rb.mass;
        frag.rb.mass = Mathf.Max(0.05f, m0 * frac);
        p.rb.mass = Mathf.Max(0.05f, m0 * (1f - frac));
        frag.limbBlood = p.limbBlood * frac; p.limbBlood -= frag.limbBlood;
        frag.maxHp = Mathf.Max(10f, p.maxHp * frac); frag.hp = frag.maxHp * 0.5f;
        p.hp = Mathf.Max(p.hp, p.maxHp * 0.35f);
        ResizeCollider(p.col, def, axis, aC, lo, s);
        ResizeCollider(frag.col, def, axis, aC, s, hi);
        p.segHi = s;
        // ossos, órgãos e vasos ficam com o lado em que estão
        SplitAnatomy(p, frag, axis, aC, s);

        frag.rb.linearVelocity = p.rb.linearVelocity; frag.rb.angularVelocity = p.rb.angularVelocity;
        Physics.IgnoreCollision(p.col, frag.col, true);
        StartCoroutine(RestoreCollision(p.col, frag.col, 0.45f));

        // partes penduradas no pedaço cortado passam a depender do fragmento
        var kids = new List<DummyPart>(p.childrenParts);
        p.childrenParts.Clear();
        foreach (var c in kids)
        {
            float theta = 0f;
            if (c.def.joint.type == "hinge")
                theta = Mathf.DeltaAngle(0f, (Quaternion.Inverse(p.transform.rotation) * c.transform.rotation).eulerAngles.x);
            if (c.joint != null) DestroyImmediate(c.joint);
            c.parentPart = frag; frag.childrenParts.Add(c);
            MakeJoint(c, theta);
        }

        foreach (var c in Chain(frag))
        {
            c.severed = true; c.meshDetached = true; c.Freeze = false; c.pressure = 0f;
        }
        frag.cutHere = true; frag.hasStump = true;
        frag.stumpLocal = axis * (s - aC); frag.stumpNormal = -axis;

        // ---- fisiologia e efeitos
        var cutLocal = (info.cutTriangles > 0 ? info.proxCenter : centerRest) - def.pos;
        var cw = p.Xform.Apply(cutLocal);
        var nw = (p.transform.rotation * axis).normalized;
        if (wasAttached)
        {
            float lost = 0f;
            foreach (var c in Chain(frag)) if (!c.destroyed) lost += c.limbBlood;
            blood = Mathf.Max(0f, blood - lost * 0.55f);
            totalLost += lost * 0.55f;
            frag.drainBlood = lost * 0.45f;
            var w = new Wound { kind = "toco", localPos = cutLocal, localNormal = axis, bleed = def.stumpBleed * Mathf.Lerp(1f, 0.75f, ragged), arterial = true, vessel = def.stumpVessel, pain = 0.35f };
            p.AddWound(w);
            frag.stumpParentWound = w;
            acutePain += 0.7f;
        }
        var dv = dirW != Vector3.zero ? dirW : nw;
        frag.rb.AddForce((dv.normalized * 6f + U.RandomUnit() * 2f) * Mathf.Min(frag.mass, 6f) * 0.6f, ForceMode.Impulse);
        p.impactGrace = 0.5f; frag.impactGrace = 0.5f;
        Game.blood.Spray(cw, nw, 40, 4f, 0.5f, p.col);
        Game.blood.Spray(cw, -nw, 20, 2.5f, 0.6f, frag.col);
        Game.blood.Mist(cw, dv, 2f);
        Game.sfx.Play("sever", cw, 2f);
        Game.LogEvent($"[b]AMPUTAÇÃO:[/b] {def.name} cortado(a) a {(s - p.segLo) * 100f:F0} cm da articulação — {def.stumpVessel} jorrando", new Color(1, 0.25f, 0.25f));
        if (p.partId == "neck" || p.partId == "head") Die("Decapitação");
        return true;
    }

    static IEnumerator RestoreCollision(Collider a, Collider b, float t)
    {
        yield return new WaitForSeconds(t);
        if (a && b) Physics.IgnoreCollision(a, b, false);
    }

    /// Reparte ossos, órgãos e vasos entre o lado proximal (p) e o distal (frag) pela posição ao longo do eixo.
    static void SplitAnatomy(DummyPart p, DummyPart frag, Vector3 axis, float aC, float s)
    {
        // coordenada axial no espaço local da parte: a_local + aC (o referencial local é o de repouso, sem a posição)
        bool Distal(Vector3 a, Vector3 b) => Vector3.Dot((a + b) * 0.5f, axis) + aC > s;
        bool DistalPt(Vector3 c) => Vector3.Dot(c, axis) + aC > s;
        p.bones.RemoveAll(b => !b.def.shell && Distal(b.def.a, b.def.b));
        frag.bones.RemoveAll(b => b.def.shell || !Distal(b.def.a, b.def.b));
        p.organs.RemoveAll(o => DistalPt(o.def.c));
        frag.organs.RemoveAll(o => !DistalPt(o.def.c));
        p.vessels.RemoveAll(v => Distal(v.def.a, v.def.b));
        frag.vessels.RemoveAll(v => !Distal(v.def.a, v.def.b));
    }
}
