using System.Collections.Generic;
using UnityEngine;

/// Perfil de dano (os valores estão em WeaponData.cs, gerado a partir do Godot). Os padrões são os
/// mesmos do prof.get(..., padrão) do GDScript.
public class Prof
{
    public string name = "?", kind = "bala";
    public float radius = 0.006f, depth = 10f, flesh = 250f, bone = 1f, organ = 1f, bleed = 1f, vessel = 1f;
    public float @struct = 8f, structMult = 1f, impulse = 4f, pain = 0.2f, boneCost = 35f;
    public bool exitWound = true, plug = false;
}

public class WeaponDef
{
    public string id, name, type, sound;
    public float rpm, energy, spread, recoil, reload;
    public bool auto;
    public int pellets, mag;
    public Prof prof;
    public int slot;              // tecla (1..9, 0 = 10); repetir a tecla alterna as armas da mesma categoria
    public float pitch = 1f;      // tom do som do tiro
    public int burst;             // > 0: cada clique dispara uma rajada desse tamanho
    public string proj;           // projétil de verdade (modelo em weapons.bytes) em vez de tiro instantâneo: virote, arpão
    public float projSpeed;       // m/s
}

/// Golpe de arma branca: perfil, energia, alcance, recarga e tipo de animação (slash, stab, chop, thrust, smash).
public class MeleeAttack
{
    public Prof prof; public float energy, reach, cooldown; public string anim;
    public bool sweep;            // golpe em arco: acerta tudo que estiver na frente (vários raios em leque)
}

public static partial class Weapons
{
    // ---- armas adicionadas no Unity (modelos CC0 do Quaternius: Ultimate Gun Pack e Medieval Weapons)
    public static readonly Prof P_SMG = new Prof { name = "SMG 9mm", kind = "bala", radius = 0.005f, flesh = 270f, bone = 0.9f, organ = 1f, bleed = 1f, @struct = 6f, structMult = 1f, impulse = 5f, pain = 0.22f, boneCost = 38f };
    public static readonly Prof P_MAGNUM = new Prof { name = "Revólver .357", kind = "bala", radius = 0.0065f, flesh = 200f, bone = 1.7f, organ = 1.5f, bleed = 1.4f, @struct = 16f, structMult = 1.2f, impulse = 15f, pain = 0.38f, boneCost = 24f };
    public static readonly Prof P_SWORD = new Prof { name = "Espada", kind = "machado", radius = 0.025f, depth = 0.11f, flesh = 520f, bone = 1.5f, organ = 1f, bleed = 2.3f, vessel = 1.2f, @struct = 34f, structMult = 0.25f, impulse = 7f, pain = 0.45f, boneCost = 16f, exitWound = false };
    public static readonly Prof P_SWORD_STAB = new Prof { name = "Espada (estocada)", kind = "perfuração", radius = 0.012f, depth = 0.3f, flesh = 280f, bone = 0.6f, organ = 1.7f, bleed = 1.6f, @struct = 8f, structMult = 0.3f, impulse = 6f, pain = 0.4f, boneCost = 45f };
    public static readonly Prof P_SPEAR = new Prof { name = "Lança", kind = "perfuração", radius = 0.013f, depth = 0.38f, flesh = 250f, bone = 0.7f, organ = 1.8f, bleed = 1.5f, @struct = 10f, structMult = 0.35f, impulse = 10f, pain = 0.42f, boneCost = 40f };
    public static readonly Prof P_556 = new Prof { name = "Carabina 5.56", kind = "bala", radius = 0.005f, flesh = 175f, bone = 1.2f, organ = 1.5f, bleed = 1.2f, @struct = 9f, structMult = 1.1f, impulse = 8f, pain = 0.3f, boneCost = 26f };
    public static readonly Prof P_50AE = new Prof { name = "Pistola .50", kind = "bala", radius = 0.0068f, flesh = 165f, bone = 2.1f, organ = 1.8f, bleed = 1.6f, @struct = 24f, structMult = 1.3f, impulse = 19f, pain = 0.45f, boneCost = 18f };
    public static readonly Prof P_CLAYMORE = new Prof { name = "Montante", kind = "machado", radius = 0.03f, depth = 0.17f, flesh = 420f, bone = 2.4f, organ = 1.2f, bleed = 2.4f, vessel = 1.3f, @struct = 58f, structMult = 0.25f, impulse = 17f, pain = 0.55f, boneCost = 8f, exitWound = false };
    public static readonly Prof P_SCYTHE = new Prof { name = "Foice", kind = "corte", radius = 0.025f, depth = 0.13f, flesh = 470f, bone = 1.6f, organ = 1.1f, bleed = 2.7f, vessel = 1.4f, @struct = 42f, structMult = 0.25f, impulse = 9f, pain = 0.5f, boneCost = 12f, exitWound = false };
    public static readonly Prof P_THROW_KNIFE = new Prof { name = "Faca de arremesso", kind = "perfuração", radius = 0.008f, depth = 0.13f, flesh = 290f, bone = 0.5f, organ = 1.5f, bleed = 1.4f, @struct = 7f, structMult = 0.3f, impulse = 4f, pain = 0.35f, boneCost = 50f, exitWound = false, plug = true };
    public static readonly Prof P_BOLT = new Prof { name = "Virote de besta", kind = "perfuração", radius = 0.011f, depth = 0.5f, flesh = 210f, bone = 1.1f, organ = 1.7f, bleed = 1.8f, vessel = 1.3f, @struct = 20f, structMult = 0.5f, impulse = 16f, pain = 0.42f, boneCost = 30f, exitWound = false, plug = true };
    public static readonly Prof P_HARPOON = new Prof { name = "Arpão", kind = "perfuração", radius = 0.017f, depth = 0.7f, flesh = 150f, bone = 1.9f, organ = 2.0f, bleed = 2.4f, vessel = 1.6f, @struct = 52f, structMult = 0.5f, impulse = 34f, pain = 0.55f, boneCost = 16f, exitWound = false, plug = true };
    public static readonly Prof P_HAMMER = new Prof { name = "Marreta", kind = "contusão", radius = 0.035f, depth = 0.07f, flesh = 160f, bone = 2.8f, organ = 0.6f, bleed = 0.15f, vessel = 0.2f, @struct = 20f, structMult = 0.18f, impulse = 22f, pain = 0.5f, boneCost = 6f, exitWound = false };

