using System.Collections.Generic;
using UnityEngine;

/// Visual das feridas: decalques que seguem a pele (triângulos do próprio corpo recortados em volta do ponto),
/// com textura procedural por tipo e um filete de sangue que escorre e cresce com o tempo.
public static class WoundFx
{
    static Shader shader;
    static readonly Dictionary<string, Texture2D[]> texs = new Dictionary<string, Texture2D[]>();
    static Texture2D[] trickles;
    const int VARIANTS = 3;

    // ---- malha do corpo em repouso, triângulos agrupados por parte
    static Vector3[] bodyV, bodyN;
    static Dictionary<int, int[]> trisByPart;

    static bool LoadBody()
    {
        if (trisByPart != null) return bodyV != null;
        trisByPart = new Dictionary<int, int[]>();
        var sm = MeshStore.Human("body");
        if (sm == null || !sm.skinned) return false;
        bodyV = sm.vertices;
        bodyN = sm.mesh.normals;
        var uv2 = sm.mesh.uv2;
        var t = sm.triangles;
        var lists = new Dictionary<int, List<int>>();
        for (int i = 0; i < t.Length; i += 3)
        {
            // um triângulo entra em todas as partes que dominam algum vértice (decalque na divisa não corta)
            for (int k = 0; k < 3; k++)
            {
                int part = Mathf.Clamp((int)(uv2[t[i + k]].x * 32f), 0, 16);
                if (!lists.TryGetValue(part, out var l)) lists[part] = l = new List<int>();
                if (l.Count >= 3 && l[l.Count - 3] == t[i] && l[l.Count - 2] == t[i + 1]) continue;
                l.Add(t[i]); l.Add(t[i + 1]); l.Add(t[i + 2]);
            }
        }
        foreach (var kv in lists) trisByPart[kv.Key] = kv.Value.ToArray();
        return true;
    }

    public static bool Available => LoadBody();

