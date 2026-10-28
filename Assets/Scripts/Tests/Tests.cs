using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Teste automático (executável com -autotest): mesmo roteiro do autotest.gd.
public class AutoTest : MonoBehaviour
{
    float perfMax, perfSum; int perfN, spikes;

    void Update()
    {
        float t = Time.unscaledDeltaTime;
        perfMax = Mathf.Max(perfMax, t); perfSum += t; perfN++;
        if (t > 0.05f) spikes++;
    }

    static void Log(string s) => Debug.Log(s);

    void Report(Dummy d, string label)
    {
        if (!d) { Log($"  {label}: (removido)"); return; }
        int nw = 0; foreach (var p in d.parts.Values) nw += p.wounds.Count;
        Log($"  {label} [{d.dummyName}] estado={d.StatusText()} HP={d.HealthPct()} sangue={(int)d.blood} perda={d.bleedRateNow:F1}ml/s FC={(int)d.heartRate} PA={(int)d.systolic} SpO2={(int)d.spo2} consc={d.consciousness:F2} dor={d.pain:F2} medo={d.fear:F2} fazendo={d.mind} acao={d.action} feridas={nw} {(d.dead ? "MORTO: " + d.deathCause : "")}");
    }

    RayHit HitPoint(DummyPart part, Vector3 local, out Vector3 dir)
    {
        var target = part.Xform.Apply(local);
        var from = target + new Vector3(0, 0.25f, 3f);
        dir = (target - from).normalized;
        return Phys.Ray(from, target + dir * 0.5f, Game.L_DUMMY);
    }

    void Gun(string id, DummyPart part, Vector3 local)
    {
        var w = Weapons.ById(id);
        var target = part.Xform.Apply(local);
        var from = target + new Vector3(0, 0.25f, 3f);
        for (int i = 0; i < w.pellets; i++)
        {
            var d = ((target - from).normalized + new Vector3(U.Randfn(0, w.spread), U.Randfn(0, w.spread), 0)).normalized;
            Weapons.FireRay(from, d, w.energy, w.prof, null);
        }
    }

    void Melee(Prof prof, float energy, DummyPart part, Vector3 local)
    {
        var h = HitPoint(part, local, out var dir);
        if (h.hit && h.part != null) h.part.dummy.ApplyPenetration(h.part, h.position, dir, energy, prof, h.normal);
        else Log("  (golpe errou)");
    }

    IEnumerator Start()
    {
        Log("=== AUTOTESTE INICIADO ===");
        yield return new WaitForSeconds(1f);
        var ds = Game.dummies;
        Dummy d0 = ds[0], d1 = ds[1], d2 = ds[2], d3 = ds[3];
        Log("-- 1) Pistola na coxa esquerda (artéria femoral) do #1");
        Gun("pistol", d0.parts["thigh_l"], new Vector3(-0.035f, 0.05f, 0.04f));
        Gun("pistol", d0.parts["thigh_l"], new Vector3(-0.03f, 0f, 0.04f));
        yield return new WaitForSeconds(0.3f);
        Report(d0, "logo após");
        Log("-- 2) Sniper na cabeça do #2");
        Gun("sniper", d1.parts["head"], new Vector3(0, 0.02f, 0.1f));
        yield return new WaitForSeconds(0.2f);
        Report(d1, "após sniper");
        Log("-- 3) Machado no braço esquerdo do #3 (3 golpes)");
        for (int i = 0; i < 3; i++) { Melee(Weapons.P_AXE, 320f, d2.parts["upper_arm_l"], new Vector3(0, 0, 0.05f)); yield return new WaitForSeconds(0.4f); }
        Report(d2, "após machado");
        Log("-- 4) Flecha no abdômen do #3");
        var tgt = d2.parts["abdomen"].transform.position;
        var a = new GameObject("flecha").AddComponent<Arrow>();
        a.Launch(tgt + new Vector3(0, 0.3f, 4), (tgt - (tgt + new Vector3(0, 0.3f, 4))).normalized * 70f, 200f, null);
        yield return new WaitForSeconds(0.5f);
        Log($"   flecha cravada? {a && a.stuck}");
        yield return new WaitForSeconds(7f);
        Log("-- após 8s:");
        Report(d0, "#1"); Report(d2, "#3");
        Log("-- 5) Faca: corte na garganta de um boneco novo");
        var d4 = Game.main.SpawnDummy(new Vector3(-6, 0.005f, 2), Quaternion.identity);
        yield return new WaitForSeconds(0.3f);
        Melee(Weapons.P_KNIFE_SLASH, 100f, d4.parts["neck"], new Vector3(0, 0, 0.05f));
        Melee(Weapons.P_KNIFE_STAB, 140f, d4.parts["abdomen"], new Vector3(-0.07f, 0.05f, 0.09f));
        yield return new WaitForSeconds(0.3f);
        Report(d4, "após faca");
        Log("-- 6) Escopeta de perto na canela direita do boneco da plataforma");
        for (int i = 0; i < 3; i++) Gun("shotgun", d3.parts["shin_r"], new Vector3(0, 0, 0.05f));
        yield return new WaitForSeconds(0.3f);
        Report(d3, "após escopeta");
        Log("-- 7) C4 grudada no peito de outro boneco novo e detonada");
        var d5 = Game.main.SpawnDummy(new Vector3(6, 0.005f, 2), Quaternion.identity);
        yield return new WaitForSeconds(0.3f);
        var h = HitPoint(d5.parts["chest"], new Vector3(0, 0, 0.1f), out _);
        Game.player.PlaceC4(h);
        yield return new WaitForSeconds(0.3f);
        Game.player.DetonateC4();
        yield return new WaitForSeconds(0.4f);
        Report(d5, "após C4");
        var sev = new List<string>();
        foreach (var p in d5.parts.Values) if (p.cutHere || p.destroyed) sev.Add(p.partId + (p.destroyed ? "(destruída)" : ""));
        Log("   partes arrancadas: " + string.Join(", ", sev));
        Log("-- 8) Granada perto do #1");
        var g = new GameObject("granada").AddComponent<Grenade>();
        g.transform.position = d0.parts["pelvis"].transform.position + new Vector3(0.6f, 0.3f, 0.5f);
        g.fuse = 0.3f;
        yield return new WaitForSeconds(1f);
        Report(d0, "#1 após granada");
        Log("-- 9) Tiros de fuzil no peito de um boneco (coração/pulmão)");
        var d6 = Game.main.SpawnDummy(new Vector3(-12, 0.005f, 0), Quaternion.identity);
        yield return new WaitForSeconds(0.3f);
        Gun("rifle", d6.parts["chest"], new Vector3(0.09f, 0.02f, 0.1f));
        yield return new WaitForSeconds(0.1f);
        Gun("rifle", d6.parts["chest"], new Vector3(0.04f, -0.02f, 0.1f));
        yield return new WaitForSeconds(0.2f);
        Report(d6, "após fuzil");
        Log("-- 10) Torniquete do jogador na coxa de um boneco com femoral rompida");
        var d7 = Game.main.SpawnDummy(new Vector3(12, 0.005f, 0), Quaternion.identity);
        yield return new WaitForSeconds(0.3f);
        d7.parts["thigh_r"].vessels[0].cut = true;
        Gun("pistol", d7.parts["thigh_r"], new Vector3(0.035f, 0.05f, 0.04f));
        yield return new WaitForSeconds(1f);
        float before = d7.bleedRateNow;
        d7.ApplyTourniquet(d7.parts["thigh_r"]);
        yield return new WaitForSeconds(1f);
        Log($"   sangramento antes={before:F1} depois={d7.bleedRateNow:F1} ml/s");
        Log("-- aguardando 20s de simulação...");
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(5f);
            Log($" t+{(i + 1) * 5}s");
            foreach (var d in Game.dummies) Report(d, "");
        }
        var b = Game.blood;
        Log($"== SANGUE: gotas criadas={b.totalDropsSpawned} ativas={b.ActiveDrops} respingos={b.totalSplats} poças={b.PoolCount} volume_em_poças={(int)b.PoolVolume()} ml gibs={b.GibCount}");
        Log($"== PERF: quadro médio {perfSum / Mathf.Max(1, perfN) * 1000f:F2} ms, pior {perfMax * 1000f:F2} ms, quadros >50ms: {spikes} de {perfN}");
        Log("=== AUTOTESTE FIM ===");
        Application.Quit();
    }
}

