using UnityEngine;

/// Mira configurável (estilo, cor, tamanho, espessura, abertura, contorno, opacidade, dinâmica) e sensibilidade/FOV.
/// As escolhas ficam salvas (PlayerPrefs). Menu na tecla O; desenhado pelo Hud na tela virtual 1600x900.
public static class Crosshair
{
    public static readonly string[] STYLES = { "Cruz", "Cruz + ponto", "Ponto", "Círculo", "Círculo + ponto", "T" };
    public static readonly string[] COLOR_NAMES = { "Branco", "Verde", "Ciano", "Amarelo", "Vermelho", "Magenta", "Laranja" };
    static readonly Color[] COLORS =
    {
        new Color(1, 1, 1), new Color(0.3f, 1f, 0.35f), new Color(0.3f, 0.95f, 1f), new Color(1f, 0.92f, 0.25f),
        new Color(1f, 0.25f, 0.25f), new Color(1f, 0.35f, 1f), new Color(1f, 0.6f, 0.15f),
    };

    public static int style = 0, color = 0;
    public static float length = 9f, thickness = 1.5f, gap = 5f, opacity = 0.9f, dot = 2.5f;
    public static bool outline = true, dynamic = true, hitmarker = true;
    public static float sensitivity = 1f, fov = 78f, volume = 1f;
    public static bool bodyV2 = true;     // corpo novo v3 (padrão); vale ao reiniciar o jogo
    public static bool menuOpen;