    /// Recorta a pele em volta de c (espaço local da parte) e projeta a textura no plano (t, b).
    /// size = metade da largura (x) e da altura (y) do decalque.
    static Mesh BuildPatch(DummyPart part, Vector3 c, Vector3 n, Vector3 t, Vector2 half, Vector2 uvOff, float lift)
    {
        if (!LoadBody()) return null;
        if (!trisByPart.TryGetValue(Anatomy.OrderIndex(part.partId), out var tris)) return null;
        var pos0 = part.def.pos;
        var b = Vector3.Cross(n, t).normalized;
        float rad = Mathf.Max(half.x, half.y) * 1.45f;
        float r2 = rad * rad;
        var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var idx = new List<int>();
        var map = new Dictionary<int, int>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            bool near = false, outside = true;
            for (int k = 0; k < 3; k++)
            {
                var p = bodyV[tris[i + k]] - pos0;
                if ((p - c).sqrMagnitude < r2) near = true;
                var d = p - c;
                float u = Vector3.Dot(d, t) / half.x, v = Vector3.Dot(d, b) / half.y;
                if (Mathf.Abs(u) < 1.05f && Mathf.Abs(v) < 1.05f) outside = false;
            }
            if (!near || outside) continue;
            // só a face voltada para o lado da ferida (não atravessa o membro)
            if (Vector3.Dot(bodyN[tris[i]], n) < 0.15f) continue;
            for (int k = 0; k < 3; k++)
            {
                int vi = tris[i + k];
                if (!map.TryGetValue(vi, out int ni))
                {
                    ni = verts.Count;
                    map[vi] = ni;
                    var p = bodyV[vi] - pos0;
                    var d = p - c;
                    verts.Add(p + bodyN[vi] * lift);
                    nrm.Add(bodyN[vi]);
                    uv.Add(new Vector2(Vector3.Dot(d, t) / half.x * 0.5f + 0.5f + uvOff.x, Vector3.Dot(d, b) / half.y * 0.5f + 0.5f + uvOff.y));
                }
                idx.Add(ni);
            }
        }
        if (idx.Count == 0) return null;
        var m = new Mesh { name = "ferida" };
        m.SetVertices(verts); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(idx, 0);
        m.RecalculateBounds();
        return m;
    }

    static Material MakeMat(Texture2D tex, float gloss)
    {
        if (shader == null) shader = Resources.Load<Shader>("Shaders/BG_Wound");
        var m = new Material(shader);
        m.SetTexture("_MainTex", tex);
        m.SetFloat("_Gloss", gloss);
        return m;
    }

    /// Cria o visual da ferida na parte; devolve false se não houver modelo real (usa o visual antigo).
    public static bool Create(DummyPart part, Wound w)
    {
        if (!part.realModel || !LoadBody()) return false;
        var n = w.localNormal.normalized;
        // eixo "comprido" do decalque: aleatório no plano tangente (cortes); para baixo nos demais
        var down = Vector3.ProjectOnPlane(Vector3.down, n);
        if (down.sqrMagnitude < 1e-4f) down = Vector3.ProjectOnPlane(Vector3.forward, n);
        down.Normalize();
        var t = Vector3.Cross(down, n).normalized;
        string kind = w.kind;
        Vector2 half;
        float gloss = 0.75f;
        switch (kind)
        {
            case "saída": half = Vector2.one * U.RandRange(0.022f, 0.032f); break;
            case "corte": half = new Vector2(U.RandRange(0.04f, 0.06f), 0.012f); break;
            case "machado": half = new Vector2(U.RandRange(0.05f, 0.07f), 0.02f); break;
            case "perfuração": half = new Vector2(0.014f, 0.007f); break;
            case "queimadura": half = Vector2.one * U.RandRange(0.04f, 0.06f); gloss = 0.35f; break;
            case "contusão": half = Vector2.one * U.RandRange(0.035f, 0.05f); gloss = 0.2f; break;
            case "estilhaço": half = Vector2.one * U.RandRange(0.009f, 0.014f); break;
            case "flecha": half = Vector2.one * 0.016f; break;
            default: half = Vector2.one * 0.014f; break;     // bala
        }
        if (kind == "corte" || kind == "machado" || kind == "perfuração")
        {
            var rot = U.Axis(n, U.RandRange(-1.2f, 1.2f) + Mathf.PI * 0.5f);
            t = rot * t;
        }
        var tex = Tex(kind);
        var mesh = BuildPatch(part, w.localPos, n, t, half, Vector2.zero, 0.0012f);
        if (mesh == null) return false;
        var go = Prims.Visual("ferida_" + kind, mesh, MakeMat(tex, gloss), part.transform, false);
        w.visual = go;
        // filete de sangue que escorre (as feridas que sangram)
        if (kind != "contusão" && kind != "queimadura" && kind != "toco")
        {
            float len = U.RandRange(0.07f, 0.14f) * (kind == "saída" || kind == "machado" ? 1.4f : 1f);
            var tt = Vector3.Cross(down, n).normalized;
            var hc = new Vector2(U.RandRange(0.006f, 0.01f) * (half.x > 0.03f ? 1.6f : 1f), len * 0.5f);
            var tm = BuildPatch(part, w.localPos + down * (len * 0.5f - half.y * 0.3f), n, tt, hc, Vector2.zero, 0.0016f);
            if (tm != null)
            {
                // o plano tem uv.y crescendo para baixo: inverte (1 na ferida, 0 na ponta, como a textura)
                var uvs = new List<Vector2>(); tm.GetUVs(0, uvs);
                for (int i = 0; i < uvs.Count; i++) uvs[i] = new Vector2(uvs[i].x, 1f - uvs[i].y);
                tm.SetUVs(0, uvs);
                var mat = MakeMat(trickles[U.RandInt(0, VARIANTS - 1)], 0.8f);
                mat.SetFloat("_Grow", 0.05f);
                Prims.Visual("filete", tm, mat, part.transform, false);
                w.trickle = mat;
            }
        }
        return true;
    }

    /// O filete cresce conforme o sangue escorre (chamado pela fisiologia).
    public static void Grow(Wound w, float dt, float rate)
    {
        if (w.trickle == null) return;
        w.trickleGrow = Mathf.Min(1f, w.trickleGrow + dt * Mathf.Clamp(rate * 0.06f, 0.004f, 0.35f));
        w.trickle.SetFloat("_Grow", 0.05f + w.trickleGrow);
    }

    // ------------------------------------------------------------------ texturas procedurais

    static Texture2D Tex(string kind)
    {
        if (!texs.TryGetValue(kind, out var arr))
        {
            arr = new Texture2D[VARIANTS];
            for (int i = 0; i < VARIANTS; i++) arr[i] = Make(kind, 1000 + i * 37 + kind.Length * 7);
            texs[kind] = arr;
        }
        if (trickles == null)
        {
            trickles = new Texture2D[VARIANTS];
            for (int i = 0; i < VARIANTS; i++) trickles[i] = Trickle(200 + i * 13);
        }
        return arr[U.RandInt(0, VARIANTS - 1)];
    }

    static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

    static Texture2D Finish(Color[] px, int s, int h = 0)
    {
        if (h == 0) h = s;
        var t = new Texture2D(s, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        t.SetPixels(px);
        t.Apply(true);
        return t;
    }

    static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a < 1e-4f) return C(0, 0, 0, 0);
        var rgb = ((Vector4)over * over.a + (Vector4)under * under.a * (1f - over.a)) / a;
        return C(rgb.x, rgb.y, rgb.z, a);
    }

    static Texture2D Make(string kind, int seed)
    {
        const int S = 128;
        var px = new Color[S * S];
        float ox = seed * 0.731f, oy = seed * 1.37f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                float n1 = Mathf.PerlinNoise(ox + u * 3f, oy + v * 3f);
                float n2 = Mathf.PerlinNoise(ox + u * 11f + 5f, oy + v * 11f);
                float na = Mathf.PerlinNoise(ox + Mathf.Cos(ang) * 1.6f, oy + Mathf.Sin(ang) * 1.6f);
                Color c = C(0, 0, 0, 0);
                switch (kind)
                {
                    case "saída":
                    {
                        // rasgo estrelado: carne viva, bordas laceradas, gordura
                        float star = 0.45f + 0.4f * Mathf.PerlinNoise(ox + Mathf.Cos(ang * 3f) * 2f, oy + Mathf.Sin(ang * 3f) * 2f) + 0.1f * n2;
                        float rn = r / star;
                        var bruise = C(0.32f, 0.08f, 0.12f, Mathf.Clamp01(1.15f - rn * 0.75f) * 0.55f);
                        c = Over(c, bruise);
                        if (rn < 1f)
                        {
                            var flesh = Color.Lerp(C(0.62f, 0.1f, 0.1f), C(0.3f, 0.01f, 0.02f), Mathf.Clamp01(1f - rn * 1.3f) + n2 * 0.3f);
                            if (n2 > 0.66f && rn > 0.4f) flesh = Color.Lerp(flesh, C(0.85f, 0.72f, 0.45f), 0.7f);   // gordura
                            if (rn < 0.3f) flesh = Color.Lerp(flesh, C(0.08f, 0f, 0f), 1f - rn / 0.3f);
                            c = Over(c, C(flesh.r, flesh.g, flesh.b, Mathf.Clamp01((1f - rn) * 8f)));
                        }
                        break;
                    }
                    case "corte":
                    case "machado":
                    case "perfuração":
                    {
                        // fenda: largura máxima no meio, pontas finas; lábios de carne, fundo escuro
                        bool chop = kind == "machado";
                        float along = Mathf.Abs(u);
                        float wprof = Mathf.Pow(Mathf.Clamp01(1f - along * along), chop ? 0.45f : 0.7f) * (0.55f + 0.25f * n1);
                        float dv = Mathf.Abs(v + (n1 - 0.5f) * 0.25f) / Mathf.Max(wprof, 1e-3f);
                        var smear = C(0.4f, 0.02f, 0.03f, Mathf.Clamp01(1.4f - dv * 0.6f) * 0.45f * Mathf.Clamp01(1.2f - along));
                        c = Over(c, smear);
                        if (dv < 1f)
                        {
                            var lip = Color.Lerp(C(0.7f, 0.18f, 0.16f), C(0.5f, 0.05f, 0.06f), dv);
                            if (chop && dv > 0.45f && dv < 0.75f && n2 > 0.45f) lip = Color.Lerp(lip, C(0.9f, 0.78f, 0.5f), 0.75f);   // gordura
                            float core = Mathf.Clamp01(1f - dv / (chop ? 0.5f : 0.4f));
                            var col = Color.Lerp(lip, C(0.1f, 0f, 0.005f), core);
                            if (chop && core > 0.6f && n2 > 0.58f) col = Color.Lerp(col, C(0.92f, 0.88f, 0.78f), 0.6f);  // osso
                            c = Over(c, C(col.r, col.g, col.b, Mathf.Clamp01((1f - dv) * 6f)));
                        }
                        break;
                    }
                    case "queimadura":
                    {
                        float rn = r / (0.6f + 0.35f * na + 0.15f * n1);
                        if (rn < 1.25f)
                        {
                            var raw = C(0.55f, 0.12f, 0.08f, Mathf.Clamp01((1.25f - rn) * 4f) * 0.85f);
                            c = Over(c, raw);
                        }
                        if (rn < 1f)
                        {
                            var ch = Color.Lerp(C(0.25f, 0.12f, 0.07f), C(0.03f, 0.02f, 0.015f), Mathf.Clamp01(1.2f - rn) + n2 * 0.4f);
                            if (n2 > 0.7f) ch = Color.Lerp(ch, C(0.85f, 0.75f, 0.55f), 0.6f);    // bolhas
                            c = Over(c, C(ch.r, ch.g, ch.b, Mathf.Clamp01((1f - rn) * 5f)));
                        }
                        break;
                    }
                    case "contusão":
                    {
                        // hematoma: roxo no centro, azulado e amarelado na borda, com escoriação
                        float rn = r / (0.65f + 0.3f * na);
                        float a = Mathf.Clamp01(1f - rn) * (0.55f + 0.35f * n1);
                        var bc = Color.Lerp(C(0.3f, 0.06f, 0.22f), C(0.45f, 0.4f, 0.2f), Mathf.Clamp01(rn * 1.2f - 0.2f));
                        c = Over(c, C(bc.r, bc.g, bc.b, a));
                        if (rn < 0.35f && n2 > 0.55f) c = Over(c, C(0.55f, 0.08f, 0.07f, 0.8f));
                        break;
                    }
                    default:
                    {
                        // bala/flecha/estilhaço: furo escuro, colar de abrasão, halo de hematoma
                        float rn = r * (1f + 0.25f * (na - 0.5f)) * (kind == "estilhaço" ? 1f + 0.5f * (n2 - 0.5f) : 1f);
                        c = Over(c, C(0.35f, 0.1f, 0.14f, Mathf.Clamp01(1f - rn) * 0.5f));
                        if (rn < 0.46f)
                        {
                            var ab = Color.Lerp(C(0.42f, 0.1f, 0.07f), C(0.28f, 0.04f, 0.03f), n2);
                            c = Over(c, C(ab.r, ab.g, ab.b, Mathf.Clamp01((0.46f - rn) * 25f)));
                        }
                        if (rn < 0.25f)
                            c = Over(c, C(0.05f, 0f, 0.005f, Mathf.Clamp01((0.25f - rn) * 30f)));
                        break;
                    }
                }
                // respingos em volta
                if (c.a < 0.3f && n2 > 0.74f && r < 0.95f) c = Over(c, C(0.35f, 0.01f, 0.02f, 0.7f));
                // borda do decalque sempre transparente
                c.a *= Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))) * 12f);
                px[y * S + x] = c;
            }
        return Finish(px, S);
    }

    /// Filete: linha sinuosa que afina para baixo, com gota na ponta (v = 1 é a ferida, 0 a ponta).
    static Texture2D Trickle(int seed)
    {
        const int W = 32, H = 128;
        var px = new Color[W * H];
        float o = seed * 0.913f;
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;              // 0 = ponta, 1 = ferida
            float cx = 0.5f + (Mathf.PerlinNoise(o, v * 3f) - 0.5f) * 0.5f;
            float wdt = Mathf.Lerp(0.07f, 0.2f, v) + (v < 0.06f ? 0.12f * (1f - v / 0.06f) : 0f);
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                float d = Mathf.Abs(u - cx) / wdt;
                float a = Mathf.Clamp01((1f - d) * 3f) * Mathf.Clamp01(v * 30f) * Mathf.Clamp01((1f - v) * 40f + 0.3f);
                var col = Color.Lerp(C(0.42f, 0.02f, 0.03f), C(0.22f, 0f, 0.01f), Mathf.Clamp01(1f - d));
                px[y * W + x] = C(col.r, col.g, col.b, a * 0.95f);
            }
        }
        return Finish(px, W, H);
    }
}