/// Estabilidade física do ragdoll (-phystest): vibração, chão, juntas abertas, levantar, direção do joelho.
public class PhysTest : MonoBehaviour
{
    int fails;
    void Check(bool ok, string what) { Debug.Log((ok ? "  OK    " : "  FALHA ") + what); if (!ok) fails++; }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        var ds = Game.dummies;
        var a = ds[0];
        a.Collapse();
        float restT = -1f, tt = 0f;
        while (tt < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            tt += 0.1f;
            if (restT < 0f && tt > 0.4f && Metrics(a).vmax < 0.15f) restT = tt;
        }
        Debug.Log($"[{a.dummyName}] tempo até repouso: {(restT < 0f ? "não repousou em 5s" : restT.ToString("F1") + "s")}");
        Rest(a, "queda simples (5s)");
        var b = ds[1];
        b.Collapse();
        yield return new WaitForSeconds(1.5f);
        string[] ids = { "chest", "abdomen", "thigh_l", "upper_arm_r", "head" };
        for (int i = 0; i < 40; i++)
        {
            var p = b.parts[ids[i % 5]];
            if (p && !p.destroyed)
            {
                var from = p.transform.position + new Vector3(0, 0.6f, 0.2f);
                Weapons.FireRay(from, (p.transform.position - from).normalized, 110f, Weapons.P_PISTOL, null);
            }
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(4f);
        Rest(b, "após 40 tiros (4s)");
        // joelho dobra para trás (sinal das dobradiças)
        var shin = b.parts["shin_l"]; var thigh = b.parts["thigh_l"];
        var rel = Quaternion.Inverse(thigh.transform.rotation) * shin.transform.rotation;
        float kx = Mathf.DeltaAngle(0, rel.eulerAngles.x);
        Check(kx > -12f, $"joelho dobra para o lado certo (rotação X do joelho = {kx:F0}°)");
        var c = ds[2];
        c.Collapse();
        foreach (var p in c.ValidParts()) p.rb.AddForce(new Vector3(0, 2f, -9f) * p.mass, ForceMode.Impulse);
        yield return new WaitForSeconds(4f);
        Rest(c, "arremesso na parede (4s)");
        float t = 0; bool stood = false;
        while (t < 16f)
        {
            yield return new WaitForSeconds(0.5f);
            t += 0.5f;
            if (a.state == Dummy.State.STANDING && !a.gettingUp) { stood = true; break; }
        }
        Check(stood, $"levantou depois da queda ({t:F1}s)");
        if (stood) Check(a.parts["pelvis"].transform.position.y > 0.8f, $"pelve na altura certa em pé (y={a.parts["pelvis"].transform.position.y:F2})");
        Debug.Log($"== PHYSTEST: {fails} falha(s)");
        Application.Quit();
    }

    struct M { public float vmax, wmax, energy, depth, gap; public string gid; }

    /// Métricas de estabilidade: velocidades, energia cinética, penetração no chão, junta mais aberta.
    static M Metrics(Dummy d)
    {
        var m = new M { gid = "" };
        foreach (var p in d.ValidParts())
        {
            float v = p.rb.linearVelocity.magnitude, w = p.rb.angularVelocity.magnitude;
            m.vmax = Mathf.Max(m.vmax, v); m.wmax = Mathf.Max(m.wmax, w);
            m.energy += 0.5f * p.mass * v * v;
            m.depth = Mathf.Max(m.depth, -p.transform.position.y);
            if (p.parentPart && !p.severed && p.def.joint != null)
            {
                var anc = p.def.joint.anchor;
                float g = Vector3.Distance(p.parentPart.Xform.Apply(anc - p.parentPart.def.pos), p.Xform.Apply(anc - p.def.pos));
                if (g > m.gap) { m.gap = g; m.gid = p.partId; }
            }
        }
        return m;
    }

