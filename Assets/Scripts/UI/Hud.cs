using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// Interface: mira, barra de armas, alvo (HP / parte / estado), log de eventos e painel médico.
/// Desenhada com IMGUI numa tela virtual de 1600x900 (a mesma do Godot), escalada para a janela.
public class Hud : MonoBehaviour
{
    float hitT; bool hitKill;
    Dummy focusDummy; DummyPart focusPart; float focusT;
    float toastT; string toastText = "";
    bool showPanel = true, showHelp = true;
    float panelAcc; string panelText = "";
    int weaponI;
    string ammoText = ""; Color ammoColor = Color.white;
    class LogLine { public string text; public float t; }
    readonly List<LogLine> log = new List<LogLine>();
    GUIStyle sText, sCenter, sRight, sBig, sPanel;
    Texture2D white;
    const float W = 1600f, H = 900f;

    void Awake()
    {
        Game.EventLogged += OnEvent;
        white = Mats.White;
        Crosshair.Load();
    }
    void OnDestroy() { Game.EventLogged -= OnEvent; }

    void Styles()
    {
        if (sText != null) return;
        sText = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
        sText.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
        sCenter = new GUIStyle(sText) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        sRight = new GUIStyle(sText) { alignment = TextAnchor.UpperRight, fontSize = 14 };
        sBig = new GUIStyle(sText) { alignment = TextAnchor.MiddleRight, fontSize = 30, fontStyle = FontStyle.Bold };
        sPanel = new GUIStyle(sText) { fontSize = 13 };
    }

    /// BBCode do Godot -> rich text do Unity
    public static string Bb(string s)
    {
        s = s.Replace("[b]", "<b>").Replace("[/b]", "</b>").Replace("[i]", "<i>").Replace("[/i]", "</i>");
        s = Regex.Replace(s, @"\[color=(#[0-9a-fA-F]{6})\]", "<color=$1>");
        s = s.Replace("[/color]", "</color>").Replace("[right]", "").Replace("[/right]", "");
        return s;
    }

    public void SetWeapon(int i) { weaponI = i; }

    public void SetAmmo(int cur, int mag, float reloading = -1f)
    {
        if (mag <= 0) { ammoText = ""; return; }
        if (reloading >= 0f)
        {
            int n = (int)(reloading * 8f);
            ammoText = "RECARREGANDO " + new string('▮', n) + new string('▯', 8 - n);
            ammoColor = new Color(1f, 0.85f, 0.4f);
        }
        else
        {
            ammoText = $"{cur} / {mag}";
            ammoColor = cur <= mag / 5 ? new Color(1, 0.35f, 0.3f) : new Color(1, 1, 1, 0.92f);
        }
    }

    public void Hitmarker(Dummy d, DummyPart p)
    {
        hitT = 0.18f; hitKill = d.dead;
        focusDummy = d; focusPart = p; focusT = 3f;
    }

    public void Toast(string t) { toastText = t; toastT = 2.5f; }

    void OnEvent(string text, Color c)
    {
        log.Add(new LogLine { text = $"<color=#{U.Hex(c)}>{Bb(text)}</color>", t = 9f });
        while (log.Count > 9) log.RemoveAt(0);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hitT -= dt; toastT -= dt;
        foreach (var l in log) l.t -= dt;
        if (InputX.Down(InputX.A.Panel)) showPanel = !showPanel;
        if (InputX.Down(InputX.A.Help)) showHelp = !showHelp;
        if (InputX.Down(InputX.A.Options)) Crosshair.ToggleMenu();
        if (Game.player != null)
        {
            var h = Game.player.AimRay(60f);
            if (h.hit && h.part != null) { focusPart = h.part; focusDummy = focusPart.dummy; focusT = 2f; }
        }
        focusT -= dt;
        if (focusDummy != null && !focusDummy) { focusDummy = null; focusPart = null; }
        panelAcc -= dt;
        if (panelAcc <= 0f && showPanel) { panelAcc = 0.2f; panelText = BuildPanel(); }
    }

