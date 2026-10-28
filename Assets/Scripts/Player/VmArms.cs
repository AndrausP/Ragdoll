using System.Collections.Generic;
using UnityEngine;

/// Braço do viewmodel recortado da pele contínua do corpo (ombro -> mão, sem costura no punho/cotovelo), com ossos
/// braço/antebraço/mão e um blend shape "punho" que fecha os dedos em arco em volta de um cabo (mesma conta do
/// grip_hand() do tools/build_human.py, aplicada na malha densa do corpo).
public static class VmArms
{
    // O corpo original tem a palma voltada para a frente e os nós dos dedos a 1,2 cm abaixo do centro da mão; o corpo v2
    // (male base mesh) tem a palma de lado e os dedos mais longos (nós a 2,6 cm). Ver docs/corpo-v2.md.
    public static float GRIP_R => MeshStore.UseV2 ? 0.022f : 0.021f;
    public static float KNUCKLE_Y => MeshStore.UseV2 ? -0.026f : -0.012f;

    public class ArmMesh
    {
        public Mesh mesh;
        public Vector3 gripCenter;       // centro do canal da mão fechada (espaço local da mão)
        public string[] parts;           // ossos: braço, antebraço, mão
    }

    static readonly Dictionary<string, ArmMesh> cache = new Dictionary<string, ArmMesh>();

    public static ArmMesh Get(string side)
    {
        if (cache.TryGetValue(side, out var am)) return am;
        cache[side] = am = Build(side);
        return am;
    }

