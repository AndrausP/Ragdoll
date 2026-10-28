using System.Collections.Generic;
using UnityEngine;

public struct PenResult { public bool stopped; public Vector3 exit; public float energy; }

/// Dano: projétil/lâmina atravessando uma parte, explosão, amputação e destruição.
public partial class Dummy
{
    class Ev { public float t, len; public string type; public BoneState bone; public OrganState organ; public VesselState vessel; }

    // onde e com que "irregularidade" o último golpe atingiu: o corte da amputação usa isso (Dummy.Cut.cs)
    Vector3? cutHint; float cutRagged = 0.5f;

    public PenResult ApplyPenetration(DummyPart p, Vector3 entryW, Vector3 dirW, float energy, Prof prof, Vector3 normalW)
    {
        if (dead && p.destroyed) return new PenResult { stopped = true, exit = entryW, energy = 0f };
        var inv = p.Xform.Inverse();
        var dl = (inv.q * dirW).normalized;
        var o = inv.Apply(entryW);
        var start = o - dl * 0.06f;
        var span = p.RaySpan(start, dl);
        float tIn = 0f, tFull = 0.03f;
        if (span.y > 0f)
        {
            tIn = Mathf.Max(span.x - 0.06f, 0f);
            tFull = Mathf.Max(span.y - 0.06f, tIn + 0.005f);
        }
        float maxDepth = prof.depth;
        float tOut = Mathf.Min(tFull, tIn + maxDepth);
        float pathLen = Mathf.Max(tOut - tIn, 0.005f);
        var e0 = o + dl * tIn;
        var e1 = e0 + dl * pathLen;
        float rad = prof.radius;
        var events = new List<Ev>();
        foreach (var b in p.bones)
        {
            if (b.def.shell)
            {
                float side = b.def.side;
                if (side != 0f && Mathf.Sign(e0.x + 0.0001f) != side) continue;
                if (U.Randf() < b.def.chance) events.Add(new Ev { t = tIn + 0.004f, type = "bone", bone = b });
            }
            else
            {
                var r = DummyPart.SegSeg(e0, e1, b.def.a, b.def.b);
                if (r.x <= b.def.r + rad) events.Add(new Ev { t = tIn + r.y * pathLen, type = "bone", bone = b });
            }
        }
        foreach (var org in p.organs)
        {
            var c = org.def.c;
            var s = org.def.s * 0.5f + Vector3.one * rad;
            var sp = DummyPart.Slab(e0, dl, c - s, c + s);
            if (sp.y >= 0f && sp.x <= pathLen && sp.y >= sp.x)
            {
                float a0 = Mathf.Max(sp.x, 0f);
                events.Add(new Ev { t = tIn + a0, type = "organ", organ = org, len = Mathf.Min(sp.y, pathLen) - a0 });
            }
        }
        foreach (var v in p.vessels)
        {
            var r2 = DummyPart.SegSeg(e0, e1, v.def.a, v.def.b);
            if (r2.x <= v.def.r + rad * 1.5f) events.Add(new Ev { t = tIn + r2.y * pathLen, type = "vessel", vessel = v });
        }
        events.Sort((a, b) => a.t.CompareTo(b.t));

        float eLeft = energy, tPrev = tIn, flesh = prof.flesh;
        bool stopped = false;
        var summary = new List<string>();
        float woundBleed = p.def.baseBleed * prof.bleed, internalB = 0f, painAdd = prof.pain, strc = prof.@struct;
        bool arterial = false, boneHit = false;
        string vesselName = "";
        foreach (var ev in events)
        {
            eLeft -= (ev.t - tPrev) * flesh;
            tPrev = ev.t;
            if (eLeft <= 0f) { stopped = true; break; }
            switch (ev.type)
            {
                case "bone":
                    var b = ev.bone;
                    float dmg = (15f + eLeft * 0.45f) * prof.bone;
                    b.hp -= dmg;
                    eLeft -= prof.boneCost;
                    boneHit = true;
                    if (b.hp <= 0f && !b.fractured)
                    {
                        b.fractured = true; b.hp = 0f;
                        summary.Add($"[b]{b.name} FRATURADO[/b]");
                        painAdd += 0.3f;
                        strc += 14f;
                        p.RefreshBoneVisual(b);
                        OnFracture(p, b);
                    }
                    break;
                case "organ":
                    var org = ev.organ;
                    float dmg2 = (22f + eLeft * 0.4f) * prof.organ * Mathf.Clamp(ev.len / 0.05f, 0.4f, 1.5f);
                    float was = org.hp;
                    org.hp = Mathf.Max(0f, org.hp - dmg2);
                    internalB += org.def.bleed * Mathf.Clamp((was - org.hp) / org.max, 0.15f, 1f);
                    painAdd += 0.1f;
                    if (org.hp <= 0f && !org.destroyed) { org.destroyed = true; summary.Add($"[b]{org.name} DESTRUÍDO[/b]"); }
                    else summary.Add($"{org.name} perfurado ({(int)(100f * org.hp / org.max)}%)");
                    p.RefreshOrganVisual(org);
                    break;
                case "vessel":
                    var v = ev.vessel;
                    if (!v.cut)
                    {
                        v.cut = true;
                        summary.Add(v.def.artery ? $"[b]{v.name} ROMPIDA[/b]" : $"{v.name} rompida");
                        woundBleed += v.def.bleed * prof.vessel;
                    }
                    else woundBleed += v.def.bleed * 0.3f;
                    arterial = arterial || v.def.artery;
                    vesselName = v.name;
                    p.RefreshVesselVisual(v);
                    break;
            }
        }
        if (!stopped)
        {
            eLeft -= (tOut - tPrev) * flesh;
            if (eLeft <= 0f) stopped = true;
        }
        bool exited = !stopped && tFull <= tIn + maxDepth + 1e-4f;
        if (!exited && !stopped) stopped = true;   // lâmina parou na profundidade máxima
        float used = energy - Mathf.Max(eLeft, 0f);
        float sdmg = strc + used * 0.12f * prof.structMult;
        p.hp -= sdmg;

        // ---- ferida de entrada
        var w = new Wound { kind = prof.kind, localPos = e0 };
        var nl = -dl;
        if (normalW != Vector3.zero) nl = (inv.q * normalW).normalized;
        w.localNormal = nl;
        w.bleed = woundBleed; w.internalBleed = internalB; w.arterial = arterial; w.vessel = vesselName;
        w.pain = 0.08f + painAdd * 0.4f + (arterial ? 0.12f : 0f);
        w.plugged = prof.plug && stopped;
        p.AddWound(w);
        var exitW = p.Xform.Apply(o + dl * tFull);
        if (exited && prof.exitWound)
        {
            var w2 = new Wound { kind = "saída", localPos = o + dl * tFull, localNormal = dl, bleed = p.def.baseBleed * prof.bleed * 1.5f, pain = 0.05f };
            p.AddWound(w2);
            DeformSkin(p, w2, prof, Mathf.Max(eLeft, 0f));
        }
        acutePain += painAdd;
        bool blunt = prof.kind == "contusão";
        concussion = Mathf.Min(1f, concussion + (p.partId == "head" ? (boneHit ? 0.5f : 0f) + (blunt ? 0.45f : 0f) : 0f));
        var gw = w;
        if (exited && prof.exitWound && w.kind == "bala") gw = p.wounds[p.wounds.Count - 1];
        WallDamage(p, sdmg * (w.kind == "machado" || w.kind == "corte" ? 1.4f : 0.5f), gw.localPos, gw.localNormal);

        // ---- sangue
        var src = p.col;
        float big = Mathf.Clamp(used / 150f, 0.3f, 2.5f) * (blunt ? 0.25f : 1f);   // contusão quase não espirra
        Game.blood.Mist(entryW, -dirW * 0.6f + Vector3.up * 0.2f, 0.6f * big);
        if (!blunt) Game.blood.Spray(entryW, (-dirW + U.RandomUnit() * 0.5f).normalized, (int)(3 + 4 * big), 1.8f, 0.7f, src);
        if (exited)
        {
            float ef = Mathf.Clamp(eLeft / 80f, 0.4f, 3f);
            Game.blood.Spray(exitW, dirW, (int)(10 + 16 * ef), 3f + 2.5f * ef, 0.35f, src);
            Game.blood.Mist(exitW, dirW, 1.2f * ef);
        }
        if (prof.kind == "corte" || prof.kind == "machado")
            Game.blood.Spray(entryW, (Vector3.up * 0.6f - dirW).normalized, 10 + (int)(10 * big), 2.5f, 0.8f, src);
        Game.sfx.Play("flesh", entryW, -2f, U.RandRange(0.85f, 1.15f));
        if (boneHit) Game.sfx.Play("bone", entryW, -3f, U.RandRange(0.9f, 1.2f));

        // ---- física e reação
        float imp = prof.impulse;
        p.impactGrace = 0.15f;
        fear = Mathf.Min(1f, fear + 0.45f);
        if (state == State.STANDING)
        {
            FlinchFrom(dirW, imp * 0.25f);
            Stagger(dirW, imp * ImpactConfig.STAGGER_PER_IMPULSE + sdmg * ImpactConfig.STAGGER_PER_DAMAGE
                + (p.def.limb.StartsWith("leg") ? ImpactConfig.STAGGER_LEG_HIT : 0f));
            HitKick(p, dirW, imp);
        }
        else if (!p.Freeze) Knockback(p, dirW, imp * 0.6f, entryW);
        lastHitSummary = $"{prof.name} → {p.def.name}";
        if (summary.Count > 0) Game.LogEvent($"{prof.name} → {p.def.name}: {string.Join(", ", summary)}", new Color(1, 0.55f, 0.5f));
        else if (arterial || woundBleed > 4f) Game.LogEvent($"{prof.name} → {p.def.name}: sangramento intenso", new Color(1, 0.6f, 0.55f));
        entryLast = entryW;
        cutHint = entryW;
        cutRagged = prof.kind == "corte" || prof.kind == "perfuração" ? 0.12f : prof.kind == "machado" ? 0.45f : 0.9f;
        DeformSkin(p, w, prof, used);
        AfterDamage(p, dirW, imp, sdmg);
        return new PenResult { stopped = stopped, exit = exitW, energy = Mathf.Max(eLeft, 0f) };
    }