    void Rest(Dummy d, string label)
    {
        var mt = Metrics(d);
        Debug.Log($"  métricas {label}: energia cinética {mt.energy:F2} J, profundidade máx abaixo do chão {mt.depth:F3} m");
        float vmax = 0, wmax = 0, gap = 0; int below = 0; string gid = "";
        foreach (var p in d.ValidParts())
        {
            vmax = Mathf.Max(vmax, p.rb.linearVelocity.magnitude);
            wmax = Mathf.Max(wmax, p.rb.angularVelocity.magnitude);
            if (p.transform.position.y < -0.05f) below++;
            if (p.parentPart && !p.severed && p.def.joint != null)
            {
                var anc = p.def.joint.anchor;
                var onPar = p.parentPart.Xform.Apply(anc - p.parentPart.def.pos);
                var onMe = p.Xform.Apply(anc - p.def.pos);
                float g = Vector3.Distance(onPar, onMe);
                if (g > gap) { gap = g; gid = p.partId; }
            }
        }
        Debug.Log($"[{d.dummyName}] {label}: vel máx {vmax:F2} m/s, ang máx {wmax:F2} rad/s, abaixo do chão {below}, junta aberta {gap:F3} m ({gid}), estado {d.StatusText()}");
        Check(vmax < 0.6f || d.gettingUp || (d.state == Dummy.State.DOWN && d.muscleLevel > 0.1f), label + ": sem vibração/deriva");
        Check(below == 0, label + ": nada abaixo do chão");
        Check(gap < 0.05f, label + ": juntas fechadas");
    }
}

/// Estresse com renderização (-stress): 10 bonecos, tiros e explosão; mede FPS.
public class StressTest : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < 6; i++) Game.main.SpawnDummy(new Vector3(-5 + i * 2f, 0.005f, -3f), Quaternion.identity);
        yield return new WaitForSeconds(0.5f);
        foreach (var d in new List<Dummy>(Game.dummies))
            for (int k = 0; k < 3; k++)
            {
                var p = d.parts["abdomen"];
                Weapons.FireRay(p.transform.position + new Vector3(0, 0, 0.6f), new Vector3(0, 0, -1), 900f, Weapons.P_SNIPER, null);
            }
        Explosions.Explode(new Vector3(0, 0.3f, -4.5f), 1f, 5f, 40, "Granada");
        float t = 0, fpsMin = 1e9f, fpsSum = 0; int n = 0;
        while (t < 8f)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            if (t < 1f) continue;
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            fpsMin = Mathf.Min(fpsMin, fps); fpsSum += fps; n++;
        }
        Debug.Log($"== STRESS: {Game.dummies.Count} bonecos, FPS médio {fpsSum / n:F0} (mín {fpsMin:F0})");
        Application.Quit();
    }
}

/// Capturas de tela para conferência (-shots=<pasta>)
public class Shots : MonoBehaviour
{
    Camera cam;