    // montado no construtor estático: usa perfis do WeaponData.cs (a ordem de inicialização entre arquivos parciais não é garantida)
    static Dictionary<string, MeleeAttack[]> MELEE;

    static Dictionary<string, MeleeAttack[]> MeleeTable() => new Dictionary<string, MeleeAttack[]>
    {
        // [0] = clique, [1] = botão direito
        { "knife", new[] { new MeleeAttack { prof = P_KNIFE_SLASH, energy = 100f, reach = 1.9f, cooldown = 0.32f, anim = "slash" }, new MeleeAttack { prof = P_KNIFE_STAB, energy = 140f, reach = 1.7f, cooldown = 0.5f, anim = "stab" } } },
        { "axe", new[] { new MeleeAttack { prof = P_AXE, energy = 320f, reach = 2.2f, cooldown = 0.8f, anim = "chop" }, null } },
        { "sword", new[] { new MeleeAttack { prof = P_SWORD, energy = 300f, reach = 2.4f, cooldown = 0.55f, anim = "slash" }, new MeleeAttack { prof = P_SWORD_STAB, energy = 220f, reach = 2.5f, cooldown = 0.6f, anim = "stab" } } },
        { "spear", new[] { new MeleeAttack { prof = P_SPEAR, energy = 240f, reach = 3.2f, cooldown = 0.7f, anim = "thrust" }, null } },
        { "hammer", new[] { new MeleeAttack { prof = P_HAMMER, energy = 360f, reach = 2.1f, cooldown = 0.95f, anim = "smash" }, null } },
        { "claymore", new[] { new MeleeAttack { prof = P_CLAYMORE, energy = 480f, reach = 2.6f, cooldown = 1.1f, anim = "smash" },
                              new MeleeAttack { prof = P_CLAYMORE, energy = 380f, reach = 2.5f, cooldown = 1.0f, anim = "reap", sweep = true } } },
        { "scythe", new[] { new MeleeAttack { prof = P_SCYTHE, energy = 380f, reach = 2.8f, cooldown = 0.9f, anim = "reap", sweep = true }, null } },
    };

    public static MeleeAttack Melee(string id, bool alt) => MELEE.TryGetValue(id, out var m) ? m[alt ? 1 : 0] : null;