    void Shadowed(Rect r, string text, GUIStyle st)
    {
        var c = st.normal.textColor;
        st.normal.textColor = new Color(0, 0, 0, 0.85f);
        string plain = Regex.Replace(text, "<.*?>", "");
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), plain, st);
        st.normal.textColor = c;
        GUI.Label(r, text, st);
    }

    void Line(Vector2 a, Vector2 b, Color c, float w)
    {
        var d = b - a;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, a);
        GUI.color = c;
        GUI.DrawTexture(new Rect(a.x, a.y - w * 0.5f, d.magnitude, w), white);
        GUI.matrix = m;
        GUI.color = Color.white;
    }

    void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, white); GUI.color = Color.white; }

    void OnGUI()
    {
        Styles();
        float s = Screen.height / H;
        float vw = Screen.width / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        var c = new Vector2(vw * 0.5f, H * 0.5f);

        // mira
        var pl = Game.player;
        if (pl != null && pl.zoom)
        {
            Line(c + new Vector2(-400, 0), c + new Vector2(400, 0), new Color(0, 0, 0, 0.8f), 1.5f);
            Line(c + new Vector2(0, -300), c + new Vector2(0, 300), new Color(0, 0, 0, 0.8f), 1.5f);
        }
        else
        {
            // mirando pela arma (ADS) a mira some aos poucos: quem mira é a própria arma
            float ads = pl != null ? pl.Ads : 0f;
            if (ads < 0.85f) Crosshair.Draw(c, pl != null ? pl.SpreadNow : 0f, Line, Fill, 1f - ads);
            if (pl != null && pl.Weapon().id == "bow" && pl.BowDraw > 0f)
                Fill(new Rect(c.x - 20, c.y + 20, 40 * pl.BowDraw, 3), new Color(1, 0.9f, 0.4f));
            if (hitT > 0f && Crosshair.hitmarker)
            {
                var hc = hitKill ? new Color(1, 0.15f, 0.15f) : Color.white;
                foreach (var v in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                    Line(c + v * 6, c + v * 14, hc, 2.5f);
            }
        }

        // ajuda
        if (showHelp)
            Shadowed(new Rect(14, 10, 900, 80),
                "WASD mover · Shift correr · Espaço pular · Ctrl agachar · Mouse mirar · Direito: mirar pela arma\n1-9/0 ou rodinha: armas (repita a tecla para trocar dentro da categoria) · E (segurar): agarrar corpo/membro (clique = arremessar)\nF: novo boneco · R: resetar · K: limpar sangue · X: raio-X (órgãos e ossos) · T: câmera lenta · Q: recarregar\nTab: painel médico · O: configurar mira · H: esconder ajuda · Esc: soltar mouse", sText);

        // modos
        var modes = new List<string>();
        if (Time.timeScale < 0.99f) modes.Add("CÂMERA LENTA");
        if (Game.xray) modes.Add("RAIO-X");
        if (modes.Count > 0) Shadowed(new Rect(vw * 0.5f - 300, 12, 600, 28), "<color=#99e6ff>" + string.Join("  ·  ", modes) + "</color>", sCenter);

        // log
        float ly = 12;
        foreach (var l in log)
        {
            if (l.t <= 0f) continue;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(l.t / 2f));
            float hgt = sRight.CalcHeight(new GUIContent(l.text), 546);
            Shadowed(new Rect(vw - 560, ly, 546, hgt), l.text, sRight);
            ly += hgt + 2;
            GUI.color = Color.white;
        }

        // barra de armas: uma caixa por tecla; a categoria mostra a arma atual dela e quantas tem (repetir a tecla alterna)
        float bw = 96, bh = 48, gap = 4;
        float x0 = vw * 0.5f - (10 * (bw + gap) - gap) * 0.5f;
        var cur = Weapons.LIST[weaponI];
        for (int slot = 1; slot <= 10; slot++)
        {
            var inSlot = new List<WeaponDef>();
            foreach (var w in Weapons.LIST) if (w.slot == slot) inSlot.Add(w);
            if (inSlot.Count == 0) continue;
            var shown = cur.slot == slot ? cur : (pl != null ? pl.LastInSlot(slot) : inSlot[0]);
            var r = new Rect(x0 + (slot - 1) * (bw + gap), H - 62, bw, bh);
            Fill(r, cur.slot == slot ? new Color(0.15f, 0.55f, 0.25f, 0.9f) : new Color(0.08f, 0.08f, 0.1f, 0.7f));
            string more = inSlot.Count > 1 ? $"  <color=#9fd0a8>{inSlot.IndexOf(shown) + 1}/{inSlot.Count}</color>" : "";
            GUI.Label(r, $"{slot % 10}{more}\n{shown.name}", sCenter);
        }
        if (Weapons.HINTS.TryGetValue(Weapons.LIST[weaponI].id, out var hint))
            Shadowed(new Rect(vw * 0.5f - 400, H - 92, 800, 24), $"<color=#fff2b3>{hint}</color>", sCenter);
        if (ammoText != "")
        {
            sBig.normal.textColor = ammoColor;
            Shadowed(new Rect(vw - 520, H - 78, 490, 42), ammoText, sBig);
        }
        if (toastT > 0f)
        {
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastT));
            Shadowed(new Rect(vw * 0.5f - 400, H * 0.5f + 40, 800, 30), $"<size=18><color=#ffffcc>{toastText}</color></size>", sCenter);
            GUI.color = Color.white;
        }

        // alvo
        if (focusDummy != null)
        {
            float a = focusT < 0.5f ? Mathf.Clamp01(focusT * 2f) : 1f;
            GUI.color = new Color(1, 1, 1, a);
            var d = focusDummy;
            int hp = d.HealthPct();
            Shadowed(new Rect(vw * 0.5f - 200, H - 205, 400, 26), $"<size=20>{d.dummyName}  {hp} HP</size>", sCenter);
            Fill(new Rect(vw * 0.5f - 190, H - 176, 380, 12), new Color(0.1f, 0.1f, 0.12f, 0.8f * a));
            Fill(new Rect(vw * 0.5f - 190, H - 176, 380 * hp / 100f, 12), Color.Lerp(new Color(0.85f, 0.15f, 0.15f), new Color(0.2f, 0.85f, 0.3f), hp / 100f));
            if (focusPart)
            {
                var p = focusPart;
                var cond = new List<string>();
                if (p.cutHere) cond.Add("AMPUTADO");
                int fr = 0; foreach (var b in p.bones) if (b.fractured) fr++;
                if (fr > 0) cond.Add($"{fr} osso(s) fraturado(s)");
                int arts = 0; foreach (var v in p.vessels) if (v.cut && v.def.artery) arts++;
                if (arts > 0) cond.Add("artéria rompida");
                if (p.tourniquet) cond.Add("torniquete");
                string info = $"{p.def.name} - {p.wounds.Count}" + (cond.Count > 0 ? "  (" + string.Join(", ", cond) + ")" : "");
                Shadowed(new Rect(vw * 0.5f - 300, H - 160, 600, 22), info, sCenter);
            }
            string col = d.state == Dummy.State.STANDING ? "#66ff80" : d.state == Dummy.State.DOWN ? "#ff9933" : d.state == Dummy.State.UNCONSCIOUS ? "#ffe64d" : "#ff4040";
            Shadowed(new Rect(vw * 0.5f - 200, H - 138, 400, 22), $"<color={col}>{d.StatusText()}</color>", sCenter);
            GUI.color = Color.white;
        }

        // painel médico
        if (showPanel)
        {
            var r = new Rect(12, 150, 440, 710);
            Fill(r, new Color(0.03f, 0.03f, 0.05f, 0.72f));
            GUI.Label(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16), panelText, sPanel);
        }
        Crosshair.Menu(vw, H, Line, Fill);
    }

    static string Bar(float v, int w = 14)
    {
        int nn = Mathf.Clamp(Mathf.RoundToInt(v * w), 0, w);
        return new string('█', nn) + new string('░', w - nn);
    }

    string BuildPanel()
    {
        var d = focusDummy;
        if (d == null)
        {
            foreach (var x in Game.dummies) if (x) { d = x; break; }
            if (d == null) return "<b>PAINEL MÉDICO</b>\nNenhum boneco. Aperte F.";
        }
        float loss = 1f - d.blood / Anatomy.BLOOD_TOTAL;
        string shock = "—";
        if (loss >= 0.4f) shock = "<color=#ff3030>Classe IV (choque grave)</color>";
        else if (loss >= 0.3f) shock = "<color=#ff7030>Classe III</color>";
        else if (loss >= 0.15f) shock = "<color=#ffb030>Classe II</color>";
        else if (loss > 0.03f) shock = "Classe I";
        var sb = new System.Text.StringBuilder();
        sb.Append($"<b>PAINEL MÉDICO — {d.dummyName}</b>\n");
        string stc = d.state == Dummy.State.DOWN ? "#ff9a30" : d.state == Dummy.State.UNCONSCIOUS ? "#ffe040" : d.state == Dummy.State.DEAD ? "#ff3030" : "#60ff80";
        sb.Append($"Estado: <color={stc}><b>{d.StatusText()}</b></color>   HP {d.HealthPct()}   t={d.timeAlive:F0}s\n");
        if (d.dead) sb.Append($"<color=#ff5050>Causa da morte: {d.deathCause}</color>\n");
        sb.Append($"Sangue  {Bar(d.blood / 5000f)} {(int)d.blood}/5000 ml ({(int)(100f * d.blood / 5000f)}%)\n");
        sb.Append($"Perdendo <color=#ff6060>{d.bleedRateNow:F1} ml/s</color>   total perdido {(int)d.totalLost} ml   choque: {shock}\n");
        float bf = d.BurnFraction();
        if (bf > 0.005f || d.OnFire()) sb.Append($"<color=#ff9a40>Queimaduras: {(int)(bf * 100f)}% do corpo{(d.OnFire() ? "  — PEGANDO FOGO" : "")}</color>\n");
        sb.Append($"FC <b>{(int)d.heartRate}</b> bpm   PA <b>{(int)d.systolic}/{(int)d.diastolic}</b>   SpO₂ <b>{(int)d.spo2}%</b>   FR {(int)d.respRate} irpm\n");
        sb.Append($"Consciência {Bar(d.consciousness)} {(int)(d.consciousness * 100)}%\n");
        sb.Append($"Dor         {Bar(Mathf.Clamp01(d.pain))} {(int)(d.pain * 100)}%\n");
        if (!d.dead)
            sb.Append($"Medo        <color=#c0a0ff>{Bar(d.fear)}</color> {(int)(d.fear * 100)}%\n");
        if (d.state == Dummy.State.STANDING && !d.dead)
        {
            string bc = d.balance > 0.6f ? "#80ff90" : (d.balance > 0.3f ? "#ffd060" : "#ff6040");
            sb.Append($"Equilíbrio  <color={bc}>{Bar(Mathf.Clamp01(d.balance))}</color> {(int)(Mathf.Max(d.balance, 0f) * 100)}%   (máx {(int)(d.BalanceCap() * 100)}%)\n");
            sb.Append($"Fazendo: <i>{d.mind}</i>\n");
        }
        else if (d.gettingUp) sb.Append("<color=#a0d0ff>Levantando…</color>\n");
        else if (d.fallReason != "" && !d.dead) sb.Append($"Caiu por: <color=#ffc080>{d.fallReason}</color>\n");
        if (d.hypoxiaTime > 0.5f) sb.Append($"<color=#ff8080>Hipóxia cerebral: {d.hypoxiaTime:F1} s / 14 s</color>\n");
        var flags = new List<string>();
        if (d.cardiacArrest) flags.Add("<color=#ff3030>PARADA CARDÍACA</color>");
        if (d.apnea) flags.Add("<color=#ff3030>APNEIA</color>");
        if (d.paralysis != "") flags.Add($"<color=#ff9050>{d.paralysis.ToUpper()}</color>");
        if (d.concussion > 0.3f) flags.Add("concussão");
        if (flags.Count > 0) sb.Append(string.Join(" · ", flags) + "\n");
        if (!d.dead && d.state != Dummy.State.STANDING)
        {
            var act = new Dictionary<string, string> { { "reach", "pressionando ferida" }, { "writhe", "se contorcendo de dor" }, { "fetal", "encolhido (posição fetal)" }, { "crawl", "rastejando" }, { "rest", "ofegante, quase parado" }, { "none", "—" } };
            string a = d.state == Dummy.State.DOWN ? (act.TryGetValue(d.action, out var aa) ? aa : d.action) : "inconsciente";
            sb.Append($"Comportamento: <i>{a}</i>   torniquetes no bolso: {d.tourniquetsLeft}\n");
        }
        sb.Append("\n<b>ANATOMIA</b>\n");
        int intact = 0;
        foreach (var id in Anatomy.ORDER)
        {
            if (!d.parts.TryGetValue(id, out var p) || !p) continue;
            var lines = p.ConditionLines();
            if (lines.Count == 0 && p != focusPart) { intact++; continue; }
            float hpf = Mathf.Clamp01(p.hp / p.maxHp);
            string nc = p == focusPart ? "#80ffff" : "#ffd0c0";
            sb.Append($"<color={nc}>{p.def.name}</color> <color=#808080>{(int)(hpf * 100)}%</color>");
            if (lines.Count > 0) sb.Append(": " + Bb(string.Join(" · ", lines)));
            sb.Append("\n");
        }
        if (intact > 0) sb.Append($"<color=#808080>{intact} parte(s) intacta(s)</color>\n");
        return sb.ToString();
    }
}