    IEnumerator Shot(string name, Vector3 from, Vector3 at)
    {
        cam.transform.position = from;
        // a câmera de captura também é espelhada: olha com o -Z do Godot
        cam.transform.rotation = Quaternion.LookRotation(at - from) ;
        for (int i = 0; i < 6; i++) yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Game.shotsDir, name + ".png"));
        yield return null;
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(Game.shotsDir);
        yield return new WaitForSeconds(1.5f);
        var go = new GameObject("shotcam");
        cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60; cam.nearClipPlane = 0.03f;
        go.AddComponent<MirrorCamera>();
        cam.depth = 10;
        var ds = Game.dummies;
        var c = ds[1].parts["chest"].transform.position;
        yield return Shot("1_bonecos", new Vector3(0, 1.5f, -0.5f), new Vector3(0, 1.0f, -5.5f));
        yield return Shot("2_perto", c + new Vector3(0, 0.1f, 1.8f), c + new Vector3(0, -0.2f, 0));
        Game.xray = true; foreach (var d in ds) d.SetXray(true);
        yield return Shot("3_raiox", c + new Vector3(0, 0.1f, 1.8f), c + new Vector3(0, -0.2f, 0));
        Game.xray = false; foreach (var d in ds) d.SetXray(false);
        // mirar: medo, mãos para cima
        ds[0].aimedAt = 3f; ds[0].fear = 0.8f;
        yield return new WaitForSeconds(1.5f);
        var c0 = ds[0].parts["chest"].transform.position;
        yield return Shot("4_medo", c0 + new Vector3(0.4f, 0.1f, 2f), c0);
        // ferido na perna: tenta fugir mancando e segura a perna
        var w = Weapons.ById("pistol");
        var sh = ds[2].parts["shin_l"].transform.position;
        Weapons.FireRay(sh + new Vector3(0, 0, 0.6f), new Vector3(0, 0, -1), w.energy, w.prof, null);
        yield return new WaitForSeconds(0.6f);
        var c2 = ds[2].parts["chest"].transform.position;
        yield return Shot("5_perna", c2 + new Vector3(1.6f, 0.2f, 1.2f), c2 + new Vector3(0, -0.4f, 0));
        yield return new WaitForSeconds(3f);
        c2 = ds[2].parts["chest"].transform.position;
        yield return Shot("6_fugindo", c2 + new Vector3(2.5f, 0.5f, 2.5f), c2);
        // ragdoll e gore
        ds[1].Collapse();
        ds[1].SeverPart(ds[1].parts["forearm_l"], Vector3.left);
        yield return new WaitForSeconds(2.5f);
        var c1 = ds[1].parts["pelvis"].transform.position;
        yield return Shot("7_ragdoll", c1 + new Vector3(1.6f, 1.2f, 1.2f), c1);
        // feridas de perto: tiro no peito (entrada e saída), cortes, marreta (hematoma)
        var d0 = ds[0];
        d0.aimedAt = 0f; d0.fear = 0f; d0.holdStill = true;
        yield return new WaitForSeconds(0.5f);
        var ch = d0.parts["chest"].transform;
        var fwd = ch.rotation * Vector3.forward;
        var rgt = ch.rotation * Vector3.right;
        var pw = Weapons.ById("pistol");
        Weapons.FireRay(ch.position + fwd * 0.6f + Vector3.up * 0.05f + rgt * 0.06f, -fwd, pw.energy, pw.prof, null);
        var ab = d0.parts["abdomen"];
        d0.ApplyPenetration(ab, ab.transform.position + fwd * 0.12f - rgt * 0.05f, -fwd, 140f, Weapons.P_KNIFE_SLASH, fwd);
        var ua = d0.parts["upper_arm_l"];
        d0.ApplyPenetration(ua, ua.transform.position + fwd * 0.06f, -fwd, 100f, Weapons.P_KNIFE_SLASH, fwd);
        var ur = d0.parts["upper_arm_r"];
        d0.ApplyPenetration(ur, ur.transform.position + fwd * 0.06f, -fwd, 200f, Weapons.P_HAMMER, fwd);
        yield return new WaitForSeconds(3f);
        var cc0 = d0.parts["chest"].transform.position;
        var f0 = d0.parts["chest"].transform.rotation * Vector3.forward;
        yield return Shot("8_feridas", cc0 + f0 * 0.9f + Vector3.down * 0.15f, cc0 + Vector3.down * 0.15f);
        yield return Shot("9_feridas_perto", cc0 + f0 * 0.42f + Vector3.up * 0.03f, cc0);
        // levantar: derruba e força a sequência (de bruços ou de costas, conforme caiu)
        d0.holdStill = false;
        foreach (var d in new[] { d0, ds[2] })
        {
            if (d.state == Dummy.State.STANDING) d.Collapse();
        }
        yield return new WaitForSeconds(2.5f);
        foreach (var d in new[] { d0, ds[2] }) d.StartGetUp();
        for (int i = 0; i < 6; i++)
        {
            var pp = d0.parts["pelvis"].transform.position;
            var side = Vector3.Cross(Vector3.up, d0.parts["chest"].transform.rotation * Vector3.up).normalized;
            if (side.magnitude < 0.5f) side = Vector3.right;
            yield return Shot("levantar_" + i, pp + side * 2.2f + Vector3.up * 0.6f, pp + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.55f);
        }
        yield return new WaitForSeconds(1f);        // fogo: jato de lança-chamas num boneco em pé + facas de arremesso noutro
        var d3 = ds[ds.Count - 1];
        var tgt = d3.parts["chest"].transform.position;
        var src = tgt + new Vector3(2.5f, 0.2f, 1.5f);
        for (float tt = 0; tt < 1.6f; tt += Time.deltaTime)
        {
            Flames.Spray(src, (tgt - src).normalized, Vector3.zero, Time.deltaTime);
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);
        var b3 = d3.parts["chest"].transform.position;
        yield return Shot("fogo_1", b3 + new Vector3(2.2f, 0.8f, 2.2f), b3);
        yield return new WaitForSeconds(2.5f);
        b3 = d3.parts["chest"].transform.position;
        yield return Shot("fogo_2", b3 + new Vector3(1.6f, 1f, 1.6f), b3);
        var dk = ds[1];
        foreach (var pid in new[] { "chest", "thigh_l", "abdomen" })
        {
            var pp2 = dk.parts[pid].transform.position;
            var fromK = pp2 + new Vector3(0, 0.6f, 2.5f);
            new GameObject("faca").AddComponent<Arrow>().Launch(fromK, (pp2 - fromK).normalized * 22f, 135f, null, Weapons.P_THROW_KNIFE, "dagger");
        }
        yield return new WaitForSeconds(1.2f);
        var bk = dk.parts["abdomen"].transform.position;
        yield return Shot("facas", bk + new Vector3(0.9f, 0.7f, 0.9f), bk);
        // viewmodel de cada arma (câmera do jogador)
        Destroy(cam.gameObject);
        var pl = Game.player;
        foreach (var wd in Weapons.LIST)
        {
            pl.SelectWeapon(Weapons.LIST.IndexOf(wd));
            yield return new WaitForSeconds(0.6f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Game.shotsDir, "vm_" + wd.id + ".png"));
            yield return null;
        }
        pl.SelectWeapon(Weapons.LIST.IndexOf(Weapons.ById("rifle")));
        pl.forceAds = true;
        yield return new WaitForSeconds(1f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Game.shotsDir, "vm_rifle_ads.png"));
        yield return null;
        pl.forceAds = false;
        Crosshair.menuOpen = true;
        yield return new WaitForSeconds(0.3f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Game.shotsDir, "menu_mira.png"));
        yield return null;
        Application.Quit();
    }
}

