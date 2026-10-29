using System.Collections.Generic;
using UnityEngine;

/// Decalques de ferida presos à pele deformada (pendências 04 e 07-C): cada decalque é um recorte dos vértices da pele
/// deste boneco (já com o formato individual, amassados e cortes), com os mesmos ossos. Quando a pele amassa, o decalque
/// amassa junto; quando ela é furada ou cortada, os triângulos do decalque que ficaram sem pele somem. O decalque antigo
/// (cópia rígida da malha original pendurada na parte) descolava perto das juntas e virava "quadrados" sobre o tronco
/// deformado.
public partial class Dummy
{
    class SkinDecal
    {
        public SkinnedMeshRenderer r;
        public Mesh mesh;
        public int[] src;               // vértice da pele de origem de cada vértice do decalque
        public List<int> tris;          // índices locais
        public float lift;
        public Vector3 center;          // repouso
        public string kind;
    }

    readonly List<SkinDecal> decals = new List<SkinDecal>();
    const int MAX_DECALS = 120;
    bool[] restCloth;                    // vértice original pintado de roupa (o decalque sobe a espessura dela)

    /// A ferida pode ganhar decalque na pele contínua? (parte arrancada na junta usa a peça rígida e o decalque antigo)
    public bool CanSkinDecal(DummyPart p) => skinBody != null && p != null && !Collapsed(p);

    /// Há um decalque do mesmo tipo a menos de `dist` (repouso)? Evita empilhar dezenas no mesmo ponto (explosão).
    public bool DecalNear(Vector3 restPos, string kind, float dist)
    {
        foreach (var dc in decals) if (dc.r && dc.kind == kind && (dc.center - restPos).sqrMagnitude < dist * dist) return true;
        return false;
    }

    /// Recorta a pele em volta de `c` (repouso) e projeta a textura no plano (t, b). half = metade da largura/altura.
    public GameObject AddSkinDecal(DummyPart part, Vector3 c, Vector3 n, Vector3 t, Vector2 half, float lift, Material mat, bool flipV, string kind)
    {
        EnsureData();
        if (restCloth == null)
        {
            var sm = MeshStore.Human("body");
            var cols = sm != null ? sm.mesh.colors : null;
            restCloth = new bool[cols != null ? cols.Length : 0];
            for (int i = 0; i < restCloth.Length; i++) restCloth[i] = ClothLayer.IsCloth(cols[i]);
        }
        var b = Vector3.Cross(n, t).normalized;
        float rad = Mathf.Max(half.x, half.y) * 1.45f;
        // ossos desta parte (e auxiliares das juntas dela): o decalque não passa para o membro encostado
        var own = new HashSet<int> { part.boneIndex };
        int ax = AuxIndex(part.partId); if (ax >= 0 && !part.fragment) own.Add(ax);
        foreach (var ch in part.childrenParts) { int ca = AuxIndex(ch.partId); if (ca >= 0) own.Add(ca); }

        bodyData.Query(c, rad, nearBuf);
        var near = new HashSet<int>(nearBuf);
        var map = new Dictionary<int, int>();
        var src = new List<int>(); var tris = new List<int>();
        var tr = bodyData.tris;
        for (int i = 0; i < tr.Count; i += 3)
        {
            int a0 = tr[i], a1 = tr[i + 1], a2 = tr[i + 2];
            if (!near.Contains(a0) && !near.Contains(a1) && !near.Contains(a2)) continue;
            bool outside = true, mine = false;
            for (int k = 0; k < 3; k++)
            {
                int vi = tr[i + k];
                var d = bodyData.v[vi] - c;
                if (Mathf.Abs(Vector3.Dot(d, t) / half.x) < 1.05f && Mathf.Abs(Vector3.Dot(d, b) / half.y) < 1.05f) outside = false;
                var w = bodyData.bw[vi];
                float s = 0f;
                foreach (int o in own) s += MeshCut.WeightOn(w, o);
                if (s >= 0.4f) mine = true;
            }
            if (outside || !mine) continue;
            if (Vector3.Dot(bodyData.n[a0], n) < 0.15f) continue;
            for (int k = 0; k < 3; k++)
            {
                int vi = tr[i + k];
                if (!map.TryGetValue(vi, out int li)) { li = src.Count; map[vi] = li; src.Add(vi); }
                tris.Add(li);
            }
        }
        if (tris.Count == 0) return null;
        var uv = new List<Vector2>(src.Count);
        foreach (int vi in src)
        {
            var d = bodyData.v[vi] - c;
            float u = Vector3.Dot(d, t) / half.x * 0.5f + 0.5f, v = Vector3.Dot(d, b) / half.y * 0.5f + 0.5f;
            uv.Add(new Vector2(u, flipV ? 1f - v : v));
        }
        var dc = new SkinDecal { src = src.ToArray(), tris = tris, lift = lift, center = c, kind = kind };
        dc.mesh = new Mesh { name = "ferida_pele" };
        dc.mesh.MarkDynamic();
        var go = new GameObject("ferida_" + kind);
        go.transform.SetParent(transform, false);
        dc.r = go.AddComponent<SkinnedMeshRenderer>();
        dc.r.rootBone = transform;
        dc.r.updateWhenOffscreen = true;
        dc.r.quality = SkinQuality.Bone4;
        dc.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dc.r.sharedMaterial = mat;
        FillDecal(dc, true);
        dc.mesh.SetUVs(0, uv);
        decals.Add(dc);
        while (decals.Count > MAX_DECALS)
        {
            if (decals[0].r) Destroy(decals[0].r.gameObject);
            decals.RemoveAt(0);
        }
        return go;
    }