    static ArmMesh Build(string side)
    {
        var sm = MeshStore.Human("body");
        if (sm == null || !sm.skinned) return null;
        var src = sm.mesh;
        var defs = Anatomy.Parts();
        var parts = new[] { "upper_arm_" + side, "forearm_" + side, "hand_" + side };
        var pidx = new int[3];
        for (int i = 0; i < 3; i++) pidx[i] = Anatomy.OrderIndex(parts[i]);
        var V = sm.vertices; var N = src.normals; var Cl = src.colors; var uv2 = src.uv2; var BW = src.boneWeights;
        var T = sm.triangles;
        var map = new Dictionary<int, int>();
        var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var cols = new List<Color>(); var bws = new List<BoneWeight>();
        var tris = new List<int>();
        bool Mine(int vi)
        {
            int p = Mathf.Clamp((int)(uv2[vi].x * 32f), 0, 16);
            return p == pidx[0] || p == pidx[1] || p == pidx[2];
        }
        for (int i = 0; i < T.Length; i += 3)
        {
            if (!Mine(T[i]) || !Mine(T[i + 1]) || !Mine(T[i + 2])) continue;
            for (int k = 0; k < 3; k++)
            {
                int vi = T[i + k];
                if (!map.TryGetValue(vi, out int ni))
                {
                    ni = verts.Count; map[vi] = ni;
                    verts.Add(V[vi]); nrm.Add(N[vi]);
                    cols.Add(Cl != null && Cl.Length > vi ? Cl[vi] : Color.white);
                    bws.Add(Remap(BW[vi], pidx));
                }
                tris.Add(ni);
            }
        }
        // blend shape "punho" no espaço local da mão (Godot: y para cima, palma em +z)
        var hPos = defs[parts[2]].pos;
        float sgn = side == "l" ? 1f : -1f;
        // corpo v2: gira o antebraço (supinação) até a palma ficar voltada para +z, como o blend shape espera
        if (MeshStore.UseV2)
        {
            var elb = defs[parts[1]].joint.anchor; var wr = defs[parts[2]].joint.anchor;
            var axis = (wr - elb).normalized; float len = (wr - elb).magnitude;
            for (int i = 0; i < verts.Count; i++)
            {
                float t = Mathf.Clamp01(Vector3.Dot(verts[i] - elb, axis) / len);
                var rot = Quaternion.AngleAxis(-sgn * 90f * Mathf.SmoothStep(0f, 1f, t), axis);
                verts[i] = elb + rot * (verts[i] - elb);
                nrm[i] = rot * nrm[i];
            }
        }
        float yoff = MeshStore.UseV2 ? 0.014f : 0f;     // faixa do polegar acompanha a posição dos nós dos dedos
        int n = verts.Count;
        var P = new Vector3[n];
        var wt = new float[n];
        for (int i = 0; i < n; i++)
        {
            P[i] = verts[i] - hPos;
            float handW = bws[i].boneIndex0 == 2 ? bws[i].weight0 : 0f;
            if (handW < 0.5f) { wt[i] = -1f; continue; }      // só a mão
            var p = P[i];
            // polegar: lado de fora da mão, acima da ponta dos dedos, abaixo do punho
            wt[i] = Mathf.Clamp01((p.x * sgn - 0.008f) / 0.014f) * Mathf.Clamp01((p.y + yoff + 0.07f) / 0.02f) * Mathf.Clamp01((0.045f - p.y - yoff) / 0.025f);
        }
        var zs = new List<float>(); var xs = new List<float>();
        for (int i = 0; i < n; i++)
        {
            if (wt[i] < 0f) continue;
            if (P[i].y < KNUCKLE_Y && P[i].y > KNUCKLE_Y - 0.015f && wt[i] < 0.5f) zs.Add(P[i].z);
            if (P[i].y < KNUCKLE_Y - 0.01f && wt[i] < 0.5f) xs.Add(P[i].x);
        }
        zs.Sort(); xs.Sort();
        float zc = zs.Count > 0 ? zs[zs.Count / 2] : 0.01f;
        float xc = xs.Count > 0 ? xs[xs.Count / 2] : 0f;
        var dv = new Vector3[n]; var dn = new Vector3[n];
        var tbase = new Vector3(0.012f * sgn, -0.005f, zc);
        for (int i = 0; i < n; i++)
        {
            if (wt[i] < 0f) continue;
            var p = P[i]; var q = p; var nn = nrm[i];
            if (p.y < KNUCKLE_Y)
            {
                // dedo em arco: a distância ao longo do dedo vira ângulo
                float s = KNUCKLE_Y - p.y;
                float th = s / GRIP_R * 0.82f * (1f - wt[i]);
                float r = GRIP_R - (p.z - zc);
                float c = Mathf.Cos(th), si = Mathf.Sin(th);
                q.y = KNUCKLE_Y - r * si;
                q.z = zc + GRIP_R - r * c;
                nn = new Vector3(nn.x, c * nn.y + si * nn.z, -si * nn.y + c * nn.z);
            }
            if (wt[i] > 0f)
            {
                // polegar gira em torno do eixo vertical da base, para a frente da palma
                float a = 0.7f * wt[i], c = Mathf.Cos(a), si = Mathf.Sin(a);
                var tp = q - tbase;
                q = tbase + new Vector3(c * tp.x - sgn * si * tp.z, tp.y, sgn * si * tp.x + c * tp.z);
                nn = new Vector3(c * nn.x - sgn * si * nn.z, nn.y, sgn * si * nn.x + c * nn.z);
            }
            dv[i] = q - p;
            dn[i] = nn - nrm[i];
        }
        var m = new Mesh { name = "vm_braco_" + side };
        if (n > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts); m.SetNormals(nrm); m.SetColors(cols);
        m.SetTriangles(tris, 0);
        m.boneWeights = bws.ToArray();
        m.bindposes = new[] { Matrix4x4.Translate(-defs[parts[0]].pos), Matrix4x4.Translate(-defs[parts[1]].pos), Matrix4x4.Translate(-hPos) };
        m.AddBlendShapeFrame("punho", 100f, dv, dn, null);
        m.RecalculateBounds();
        return new ArmMesh { mesh = m, gripCenter = new Vector3(xc, KNUCKLE_Y, zc + GRIP_R), parts = parts };
    }

    /// Pesos do corpo -> 3 ossos (braço 0, antebraço 1, mão 2). Peso de outra parte (peito, perto do ombro) vai para o braço.
    static BoneWeight Remap(BoneWeight b, int[] pidx)
    {
        var acc = new float[3];
        void Add(int bone, float w)
        {
            if (w <= 0f) return;
            int k = bone == pidx[1] ? 1 : bone == pidx[2] ? 2 : 0;
            acc[k] += w;
        }
        Add(b.boneIndex0, b.weight0); Add(b.boneIndex1, b.weight1); Add(b.boneIndex2, b.weight2); Add(b.boneIndex3, b.weight3);
        float tot = acc[0] + acc[1] + acc[2];
        if (tot <= 0f) { acc[0] = 1f; tot = 1f; }
        var idx = new[] { 0, 1, 2 };
        System.Array.Sort(idx, (x, y) => acc[y].CompareTo(acc[x]));
        return new BoneWeight
        {
            boneIndex0 = idx[0], weight0 = acc[idx[0]] / tot,
            boneIndex1 = idx[1], weight1 = acc[idx[1]] / tot,
            boneIndex2 = idx[2], weight2 = acc[idx[2]] / tot,
        };
    }
}
