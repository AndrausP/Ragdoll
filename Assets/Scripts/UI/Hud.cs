using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// Interface em estilo cartoon: mira, barra de armas, inventário (I), alvo (HP / parte / estado), log de eventos e painel médico.
/// Desenhada com IMGUI numa tela virtual de 1600x900 (a mesma do Godot), escalada para a janela.
public class Hud : MonoBehaviour
{
    public static bool inventoryOpen;

    float hitT; bool hitKill;
    Dummy focusDummy; DummyPart focusPart; float focusT;
    float toastT; string toastText = "";
    bool showPanel = true, showHelp = true;
    float panelAcc; string panelText = "";
    int weaponI;
    string ammoText = ""; Color ammoColor = Color.white; int ammoCur = -1, ammoMag; float reloadFrac = -1f;
    class LogLine { public string text; public float t; }
    readonly List<LogLine> log = new List<LogLine>();
    GUIStyle sText, sCenter, sRight, sBig, sPanel, sTitle, sSmall, sBadge, sCard, sNote;
    Texture2D white;
    const float W = 1600f, H = 900f;
    int hoverWeapon = -1;

    void Awake()
    {
        Game.EventLogged += OnEvent;
        white = Mats.White;
        Crosshair.Load();
    }
    void Start() { StartCoroutine(HudKit.BuildIcons()); }
    void OnDestroy() { Game.EventLogged -= OnEvent; }

