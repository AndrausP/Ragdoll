using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Peças do HUD em estilo cartoon (cantos redondos, contorno grosso, cores vivas) e os ícones das armas, que são
/// renderizados uma vez a partir das próprias malhas do jogo.
public static class HudKit
{
    public static readonly Color Ink = new Color(0.07f, 0.06f, 0.13f), Cream = new Color(1f, 0.96f, 0.84f), Navy = new Color(0.12f, 0.13f, 0.25f),
        Yellow = new Color(1f, 0.82f, 0.22f), Red = new Color(0.95f, 0.30f, 0.28f), Green = new Color(0.32f, 0.82f, 0.44f),
        Blue = new Color(0.36f, 0.66f, 0.98f), Purple = new Color(0.70f, 0.52f, 0.96f), Orange = new Color(1f, 0.60f, 0.22f),
        Teal = new Color(0.25f, 0.80f, 0.78f), Pink = new Color(1f, 0.52f, 0.72f), Brown = new Color(0.66f, 0.45f, 0.30f), Mint = new Color(0.55f, 0.92f, 0.76f);

    /// Cor da categoria (tecla) de arma
    public static Color SlotColor(int slot)
    {
        switch (slot)
        {
            case 1: return Blue; case 2: return Teal; case 3: return Orange; case 4: return Purple; case 5: return Green;
            case 6: return Red; case 7: return Brown; case 8: return Yellow; case 9: return Pink; default: return Mint;
        }
    }

    static Texture2D White => Texture2D.whiteTexture;

