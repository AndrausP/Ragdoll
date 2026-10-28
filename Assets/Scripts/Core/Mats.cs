using System.Collections.Generic;
using UnityEngine;

/// Materiais (equivalentes ao Game.mat / Game.emissive do Godot) sobre os shaders em Resources/Shaders.
public static class Mats
{
    static Shader _lit, _fade, _skin, _xray, _unlit;
    public static Shader Lit => _lit ? _lit : (_lit = Resources.Load<Shader>("Shaders/BG_Lit"));
    public static Shader Fade => _fade ? _fade : (_fade = Resources.Load<Shader>("Shaders/BG_LitFade"));
    public static Shader Skin => _skin ? _skin : (_skin = Resources.Load<Shader>("Shaders/BG_Skin"));
    public static Shader XRayS => _xray ? _xray : (_xray = Resources.Load<Shader>("Shaders/BG_XRay"));
    public static Shader UnlitS => _unlit ? _unlit : (_unlit = Resources.Load<Shader>("Shaders/BG_Unlit"));

    /// Game.mat(color, rough, metal)
    public static Material Mat(Color c, float rough = 0.8f, float metal = 0f)
    {
        var m = new Material(Lit);
        m.SetColor("_Color", c);
        m.SetFloat("_Glossiness", 1f - rough);
        m.SetFloat("_Metallic", metal);
        m.enableInstancing = true;
        return m;
    }

    public static Material FadeMat(Color c, float rough = 0.8f, float metal = 0f)
    {
        var m = new Material(Fade);
        m.SetColor("_Color", c);
        m.SetFloat("_Glossiness", 1f - rough);
        m.SetFloat("_Metallic", metal);
        return m;
    }

    /// Liga/desliga a transparência de um material Lit (troca para o shader Fade e volta)
    public static void SetTransparent(Material m, bool on)
    {
        var target = on ? Fade : Lit;
        if (m.shader != target)
            m.shader = target;
        m.renderQueue = on ? 3000 : -1;
    }

    public static void SetRough(Material m, float rough) => m.SetFloat("_Glossiness", 1f - rough);
    public static Color GetColor(Material m) => m.GetColor("_Color");
    public static void SetColor(Material m, Color c) => m.SetColor("_Color", c);

    /// Game.emissive: cor sem sombreamento, brilhante
    public static Material Emissive(Color c, float energy = 2f)
    {
        var m = new Material(UnlitS);
        var k = Mathf.Clamp(energy * 0.45f, 1f, 3f);
        m.SetColor("_Color", new Color(c.r * k, c.g * k, c.b * k, 1));
        m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 0); m.SetFloat("_ZWrite", 1);
        m.renderQueue = 2000;
        return m;
    }

    /// Transparente sem luz (normal ou aditivo), para partículas e névoa
    public static Material Unlit(Color c, Texture tex = null, bool additive = false)
    {
        var m = new Material(UnlitS);
        m.SetColor("_Color", c);
        if (tex) m.SetTexture("_MainTex", tex);
        m.SetFloat("_SrcBlend", 5);
        m.SetFloat("_DstBlend", additive ? 1 : 10);
        m.SetFloat("_ZWrite", 0);
        return m;
    }

    /// Raio-X (desenha por cima). prio ordena: ossos 1, órgãos 1, elipses 2, vasos 3
    public static Material XRay(Color c, int prio = 1, bool vertexColor = false)
    {
        var m = new Material(XRayS);
        m.SetColor("_Color", c);
        m.SetFloat("_UseVC", vertexColor ? 1 : 0);
        m.renderQueue = 4000 + prio;
        return m;
    }

    /// Superfície com textura PBR real (Resources/Textures/<id>_Color|NormalGL|Roughness) em projeção triplanar.
    /// Devolve null se a textura não existir.
    public static Material Pbr(string id, float perMeter, Color tint, float normal = 1f, float roughMul = 1f, float metal = 0f)
    {
        var c = Resources.Load<Texture2D>("Textures/" + id + "_Color");
        if (c == null) return null;
        var m = new Material(Resources.Load<Shader>("Shaders/BG_Pbr"));
        m.SetTexture("_MainTex", c);
        var n = Resources.Load<Texture2D>("Textures/" + id + "_NormalGL");
        if (n != null) m.SetTexture("_BumpMap", n);
        var r = Resources.Load<Texture2D>("Textures/" + id + "_Roughness");
        if (r != null) m.SetTexture("_RoughMap", r);
        m.SetColor("_Color", tint);
        m.SetFloat("_TriScale", perMeter);
        m.SetFloat("_NormalStrength", normal);
        m.SetFloat("_RoughMul", roughMul);
        m.SetFloat("_Metallic", metal);
        return m;
    }

    /// Material de "acabamento molhado" (vísceras)
    public static Material Wet(Color c)
    {
        var m = Mat(c, 0.12f);
        return m;
    }

    static Texture2D _white;
    public static Texture2D White
    {
        get
        {
            if (_white) return _white;
            _white = new Texture2D(2, 2);
            _white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            _white.Apply();
            return _white;
        }
    }

    /// Ponto suave (alfa radial), usado nas partículas
    static Texture2D _dot;
    public static Texture2D SoftDot
    {
        get
        {
            if (_dot) return _dot;
            _dot = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var px = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f;
                    px[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - d), 1.5f));
                }
            _dot.SetPixels(px);
            _dot.Apply();
            _dot.wrapMode = TextureWrapMode.Clamp;
            return _dot;
        }
    }
}