    void Styles()
    {
        if (sText != null) return;
        sText = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true, fontStyle = FontStyle.Bold };
        sText.normal.textColor = HudKit.Cream;
        sCenter = new GUIStyle(sText) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        sRight = new GUIStyle(sText) { alignment = TextAnchor.UpperLeft, fontSize = 15 };
        sBig = new GUIStyle(sText) { alignment = TextAnchor.MiddleCenter, fontSize = 40, wordWrap = false };
        sPanel = new GUIStyle(sText) { fontSize = 14, fontStyle = FontStyle.Normal };
        sTitle = new GUIStyle(sText) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        sSmall = new GUIStyle(sText) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        sBadge = new GUIStyle(sText) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        sCard = new GUIStyle(sText) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        sNote = new GUIStyle(sText) { fontSize = 13, alignment = TextAnchor.UpperCenter, wordWrap = true };
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
        ammoCur = cur; ammoMag = mag; reloadFrac = reloading;
        if (mag <= 0) { ammoText = ""; return; }
        if (reloading >= 0f) { ammoText = "RECARREGANDO"; ammoColor = HudKit.Yellow; }
        else
        {
            ammoText = $"{cur} / {mag}";
            ammoColor = cur <= mag / 5 ? HudKit.Red : HudKit.Cream;
        }
    }

    public void Hitmarker(Dummy d, DummyPart p)
    {
        hitT = 0.18f; hitKill = d.dead;
        focusDummy = d; focusPart = p; focusT = 3f;
    }

    public void Toast(string t) { toastText = t; toastT = 2.5f; }

    public void ShowInventory(bool on)
    {
        inventoryOpen = on;
        if (!Game.autotest && Game.shotsDir == "" && !Game.cutTest && !Game.woundTest) InputX.Capture(!on);
    }

    void OnEvent(string text, Color c)
    {
        log.Add(new LogLine { text = $"<color=#{U.Hex(c)}>{Bb(text)}</color>", t = 9f });
        while (log.Count > 7) log.RemoveAt(0);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hitT -= dt; toastT -= dt;
        foreach (var l in log) l.t -= dt;
        if (InputX.Down(InputX.A.Panel)) showPanel = !showPanel;
        if (InputX.Down(InputX.A.Help)) showHelp = !showHelp;
        if (InputX.Down(InputX.A.Options)) Crosshair.ToggleMenu();
        if (InputX.Down(InputX.A.Inventory) && !Crosshair.menuOpen) ShowInventory(!inventoryOpen);
        if (inventoryOpen && InputX.Down(InputX.A.Escape)) ShowInventory(false);
        if (Game.player != null && !inventoryOpen)
        {
            var h = Game.player.AimRay(60f);
            if (h.hit && h.part != null) { focusPart = h.part; focusDummy = focusPart.dummy; focusT = 2f; }
        }
        focusT -= dt;
        if (focusDummy != null && !focusDummy) { focusDummy = null; focusPart = null; }
        panelAcc -= dt;
        if (panelAcc <= 0f && showPanel) { panelAcc = 0.2f; panelText = BuildAnatomy(); }
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

    void WeaponIcon(Rect r, string id, bool outline = true)
    {
        var tex = HudKit.Icon(id);
        if (tex == null) return;
        if (outline)
        {
            GUI.color = HudKit.Ink;
            foreach (var o in new[] { new Vector2(2.5f, 0), new Vector2(-2.5f, 0), new Vector2(0, 2.5f), new Vector2(0, -2.5f), new Vector2(2, 2), new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 2) })
                GUI.DrawTexture(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), tex, ScaleMode.ScaleToFit, true);
        }
        GUI.color = Color.white;
        GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
    }

    static readonly bool noHud = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--nohud") >= 0;

    void OnGUI()
    {
        if (noHud) return;
        Styles();
        float s = Screen.height / H;
        float vw = Screen.width / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        var c = new Vector2(vw * 0.5f, H * 0.5f);
        var pl = Game.player;

        // mira
        if (!inventoryOpen)
        {
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
                    HudKit.Bar(new Rect(c.x - 40, c.y + 26, 80, 12), pl.BowDraw, HudKit.Yellow);
                if (hitT > 0f && Crosshair.hitmarker)
                {
                    var hc = hitKill ? HudKit.Red : Color.white;
                    foreach (var v in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                        Line(c + v * 6, c + v * 14, hc, 3f);
                }
            }
        }

        if (inventoryOpen) { DrawInventory(vw); Crosshair.Menu(vw, H, Line, Fill); return; }
        DrawHelp();
        DrawModes(vw);
        DrawLog(vw);
        DrawHotbar(vw);
        DrawAmmo(vw);
        if (toastT > 0f)
        {
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastT));
            var tr = new Rect(vw * 0.5f - 330, H * 0.5f + 48, 660, 40);
            HudKit.Pill(tr, HudKit.Yellow);
            sCard.normal.textColor = HudKit.Ink;
            GUI.Label(tr, toastText, sCard);
            GUI.color = Color.white;
        }
        DrawTarget(vw);
        if (showPanel) DrawPanel();
        Crosshair.Menu(vw, H, Line, Fill);
    }

    // ------------------------------------------------------------ partes

    void DrawHelp()
    {
        if (!showHelp)
        {
            var pr = new Rect(14, 12, 150, 30);
            HudKit.Pill(pr, HudKit.Navy);
            sSmall.normal.textColor = HudKit.Cream; GUI.Label(pr, "H: ajuda  ·  I: inventário", sSmall);
            return;
        }
        var r = new Rect(14, 12, 800, 118);
        HudKit.Card(r, new Color(0.12f, 0.13f, 0.25f, 0.88f), 16);
        HudKit.Gloss(r, 16);
        string Y(string k) => $"<color=#ffd138>{k}</color>";
        string txt = $"{Y("WASD")} mover   {Y("Shift")} correr   {Y("Espaço")} pular   {Y("Ctrl")} agachar   {Y("Mouse")} mirar\n" +
                     $"{Y("Clique")} usar arma   {Y("Direito")} mirar / secundário   {Y("Q")} recarregar   {Y("1-9/0")} ou {Y("rodinha")} trocar arma\n" +
                     $"{Y("I")} inventário   {Y("E")} agarrar corpo   {Y("F")} novo boneco   {Y("R")} resetar   {Y("K")} limpar sangue\n" +
                     $"{Y("X")} raio-X   {Y("T")} câmera lenta   {Y("Tab")} painel médico   {Y("G")} armadilha   {Y("O")} mira   {Y("H")} esconder";
        sText.normal.textColor = HudKit.Cream;
        GUI.Label(new Rect(r.x + 16, r.y + 8, r.width - 28, r.height - 12), txt, sText);
    }

    void DrawModes(float vw)
    {
        var modes = new List<string>();
        if (Time.timeScale < 0.99f) modes.Add("CÂMERA LENTA");
        if (Game.xray) modes.Add("RAIO-X");
        if (modes.Count == 0) return;
        var r = new Rect(vw * 0.5f - 160, 12, 320, 36);
        HudKit.Pill(r, HudKit.Blue);
        sCard.normal.textColor = HudKit.Ink;
        GUI.Label(r, string.Join("  ·  ", modes), sCard);
    }

    void DrawLog(float vw)
    {
        float ly = 12;
        foreach (var l in log)
        {
            if (l.t <= 0f) continue;
            float a = Mathf.Clamp01(l.t / 2f);
            GUI.color = new Color(1, 1, 1, a);
            float hgt = sRight.CalcHeight(new GUIContent(l.text), 480) + 12;
            var r = new Rect(vw - 520, ly, 506, hgt);
            HudKit.Card(r, new Color(0.12f, 0.13f, 0.25f, 0.9f), 12, 2.5f, false);
            GUI.Label(new Rect(r.x + 12, r.y + 5, 484, hgt - 8), l.text, sRight);
            ly += hgt + 6;
            GUI.color = Color.white;
        }
    }

    void DrawHotbar(float vw)
    {
        var pl = Game.player;
        var cur = Weapons.LIST[weaponI];
        const float bw = 112, bh = 92, gap = 7;
        float x0 = vw * 0.5f - (10 * (bw + gap) - gap) * 0.5f;
        float y0 = H - 112;
        for (int slot = 1; slot <= 10; slot++)
        {
            var inSlot = new List<WeaponDef>();
            foreach (var w in Weapons.LIST) if (w.slot == slot) inSlot.Add(w);
            if (inSlot.Count == 0) continue;
            bool sel = cur.slot == slot;
            var shown = sel ? cur : (pl != null ? pl.LastInSlot(slot) : inSlot[0]);
            var col = HudKit.SlotColor(slot);
            var r = new Rect(x0 + (slot - 1) * (bw + gap), y0 - (sel ? 14f : 0f), bw, bh + (sel ? 6f : 0f));
            var fill = sel ? col : Color.Lerp(col, HudKit.Navy, 0.72f);
            HudKit.Card(r, new Color(fill.r, fill.g, fill.b, sel ? 1f : 0.92f), 14, sel ? 4f : 3f);
            if (sel) HudKit.Gloss(r, 14, 4);
            WeaponIcon(new Rect(r.x + 8, r.y + 12, r.width - 16, r.height - 38), shown.id);
            sSmall.normal.textColor = sel ? HudKit.Ink : HudKit.Cream;
            GUI.Label(new Rect(r.x + 2, r.yMax - 28, r.width - 4, 22), shown.name, sSmall);
            // número da tecla
            var nb = new Rect(r.x - 6, r.y - 8, 28, 28);
            HudKit.Card(nb, sel ? HudKit.Yellow : HudKit.Cream, 14, 2.5f, false);
            sBadge.normal.textColor = HudKit.Ink; GUI.Label(nb, (slot % 10).ToString(), sBadge);
            if (inSlot.Count > 1)
            {
                var cb = new Rect(r.xMax - 38, r.y - 6, 40, 22);
                HudKit.Pill(cb, HudKit.Cream, 2f);
                sSmall.normal.textColor = HudKit.Ink; GUI.Label(cb, $"{inSlot.IndexOf(shown) + 1}/{inSlot.Count}", sSmall);
            }
        }
        if (Weapons.HINTS.TryGetValue(cur.id, out var hint))
        {
            var hr = new Rect(vw * 0.5f - 430, H - 168, 860, 30);
            HudKit.Pill(hr, new Color(0.12f, 0.13f, 0.25f, 0.9f));
            sCard.normal.textColor = HudKit.Yellow; GUI.Label(hr, hint, sCard);
        }
    }

    void DrawAmmo(float vw)
    {
        if (ammoText == "") return;
        var r = new Rect(vw - 330, H - 250, 306, 100);
        HudKit.Card(r, new Color(0.12f, 0.13f, 0.25f, 0.92f), 18);
        HudKit.Gloss(r, 18);
        var cur = Weapons.LIST[weaponI];
        sSmall.normal.textColor = HudKit.Cream; GUI.Label(new Rect(r.x, r.y + 6, r.width, 18), cur.name.ToUpper(), sSmall);
        if (reloadFrac >= 0f)
        {
            sCard.normal.textColor = HudKit.Yellow; GUI.Label(new Rect(r.x, r.y + 28, r.width, 28), "RECARREGANDO", sCard);
            HudKit.Bar(new Rect(r.x + 20, r.y + 62, r.width - 40, 20), reloadFrac, HudKit.Yellow);
        }
        else
        {
            HudKit.Label(new Rect(r.x, r.y + 22, r.width, 52), ammoText, sBig, ammoColor, 2.5f);
            // balinhas
            int n = Mathf.Min(ammoMag, 24);
            if (ammoMag > 0)
            {
                float bwid = Mathf.Min(10f, (r.width - 40f) / Mathf.Max(n, 1) - 2f);
                float sx = r.x + (r.width - n * (bwid + 2f)) * 0.5f;
                int filled = ammoMag > 24 ? Mathf.RoundToInt(24f * ammoCur / ammoMag) : ammoCur;
                for (int i = 0; i < n; i++)
                {
                    var b = new Rect(sx + i * (bwid + 2f), r.yMax - 22, bwid, 12);
                    GUI.color = i < filled ? HudKit.Yellow : new Color(1, 1, 1, 0.18f);
                    GUI.DrawTexture(b, white);
                }
                GUI.color = Color.white;
            }
        }
    }

    void DrawTarget(float vw)
    {
        if (focusDummy == null || inventoryOpen || focusT <= 0f) return;
        float a = focusT < 0.5f ? Mathf.Clamp01(focusT * 2f) : 1f;
        GUI.color = new Color(1, 1, 1, a);
        var d = focusDummy;
        int hp = d.HealthPct();
        var r = new Rect(vw * 0.5f - 230, H - 300, 460, 100);
        HudKit.Card(r, new Color(0.12f, 0.13f, 0.25f, 0.92f), 16);
        sCard.normal.textColor = HudKit.Cream;
        GUI.Label(new Rect(r.x, r.y + 5, r.width, 24), $"{d.dummyName}   <color=#ffd138>{hp} HP</color>", sCard);
        HudKit.Bar(new Rect(r.x + 24, r.y + 32, r.width - 48, 18), hp / 100f, Color.Lerp(HudKit.Red, HudKit.Green, hp / 100f));
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
            string info = $"{p.def.name} - {p.wounds.Count} ferida(s)" + (cond.Count > 0 ? "  (" + string.Join(", ", cond) + ")" : "");
            sSmall.normal.textColor = HudKit.Cream; GUI.Label(new Rect(r.x, r.y + 54, r.width, 20), info, sSmall);
        }
        string col = d.state == Dummy.State.STANDING ? "#66ff80" : d.state == Dummy.State.DOWN ? "#ff9933" : d.state == Dummy.State.UNCONSCIOUS ? "#ffe64d" : "#ff4040";
        sCard.normal.textColor = HudKit.Cream;
        GUI.Label(new Rect(r.x, r.y + 72, r.width, 24), $"<color={col}>{d.StatusText()}</color>", sCard);
        GUI.color = Color.white;
    }

    void StatBar(ref float y, Rect panel, string label, float v, Color fill, string value)
    {
        sSmall.alignment = TextAnchor.MiddleLeft; sSmall.normal.textColor = HudKit.Cream;
        GUI.Label(new Rect(panel.x + 16, y, 110, 22), label, sSmall);
        HudKit.Bar(new Rect(panel.x + 118, y + 1, panel.width - 118 - 84, 20), v, fill);
        sSmall.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(panel.xMax - 84, y, 68, 22), value, sSmall);
        sSmall.alignment = TextAnchor.MiddleCenter;
        y += 27;
    }

    void DrawPanel()
    {
        var d = focusDummy;
        if (d == null) foreach (var x in Game.dummies) if (x) { d = x; break; }
        var r = new Rect(14, 140, 440, 600);
        HudKit.Card(r, new Color(0.12f, 0.13f, 0.25f, 0.9f), 18);
        HudKit.Gloss(r, 18);
        if (d == null)
        {
            sCard.normal.textColor = HudKit.Cream; GUI.Label(r, "Nenhum boneco. Aperte F.", sCard);
            return;
        }
        float y = r.y + 12;
        sTitle.normal.textColor = HudKit.Yellow;
        HudKit.Label(new Rect(r.x, y, r.width, 30), d.dummyName.ToUpper(), sTitle, HudKit.Yellow, 2f);
        y += 34;
        string stc = d.state == Dummy.State.DOWN ? "#ff9a30" : d.state == Dummy.State.UNCONSCIOUS ? "#ffe040" : d.state == Dummy.State.DEAD ? "#ff4a4a" : "#60ff80";
        sCard.normal.textColor = HudKit.Cream;
        GUI.Label(new Rect(r.x, y, r.width, 24), $"<color={stc}>{d.StatusText()}</color>   ·   HP {d.HealthPct()}   ·   {d.timeAlive:F0}s", sCard);
        y += 28;
        if (d.dead)
        {
            GUI.Label(new Rect(r.x + 10, y, r.width - 20, 22), $"<color=#ff6a6a>Causa da morte: {d.deathCause}</color>", sCard);
            y += 24;
        }
        float loss = 1f - d.blood / Anatomy.BLOOD_TOTAL;
        StatBar(ref y, r, "Sangue", d.blood / Anatomy.BLOOD_TOTAL, HudKit.Red, $"{(int)d.blood} ml");
        StatBar(ref y, r, "Consciência", d.consciousness, HudKit.Blue, $"{(int)(d.consciousness * 100)}%");
        StatBar(ref y, r, "Dor", Mathf.Clamp01(d.pain), HudKit.Orange, $"{(int)(d.pain * 100)}%");
        if (!d.dead) StatBar(ref y, r, "Medo", d.fear, HudKit.Purple, $"{(int)(d.fear * 100)}%");
        if (d.state == Dummy.State.STANDING && !d.dead)
            StatBar(ref y, r, "Equilíbrio", Mathf.Clamp01(d.balance), HudKit.Green, $"{(int)(Mathf.Max(d.balance, 0f) * 100)}%");
        // sinais vitais em "fichas"
        y += 2;
        float cw = (r.width - 32 - 12) / 3f;
        var chips = new[] { ("FC", $"{(int)d.heartRate}", HudKit.Pink), ("PA", $"{(int)d.systolic}/{(int)d.diastolic}", HudKit.Teal), ("SpO₂", $"{(int)d.spo2}%", HudKit.Blue) };
        for (int i = 0; i < 3; i++)
        {
            var cr = new Rect(r.x + 16 + i * (cw + 6), y, cw, 44);
            HudKit.Card(cr, Color.Lerp(chips[i].Item3, HudKit.Navy, 0.55f), 10, 2.5f, false);
            sSmall.normal.textColor = HudKit.Cream; GUI.Label(new Rect(cr.x, cr.y + 2, cr.width, 16), chips[i].Item1, sSmall);
            sCard.normal.textColor = HudKit.Cream; GUI.Label(new Rect(cr.x, cr.y + 17, cr.width, 24), chips[i].Item2, sCard);
        }
        y += 52;
        string shock = loss >= 0.4f ? "<color=#ff4a4a>CHOQUE GRAVE</color>" : loss >= 0.3f ? "<color=#ff7a3a>choque classe III</color>" : loss >= 0.15f ? "<color=#ffc03a>choque classe II</color>" : loss > 0.03f ? "choque classe I" : "";
        string extra = $"perdendo <color=#ff6a6a>{d.bleedRateNow:F1} ml/s</color>" + (shock != "" ? "   ·   " + shock : "");
        sNote.normal.textColor = HudKit.Cream; GUI.Label(new Rect(r.x + 8, y, r.width - 16, 20), extra, sNote);
        y += 22;
        float bf = d.BurnFraction();
        if (bf > 0.005f || d.OnFire())
        {
            GUI.Label(new Rect(r.x + 8, y, r.width - 16, 20), $"<color=#ffa040>Queimaduras: {(int)(bf * 100f)}%{(d.OnFire() ? "  — PEGANDO FOGO" : "")}</color>", sNote);
            y += 20;
        }
        var flags = new List<string>();
        if (d.cardiacArrest) flags.Add("<color=#ff4a4a>PARADA CARDÍACA</color>");
        if (d.apnea) flags.Add("<color=#ff4a4a>APNEIA</color>");
        if (d.paralysis != "") flags.Add($"<color=#ff9a50>{d.paralysis.ToUpper()}</color>");
        if (d.concussion > 0.3f) flags.Add("concussão");
        if (d.hypoxiaTime > 0.5f) flags.Add($"<color=#ff8a8a>hipóxia {d.hypoxiaTime:F0}/14s</color>");
        if (flags.Count > 0) { GUI.Label(new Rect(r.x + 8, y, r.width - 16, 20), string.Join("  ·  ", flags), sNote); y += 20; }
        string beh = "";
        if (!d.dead && d.state == Dummy.State.STANDING) beh = $"Fazendo: {d.mind}";
        else if (d.gettingUp) beh = "<color=#a0d0ff>Levantando…</color>";
        else if (!d.dead && d.state != Dummy.State.STANDING)
        {
            var act = new Dictionary<string, string> { { "reach", "pressionando a ferida" }, { "writhe", "se contorcendo de dor" }, { "fetal", "encolhido (posição fetal)" }, { "crawl", "rastejando" }, { "rest", "ofegante, quase parado" }, { "none", "—" } };
            beh = "Comportamento: " + (d.state == Dummy.State.DOWN ? (act.TryGetValue(d.action, out var aa) ? aa : d.action) : "inconsciente");
        }
        if (!d.dead && d.fallReason != "" && d.state != Dummy.State.STANDING) beh += $"   (caiu por: <color=#ffc080>{d.fallReason}</color>)";
        if (beh != "") { GUI.Label(new Rect(r.x + 8, y, r.width - 16, 36), beh, sNote); y += 38; }
        // anatomia
        y += 4;
        HudKit.Pill(new Rect(r.x + 16, y, r.width - 32, 24), HudKit.Pink);
        sSmall.normal.textColor = HudKit.Ink; GUI.Label(new Rect(r.x + 16, y, r.width - 32, 24), "ANATOMIA", sSmall);
        y += 30;
        sPanel.normal.textColor = HudKit.Cream;
        GUI.BeginGroup(new Rect(r.x + 14, y, r.width - 28, r.yMax - y - 10));
        GUI.Label(new Rect(0, 0, r.width - 28, 1000), panelText, sPanel);
        GUI.EndGroup();
    }

    static string Bar(float v, int w = 14)
    {
        int nn = Mathf.Clamp(Mathf.RoundToInt(v * w), 0, w);
        return new string('█', nn) + new string('░', w - nn);
    }

    string BuildAnatomy()
    {
        var d = focusDummy;
        if (d == null) foreach (var x in Game.dummies) if (x) { d = x; break; }
        if (d == null) return "";
        var sb = new System.Text.StringBuilder();
        int intact = 0;
        foreach (var kv in d.parts)
        {
            var p = kv.Value;
            if (!p || p.fragment) continue;
            var lines = p.ConditionLines();
            if (lines.Count == 0 && p != focusPart) { intact++; continue; }
            float hpf = Mathf.Clamp01(p.hp / p.maxHp);
            string nc = p == focusPart ? "#7affff" : "#ffd6c4";
            sb.Append($"<color={nc}><b>{p.def.name}</b></color> <color=#9a9ab8>{(int)(hpf * 100)}%</color>");
            if (lines.Count > 0) sb.Append("  " + Bb(string.Join(" · ", lines)));
            sb.Append("\n");
        }
        if (intact > 0) sb.Append($"<color=#9a9ab8>{intact} parte(s) intacta(s)</color>\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------ inventário

    void DrawInventory(float vw)
    {
        Fill(new Rect(0, 0, vw, H), new Color(0.03f, 0.03f, 0.1f, 0.68f));
        var title = new Rect(vw * 0.5f - 230, 26, 460, 64);
        HudKit.Card(title, HudKit.Yellow, 32, 4);
        HudKit.Gloss(title, 32, 4);
        sTitle.fontSize = 34; sTitle.normal.textColor = HudKit.Ink;
        GUI.Label(title, "INVENTÁRIO", sTitle);
        sTitle.fontSize = 22;
        var sub = new Rect(vw * 0.5f - 300, 94, 600, 24);
        sSmall.normal.textColor = HudKit.Cream; GUI.Label(sub, "Clique numa arma para pegar  ·  I ou Esc para fechar", sSmall);

        var list = Weapons.LIST;
        const int cols = 7;
        const float cw = 200, ch = 150, gx = 14, gy = 16;
        int rows = (list.Count + cols - 1) / cols;
        float x0 = vw * 0.5f - (cols * (cw + gx) - gx) * 0.5f;
        float y0 = 132;
        var mouse = Event.current.mousePosition;
        hoverWeapon = -1;
        for (int i = 0; i < list.Count; i++)
        {
            var w = list[i];
            int cx = i % cols, cy = i / cols;
            var r = new Rect(x0 + cx * (cw + gx), y0 + cy * (ch + gy), cw, ch);
            bool hover = r.Contains(mouse);
            bool sel = i == weaponI;
            if (hover) hoverWeapon = i;
            var col = HudKit.SlotColor(w.slot);
            var rr = hover ? new Rect(r.x - 4, r.y - 6, r.width + 8, r.height + 8) : r;
            var fill = Color.Lerp(col, HudKit.Navy, hover || sel ? 0.35f : 0.62f);
            HudKit.Card(rr, fill, 18, sel ? 5f : 3.5f);
            if (sel)
            {
                // moldura amarela da arma na mão
                GUI.color = HudKit.Yellow;
                GUI.DrawTexture(new Rect(rr.x + 2, rr.y + 2, rr.width - 4, rr.height - 4), white, ScaleMode.StretchToFill, true, 0f, HudKit.Yellow, new Vector4(4, 4, 4, 4), new Vector4(16, 16, 16, 16));
                GUI.color = Color.white;
            }
            HudKit.Gloss(rr, 18, 3.5f);
            WeaponIcon(new Rect(rr.x + 14, rr.y + 16, rr.width - 28, rr.height - 66), w.id);
            sCard.normal.textColor = HudKit.Cream;
            HudKit.Label(new Rect(rr.x, rr.yMax - 44, rr.width, 26), w.name, sCard, HudKit.Cream, 1.6f);
            // tecla e munição
            var nb = new Rect(rr.x + 8, rr.y + 8, 30, 30);
            HudKit.Card(nb, col, 15, 2.5f, false);
            sBadge.normal.textColor = HudKit.Ink; GUI.Label(nb, (w.slot % 10).ToString(), sBadge);
            string tag = w.mag > 0 && Game.player != null ? $"{Game.player.AmmoOf(w.id)}/{w.mag}" : (w.type == "melee" ? "corpo a corpo" : w.type == "bow" ? "flechas" : "");
            if (tag != "")
            {
                var tr = new Rect(rr.x + 10, rr.yMax - 22, rr.width - 20, 18);
                sSmall.normal.textColor = sel ? HudKit.Yellow : new Color(1, 1, 1, 0.75f); GUI.Label(tr, tag, sSmall);
            }
            if (sel)
            {
                var tg = new Rect(rr.xMax - 74, rr.y + 8, 66, 22);
                HudKit.Pill(tg, HudKit.Yellow, 2f);
                sSmall.normal.textColor = HudKit.Ink; GUI.Label(tg, "NA MÃO", sSmall);
            }
            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0 && Game.player != null)
            {
                Game.player.SelectWeapon(i);
                ShowInventory(false);
                Event.current.Use();
            }
        }
        // balão com a descrição da arma sob o mouse (ou a da mão)
        int hi = hoverWeapon >= 0 ? hoverWeapon : weaponI;
        float by = y0 + rows * (ch + gy) + 6;
        if (Weapons.HINTS.TryGetValue(list[hi].id, out var hint))
        {
            var br = new Rect(vw * 0.5f - 480, by, 960, 54);
            HudKit.Card(br, HudKit.Cream, 20, 3.5f);
            sCard.normal.textColor = HudKit.Ink;
            GUI.Label(new Rect(br.x + 14, br.y + 4, br.width - 28, br.height - 8), $"<b>{list[hi].name}</b>  —  {hint}", sCard);
        }
    }
}