/// Agonia (--agonytest): um boneco leva tiros no abdômen e na coxa e agoniza por até 60 s; mede estabilidade da física
/// (velocidade, junta aberta) e conta espasmos e suspiros; com --shots-agony=<pasta> tira capturas laterais.
public class AgonyTest : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.6f);
        var d = Game.dummies[0];
        string dir = "";
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("--shots-agony=")) dir = a.Substring(14);
        Camera cam = null;
        if (dir != "")
        {
            Directory.CreateDirectory(dir);
            var go = new GameObject("agonycam"); cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55; cam.nearClipPlane = 0.03f; go.AddComponent<MirrorCamera>(); cam.depth = 10;
        }
        var pw = Weapons.ById("pistol");
        foreach (var (pid, loc) in new[] { ("abdomen", new Vector3(0.03f, 0f, 0.1f)), ("thigh_l", new Vector3(0.0f, 0.05f, 0.05f)), ("abdomen", new Vector3(-0.04f, 0.02f, 0.1f)) })
        {
            var part = d.parts[pid]; var tgt = part.Xform.Apply(loc); var from = tgt + new Vector3(0, 0.2f, 2.5f);
            Weapons.FireRay(from, (tgt - from).normalized, pw.energy, pw.prof, null);
            yield return new WaitForSeconds(0.15f);
        }
        // contorcer consciente (ação "writhe"), outro boneco: mede estabilidade da física por 8 s
        var w = Game.dummies[1];
        w.paralysis = "paraplegia";      // não consegue se levantar: fica no chão
        w.Collapse(); yield return new WaitForSeconds(1.2f);
        w.pain = 1.2f; w.DebugAction("writhe", "writhe_a", 9f);
        float wv = 0, wg = 0; var series = new List<float>();
        for (int i = 0; i < 80; i++)
        {
            yield return new WaitForSeconds(0.1f);
            float step = 0;
            foreach (var p in w.ValidParts())
            {
                step = Mathf.Max(step, p.rb.linearVelocity.magnitude);
                wv = Mathf.Max(wv, p.rb.linearVelocity.magnitude);
                if (p.parentPart && !p.severed && p.def.joint != null)
                {
                    var anc = p.def.joint.anchor;
                    wg = Mathf.Max(wg, Vector3.Distance(p.parentPart.Xform.Apply(anc - p.parentPart.def.pos), p.Xform.Apply(anc - p.def.pos)));
                }
            }
            series.Add(step);
            if (cam != null && (i == 15 || i == 40 || i == 65))
            {
                var c = w.parts["pelvis"].transform.position;
                cam.transform.position = c + new Vector3(1.6f, 1.0f, 1.3f); cam.transform.rotation = Quaternion.LookRotation(c - cam.transform.position);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"contorcer_{i}.png"));
            }
        }
        series.Sort(); int fr = 0; foreach (var p in w.ValidParts()) foreach (var b in p.bones) if (b.fractured) fr++;
        Debug.Log($"== CONTORCER: {(Game.legacyAgony ? "ANTIGA" : "NOVA")} vmax {wv:F1} m/s, p50 {series[series.Count / 2]:F1}, p95 {series[(int)(series.Count * 0.95f)]:F1}, junta aberta máx {wg:F3} m, ossos fraturados {fr}, estado {w.StatusText()}");
        d.blood = 3000f;      // hemorragia grave: começa já na fase em que espasmos e suspiros aparecem
        float t = 0, vmaxAll = 0, gapAll = 0, nextLog = 0; int shot = 0;
        float[] shotAt = { 3f, 10f, 18f, 26f, 36f, 46f };
        while (t < 60f && !d.dead)
        {
            yield return new WaitForSeconds(0.1f); t += 0.1f;
            float vmax = 0, wmax = 0, gap = 0;
            foreach (var p in d.ValidParts())
            {
                vmax = Mathf.Max(vmax, p.rb.linearVelocity.magnitude); wmax = Mathf.Max(wmax, p.rb.angularVelocity.magnitude);
                if (p.parentPart && !p.severed && p.def.joint != null)
                {
                    var anc = p.def.joint.anchor;
                    gap = Mathf.Max(gap, Vector3.Distance(p.parentPart.Xform.Apply(anc - p.parentPart.def.pos), p.Xform.Apply(anc - p.def.pos)));
                }
            }
            vmaxAll = Mathf.Max(vmaxAll, vmax); gapAll = Mathf.Max(gapAll, gap);
            if (t >= nextLog)
            {
                nextLog += 2f;
                Debug.Log($"[agonia] t={t:F0}s {d.StatusText()} sangue={(int)d.blood} consc={d.consciousness:F2} {d.AgonyDebug} vmax={vmax:F1} wmax={wmax:F1} junta={gap:F3}");
            }
            if (cam != null && shot < shotAt.Length && t >= shotAt[shot])
            {
                var c = d.parts["pelvis"].transform.position;
                cam.transform.position = c + new Vector3(1.8f, 1.1f, 1.4f); cam.transform.rotation = Quaternion.LookRotation(c - cam.transform.position);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"agonia_{shot}.png"));
                shot++;
            }
        }
        if (d.dead) { Debug.Log($"[agonia] morreu aos {t:F1}s: {d.deathCause}"); for (int i = 0; i < 40; i++) yield return new WaitForSeconds(0.1f); }
        Debug.Log($"== AGONIA: duração {t:F1}s, espasmos {d.spasmCount}, suspiros {d.gaspCount}, vmax {vmaxAll:F1} m/s, junta aberta máx {gapAll:F3} m, morto={d.dead}");
        Application.Quit();
    }
}

