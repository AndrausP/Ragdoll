using System;
using System.Collections.Generic;
using UnityEngine;

/// Cópia editável (em listas) de uma malha com pele: posições de repouso, normais, cores, UV2 (índice da parte),
/// UV3/UV4 (posição e normal de repouso para o detalhe do shader) e pesos de ossos.
public class MeshData
{
    public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(), rest = new List<Vector3>(), restN = new List<Vector3>();
    public readonly List<Color> c = new List<Color>();
    public readonly List<Vector2> uv2 = new List<Vector2>();
    public readonly List<BoneWeight> bw = new List<BoneWeight>();
    public readonly List<int> tris = new List<int>();
    public bool hasColors, hasUV2, hasRest, hasBW;
    public Matrix4x4[] bindposes;

    public int Count => v.Count;

    // grade espacial (células de 6 cm) para achar os vértices perto de um ponto sem varrer a malha inteira;
    // reconstruída quando o número de vértices muda (corte, buraco); os amassados só movem vértices alguns mm
    Dictionary<long, List<int>> grid; int gridCount = -1;
    const float CELL = 0.06f;
    static long CellKey(int x, int y, int z) => ((long)(x + 1024) << 40) | ((long)(y + 1024) << 20) | (long)(z + 1024);

    public void Query(Vector3 c, float r, List<int> result)
    {
        result.Clear();
        if (grid == null || gridCount != v.Count)
        {
            grid = new Dictionary<long, List<int>>(8192);
            for (int i = 0; i < v.Count; i++)
            {
                long k = CellKey(Mathf.FloorToInt(v[i].x / CELL), Mathf.FloorToInt(v[i].y / CELL), Mathf.FloorToInt(v[i].z / CELL));
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>(16);
                l.Add(i);
            }
            gridCount = v.Count;
        }
        int x0 = Mathf.FloorToInt((c.x - r) / CELL), x1 = Mathf.FloorToInt((c.x + r) / CELL);
        int y0 = Mathf.FloorToInt((c.y - r) / CELL), y1 = Mathf.FloorToInt((c.y + r) / CELL);
        int z0 = Mathf.FloorToInt((c.z - r) / CELL), z1 = Mathf.FloorToInt((c.z + r) / CELL);
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    if (grid.TryGetValue(CellKey(x, y, z), out var l)) result.AddRange(l);
    }

    public static MeshData From(Mesh m)
    {
        var d = new MeshData();
        d.v.AddRange(m.vertices);
        d.n.AddRange(m.normals);
        var cols = m.colors; d.hasColors = cols != null && cols.Length == d.v.Count;
        if (d.hasColors) d.c.AddRange(cols);
        var u2 = new List<Vector2>(); m.GetUVs(1, u2); d.hasUV2 = u2.Count == d.v.Count;
        if (d.hasUV2) d.uv2.AddRange(u2);
        var r = new List<Vector3>(); m.GetUVs(2, r);
        var rn = new List<Vector3>(); m.GetUVs(3, rn);
        d.hasRest = r.Count == d.v.Count && rn.Count == d.v.Count;
        if (d.hasRest) { d.rest.AddRange(r); d.restN.AddRange(rn); }
        var w = m.boneWeights; d.hasBW = w != null && w.Length == d.v.Count;
        if (d.hasBW) d.bw.AddRange(w);
        for (int s = 0; s < m.subMeshCount; s++) d.tris.AddRange(m.GetTriangles(s));
        d.bindposes = m.bindposes;
        return d;
    }

    /// Escreve de volta numa malha existente (mesmo objeto, para o SkinnedMeshRenderer continuar apontando para ela).
    public void Apply(Mesh m, Matrix4x4[] newBindposes = null)
    {
        var bp = newBindposes ?? bindposes;
        m.Clear();
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetNormals(n);
        if (hasColors) m.SetColors(c);
        if (hasUV2) m.SetUVs(1, uv2);
        if (hasRest) { m.SetUVs(2, rest); m.SetUVs(3, restN); }
        if (hasBW) m.boneWeights = bw.ToArray();
        if (bp != null && bp.Length > 0) m.bindposes = bp;
        m.subMeshCount = 1;
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
    }

