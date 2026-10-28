using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// Lê os .bytes gerados por tools/export_unity.py (Resources/Models/human.bytes e weapons.bytes).
public class StoredMesh
{
    public string name;
    public Mesh mesh;
    public List<string> materialNames = new List<string>();
    public List<Color> materialColors = new List<Color>();
    public bool hasColors, hasUV2, skinned;
    public Vector3[] vertices;
    public int[] triangles;     // todos os triângulos (para projeção de feridas)
}

public static class MeshStore
{
    static Dictionary<string, StoredMesh> human, weapons;

    /// Corpo: "human" (original) ou "human_v2" (male base mesh CC0 retargetado; ver tools/build_human_v2.py).
    /// Escolha: argumento --body=v2 ou PlayerPrefs "corpo_v2" (menu O); o original continua intacto.
    public static bool UseV2 { get; private set; }
    static string HumanResource => UseV2 ? "Models/human_v2" : "Models/human";

    public static void SelectBody(bool v2)
    {
        if (v2 == UseV2 && human != null) return;
        UseV2 = v2;
        human = null;
    }

    public static StoredMesh Human(string name)
    {
        if (human == null) human = Load(HumanResource);
        return human.TryGetValue(name, out var m) ? m : null;
    }

    public static StoredMesh Weapon(string id)
    {
        if (weapons == null) weapons = Load("Models/weapons");
        return weapons.TryGetValue(id, out var m) ? m : null;
    }

    public static bool HumanAvailable
    {
        get { if (human == null) human = Load(HumanResource); return human.Count > 0; }
    }