    /// Empurrão físico: forte na parte atingida (no ponto do impacto, gira o corpo) e parte do momento no corpo todo.
    void Knockback(DummyPart p, Vector3 dirW, float imp, Vector3 atW)
    {
        var push = dirW.normalized * imp * ImpactConfig.KNOCKBACK;
        if (!p.Freeze) p.rb.AddForceAtPosition(push * ImpactConfig.KNOCKBACK_HIT_FRAC, atW, ForceMode.Impulse);
        float massTotal = 0f;
        foreach (var q in ValidParts()) if (!q.severed || q == p) massTotal += q.mass;
        foreach (var q in ValidParts())
        {
            if (q.Freeze || (q.severed && q != p)) continue;
            q.rb.AddForce(push * ImpactConfig.KNOCKBACK_BODY_FRAC * q.mass / Mathf.Max(massTotal, 1f), ForceMode.Impulse);
            q.rb.WakeUp();
        }
    }

    void OnFracture(DummyPart p, BoneState b)
    {
        Game.sfx.Play("bone", p.transform.position, 2f, U.RandRange(0.8f, 1f));
        switch (b.name)
        {
            case "Crânio": DamageOrgan(p, "brain", 30f); concussion = 1f; break;
            case "Vértebras cervicais": if (U.Randf() < 0.6f) DamageOrgan(p, "spinal_c", 50f); break;
            case "Vértebras torácicas": if (U.Randf() < 0.5f) DamageOrgan(p, "spinal_t", 50f); break;
            case "Vértebras lombares": if (U.Randf() < 0.5f) DamageOrgan(p, "spinal_l", 50f); break;
            case "Costelas esquerdas": if (U.Randf() < 0.45f) DamageOrgan(p, "lung", 25f); break;
            case "Costelas direitas":
                if (U.Randf() < 0.45f)
                    foreach (var o in p.organs)
                        if (o.name == "Pulmão direito") { o.hp = Mathf.Max(0f, o.hp - 25f); p.RefreshOrganVisual(o); }
                break;
        }
    }