/// Marcha (--gaittest --shots-gait=<pasta>): um boneco anda e corre em linha reta; capturas laterais de um ciclo e
/// medidas do tronco (inclinação, avanço do peito em relação à pelve, balanço vertical).
public class GaitTest : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.6f);
        string dir = "";
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("--shots-gait=")) dir = a.Substring(13);
        Camera cam = null;
        if (dir != "")
        {
            Directory.CreateDirectory(dir);
            var go = new GameObject("gaitcam"); cam = go.AddComponent<Camera>();
            cam.fieldOfView = 38; cam.nearClipPlane = 0.03f; go.AddComponent<MirrorCamera>(); cam.depth = 10;
        }
        var d = Game.main.SpawnDummy(new Vector3(3.5f, 0.005f, -6f), Quaternion.identity);
        d.holdStill = false;
        foreach (var (name, spd) in new[] { ("parado", 0f), ("andar", 1.3f), ("correr", 3.2f) })
        {
            d.DebugWalk(spd);
            yield return new WaitForSeconds(name == "parado" ? 1f : 2.2f);
            float lean = 0, lean2 = 0, fwd = 0, head = 0, bobMin = 9, bobMax = -9; int n = 0;
            int shot = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSeconds(0.06f);
                var pel = d.parts["pelvis"].transform; var ch = d.parts["chest"].transform; var hd = d.parts["head"].transform; var nk = d.parts["neck"].transform;
                var up = ch.rotation * Vector3.up;
                var fw = d.transform.rotation * Vector3.forward; fw = new Vector3(Mathf.Sin(0f), 0, 1f);
                float tilt = Mathf.Atan2(Vector3.Dot(up, fw), up.y) * Mathf.Rad2Deg;                       // + = tronco inclinado para a frente
                float tilt2 = Mathf.Atan2(Vector3.Dot(pel.rotation * Vector3.up, fw), (pel.rotation * Vector3.up).y) * Mathf.Rad2Deg;
                lean += tilt; lean2 += tilt2; n++;
                fwd += Vector3.Dot(ch.position - pel.position, fw);
                head += Vector3.Dot(hd.position - ch.position, fw);
                bobMin = Mathf.Min(bobMin, pel.position.y); bobMax = Mathf.Max(bobMax, pel.position.y);
                if (cam != null && name != "parado" && i % 3 == 0 && shot < 6)
                {
                    var c = pel.position;
                    cam.transform.position = new Vector3(c.x - 3.4f, 0.95f, c.z); cam.transform.rotation = Quaternion.LookRotation(new Vector3(c.x, 0.95f, c.z) - cam.transform.position);
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{name}_{shot}.png"));
                    shot++;
                }
            }
            Debug.Log($"[marcha] {name}: v={d.GaitSpeed:F2} m/s, tronco inclinado {lean / n:F1} graus, pelve {lean2 / n:F1} graus, peito-pelve {fwd / n * 100:F1} cm à frente, cabeça-peito {head / n * 100:F1} cm, pelve sobe/desce {(bobMax - bobMin) * 100:F1} cm");
        }
        Debug.Log("== MARCHA fim");
        Application.Quit();
    }
}

/// Armadilhas (--hazardtest --shots-hazard=<pasta>): coloca um boneco em cada máquina, aciona e mede o resultado
/// (partes cortadas/destruídas, ossos fraturados, sangue perdido); capturas antes e depois.
public class HazardTest : MonoBehaviour
{
    static void Teleport(Dummy d, Vector3 pivotFrom, Vector3 pivotTo, float yawDeg)
    {
        var rot = Quaternion.AngleAxis(yawDeg, Vector3.up);
        foreach (var p in d.ValidParts())
        {
            var np = pivotTo + rot * (p.rb.position - pivotFrom);
            p.rb.position = np; p.transform.position = np;
            var nr = rot * p.rb.rotation; p.rb.rotation = nr; p.transform.rotation = nr;
            p.rb.linearVelocity = Vector3.zero; p.rb.angularVelocity = Vector3.zero;
        }
    }

    /// Deita o boneco (caído e imóvel) com o ponto `anchorPart` em `target`, com o eixo pelve-peito ao longo de `along` (horizontal).
    static IEnumerator Lay(Dummy d, string anchorPart, Vector3 target, Vector3 along)
    {
        d.Collapse();
        yield return new WaitForSeconds(1.6f);
        var a = d.parts[anchorPart].rb.position;
        var axis = d.parts["pelvis"].rb.position - d.parts["chest"].rb.position; axis.y = 0;
        float yaw = axis.sqrMagnitude < 1e-4f ? 0f : Vector3.SignedAngle(axis, along, Vector3.up);
        Teleport(d, a, target, yaw);
    }

    static string Summary(Dummy d)
    {
        int cut = 0, gone = 0, fr = 0;
        var names = new List<string>();
        foreach (var kv in d.parts)
        {
            var p = kv.Value; if (!p) continue;
            if (p.destroyed) { gone++; names.Add(kv.Key + "(destruída)"); }
            else if (p.cutHere) { cut++; names.Add(kv.Key + "(cortada)"); }
            foreach (var b in p.bones) if (b.fractured) fr++;
        }
        return $"{d.StatusText()} sangue={(int)d.blood} perdido={(int)d.totalLost}ml ossos fraturados={fr} cortes={cut} destruídas={gone} [{string.Join(", ", names)}]";
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.8f);
        string dir = "";
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("--shots-hazard=")) dir = a.Substring(15);
        Camera cam = null;
        if (dir != "")
        {
            Directory.CreateDirectory(dir);
            var go = new GameObject("hazcam"); cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55; cam.nearClipPlane = 0.03f; go.AddComponent<MirrorCamera>(); cam.depth = 10;
        }
        float z = 24f;
        var plans = new (string name, Vector3 center, Vector3 camOff, System.Func<Dummy, IEnumerator> place, float wait)[]
        {
            ("guilhotina", new Vector3(-26f, 1.3f, z), new Vector3(3.2f, 1.3f, -2.6f), d => Lay(d, "neck", new Vector3(-26f, 1.25f, z + 0.02f), Vector3.forward), 5f),
            ("prensa", new Vector3(-18f, 0.6f, z), new Vector3(3.2f, 1.6f, -3.2f), d => Lay(d, "chest", new Vector3(-18f, 0.62f, z), Vector3.forward), 9f),
            ("serra", new Vector3(-10f, 1.1f, z), new Vector3(3.4f, 1.6f, -2.6f), d => Lay(d, "chest", new Vector3(-10f, 1.12f, z), Vector3.forward), 11f),
            ("martelo", new Vector3(10f, 1.3f, z), new Vector3(0f, 1.5f, -4.5f), d => Standing(d, new Vector3(8.4f, 0.005f, z), 90f), 7f),
            ("espinhos", new Vector3(18f, 0.4f, z), new Vector3(3.0f, 1.4f, -2.8f), d => Lay(d, "chest", new Vector3(18f, 0.2f, z), Vector3.forward), 6f),
            ("lanca-chamas", new Vector3(26f, 1.2f, z), new Vector3(3.2f, 1.6f, -2.2f), d => Standing(d, new Vector3(26f, 0.005f, z - 2.4f), 0f), 6f),
        };
        foreach (var pl in plans)
        {
            var d = Game.main.SpawnDummy(new Vector3(pl.center.x, 0.005f, z - 5f), Quaternion.identity);
            yield return new WaitForSeconds(0.3f);
            yield return pl.place(d);
            yield return new WaitForSeconds(0.5f);
            Hazard hz = null;
            foreach (var h in Hazard.All) if (Vector3.Distance(h.transform.position, new Vector3(pl.center.x, 0, z)) < 1.5f) hz = h;
            if (hz == null) { Debug.Log($"[armadilha] {pl.name}: NÃO ENCONTRADA"); continue; }
            if (cam != null) yield return Snap(cam, pl.center, pl.camOff, dir, pl.name + "_0");
            hz.Trigger();
            float t = 0; bool shot = false;
            while (t < pl.wait)
            {
                yield return new WaitForSeconds(0.25f); t += 0.25f;
                if (cam != null && !shot && t >= pl.wait * 0.5f) { shot = true; yield return Snap(cam, pl.center, pl.camOff, dir, pl.name + "_1"); }
            }
            if (cam != null) yield return Snap(cam, pl.center, pl.camOff, dir, pl.name + "_2");
            Debug.Log($"[armadilha] {pl.name}: golpes={hz.hitsDealt} | {Summary(d)}");
        }
        Debug.Log("== ARMADILHAS fim");
        Application.Quit();
    }

    static IEnumerator Standing(Dummy d, Vector3 pos, float yaw)
    {
        d.holdStill = true;
        d.PlaceStanding(pos, yaw);
        yield return new WaitForSeconds(0.3f);
    }

    static IEnumerator Snap(Camera cam, Vector3 center, Vector3 off, string dir, string name)
    {
        cam.transform.position = center + off; cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        yield return null;
    }
}