    static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        style = PlayerPrefs.GetInt("mira_estilo", style);
        color = PlayerPrefs.GetInt("mira_cor", color);
        length = PlayerPrefs.GetFloat("mira_tamanho", length);
        thickness = PlayerPrefs.GetFloat("mira_espessura", thickness);
        gap = PlayerPrefs.GetFloat("mira_abertura", gap);
        opacity = PlayerPrefs.GetFloat("mira_opacidade", opacity);
        dot = PlayerPrefs.GetFloat("mira_ponto", dot);
        outline = PlayerPrefs.GetInt("mira_contorno", outline ? 1 : 0) == 1;
        dynamic = PlayerPrefs.GetInt("mira_dinamica", dynamic ? 1 : 0) == 1;
        hitmarker = PlayerPrefs.GetInt("mira_hitmarker", hitmarker ? 1 : 0) == 1;
        sensitivity = PlayerPrefs.GetFloat("sensibilidade", sensitivity);
        fov = PlayerPrefs.GetFloat("fov", fov);
        volume = Mathf.Clamp01(PlayerPrefs.GetFloat("volume", volume));
        bodyV2 = PlayerPrefs.GetInt("corpo_v2", 1) == 1;
        ApplyVolume();
        style = Mathf.Clamp(style, 0, STYLES.Length - 1);
        color = Mathf.Clamp(color, 0, COLORS.Length - 1);
    }

    /// Volume geral; os modos de teste automático continuam mudos.
    public static void ApplyVolume()
    {
        if (!(Game.autotest || Game.physTest || Game.stressTest || Game.shotsDir != "")) AudioListener.volume = volume;
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("mira_estilo", style);
        PlayerPrefs.SetInt("mira_cor", color);
        PlayerPrefs.SetFloat("mira_tamanho", length);
        PlayerPrefs.SetFloat("mira_espessura", thickness);
        PlayerPrefs.SetFloat("mira_abertura", gap);
        PlayerPrefs.SetFloat("mira_opacidade", opacity);
        PlayerPrefs.SetFloat("mira_ponto", dot);
        PlayerPrefs.SetInt("mira_contorno", outline ? 1 : 0);
        PlayerPrefs.SetInt("mira_dinamica", dynamic ? 1 : 0);
        PlayerPrefs.SetInt("mira_hitmarker", hitmarker ? 1 : 0);
        PlayerPrefs.SetFloat("sensibilidade", sensitivity);
        PlayerPrefs.SetFloat("fov", fov);
        PlayerPrefs.SetFloat("volume", volume);
        PlayerPrefs.SetInt("corpo_v2", bodyV2 ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static Color Tint => COLORS[color];

    /// Desenha a mira em c. spread (0..1) abre as linhas na mira dinâmica.
    public static void Draw(Vector2 c, float spread, System.Action<Vector2, Vector2, Color, float> line, System.Action<Rect, Color> fill, float alpha = 1f)
    {
        var col = Tint; col.a = opacity * alpha;
        var sh = new Color(0, 0, 0, 0.65f * opacity * alpha);
        float g = gap + (dynamic ? spread * 26f : 0f);
        bool cross = style == 0 || style == 1 || style == 5;
        bool circle = style == 3 || style == 4;
        bool dotOn = style == 1 || style == 2 || style == 4;
        if (cross)
            foreach (var v in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                if (style == 5 && v == Vector2.down) continue;     // T: sem a linha de cima (y cresce para baixo)
                if (outline) line(c + v * (g - 1f), c + v * (g + length + 1f), sh, thickness + 2f);
                line(c + v * g, c + v * (g + length), col, thickness);
            }
        if (circle)
        {
            float r = Mathf.Max(4f, g + length * 0.6f);
            const int N = 28;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
                var p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                var p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                if (outline) line(p0, p1, sh, thickness + 2f);
                line(p0, p1, col, thickness);
            }
        }
        if (dotOn)
        {
            if (outline) fill(new Rect(c.x - dot * 0.5f - 1f, c.y - dot * 0.5f - 1f, dot + 2f, dot + 2f), sh);
            fill(new Rect(c.x - dot * 0.5f, c.y - dot * 0.5f, dot, dot), col);
        }
    }

    // ------------------------------------------------------------------ menu

    static GUIStyle sTitle, sLabel;

    static float Slider(string label, float v, float lo, float hi, string fmt = "F1")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, sLabel, GUILayout.Width(150));
        v = GUILayout.HorizontalSlider(v, lo, hi, GUILayout.Width(220));
        GUILayout.Label(v.ToString(fmt), sLabel, GUILayout.Width(50));
        GUILayout.EndHorizontal();
        return v;
    }

    static int Choice(string label, int v, string[] names)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, sLabel, GUILayout.Width(150));
        if (GUILayout.Button("◀", GUILayout.Width(30))) v = (v + names.Length - 1) % names.Length;
        GUILayout.Label(names[v], sLabel, GUILayout.Width(150));
        if (GUILayout.Button("▶", GUILayout.Width(30))) v = (v + 1) % names.Length;
        GUILayout.EndHorizontal();
        return v;
    }

    static bool Toggle(string label, bool v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, sLabel, GUILayout.Width(150));
        v = GUILayout.Toggle(v, v ? " ligado" : " desligado");
        GUILayout.EndHorizontal();
        return v;
    }

    /// Janela de configuração (dentro do OnGUI do Hud, já com a matriz da tela virtual).
    public static void Menu(float vw, float H, System.Action<Vector2, Vector2, Color, float> line, System.Action<Rect, Color> fill)
    {
        if (!menuOpen) return;
        if (sTitle == null)
        {
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            sLabel = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        }
        var r = new Rect(vw * 0.5f - 300, H * 0.5f - 350, 600, 700);
        fill(r, new Color(0.04f, 0.04f, 0.06f, 0.93f));
        GUILayout.BeginArea(new Rect(r.x + 20, r.y + 14, r.width - 40, r.height - 28));
        GUILayout.Label("CONFIGURAÇÕES DE MIRA", sTitle);
        GUILayout.Space(6);
        style = Choice("Estilo", style, STYLES);
        color = Choice("Cor", color, COLOR_NAMES);
        length = Slider("Tamanho", length, 2f, 30f, "F0");
        thickness = Slider("Espessura", thickness, 1f, 6f);
        gap = Slider("Abertura", gap, 0f, 20f, "F0");
        dot = Slider("Tamanho do ponto", dot, 1f, 8f);
        opacity = Slider("Opacidade", opacity, 0.2f, 1f, "F2");
        outline = Toggle("Contorno", outline);
        dynamic = Toggle("Mira dinâmica", dynamic);
        hitmarker = Toggle("Marcador de acerto", hitmarker);
        GUILayout.Space(10);
        GUILayout.Label("CÂMERA", sTitle);
        sensitivity = Slider("Sensibilidade", sensitivity, 0.2f, 3f, "F2");
        fov = Slider("Campo de visão", fov, 60f, 100f, "F0");
        GUILayout.Space(10);
        GUILayout.Label("CORPO", sTitle);
        bodyV2 = Toggle("Modelo novo v3 (vale ao reiniciar o jogo)", bodyV2);
        GUILayout.Space(10);
        GUILayout.Label("SOM", sTitle);
        volume = Slider("Volume geral", volume, 0f, 1f, "F2");
        ApplyVolume();
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Padrão", GUILayout.Width(120)))
        {
            style = 0; color = 0; length = 9f; thickness = 1.5f; gap = 5f; opacity = 0.9f; dot = 2.5f;
            outline = dynamic = hitmarker = true; sensitivity = 1f; fov = 78f; volume = 1f;
        }
        if (GUILayout.Button("Fechar (O)", GUILayout.Width(120))) ToggleMenu();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        // prévia sobre um fundo claro e um escuro
        var pv = new Rect(r.xMax - 170, r.y + 60, 140, 70);
        fill(pv, new Color(0.75f, 0.75f, 0.72f, 1f));
        fill(new Rect(pv.x, pv.yMax, pv.width, 70), new Color(0.1f, 0.1f, 0.12f, 1f));
        Draw(new Vector2(pv.center.x, pv.yMax), Mathf.PingPong(Time.unscaledTime * 0.5f, 1f) * 0.4f, line, fill);
        if (GUI.changed) Save();
    }

    public static void ToggleMenu()
    {
        menuOpen = !menuOpen;
        if (!Game.autotest && Game.shotsDir == "") InputX.Capture(!menuOpen);
        if (!menuOpen) Save();
    }
}
