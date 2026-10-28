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
        yield return new WaitForSeconds(5f);
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

    void Rest(Dummy d, string label)
    {
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