/// Corte em qualquer ponto (--cuttest --shots-cut=<pasta>): corta coxa, antebraço e canela de bonecos parados e
/// tira capturas antes e depois; registra o estado físico dos fragmentos.
public class CutTest : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.6f);
        string dir = "";
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("--shots-cut=")) dir = a.Substring(12);
        Camera cam = null;
        if (dir != "")
        {
            Directory.CreateDirectory(dir);
            var go = new GameObject("cutcam"); cam = go.AddComponent<Camera>();
            cam.fieldOfView = 45; cam.nearClipPlane = 0.03f; go.AddComponent<MirrorCamera>(); cam.depth = 10;
        }
        foreach (var d in Game.dummies) d.holdStill = true;
        var cases = new[]
        {
            (name: "coxa",     dummy: 0, part: "thigh_l",   local: new Vector3(0f, 0.02f, 0f),  ragged: 0f),
            (name: "antebraco", dummy: 1, part: "forearm_r", local: new Vector3(0f, 0.00f, 0f),  ragged: 0f),
            (name: "canela",   dummy: 2, part: "shin_l",    local: new Vector3(0f, 0.06f, 0f),  ragged: 1f),
        };
        foreach (var cs in cases)
        {
            var d = Game.dummies[cs.dummy];
            var pel = d.parts["pelvis"].transform.position + Vector3.up * 0.2f;
            if (cam != null) yield return Snap(cam, pel, new Vector3(1.4f, 0.5f, 2.6f), dir, cs.name + "_0antes");
            var p = d.parts[cs.part];
            bool ok = d.CutAt(p, p.Xform.Apply(cs.local), Vector3.right, cs.ragged);
            Debug.Log($"[corte] {cs.name}: ok={ok}");
            yield return new WaitForSeconds(0.12f);
            if (cam != null) yield return Snap(cam, d.parts["pelvis"].transform.position + Vector3.up * 0.2f, new Vector3(1.4f, 0.5f, 2.6f), dir, cs.name + "_1corte");
            yield return new WaitForSeconds(1.4f);
            if (cam != null) yield return Snap(cam, d.parts["pelvis"].transform.position, new Vector3(1.5f, 1.0f, 1.8f), dir, cs.name + "_2chao");
            if (cam != null)
            {
                DummyPart fr = null;
                foreach (var q in d.ValidParts()) if (q.fragment) fr = q;
                if (fr != null)
                {
                    cam.fieldOfView = 30;
                    var pc = p.Xform.Apply(fr.stumpLocal); var fc = fr.Xform.Apply(fr.stumpLocal);
                    yield return Snap(cam, pc, new Vector3(0.35f, 0.45f, 0.55f), dir, cs.name + "_3cotoProx");
                    yield return Snap(cam, fc, new Vector3(0.35f, 0.45f, 0.55f), dir, cs.name + "_4cotoDist");
                    yield return Snap(cam, pc, new Vector3(-0.5f, 0.2f, 0.35f), dir, cs.name + "_5cotoProxB");
                    cam.fieldOfView = 45;
                }
            }
            yield return new WaitForSeconds(1.5f);
            float vmax = 0f; int frags = 0;
            foreach (var q in d.ValidParts()) { vmax = Mathf.Max(vmax, q.rb.linearVelocity.magnitude); if (q.fragment) frags++; }
            Debug.Log($"[corte] {cs.name}: fragmentos={frags} vmax={vmax:F2} sangue={(int)d.blood} estado={d.StatusText()}");
        }
        Debug.Log("== CORTE fim");
        Application.Quit();
    }

    static IEnumerator Snap(Camera cam, Vector3 center, Vector3 off, string dir, string name)
    {
        cam.transform.position = center + off; cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        yield return null;
    }
}