    /// Só posições, normais e cores (sem mexer na topologia): barato, para amassados.
    public void ApplyVerts(Mesh m)
    {
        m.SetVertices(v);
        m.SetNormals(n);
        if (hasColors) m.SetColors(c);
    }

    public int AddCopy(int i)
    {
        v.Add(v[i]); n.Add(n[i]);
        if (hasColors) c.Add(c[i]);
        if (hasUV2) uv2.Add(uv2[i]);
        if (hasRest) { rest.Add(rest[i]); restN.Add(restN[i]); }
        if (hasBW) bw.Add(bw[i]);
        return v.Count - 1;
    }

    public int AddLerp(int a, int b, float t)
    {
        int i = AddCopy(t < 0.5f ? a : b);
        v[i] = Vector3.Lerp(v[a], v[b], t);
        var nn = Vector3.Lerp(n[a], n[b], t); n[i] = nn.sqrMagnitude > 1e-8f ? nn.normalized : n[a];
        if (hasColors) c[i] = Color.Lerp(c[a], c[b], t);
        if (hasRest)
        {
            rest[i] = Vector3.Lerp(rest[a], rest[b], t);
            var rr = Vector3.Lerp(restN[a], restN[b], t); restN[i] = rr.sqrMagnitude > 1e-8f ? rr.normalized : restN[a];
        }
        return i;
    }
}

public class CutOptions
{
    public Vector3 capNormal = Vector3.down;      // direção "para o lado distal" (normal da tampa da parte proximal)
    public bool cap = true;
    public float capDepth = 0.012f;               // quanto o centro da tampa afunda (coto côncavo)
    public Color rimColor = new Color(0.55f, 0.04f, 0.05f, 1f);
    public Color coreColor = new Color(0.30f, 0.02f, 0.03f, 1f);
    public Color boneColor = new Color(0.90f, 0.86f, 0.74f, 1f);
    public float boneRadius = 0.018f;
    public float irregular = 0.6f;                // 0 = tampa lisa, 1 = carne bem irregular
    public int[] proxMap, distMap;                // por índice de osso: substituto para vértices do lado proximal / distal (-1 = mantém)
}

public struct CutInfo
{
    public int distVerts, proxVerts, cutTriangles;
    public Vector3 proxCenter, distCenter;        // centros das tampas (espaço de repouso)
    public float proxRadius;
}

