using System.Collections.Generic;
using UnityEngine;

/// Definições anatômicas (os números estão em AnatomyData.cs, gerado a partir do Godot).
/// Espaço do boneco: frente = +Z, esquerda do boneco = +X, cima = +Y (mesmos números do Godot).
public class JointDef { public string type; public Vector3 anchor, dir; public float swing, twist, lower, upper, muscle; }
public class OrganDef { public string name, vital; public Vector3 c, s; public float bleed, hp = 100f; public Color color; }
public class BoneDef
{
    public string name; public Vector3 a, b; public float r, hp = 60f;
    public bool shell; public float chance = 1f, side;      // "casca" (crânio/costelas)
}
public class VesselDef { public string name; public Vector3 a, b; public float r, bleed; public bool artery; }

public class PartDef
{
    public string id, name, parent, shape, limb;
    public Vector3 pos, size;
    public float r, len, mass, hp, baseBleed, stumpBleed;
    public string stumpVessel = "";
    public bool severable, leaf, skull, ribs;
    public JointDef joint;
    public List<OrganDef> organs = new List<OrganDef>();
    public List<BoneDef> bones = new List<BoneDef>();
    public List<VesselDef> vessels = new List<VesselDef>();
}

public static partial class Anatomy
{
    public const float BLOOD_TOTAL = 5000f;
    public static readonly Color C_LIVER = new Color(0.42f, 0.1f, 0.08f), C_STOMACH = new Color(0.9f, 0.6f, 0.5f),
        C_SPLEEN = new Color(0.45f, 0.12f, 0.25f), C_GUT = new Color(0.92f, 0.66f, 0.58f), C_LUNG = new Color(0.95f, 0.55f, 0.6f);

    public static readonly string[] ORDER =
    {
        "pelvis", "abdomen", "chest", "neck", "head",
        "upper_arm_l", "forearm_l", "hand_l", "upper_arm_r", "forearm_r", "hand_r",
        "thigh_l", "shin_l", "foot_l", "thigh_r", "shin_r", "foot_r",
    };

    static Dictionary<string, PartDef> cache;
    public static Dictionary<string, PartDef> Parts() => cache ?? (cache = Build());

    public static int OrderIndex(string id) => System.Array.IndexOf(ORDER, id);

    public static float PartRadius(PartDef p)
    {
        switch (p.shape)
        {
            case "box": return p.size.magnitude * 0.5f;
            case "capsule": return p.len * 0.5f;
            default: return p.r;
        }
    }

    /// Ponto de colocação do torniquete "alto e apertado" para um membro
    public static string TourniquetSite(string partId)
    {
        if (partId.EndsWith("_l") || partId.EndsWith("_r"))
        {
            string side = partId.Substring(partId.Length - 1);
            if (partId.StartsWith("upper_arm") || partId.StartsWith("forearm") || partId.StartsWith("hand")) return "upper_arm_" + side;
            if (partId.StartsWith("thigh") || partId.StartsWith("shin") || partId.StartsWith("foot")) return "thigh_" + side;
        }
        return "";
    }
}