/// Ferimentos de verdade (--woundtest --shots-wound=<pasta>): tiros no peito (amassado, saída, buraco), foice na canela,
/// machado no antebraço e marreta na coxa, com capturas de perto e do corpo inteiro.
public class WoundTest : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.6f);
        string dir = "";
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("--shots-wound=")) dir = a.Substring(14);
        Camera cam = null;
        if (dir != "")
        {
            Directory.CreateDirectory(dir);
            var go = new GameObject("woundcam"); cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40; cam.nearClipPlane = 0.03f; go.AddComponent<MirrorCamera>(); cam.depth = 10;
        }
        foreach (var d in Game.dummies) d.holdStill = true;

        if (cam != null)
            for (int k = 0; k < 3; k++)
                yield return Snap(cam, Game.dummies[k].parts["head"].transform.position + new Vector3(0f, -0.01f, 0f), new Vector3(0f, 0.02f, 0.62f), dir, "rosto_" + k, 24f);
        // ---- boneco 0: tiros no peito
        var d0 = Game.dummies[0];
        var ch = d0.parts["chest"];
        if (cam != null) yield return Snap(cam, ch.transform.position, new Vector3(0.5f, 0.1f, 1.1f), dir, "peito_0antes", 30f);
        var pistol = Weapons.ById("pistol");
        foreach (var off in new[] { new Vector3(0.04f, 0.05f, 0.11f), new Vector3(-0.05f, -0.02f, 0.11f), new Vector3(0.0f, 0.08f, 0.11f) })
        {
            var tgt = ch.Xform.Apply(off); var from = tgt + new Vector3(0, 0.1f, 2.5f);
            Weapons.FireRay(from, (tgt - from).normalized, pistol.energy, pistol.prof, null);
            yield return new WaitForSeconds(0.08f);
        }
        yield return new WaitForSeconds(0.05f);
        if (cam != null) yield return Snap(cam, ch.transform.position, new Vector3(0.5f, 0.1f, 1.1f), dir, "peito_1pistola", 30f);
        {
            var tgt = ch.Xform.Apply(new Vector3(-0.08f, -0.05f, 0.11f)); var from = tgt + new Vector3(0, 0.0f, 2.5f);
            Weapons.FireRay(from, (tgt - from).normalized, 900f, Weapons.P_SNIPER, null);
        }
        yield return new WaitForSeconds(0.05f);
        if (cam != null) yield return Snap(cam, ch.transform.position, new Vector3(0.5f, 0.1f, 1.1f), dir, "peito_2sniper", 30f);
        if (cam != null) yield return Snap(cam, ch.transform.position, new Vector3(-0.6f, 0.1f, -1.1f), dir, "peito_3costas", 30f);

        // ---- boneco 0 (continua): tiros na perna, sangue deve escorrer em filete (não tingir a canela toda)
        {
            var sh0 = d0.parts["shin_r"];
            foreach (var off in new[] { new Vector3(0.0f, 0.06f, 0.06f), new Vector3(0.01f, -0.02f, 0.06f) })
            {
                var tgt = sh0.Xform.Apply(off); var from = tgt + new Vector3(0, 0.05f, 2.5f);
                Weapons.FireRay(from, (tgt - from).normalized, pistol.energy, pistol.prof, null);
                yield return new WaitForSeconds(0.1f);
            }
            if (d0.state == Dummy.State.STANDING) d0.Collapse();
            yield return new WaitForSeconds(0.2f);
            if (cam != null) yield return Snap(cam, sh0.transform.position, new Vector3(0.7f, 0.5f, 1.0f), dir, "perna_0", 35f);
            yield return new WaitForSeconds(3f);
            if (cam != null) yield return Snap(cam, sh0.transform.position, new Vector3(0.7f, 0.5f, 1.0f), dir, "perna_1", 35f);
            yield return new WaitForSeconds(4f);
            if (cam != null) yield return Snap(cam, sh0.transform.position, new Vector3(0.7f, 0.5f, 1.0f), dir, "perna_2", 35f);
        }
        // ---- boneco 1: foice na canela até cortar
        var d1 = Game.dummies[1];
        var sh = d1.parts["shin_l"];
        int swings = 0;
        for (; swings < 8; swings++)
        {
            bool done = false; foreach (var q in d1.ValidParts()) if (q.fragment) done = true;
            if (done) break;
            var at = sh.Xform.Apply(new Vector3(0f, 0.04f, 0.04f));
            d1.ApplyPenetration(sh, at, new Vector3(-1f, 0f, 0f), 380f, Weapons.P_SCYTHE, Vector3.forward);
            yield return new WaitForSeconds(0.05f);
        }
        Debug.Log($"[ferida] foice na canela: golpes={swings} {d1.StatusText()}");
        yield return new WaitForSeconds(1.8f);
        if (cam != null) yield return Snap(cam, d1.parts["pelvis"].transform.position, new Vector3(1.5f, 0.9f, 1.8f), dir, "foice_canela", 45f);

        // ---- boneco 2: machado no antebraço e marreta na coxa
        var d2 = Game.dummies[2];
        var fa = d2.parts["forearm_l"]; var th = d2.parts["thigh_r"];
        for (int i = 0; i < 6; i++)
        {
            bool done = false; foreach (var q in d2.ValidParts()) if (q.fragment) done = true;
            if (done) break;
            d2.ApplyPenetration(fa, fa.Xform.Apply(new Vector3(0f, 0.03f, 0.04f)), new Vector3(1f, 0f, 0f), 320f, Weapons.P_AXE, Vector3.forward);
            yield return new WaitForSeconds(0.05f);
        }
        d2.ApplyPenetration(th, th.Xform.Apply(new Vector3(0f, 0.05f, 0.07f)), new Vector3(0f, 0f, -1f), 360f, Weapons.P_HAMMER, Vector3.forward);
        yield return new WaitForSeconds(1.6f);
        if (cam != null) yield return Snap(cam, d2.parts["pelvis"].transform.position, new Vector3(1.5f, 0.9f, 1.8f), dir, "machado_antebraco", 45f);
        Debug.Log("== FERIDAS fim");
        Application.Quit();
    }

    static IEnumerator Snap(Camera cam, Vector3 center, Vector3 off, string dir, string name, float fov)
    {
        cam.fieldOfView = fov;
        cam.transform.position = center + off; cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        yield return null;
    }
}