    void AfterDamage(DummyPart p, Vector3 dirW, float imp, float sdmg)
    {
        var brain = p.Organ("brain");
        if (brain != null && brain.destroyed) Die("Destruição cerebral");
        var stem = p.Organ("brainstem");
        if (stem != null && stem.destroyed) Die("Lesão do tronco encefálico");
        if (p.hp <= 0f && p.def.severable && !p.cutHere && !p.destroyed)
        {
            if (p.def.leaf && (p.hp < -p.maxHp * 0.45f || sdmg > p.maxHp * 0.9f)) DestroyPart(p, dirW);
            else SeverPart(p, dirW);
        }
        else if (p.hp <= -p.maxHp * 0.6f && p.def.leaf && !p.destroyed) DestroyPart(p, dirW);
        if (state == State.STANDING && (ShouldCollapse() || imp > ImpactConfig.KNOCKDOWN_IMPULSE))
        {
            Collapse();
            Knockback(p, dirW, imp, entryLast);
        }
    }

    /// Explosão: intensidade 0..1+ (já com distância), dir da explosão para a parte
    public void ApplyBlast(DummyPart p, Vector3 centerW, float intensity, float push, float power, string nm)
    {
        if (p.destroyed) return;
        var dir = (p.transform.position - centerW).normalized;
        fear = Mathf.Min(1f, fear + push);
        if (state == State.STANDING)
        {
            Stagger(dir, intensity * 1.5f + push * 0.6f);
            if (intensity > 0.3f || push > 0.9f || ShouldCollapse()) Collapse();
        }
        if ((p.partId == "abdomen" || p.partId == "pelvis" || p.partId == "chest") && intensity > 0.35f)
        {
            var lc = p.Xform.Inverse().Apply(centerW).normalized;
            WallDamage(p, 90f * intensity * power, lc * p.PartRadius(), lc);
        }
        p.impactGrace = 0.4f;
        cutHint = p.transform.position; cutRagged = 1f;
        acutePain += 0.1f * push;
        if (p.Freeze)
        {
            FlinchFrom(dir, push * 3f);
            if (intensity <= 0.01f) return;
        }
        var imp = dir * p.mass * 7f * push * power + Vector3.up * p.mass * 2.5f * push * power;
        if (!p.Freeze) p.rb.AddForce(U.LimitLength(imp, p.mass * 18f), ForceMode.Impulse);
        if (intensity <= 0.01f) return;
        float dmg = 170f * intensity * power * U.RandRange(0.6f, 1.15f);
        p.hp -= dmg * (p.IsLimb() ? 0.9f : 0.35f);
        acutePain += 0.25f * intensity;
        var summary = new List<string>();
        if (intensity > 0.12f)
        {
            var dl = (p.Xform.Inverse().q * -dir).normalized;
            var w = new Wound
            {
                kind = intensity > 0.4f ? "queimadura" : "estilhaço",
                localPos = dl * p.PartRadius(), localNormal = dl,
                bleed = p.def.baseBleed * (1f + 3f * intensity), pain = 0.1f + 0.2f * intensity,
            };
            p.AddWound(w);
            p.AddBloodiness(0.15f * intensity);
        }
        foreach (var b in p.bones)
        {
            if (b.fractured) continue;
            b.hp -= dmg * U.RandRange(0.2f, 0.7f);
            if (b.hp <= 0f)
            {
                b.fractured = true; b.hp = 0f;
                summary.Add(b.name + " FRATURADO");
                p.RefreshBoneVisual(b);
                OnFracture(p, b);
            }
        }
        foreach (var o in p.organs)
        {
            float f = o.vital == "lung" ? 0.6f : 0.3f;
            float od = dmg * f * U.RandRange(0.5f, 1f);
            if (od < 4f) continue;
            o.hp = Mathf.Max(0f, o.hp - od);
            if (o.hp <= 0f && !o.destroyed) { o.destroyed = true; summary.Add(o.name + " DESTRUÍDO"); }
            p.RefreshOrganVisual(o);
            if (p.wounds.Count > 0) p.wounds[p.wounds.Count - 1].internalBleed += o.def.bleed * 0.4f;
        }
        if (p.partId == "head") concussion = Mathf.Min(1f, concussion + intensity * power);
        if (summary.Count > 0) Game.LogEvent($"{nm} → {p.def.name}: {string.Join(", ", summary)}", new Color(1, 0.6f, 0.3f));
        if (p.def.severable && !p.cutHere)
        {
            if (p.hp <= 0f)
            {
                if (p.def.leaf && p.hp < -p.maxHp * 0.8f) DestroyPart(p, dir);
                else SeverPart(p, dir);
            }
        }
        else if (p.def.leaf && p.hp < -p.maxHp * 1.2f && !p.destroyed) DestroyPart(p, dir);
        var brain = p.Organ("brain");
        if (brain != null && brain.destroyed) Die("Destruição cerebral (explosão)");
    }