public static class MeshCut
{
    /// Corta `d` pela função f (por vértice; f > 0 = lado distal). Triângulos que cruzam são divididos; cada lado ganha
    /// uma tampa (leque ao redor do centro, afundado). Vértices do lado proximal/distal têm os ossos remapeados por
    /// `proxMap`/`distMap`. Devolve o lado de cada vértice (incluindo os novos) em `side`.
    public static CutInfo Cut(MeshData d, float[] f, CutOptions o, out bool[] side)
    {
        int n0 = d.Count;
        var sideL = new List<bool>(n0 + 512);
        for (int i = 0; i < n0; i++) sideL.Add(f[i] > 0f);

        var cache = new Dictionary<long, int[]>();       // aresta -> {copia proximal, copia distal}
        var newTris = new List<int>(d.tris.Count + 256);
        var proxBoundary = new List<int>();               // pares a->b (orientados) de arestas de borda de cada lado
        var distBoundary = new List<int>();
        int cutTris = 0;

        int[] Crossing(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out var r)) return r;
            // t medido a partir de `a` na ordem da chave, para os dois triângulos que dividem a aresta concordarem
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            float flo = f[lo], fhi = f[hi];
            float t = Mathf.Clamp01(flo / (flo - fhi));
            int p = d.AddLerp(lo, hi, t);
            int q = d.AddCopy(p);
            sideL.Add(false); sideL.Add(true);          // p (índice n) proximal, q distal
            r = new[] { p, q };
            cache[key] = r;
            return r;
        }

        var tr = d.tris;
        for (int k = 0; k < tr.Count; k += 3)
        {
            int a = tr[k], b = tr[k + 1], c = tr[k + 2];
            bool sa = f[a] > 0f, sb = f[b] > 0f, sc = f[c] > 0f;
            if (sa == sb && sb == sc) { newTris.Add(a); newTris.Add(b); newTris.Add(c); continue; }
            cutTris++;
            // vértice sozinho no seu lado, em ordem cíclica lone -> o1 -> o2 (preserva o enrolamento)
            int lone, o1, o2; bool loneDist;
            if (sa != sb && sa != sc) { lone = a; o1 = b; o2 = c; loneDist = sa; }
            else if (sb != sa && sb != sc) { lone = b; o1 = c; o2 = a; loneDist = sb; }
            else { lone = c; o1 = a; o2 = b; loneDist = sc; }
            var x1 = Crossing(lone, o1);
            var x2 = Crossing(lone, o2);
            int lonePick = loneDist ? 1 : 0, otherPick = loneDist ? 0 : 1;
            // lado do vértice sozinho: triângulo (lone, x1, x2)
            newTris.Add(lone); newTris.Add(x1[lonePick]); newTris.Add(x2[lonePick]);
            // lado oposto: quadrilátero (x1, o1, o2, x2)
            newTris.Add(x1[otherPick]); newTris.Add(o1); newTris.Add(o2);
            newTris.Add(x1[otherPick]); newTris.Add(o2); newTris.Add(x2[otherPick]);
            var loneList = loneDist ? distBoundary : proxBoundary;
            var otherList = loneDist ? proxBoundary : distBoundary;
            loneList.Add(x1[lonePick]); loneList.Add(x2[lonePick]);        // borda x1 -> x2 no triângulo do lone
            otherList.Add(x2[otherPick]); otherList.Add(x1[otherPick]);    // borda x2 -> x1 no quadrilátero
        }

        side = null;
        var info = new CutInfo { cutTriangles = cutTris };
        if (o.cap && cutTris > 0)
        {
            AddCap(d, newTris, proxBoundary, sideL, false, o, ref info);
            AddCap(d, newTris, distBoundary, sideL, true, o, ref info);
        }

        d.tris.Clear(); d.tris.AddRange(newTris);

        // ossos por lado
        if (d.hasBW)
        {
            for (int i = 0; i < d.Count; i++)
            {
                var map = sideL[i] ? o.distMap : o.proxMap;
                if (map == null) continue;
                d.bw[i] = Remap(d.bw[i], map);
            }
        }
        side = sideL.ToArray();
        for (int i = 0; i < side.Length; i++) if (side[i]) info.distVerts++; else info.proxVerts++;
        return info;
    }

    public static void AddCap(MeshData d, List<int> tris, List<int> boundary, List<bool> side, bool distSide, CutOptions o, ref CutInfo info)
    {
        if (boundary.Count == 0) return;
        // componentes conexas das bordas (um laço por membro cortado)
        var parent = new Dictionary<int, int>();
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int i = 0; i < boundary.Count; i++) if (!parent.ContainsKey(boundary[i])) parent[boundary[i]] = boundary[i];
        for (int i = 0; i < boundary.Count; i += 2) parent[Find(boundary[i])] = Find(boundary[i + 1]);
        // a malha tem costuras (vértices duplicados na mesma posição): sem soldar por posição o anel do corte vira vários pedaços
        var byPos = new Dictionary<long, int>();
        foreach (int vi in new List<int>(parent.Keys))
        {
            var q = d.v[vi];
            long key = ((long)Mathf.RoundToInt(q.x * 5000f) * 73856093L) ^ ((long)Mathf.RoundToInt(q.y * 5000f) * 19349663L) ^ ((long)Mathf.RoundToInt(q.z * 5000f) * 83492791L);
            if (byPos.TryGetValue(key, out int other)) parent[Find(vi)] = Find(other); else byPos[key] = vi;
        }

        var comp = new Dictionary<int, List<int>>();         // raiz -> índices de arestas
        for (int i = 0; i < boundary.Count; i += 2)
        {
            int r = Find(boundary[i]);
            if (!comp.TryGetValue(r, out var l)) comp[r] = l = new List<int>();
            l.Add(i);
        }

        // normal da tampa: para fora da peça: proximal olha para o lado distal (+capNormal), distal para o proximal
        var nrm = distSide ? -o.capNormal : o.capNormal;
        foreach (var kv in comp)
        {
            var edges = kv.Value;
            var verts = new HashSet<int>();
            foreach (int e in edges) { verts.Add(boundary[e]); verts.Add(boundary[e + 1]); }
            Vector3 cen = Vector3.zero; foreach (int vi in verts) cen += d.v[vi]; cen /= verts.Count;
            float rad = 0f; foreach (int vi in verts) rad = Mathf.Max(rad, Vector3.Distance(d.v[vi], cen));
            if (!distSide) { info.proxCenter = cen; info.proxRadius = rad; } else info.distCenter = cen;

            // cópias da borda com a normal da tampa e cor de carne
            var rim = new Dictionary<int, int>();
            foreach (int vi in verts)
            {
                int ci = d.AddCopy(vi);
                side.Add(distSide);
                d.n[ci] = nrm;
                if (d.hasColors) d.c[ci] = o.rimColor;
                if (d.hasRest) d.restN[ci] = nrm;
                rim[vi] = ci;
            }
            // anéis: carne -> carne escura -> osso -> centro afundado
            var inward = -nrm;
            var rings = new List<Dictionary<int, int>> { rim };
            float[] scale = { 0.80f, 0.52f, 0.25f };
            float[] depth = { 0.40f, 0.90f, 0.55f };
            Color[] col = { o.rimColor, o.coreColor, o.boneColor };
            for (int ri = 0; ri < scale.Length; ri++)
            {
                var ring = new Dictionary<int, int>();
                foreach (int vi in verts)
                {
                    int ci = d.AddCopy(vi);
                    side.Add(distSide);
                    // irregularidade da carne dilacerada (determinística pela posição)
                    float hz = Mathf.Abs(Mathf.Sin(d.v[vi].x * 913f + d.v[vi].y * 517f + d.v[vi].z * 311f + ri * 7f));
                    float rj = 1f + (hz - 0.5f) * 0.22f * o.irregular, dj = 1f + (hz - 0.5f) * 0.8f * o.irregular;
                    d.v[ci] = cen + (d.v[vi] - cen) * (scale[ri] * rj) + inward * (o.capDepth * depth[ri] * dj);
                    d.n[ci] = nrm;
                    if (d.hasColors) d.c[ci] = col[ri];
                    if (d.hasRest) { d.rest[ci] = d.v[ci]; d.restN[ci] = nrm; }
                    ring[vi] = ci;
                }
                rings.Add(ring);
            }
            // centro (osso)
            int first = -1; foreach (int vi in verts) { first = vi; break; }
            int center = d.AddCopy(first);
            side.Add(distSide);
            d.v[center] = cen + inward * (o.capDepth * 0.15f);
            d.n[center] = nrm;
            if (d.hasColors) d.c[center] = o.boneColor;
            if (d.hasRest) { d.rest[center] = d.v[center]; d.restN[center] = nrm; }

            // arestas: cada aresta a->b da borda ganha o triângulo (b, a, centro) -> aqui, em faixas entre anéis
            foreach (int e in edges)
            {
                int a = boundary[e], b = boundary[e + 1];
                for (int ri = 0; ri < rings.Count - 1; ri++)
                {
                    int a0 = rings[ri][a], b0 = rings[ri][b], a1 = rings[ri + 1][a], b1 = rings[ri + 1][b];
                    // quadrilátero (b0, a0, a1, b1): contém a aresta b0 -> a0, oposta a da malha
                    tris.Add(b0); tris.Add(a0); tris.Add(a1);
                    tris.Add(b0); tris.Add(a1); tris.Add(b1);
                }
                var last = rings[rings.Count - 1];
                tris.Add(last[b]); tris.Add(last[a]); tris.Add(center);
            }
            // a borda original da malha (sem a tampa) tem de soldar com o primeiro anel: as cópias `rim` têm posição
            // igual à da borda, então não há fresta.
        }
    }

    static long PosKey(Vector3 q) =>
        ((long)Mathf.RoundToInt(q.x * 5000f) * 73856093L) ^ ((long)Mathf.RoundToInt(q.y * 5000f) * 19349663L) ^ ((long)Mathf.RoundToInt(q.z * 5000f) * 83492791L);

    /// Fura a malha em volta de `center` (faces do lado de `n`, até `radius`); com `cap`, fecha o buraco com um funil de carne.
    public static bool Crater(MeshData d, Vector3 center, Vector3 n, float radius, CutOptions o, bool cap)
    {
        var tr = d.tris; int nt = tr.Count / 3;
        var remove = new bool[nt]; int any = 0;
        for (int t = 0; t < nt; t++)
        {
            Vector3 a = d.v[tr[t * 3]], b = d.v[tr[t * 3 + 1]], c = d.v[tr[t * 3 + 2]];
            if (((a + b + c) / 3f - center).sqrMagnitude > radius * radius) continue;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, n) < 0.2f) continue;
            remove[t] = true; any++;
        }
        if (any == 0) return false;
        var removedEdges = new HashSet<(long, long)>();
        for (int t = 0; t < nt; t++)
        {
            if (!remove[t]) continue;
            for (int e = 0; e < 3; e++)
                removedEdges.Add((PosKey(d.v[tr[t * 3 + e]]), PosKey(d.v[tr[t * 3 + (e + 1) % 3]])));
        }
        var kept = new List<int>(tr.Count);
        var boundary = new List<int>();
        for (int t = 0; t < nt; t++)
        {
            if (remove[t]) continue;
            for (int e = 0; e < 3; e++)
            {
                int a = tr[t * 3 + e], b = tr[t * 3 + (e + 1) % 3];
                kept.Add(a);
                if (removedEdges.Contains((PosKey(d.v[b]), PosKey(d.v[a])))) { boundary.Add(a); boundary.Add(b); }
            }
        }
        if (cap && boundary.Count > 0)
        {
            var info = new CutInfo();
            o.capNormal = n;
            AddCap(d, kept, boundary, new List<bool>(), false, o, ref info);
        }
        d.tris.Clear(); d.tris.AddRange(kept);
        return true;
    }

    static BoneWeight Remap(BoneWeight w, int[] map)
    {
        int[] idx = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
        float[] wt = { w.weight0, w.weight1, w.weight2, w.weight3 };
        for (int k = 0; k < 4; k++)
            if (wt[k] > 0f && idx[k] >= 0 && idx[k] < map.Length && map[idx[k]] >= 0) idx[k] = map[idx[k]];
        // junta pesos de ossos repetidos
        for (int a = 0; a < 4; a++)
            for (int b = a + 1; b < 4; b++)
                if (wt[b] > 0f && wt[a] > 0f && idx[a] == idx[b]) { wt[a] += wt[b]; wt[b] = 0f; }
        float s = wt[0] + wt[1] + wt[2] + wt[3];
        if (s > 1e-6f) for (int k = 0; k < 4; k++) wt[k] /= s;
        for (int a = 1; a < 4; a++)
            for (int k = a; k > 0 && wt[k] > wt[k - 1]; k--)
            {
                (wt[k], wt[k - 1]) = (wt[k - 1], wt[k]);
                (idx[k], idx[k - 1]) = (idx[k - 1], idx[k]);
            }
        return new BoneWeight
        {
            boneIndex0 = idx[0], boneIndex1 = idx[1], boneIndex2 = idx[2], boneIndex3 = idx[3],
            weight0 = wt[0], weight1 = wt[1], weight2 = wt[2], weight3 = wt[3],
        };
    }
}
