// GERADO por tools/dump_cs.gd a partir de scripts/weapons.gd (Godot). Não edite à mão.
using System.Collections.Generic;

public static partial class Weapons
{
    public static readonly Prof P_PISTOL = new Prof { name = "Pistola 9mm", kind = "bala", radius = 0.005f, flesh = 260.0f, bone = 1.0f, organ = 1.0f, bleed = 1.0f, @struct = 7.0f, structMult = 1.0f, impulse = 7.0f, pain = 0.25f, boneCost = 35.0f };
    public static readonly Prof P_RIFLE = new Prof { name = "Fuzil 7.62", kind = "bala", radius = 0.0055f, flesh = 190.0f, bone = 1.4f, organ = 1.3f, bleed = 1.2f, @struct = 11.0f, structMult = 1.1f, impulse = 10.0f, pain = 0.3f, boneCost = 28.0f };
    public static readonly Prof P_SHOTGUN = new Prof { name = "Escopeta 12", kind = "bala", radius = 0.004f, flesh = 300.0f, bone = 0.9f, organ = 0.9f, bleed = 0.9f, @struct = 6.0f, structMult = 1.8f, impulse = 5.0f, pain = 0.15f, boneCost = 18.0f };
    public static readonly Prof P_SNIPER = new Prof { name = "Sniper .50", kind = "bala", radius = 0.009f, flesh = 130.0f, bone = 2.6f, organ = 2.2f, bleed = 2.0f, @struct = 62.0f, structMult = 1.4f, impulse = 38.0f, pain = 0.5f, boneCost = 18.0f };
    public static readonly Prof P_KNIFE_SLASH = new Prof { name = "Faca (corte)", kind = "corte", radius = 0.02f, depth = 0.035f, flesh = 900.0f, bone = 0.3f, organ = 0.6f, bleed = 1.8f, vessel = 1.0f, @struct = 9.0f, structMult = 0.2f, impulse = 2.0f, pain = 0.3f, exitWound = false };
    public static readonly Prof P_KNIFE_STAB = new Prof { name = "Faca (facada)", kind = "perfuração", radius = 0.008f, depth = 0.16f, flesh = 300.0f, bone = 0.4f, organ = 1.5f, bleed = 1.3f, @struct = 6.0f, structMult = 0.3f, impulse = 3.0f, pain = 0.35f, exitWound = false, boneCost = 60.0f };
    public static readonly Prof P_AXE = new Prof { name = "Machado", kind = "machado", radius = 0.03f, depth = 0.2f, flesh = 450.0f, bone = 2.2f, organ = 1.2f, bleed = 2.0f, @struct = 44.0f, structMult = 0.2f, impulse = 14.0f, pain = 0.5f, boneCost = 10.0f, exitWound = false };
    public static readonly Prof P_ARROW = new Prof { name = "Flecha", kind = "flecha", radius = 0.006f, depth = 0.4f, flesh = 360.0f, bone = 0.9f, organ = 1.3f, bleed = 0.8f, @struct = 6.0f, structMult = 0.6f, impulse = 6.0f, pain = 0.35f, plug = true, boneCost = 40.0f };
    public static readonly Prof P_SHRAPNEL = new Prof { name = "Estilhaço", kind = "estilhaço", radius = 0.004f, flesh = 420.0f, bone = 0.8f, organ = 1.0f, bleed = 0.8f, @struct = 5.0f, structMult = 0.9f, impulse = 2.0f, pain = 0.15f, boneCost = 25.0f };
    public static readonly List<WeaponDef> LIST = new List<WeaponDef>
    {
        new WeaponDef { id = "pistol", name = "Pistola", type = "gun", rpm = 400.0f, auto = false, energy = 110.0f, pellets = 1, spread = 0.004f, sound = "pistol", recoil = 0.04f, prof = P_PISTOL, mag = 15, reload = 1.5f },
        new WeaponDef { id = "rifle", name = "Fuzil", type = "gun", rpm = 640.0f, auto = true, energy = 280.0f, pellets = 1, spread = 0.01f, sound = "rifle", recoil = 0.028f, prof = P_RIFLE, mag = 30, reload = 2.3f },
        new WeaponDef { id = "shotgun", name = "Escopeta", type = "gun", rpm = 70.0f, auto = false, energy = 48.0f, pellets = 10, spread = 0.05f, sound = "shotgun", recoil = 0.12f, prof = P_SHOTGUN, mag = 6, reload = 2.8f },
        new WeaponDef { id = "sniper", name = "Sniper", type = "gun", rpm = 40.0f, auto = false, energy = 900.0f, pellets = 1, spread = 0.0f, sound = "sniper", recoil = 0.16f, prof = P_SNIPER, mag = 5, reload = 3.0f },
        new WeaponDef { id = "bow", name = "Arco", type = "bow", rpm = 0.0f, auto = false, energy = 0.0f, pellets = 0, spread = 0.0f, sound = "", recoil = 0.0f, prof = null, mag = 0, reload = 0.0f },
        new WeaponDef { id = "knife", name = "Faca", type = "melee", rpm = 0.0f, auto = false, energy = 0.0f, pellets = 0, spread = 0.0f, sound = "", recoil = 0.0f, prof = null, mag = 0, reload = 0.0f },
        new WeaponDef { id = "axe", name = "Machado", type = "melee", rpm = 0.0f, auto = false, energy = 0.0f, pellets = 0, spread = 0.0f, sound = "", recoil = 0.0f, prof = null, mag = 0, reload = 0.0f },
        new WeaponDef { id = "grenade", name = "Granada", type = "throw", rpm = 0.0f, auto = false, energy = 0.0f, pellets = 0, spread = 0.0f, sound = "", recoil = 0.0f, prof = null, mag = 0, reload = 0.0f },
        new WeaponDef { id = "c4", name = "Bomba C4", type = "c4", rpm = 0.0f, auto = false, energy = 0.0f, pellets = 0, spread = 0.0f, sound = "", recoil = 0.0f, prof = null, mag = 0, reload = 0.0f },
        new WeaponDef { id = "med", name = "Kit médico", type = "med", rpm = 0.0f, auto = false, energy = 0.0f, pellets = 0, spread = 0.0f, sound = "", recoil = 0.0f, prof = null, mag = 0, reload = 0.0f },
    };
    public static readonly Dictionary<string, string> HINTS = new Dictionary<string, string>
    {
        { "pistol", "Clique: atirar  |  Q: recarregar" },
        { "rifle", "Segure: rajada automática  |  Q: recarregar" },
        { "shotgun", "Clique: 10 chumbos — de perto arranca membros  |  Q: recarregar" },
        { "sniper", "Clique: atirar  |  Direito: luneta  |  Q: recarregar" },
        { "bow", "Segure e solte: flecha (quanto mais puxar, mais fundo crava)" },
        { "knife", "Clique: corte (artérias/garganta)  |  Direito: facada profunda (órgãos)" },
        { "axe", "Clique: golpe (quebra ossos e amputa)" },
        { "grenade", "Clique: arremessar  |  Direito: rolar curto (estilhaços)" },
        { "c4", "Clique: grudar bomba  |  Direito: DETONAR todas" },
        { "med", "Clique: torniquete no membro  |  Direito: bandagem" },
    };
}