    static void Quad(Rect r, Color c, float radius)
    {
        radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(radius, radius, radius, radius));
    }

    /// Retângulo arredondado com contorno e sombra "de adesivo"
    public static void Card(Rect r, Color fill, float radius = 14f, float outline = 3f, bool shadow = true)
    {
        if (shadow) Quad(new Rect(r.x + 2f, r.y + 5f, r.width, r.height), new Color(0, 0, 0, 0.35f), radius);
        Quad(r, Ink, radius);
        Quad(new Rect(r.x + outline, r.y + outline, r.width - outline * 2f, r.height - outline * 2f), fill, Mathf.Max(0f, radius - outline));
    }

    /// Faixa mais clara no topo do cartão (brilho)
    public static void Gloss(Rect r, float radius = 14f, float outline = 3f)
    {
        Quad(new Rect(r.x + outline + 2f, r.y + outline + 2f, r.width - outline * 2f - 4f, Mathf.Min(14f, r.height * 0.3f)), new Color(1, 1, 1, 0.16f), radius * 0.6f);
    }

    /// Texto com contorno escuro (legível em qualquer fundo)
    public static void Label(Rect r, string text, GUIStyle st, Color color, float o = 2f)
    {
        string plain = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
        var c0 = st.normal.textColor;
        st.normal.textColor = Ink;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * o, r.y + Mathf.Sin(a) * o, r.width, r.height), plain, st);
        }
        st.normal.textColor = color;
        GUI.Label(r, text, st);
        st.normal.textColor = c0;
    }

    /// Barra de status arredondada (valor 0..1)
    public static void Bar(Rect r, float v, Color fill)
    {
        Quad(r, Ink, r.height * 0.5f);
        var inner = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
        Quad(inner, new Color(0.18f, 0.18f, 0.3f), inner.height * 0.5f);
        v = Mathf.Clamp01(v);
        if (v > 0.01f)
        {
            var f = new Rect(inner.x, inner.y, Mathf.Max(inner.height, inner.width * v), inner.height);
            Quad(f, fill, f.height * 0.5f);
            Quad(new Rect(f.x + 3f, f.y + 2f, f.width - 6f, f.height * 0.35f), new Color(1, 1, 1, 0.28f), 3f);
        }
    }

    public static void Pill(Rect r, Color fill, float outline = 2.5f) => Card(r, fill, r.height * 0.5f, outline, false);

    // ------------------------------------------------------------ ícones das armas

    static readonly Dictionary<string, RenderTexture> icons = new Dictionary<string, RenderTexture>();
    static readonly Dictionary<string, string> MODEL_OF = new Dictionary<string, string> { { "throwknife", "dagger" } };
    public static bool IconsReady { get; private set; }

    public static Texture Icon(string id) => icons.TryGetValue(id, out var t) ? t : null;

    /// Renderiza o ícone de cada arma (uma vez, sem som nem sombra). Chamado por Hud depois do primeiro quadro.
    public static IEnumerator BuildIcons()
    {
        yield return null;
        const int L = 31;
        var rig = new GameObject("icones"); rig.transform.position = new Vector3(0, -500f, 0);
        var camGo = new GameObject("cam"); camGo.transform.SetParent(rig.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = 1 << L; cam.enabled = false; cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
        var lgo = new GameObject("luz"); lgo.transform.SetParent(rig.transform, false);
        lgo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        var light = lgo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.25f; light.cullingMask = 1 << L; light.shadows = LightShadows.None;
        var oldAmb = RenderSettings.ambientLight; var oldMode = RenderSettings.ambientMode;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

        foreach (var w in Weapons.LIST)
        {
            var obj = Build(w.id, L);
            if (obj == null) continue;
            obj.transform.SetParent(rig.transform, false);
            // caixa no espaço do rig
            var b = Bounds(obj);
            var rt = new RenderTexture(320, 160, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            float aspect = rt.width / (float)rt.height;
            cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.12f;
            camGo.transform.position = b.center + new Vector3(0, 0, -10f);
            camGo.transform.rotation = Quaternion.identity;
            cam.Render();
            icons[w.id] = rt;
            Object.DestroyImmediate(obj);
        }
        cam.targetTexture = null;
        RenderSettings.ambientMode = oldMode; RenderSettings.ambientLight = oldAmb;
        Object.Destroy(rig);
        IconsReady = true;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = new Bounds(rs[0].bounds.center, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void SetLayer(GameObject go, int layer) { foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer; }

    static GameObject Build(string id, int layer)
    {
        var root = new GameObject("ic_" + id);
        string mid = MODEL_OF.TryGetValue(id, out var mo) ? mo : id;
        var sm = MeshStore.Weapon(mid);
        if (sm != null)
        {
            var go = new GameObject("m"); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = sm.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = MeshStore.WeaponMaterials(sm);
            var b = sm.mesh.bounds;
            // armas brancas (compridas em Y) ficam na diagonal; arcos e besta de lado
            if (b.size.y > b.size.x * 1.25f) go.transform.localRotation = Quaternion.Euler(0, 0, -42f);
            else if (id == "crossbow") go.transform.localRotation = Quaternion.Euler(62f, 0, 0);       // de cima, para aparecerem os braços da besta
            go.transform.localScale = Vector3.one;
        }
        else if (!Fallback(id, root.transform)) { Object.DestroyImmediate(root); return null; }
        SetLayer(root, layer);
        return root;
    }

    static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, float rough = 0.5f, float metal = 0f)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = Mats.Mat(c, rough, metal);
        return g;
    }

    /// Armas sem malha própria: formas simples
    static bool Fallback(string id, Transform p)
    {
        switch (id)
        {
            case "grenade":
                Prim(PrimitiveType.Sphere, p, Vector3.zero, new Vector3(0.9f, 1.05f, 0.9f), new Color(0.22f, 0.3f, 0.15f), 0.45f);
                Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.62f, 0), new Vector3(0.28f, 0.1f, 0.28f), new Color(0.5f, 0.5f, 0.5f), 0.3f, 0.7f);
                Prim(PrimitiveType.Cube, p, new Vector3(0.28f, 0.7f, 0), new Vector3(0.5f, 0.07f, 0.1f), new Color(0.6f, 0.6f, 0.6f), 0.3f, 0.7f);
                return true;
            case "c4":
                Prim(PrimitiveType.Cube, p, Vector3.zero, new Vector3(1.5f, 0.55f, 0.9f), new Color(0.42f, 0.4f, 0.3f), 0.6f);
                Prim(PrimitiveType.Cube, p, new Vector3(0.35f, 0.3f, 0), new Vector3(0.45f, 0.06f, 0.3f), new Color(0.05f, 0.05f, 0.05f), 0.4f);
                Prim(PrimitiveType.Sphere, p, new Vector3(-0.45f, 0.32f, 0), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.1f, 0.1f), 0.2f);
                return true;
            case "med":
                Prim(PrimitiveType.Cube, p, Vector3.zero, new Vector3(1.4f, 1.0f, 0.55f), new Color(0.92f, 0.92f, 0.92f), 0.6f);
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0, -0.29f), new Vector3(0.7f, 0.22f, 0.04f), new Color(0.85f, 0.12f, 0.12f), 0.4f);
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0, -0.29f), new Vector3(0.22f, 0.7f, 0.04f), new Color(0.85f, 0.12f, 0.12f), 0.4f);
                return true;
            case "flamer":
                Prim(PrimitiveType.Cylinder, p, new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0.55f, 0.5f), new Color(0.75f, 0.2f, 0.12f), 0.4f, 0.3f).transform.localRotation = Quaternion.Euler(0, 0, 90f);
                Prim(PrimitiveType.Cylinder, p, new Vector3(0.45f, 0.1f, 0), new Vector3(0.14f, 0.9f, 0.14f), new Color(0.12f, 0.12f, 0.14f), 0.4f, 0.6f).transform.localRotation = Quaternion.Euler(0, 0, 90f);
                Prim(PrimitiveType.Cube, p, new Vector3(0.1f, -0.35f, 0), new Vector3(0.18f, 0.5f, 0.18f), new Color(0.15f, 0.1f, 0.08f), 0.6f);
                return true;
        }
        return false;
    }
}