    static string ReadStr(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));

    static Dictionary<string, StoredMesh> Load(string res)
    {
        var dict = new Dictionary<string, StoredMesh>();
        var ta = Resources.Load<TextAsset>(res);
        if (ta == null)
        {
            Debug.LogWarning("Malhas não encontradas: " + res);
            return dict;
        }
        using (var r = new BinaryReader(new MemoryStream(ta.bytes)))
        {
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "BGM1")
            {
                Debug.LogError("Formato inválido: " + res);
                return dict;
            }
            int nm = r.ReadInt32();
            for (int k = 0; k < nm; k++)
            {
                var sm = new StoredMesh { name = ReadStr(r) };
                int n = r.ReadInt32();
                int flags = r.ReadInt32();
                var pos = new Vector3[n];
                var nrm = new Vector3[n];
                for (int i = 0; i < n; i++) pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < n; i++) nrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                Color[] col = null;
                if ((flags & 2) != 0)
                {
                    col = new Color[n];
                    for (int i = 0; i < n; i++) col[i] = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                }
                Vector2[] uv2 = null;
                if ((flags & 4) != 0)
                {
                    uv2 = new Vector2[n];
                    for (int i = 0; i < n; i++) uv2[i] = new Vector2(r.ReadSingle(), 0);
                }
                BoneWeight[] bw = null;
                if ((flags & 8) != 0)
                {
                    bw = new BoneWeight[n];
                    for (int i = 0; i < n; i++)
                    {
                        byte b0 = r.ReadByte(), b1 = r.ReadByte(), b2 = r.ReadByte(), b3 = r.ReadByte();
                        bw[i] = new BoneWeight
                        {
                            boneIndex0 = b0, boneIndex1 = b1, boneIndex2 = b2, boneIndex3 = b3,
                            weight0 = r.ReadSingle(), weight1 = r.ReadSingle(), weight2 = r.ReadSingle(), weight3 = r.ReadSingle(),
                        };
                    }
                }
                int nsub = r.ReadInt32();
                var subs = new List<int[]>();
                var all = new List<int>();
                for (int s = 0; s < nsub; s++)
                {
                    sm.materialNames.Add(ReadStr(r));
                    sm.materialColors.Add(new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                    int ni = r.ReadInt32();
                    var idx = new int[ni];
                    for (int i = 0; i < ni; i++) idx[i] = r.ReadInt32();
                    subs.Add(idx);
                    all.AddRange(idx);
                }
                var mesh = new Mesh { name = sm.name };
                if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = pos;
                mesh.normals = nrm;
                if (col != null) mesh.colors = col;
                if (uv2 != null) mesh.uv2 = uv2;
                if (bw != null) mesh.boneWeights = bw;
                mesh.subMeshCount = subs.Count;
                for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
                mesh.RecalculateBounds();
                sm.mesh = mesh;
                sm.hasColors = col != null;
                sm.hasUV2 = uv2 != null;
                sm.skinned = bw != null;
                sm.vertices = pos;
                sm.triangles = all.ToArray();
                dict[sm.name] = sm;
            }
        }
        return dict;
    }

    /// Acabamento PBR por nome de material (equivale ao Game._tune_materials do Godot)
    static readonly (string key, float metal, float rough)[] FINISH =
    {
        ("metal", 0.9f, 0.32f), ("steel", 0.9f, 0.28f), ("gold", 1f, 0.3f), ("glass", 0f, 0.05f),
        ("wood", 0f, 0.62f), ("black", 0.25f, 0.55f), ("grey", 0.4f, 0.45f), ("green", 0f, 0.7f), ("white", 0f, 0.8f),
    };
    static readonly Dictionary<string, Material[]> weaponMats = new Dictionary<string, Material[]>();

    public static Material[] WeaponMaterials(StoredMesh sm, string hide = null)
    {
        string key = sm.name + "|" + hide;
        if (weaponMats.TryGetValue(key, out var cached)) return cached;
        var mats = new Material[sm.materialNames.Count];
        for (int i = 0; i < mats.Length; i++)
        {
            string nm = sm.materialNames[i].ToLowerInvariant();
            if (hide != null && nm.Contains(hide.ToLowerInvariant()))
            {
                mats[i] = Mats.FadeMat(new Color(0, 0, 0, 0));
                continue;
            }
            float metal = 0f, rough = 0.6f;
            foreach (var f in FINISH)
                if (nm.Contains(f.key)) { metal = f.metal; rough = f.rough; break; }
            if (nm.Contains("dark")) rough = Mathf.Min(1f, rough + 0.12f);
            if (nm.Contains("light")) rough = Mathf.Max(0.05f, rough - 0.08f);
            var c = sm.materialColors[i].gamma;      // as cores do glTF são lineares; o Unity (gamma) espera sRGB
            bool metalPart = nm.Contains("metal") || nm.Contains("steel");
            bool polymer = !metalPart && (nm.Contains("black") || nm.Contains("grey") || nm.Contains("main"));
            if (metalPart && c.maxColorComponent < 0.5f)
            {
                // metal de arma de fogo: aço escovado em vez de preto (sem reflexo suficiente na sala, metal escuro some)
                float k = 0.55f / Mathf.Max(0.05f, (c.r + c.g + c.b) / 3f);
                c = new Color(Mathf.Min(0.62f, c.r * k * 0.95f), Mathf.Min(0.62f, c.g * k), Mathf.Min(0.66f, c.b * k * 1.05f), 1f);
                metal = 0.6f; rough = nm.Contains("dark") ? 0.5f : 0.38f;
            }
            else if (polymer)
            {
                // polímero: cinza-chumbo fosco, com diferença entre peças (Main/MainDark/MainLight)
                float g = nm.Contains("dark") ? 0.26f : nm.Contains("light") ? 0.46f : 0.34f;
                c = new Color(g, g, g * 1.06f, 1f); metal = 0.05f; rough = 0.55f;
            }
            else if (nm.Contains("wood")) c = new Color(Mathf.Min(1f, c.r * 1.5f), Mathf.Min(1f, c.g * 1.5f), Mathf.Min(1f, c.b * 1.5f), 1f);
            mats[i] = Mats.Mat(c, rough, metal);
        }
        weaponMats[key] = mats;
        return mats;
    }

    /// Projeta um ponto local na superfície da pele (raio de fora para dentro ao longo da normal).
    public static bool ProjectOnSkin(string partId, Vector3 localPos, Vector3 localNormal, out Vector3 pos, out Vector3 normal)
    {
        pos = localPos; normal = localNormal;
        var sm = Human("skin_" + partId);
        if (sm == null) return false;
        var n = localNormal.normalized;
        var o = localPos + n * 0.12f;
        var d = -n;
        var v = sm.vertices;
        var t = sm.triangles;
        float best = float.MaxValue;
        Vector3 bn = Vector3.zero;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (Mathf.Abs(det) < 1e-9f) continue;
            float inv = 1f / det;
            Vector3 tv = o - a;
            float uu = Vector3.Dot(tv, pv) * inv;
            if (uu < 0 || uu > 1) continue;
            Vector3 qv = Vector3.Cross(tv, e1);
            float vv = Vector3.Dot(d, qv) * inv;
            if (vv < 0 || uu + vv > 1) continue;
            float dist = Vector3.Dot(e2, qv) * inv;
            if (dist > 0 && dist < best)
            {
                best = dist;
                bn = Vector3.Cross(e1, e2).normalized;
            }
        }
        if (best == float.MaxValue) return false;
        var hp = o + d * best;
        if ((hp - localPos).magnitude > 0.1f) return false;
        pos = hp;
        normal = Vector3.Dot(bn, n) < 0 ? -bn : bn;
        return true;
    }
}