/// Malhas primitivas com as mesmas medidas das do Godot (BoxMesh, SphereMesh, CylinderMesh, CapsuleMesh, TorusMesh).
public static class Prims
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    static Mesh Cached(string key, System.Func<Mesh> make)
    {
        if (cache.TryGetValue(key, out var m) && m) return m;
        m = make();
        m.name = key;
        cache[key] = m;
        return m;
    }

    public static Mesh Box(Vector3 size) => Cached("box" + size.ToString("F4"), () =>
    {
        var h = size * 0.5f;
        var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        void Face(Vector3 n, Vector3 u, Vector3 v)
        {
            int b = verts.Count;
            Vector3 c = Vector3.Scale(n, h);
            Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
            verts.Add(c - du - dv); verts.Add(c + du - dv); verts.Add(c + du + dv); verts.Add(c - du + dv);
            for (int i = 0; i < 4; i++) nrm.Add(n);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            // anti-horário visto de fora (o Unity inverte o culling junto com o espelho da câmera)
            tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        Face(Vector3.right, new Vector3(0, 0, -1), Vector3.up);
        Face(Vector3.left, new Vector3(0, 0, 1), Vector3.up);
        Face(Vector3.up, Vector3.right, new Vector3(0, 0, -1));
        Face(Vector3.down, Vector3.right, new Vector3(0, 0, 1));
        Face(new Vector3(0, 0, 1), Vector3.right, Vector3.up);
        Face(new Vector3(0, 0, -1), Vector3.left, Vector3.up);
        return Build(verts, nrm, uv, tri);
    });

    /// SphereMesh(radius, height): elipsoide r x h/2 x r
    public static Mesh Sphere(float radius, float height, int segs = 16, int rings = 8) => Cached($"sph{radius:F4}_{height:F4}_{segs}_{rings}", () =>
    {
        var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float hy = height * 0.5f;
        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;
            float phi = v * Mathf.PI;
            for (int s = 0; s <= segs; s++)
            {
                float u = (float)s / segs;
                float th = u * U.TAU;
                var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                verts.Add(new Vector3(n.x * radius, n.y * hy, n.z * radius));
                nrm.Add(new Vector3(n.x / radius, n.y / hy, n.z / radius).normalized);
                uv.Add(new Vector2(u, 1 - v));
            }
        }
        int w = segs + 1;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segs; s++)
            {
                int a = r * w + s, b = a + 1, c = a + w, d = c + 1;
                tri.AddRange(new[] { a, b, c, b, d, c });
            }
        return Build(verts, nrm, uv, tri);
    });

    /// CylinderMesh(top, bottom, height), eixo Y, com tampas
    public static Mesh Cylinder(float top, float bottom, float height, int segs = 16) => Cached($"cyl{top:F4}_{bottom:F4}_{height:F4}_{segs}", () =>
    {
        var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float hy = height * 0.5f;
        float slope = (bottom - top) / Mathf.Max(height, 1e-5f);
        for (int s = 0; s <= segs; s++)
        {
            float th = (float)s / segs * U.TAU;
            float cx = Mathf.Cos(th), cz = Mathf.Sin(th);
            var n = new Vector3(cx, slope, cz).normalized;
            verts.Add(new Vector3(cx * top, hy, cz * top)); nrm.Add(n); uv.Add(new Vector2((float)s / segs, 1));
            verts.Add(new Vector3(cx * bottom, -hy, cz * bottom)); nrm.Add(n); uv.Add(new Vector2((float)s / segs, 0));
        }
        for (int s = 0; s < segs; s++)
        {
            int a = s * 2, b = a + 1, c = a + 2, d = a + 3;
            tri.AddRange(new[] { a, c, b, b, c, d });
        }
        void Cap(float y, float r, Vector3 n, bool up)
        {
            int ci = verts.Count;
            verts.Add(new Vector3(0, y, 0)); nrm.Add(n); uv.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= segs; s++)
            {
                float th = (float)s / segs * U.TAU;
                verts.Add(new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r)); nrm.Add(n); uv.Add(new Vector2(0.5f, 0.5f));
            }
            for (int s = 0; s < segs; s++)
            {
                if (up) tri.AddRange(new[] { ci, ci + s + 2, ci + s + 1 });
                else tri.AddRange(new[] { ci, ci + s + 1, ci + s + 2 });
            }
        }
        if (top > 0) Cap(hy, top, Vector3.up, true);
        if (bottom > 0) Cap(-hy, bottom, Vector3.down, false);
        return Build(verts, nrm, uv, tri);
    });

    /// CapsuleMesh(radius, height total), eixo Y
    public static Mesh Capsule(float radius, float height, int segs = 16, int rings = 6) => Cached($"cap{radius:F4}_{height:F4}_{segs}_{rings}", () =>
    {
        var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float half = Mathf.Max(0f, height * 0.5f - radius);
        int rows = rings * 2 + 2;
        for (int r = 0; r <= rows; r++)
        {
            float phi; float yoff;
            if (r <= rings) { phi = (float)r / rings * Mathf.PI * 0.5f; yoff = half; }
            else { phi = Mathf.PI * 0.5f + (float)(r - rings - 1) / rings * Mathf.PI * 0.5f; yoff = -half; }
            phi = Mathf.Clamp(phi, 0, Mathf.PI);
            for (int s = 0; s <= segs; s++)
            {
                float th = (float)s / segs * U.TAU;
                var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                verts.Add(n * radius + new Vector3(0, yoff, 0)); nrm.Add(n); uv.Add(new Vector2((float)s / segs, (float)r / rows));
            }
        }
        int w = segs + 1;
        for (int r = 0; r < rows; r++)
            for (int s = 0; s < segs; s++)
            {
                int a = r * w + s, b = a + 1, c = a + w, d = c + 1;
                tri.AddRange(new[] { a, b, c, b, d, c });
            }
        return Build(verts, nrm, uv, tri);
    });

    /// TorusMesh(inner, outer), no plano XZ
    public static Mesh Torus(float inner, float outer, int rings = 20, int sides = 10) => Cached($"tor{inner:F4}_{outer:F4}_{rings}", () =>
    {
        var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float R = (inner + outer) * 0.5f, rr = (outer - inner) * 0.5f;
        for (int i = 0; i <= rings; i++)
        {
            float a = (float)i / rings * U.TAU;
            var c = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            for (int j = 0; j <= sides; j++)
            {
                float b = (float)j / sides * U.TAU;
                var n = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                verts.Add(c * R + n * rr); nrm.Add(n); uv.Add(new Vector2((float)i / rings, (float)j / sides));
            }
        }
        int w = sides + 1;
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * w + j, b = a + 1, c = a + w, d = c + 1;
                tri.AddRange(new[] { a, b, c, b, d, c });
            }
        return Build(verts, nrm, uv, tri);
    });

    /// Quad no plano XZ virado para +Y (para decalques/poças)
    public static Mesh FloorQuad() => Cached("fquad", () =>
    {
        var verts = new List<Vector3> { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
        var nrm = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        var tri = new List<int> { 0, 2, 1, 0, 3, 2 };
        return Build(verts, nrm, uv, tri);
    });

    static Mesh Build(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
    {
        var m = new Mesh();
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();     // o shader PBR (mapa de normal) precisa
        return m;
    }

    /// Cria um objeto visível com malha e material
    public static GameObject Visual(string name, Mesh mesh, Material mat, Transform parent, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }
}