    static readonly Dictionary<string, int> SLOT = new Dictionary<string, int>
    {
        { "pistol", 1 }, { "revolver", 1 }, { "smg", 2 }, { "rifle", 2 }, { "shotgun", 3 }, { "sawedoff", 3 }, { "sniper", 4 },
        { "deagle", 1 }, { "m4", 2 }, { "bullpup", 2 }, { "throwknife", 5 }, { "scythe", 6 }, { "claymore", 7 }, { "flamer", 8 },
        { "bow", 5 }, { "crossbow", 5 }, { "harpoon", 4 }, { "knife", 6 }, { "sword", 6 }, { "spear", 6 }, { "axe", 7 }, { "hammer", 7 }, { "grenade", 8 }, { "c4", 9 }, { "med", 10 },
    };

    static Weapons()
    {
        MELEE = MeleeTable();
        LIST.Add(new WeaponDef { id = "revolver", name = "Revólver", type = "gun", rpm = 150f, energy = 380f, pellets = 1, spread = 0.003f, sound = "revolver", recoil = 0.11f, prof = P_MAGNUM, mag = 6, reload = 3.2f });
        LIST.Add(new WeaponDef { id = "smg", name = "SMG", type = "gun", rpm = 900f, auto = true, energy = 100f, pellets = 1, spread = 0.02f, sound = "smg", recoil = 0.018f, prof = P_SMG, mag = 32, reload = 2.0f });
        LIST.Add(new WeaponDef { id = "sawedoff", name = "Cano serrado", type = "gun", rpm = 180f, energy = 52f, pellets = 12, spread = 0.085f, sound = "sawedoff", recoil = 0.2f, prof = P_SHOTGUN, mag = 2, reload = 2.2f });
        LIST.Add(new WeaponDef { id = "sword", name = "Espada", type = "melee" });
        LIST.Add(new WeaponDef { id = "spear", name = "Lança", type = "melee" });
        LIST.Add(new WeaponDef { id = "hammer", name = "Marreta", type = "melee" });
        LIST.Add(new WeaponDef { id = "deagle", name = "Pistola .50", type = "gun", rpm = 170f, energy = 520f, pellets = 1, spread = 0.005f, sound = "revolver", pitch = 0.85f, recoil = 0.18f, prof = P_50AE, mag = 7, reload = 2.0f });
        LIST.Add(new WeaponDef { id = "m4", name = "Carabina M4", type = "gun", rpm = 760f, auto = true, energy = 240f, pellets = 1, spread = 0.008f, sound = "rifle", pitch = 1.08f, recoil = 0.024f, prof = P_556, mag = 30, reload = 2.1f });
        LIST.Add(new WeaponDef { id = "bullpup", name = "Bullpup (rajada)", type = "gun", rpm = 950f, burst = 3, energy = 250f, pellets = 1, spread = 0.006f, sound = "rifle", pitch = 1.14f, recoil = 0.022f, prof = P_556, mag = 30, reload = 2.4f });
        LIST.Add(new WeaponDef { id = "claymore", name = "Montante", type = "melee" });
        LIST.Add(new WeaponDef { id = "scythe", name = "Foice", type = "melee" });
        LIST.Add(new WeaponDef { id = "throwknife", name = "Facas de arremesso", type = "throwknife", mag = 8, reload = 1.6f });
        LIST.Add(new WeaponDef { id = "crossbow", name = "Besta", type = "gun", rpm = 45f, energy = 300f, pellets = 1, spread = 0.003f, sound = "bow", pitch = 0.8f, recoil = 0.05f, prof = P_BOLT, mag = 1, reload = 2.2f, proj = "bolt", projSpeed = 78f });
        LIST.Add(new WeaponDef { id = "harpoon", name = "Arpão", type = "gun", rpm = 30f, energy = 520f, pellets = 1, spread = 0.004f, sound = "shotgun", pitch = 0.6f, recoil = 0.14f, prof = P_HARPOON, mag = 1, reload = 3.2f, proj = "harpoon_bolt", projSpeed = 64f });
        LIST.Add(new WeaponDef { id = "flamer", name = "Lança-chamas", type = "flame", mag = 100, reload = 3.2f });
        foreach (var w in LIST) w.slot = SLOT.TryGetValue(w.id, out var s) ? s : 10;
        // ordem estável por categoria (a barra e a rodinha seguem essa ordem)
        var sorted = new List<WeaponDef>(LIST);
        for (int i = 1; i < sorted.Count; i++)
            for (int j = i; j > 0 && sorted[j].slot < sorted[j - 1].slot; j--)
                (sorted[j], sorted[j - 1]) = (sorted[j - 1], sorted[j]);
        LIST.Clear(); LIST.AddRange(sorted);
        HINTS["revolver"] = "Clique: atirar (.357, quebra osso)  |  Direito: mirar  |  Q: recarregar";
        HINTS["smg"] = "Segure: rajada rápida  |  Direito: mirar  |  Q: recarregar";
        HINTS["sawedoff"] = "Clique: 12 chumbos, dois canos — devastadora de perto  |  Q: recarregar";
        HINTS["sword"] = "Clique: talho (amputa)  |  Direito: estocada (órgãos)";
        HINTS["spear"] = "Clique: estocada longa (atravessa o tronco)";
        HINTS["hammer"] = "Clique: golpe contundente (fratura ossos, hematomas, concussão)";
        HINTS["deagle"] = "Clique: .50 — arranca pedaço e derruba  |  Direito: mirar  |  Q: recarregar";
        HINTS["m4"] = "Segure: automático  |  Direito: mirar  |  Q: recarregar";
        HINTS["bullpup"] = "Clique: rajada de 3  |  Direito: mirar  |  Q: recarregar";
        HINTS["claymore"] = "Clique: golpe de cima (parte ao meio)  |  Direito: varredura (pega vários)";
        HINTS["scythe"] = "Clique: ceifada em arco (corta tudo à frente)";
        HINTS["throwknife"] = "Clique: arremessar faca (crava)  |  Direito: leque de 3  |  Q: recolher";
        HINTS["crossbow"] = "Clique: virote (crava fundo)  |  Direito: mirar  |  Q: recarregar";
        HINTS["harpoon"] = "Clique: arpão (atravessa e prende)  |  Segure o direito: puxar o corpo  |  Q: recarregar / soltar corda";
        HINTS["flamer"] = "Segure: jato de fogo (queima, carboniza, incendeia)  |  Q: recarregar";
        foreach (var id in new[] { "pistol", "rifle", "shotgun" })
            if (HINTS.TryGetValue(id, out var h) && !h.Contains("Direito")) HINTS[id] = h.Replace("  |  Q:", "  |  Direito: mirar  |  Q:");
    }