    void FillDecal(SkinDecal dc, bool topo)
    {
        int n = dc.src.Length;
        var v = new Vector3[n]; var nn = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int s = dc.src[i];
            var no = bodyData.n[s];
            float up = dc.lift + (s < restCloth.Length && restCloth[s] ? ClothLayer.THICK : 0f);
            v[i] = bodyData.v[s] + no * up; nn[i] = no;
        }
        dc.mesh.vertices = v; dc.mesh.normals = nn;
        if (topo)
        {
            var bw = new BoneWeight[n];
            for (int i = 0; i < n; i++) bw[i] = bodyData.bw[dc.src[i]];
            dc.mesh.boneWeights = bw;
            dc.mesh.bindposes = skinBind.ToArray();
            dc.mesh.SetTriangles(dc.tris, 0);
            dc.r.bones = skinBones.ToArray();
            dc.r.sharedMesh = dc.mesh;
        }
        dc.mesh.RecalculateBounds();
    }

    /// A pele mudou (amassado: só posição; corte/buraco/pesos: tudo).
    void RefreshDecals(bool topo)
    {
        for (int i = decals.Count - 1; i >= 0; i--)
        {
            var dc = decals[i];
            if (!dc.r) { decals.RemoveAt(i); continue; }
            if (dc.tris.Count == 0) { Destroy(dc.r.gameObject); decals.RemoveAt(i); continue; }
            FillDecal(dc, topo);
        }
    }

    /// Corte: tira os triângulos do decalque que ficaram com vértices dos dois lados (a pele embaixo foi refeita).
    void DecalsAfterCut(bool[] side)
    {
        foreach (var dc in decals)
        {
            if (!dc.r) continue;
            var keep = new List<int>(dc.tris.Count);
            for (int i = 0; i < dc.tris.Count; i += 3)
            {
                int a = dc.src[dc.tris[i]], b = dc.src[dc.tris[i + 1]], c = dc.src[dc.tris[i + 2]];
                bool sa = side[a], sb = side[b], sc = side[c];
                if (sa != sb || sb != sc) continue;
                keep.Add(dc.tris[i]); keep.Add(dc.tris[i + 1]); keep.Add(dc.tris[i + 2]);
            }
            dc.tris = keep;
        }
    }

    /// Buraco: tira os triângulos do decalque que estavam sobre a pele removida.
    void DecalsAfterCrater(Vector3 center, Vector3 n, float radius)
    {
        float r2 = radius * radius * 1.1f;
        foreach (var dc in decals)
        {
            if (!dc.r || (dc.center - center).sqrMagnitude > (radius + 0.2f) * (radius + 0.2f)) continue;
            var keep = new List<int>(dc.tris.Count);
            for (int i = 0; i < dc.tris.Count; i += 3)
            {
                var p = (bodyData.v[dc.src[dc.tris[i]]] + bodyData.v[dc.src[dc.tris[i + 1]]] + bodyData.v[dc.src[dc.tris[i + 2]]]) / 3f;
                if ((p - center).sqrMagnitude < r2 && Vector3.Dot(bodyData.n[dc.src[dc.tris[i]]], n) > 0f) continue;
                keep.Add(dc.tris[i]); keep.Add(dc.tris[i + 1]); keep.Add(dc.tris[i + 2]);
            }
            dc.tris = keep;
        }
    }
}