    public void SeverPart(DummyPart p, Vector3 dirW)
    {
        if (TryMeshCut(p, dirW)) return;
        if (p.cutHere || p.destroyed || p.parentPart == null) return;
        if (state == State.STANDING) Collapse();
        bool wasAttached = !p.severed;
        if (p.joint != null) { Destroy(p.joint); p.joint = null; }
        p.cutHere = true;
        float lost = 0f;
        foreach (var c in Chain(p))
        {
            if (wasAttached && !c.destroyed) lost += c.limbBlood;
            c.severed = true;
            if (skinBody && !c.destroyed) c.bodyMesh.SetActive(true);
            c.Freeze = false;
            c.pressure = 0f;
        }
        if (wasAttached)
        {
            blood = Mathf.Max(0f, blood - lost * 0.55f);
            totalLost += lost * 0.55f;
            p.drainBlood = lost * 0.45f;
        }
        var par = p.parentPart;
        var anchor = p.def.joint.anchor;
        var aPar = anchor - par.def.pos;
        var aChild = anchor - p.def.pos;
        var n = (p.def.pos - anchor).normalized;
        float r = p.PartRadius();
        par.AddStumpVisual(aPar, n, r);
        p.AddStumpVisual(aChild, -n, r);
        p.hasStump = true; p.stumpLocal = aChild; p.stumpNormal = -n;
        if (wasAttached && !par.severed)
        {
            var w = new Wound { kind = "toco", localPos = aPar, localNormal = n, bleed = p.def.stumpBleed, arterial = true, vessel = p.def.stumpVessel, pain = 0.35f };
            par.AddWound(w);
            p.stumpParentWound = w;
            acutePain += 0.7f;
        }
        var aw = par.Xform.Apply(aPar);
        var dv = dirW != Vector3.zero ? dirW : n;
        p.rb.AddForce((dv.normalized * 6f + U.RandomUnit() * 2f) * Mathf.Min(p.mass, 6f) * 0.6f, ForceMode.Impulse);
        p.impactGrace = 0.5f; par.impactGrace = 0.5f;
        var nw = (par.transform.rotation * n).normalized;
        Game.blood.Spray(aw, nw, 40, 4f, 0.5f, par.col);
        Game.blood.Spray(aw, -nw, 20, 2.5f, 0.6f, p.col);
        Game.blood.Mist(aw, dv, 2f);
        Game.sfx.Play("sever", aw, 2f);
        Game.LogEvent($"[b]AMPUTAÇÃO:[/b] {p.def.name} arrancado(a) — {p.def.stumpVessel} jorrando", new Color(1, 0.25f, 0.25f));
        if (p.partId == "head" || p.partId == "neck") Die("Decapitação");
    }

    public void DestroyPart(DummyPart p, Vector3 dirW)
    {
        if (p.destroyed) return;
        if (!p.cutHere) SeverPart(p, dirW);
        p.destroyed = true;
        var pos = p.transform.position;
        Game.blood.Gibs(pos, (int)(p.mass * 3f) + 5, dirW, p.partId == "head");
        Game.blood.Mist(pos, dirW, 3f);
        Game.blood.Spray(pos, (dirW + Vector3.up * 0.3f).normalized, 50, 5f, 0.8f, p.col);
        Game.sfx.Play("gib", pos, 3f);
        p.Freeze = true;
        p.col.enabled = false;
        foreach (Transform c in p.transform) if (c.gameObject != p.xrayRoot) c.gameObject.SetActive(false);
        Game.LogEvent($"[b]{p.def.name} DESTRUÍDA[/b] em pedaços", new Color(1, 0.2f, 0.2f));
        if (p.partId == "head") Die("Cabeça destruída");
    }
}