    public static WeaponDef ById(string id) { foreach (var w in LIST) if (w.id == id) return w; return null; }

    /// Tiro hitscan com penetração. Retorna o ponto final para o traçante.
    public static Vector3 FireRay(Vector3 from, Vector3 dir, float energy, Prof prof, List<Collider> exclude, float maxDist = 400f, bool fx = true)
    {
        var ex = exclude != null ? new List<Collider>(exclude) : new List<Collider>();
        Vector3 pos = from;
        float e = energy;
        Vector3 end = from + dir * maxDist;
        int hits = 0;
        while (e > 4f && hits < 6)
        {
            var h = Phys.Ray(pos, pos + dir * maxDist, Game.L_WORLD | Game.L_DUMMY | Game.L_PROPS, ex);
            if (!h.hit) { end = pos + dir * maxDist; break; }
            var part = h.part;
            if (part != null)
            {
                var res = part.dummy.ApplyPenetration(part, h.position, dir, e, prof, h.normal);
                hits++;
                if (Game.hud) Game.hud.Hitmarker(part.dummy, part);
                if (res.stopped) { end = h.position; break; }
                e = res.energy * 0.92f;
                pos = res.exit + dir * 0.005f;
                ex.Add(h.collider);
                end = pos;
            }
            else
            {
                end = h.position;
                if (h.body != null && !h.body.isKinematic)
                {
                    h.body.AddForceAtPosition(dir * prof.impulse * 0.6f, h.position, ForceMode.Impulse);
                }
                else if (fx)
                {
                    Game.blood.BulletHole(h.position, h.normal);
                    Dust(h.position, h.normal);
                    Game.sfx.Play("impact", h.position, -6f, U.RandRange(0.8f, 1.3f));
                }
                break;
            }
        }
        return end;
    }

    public static void Dust(Vector3 pos, Vector3 n)
    {
        Fx.Burst(pos, 8, 0.5f, 2.0f, new Color(0.7f, 0.68f, 0.65f, 0.8f), new Color(0.7f, 0.68f, 0.65f, 0f), 0.05f, false,
            new Vector3(0, -3, 0), n, 35f, 0.25f);
    }
}
