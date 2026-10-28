using System.Collections.Generic;
using UnityEngine;

/// Sangue que escorre pela pele e pela roupa: cada ferida que ainda sangra desce um filete na direção da gravidade (no
/// mundo, com o corpo na pose atual) e mancha a cor de vértice ao longo do caminho. Substitui o tom vermelho da parte
/// inteira: um tiro na perna suja só a trilha do sangue.
public partial class Dummy
{
    Mesh bakeBody, bakeCloth;
    readonly List<Vector3> bakeV = new List<Vector3>(), bakeC = new List<Vector3>();
    static readonly List<int> stainBuf = new List<int>(4096);
    static readonly Color FRESH = new Color(0.52f, 0.02f, 0.03f), OLD = new Color(0.27f, 0.012f, 0.02f), SOAK = new Color(0.22f, 0.035f, 0.065f);

    void FlowStep(float dt)
    {
        if (skinBody == null || dead && Time.timeScale == 0f) return;
        List<Wound> active = null;
        foreach (var p in ValidParts())
        {
            foreach (var w in p.wounds)
            {
                if (w.flowDone) continue;
                float rate = w.ExternalRate();
                if (rate < 0.25f) { if (w.age > 4f) w.flowDone = true; continue; }
                float speed = Mathf.Min(0.012f + rate * 0.0012f, 0.05f);
                float maxLen = Mathf.Min(0.16f + rate * 0.02f, 0.65f);
                w.flow = Mathf.Min(maxLen, w.flow + speed * dt);
                if (w.flow >= maxLen) w.flowDone = true;           // pinta uma última vez e para
                if (active == null) active = new List<Wound>();
                active.Add(w);
            }
        }
        if (active == null) return;
        EnsureData();
        if (!bakeBody) bakeBody = new Mesh();
        skinBody.BakeMesh(bakeBody);
        bakeBody.GetVertices(bakeV);
        bool clothOk = false;
        if (skinCloth && clothData != null)
        {
            if (!bakeCloth) bakeCloth = new Mesh();
            skinCloth.BakeMesh(bakeCloth);
            bakeCloth.GetVertices(bakeC);
            clothOk = bakeC.Count == clothData.Count;
        }
        if (bakeV.Count != bodyData.Count) return;
        foreach (var w in active)
        {
            var p = w.part;
            var wp = p.Xform.Apply(w.localPos);
            var wr = w.localPos + p.def.pos;
            float reach = w.flow + 0.06f;
            var blood = Color.Lerp(FRESH, OLD, Mathf.Clamp01(w.age / 90f));
            Stain(bodyData, bakeV, skinBody.transform, wp, wr, w, reach, blood, 0.8f, 1f);
            if (clothOk) Stain(clothData, bakeC, skinCloth.transform, wp, wr, w, reach, Color.Lerp(SOAK, blood * 0.55f, 0.35f), 0.85f, 1.8f);
        }
        meshVertsDirty = true;
    }

    /// Mancha os vértices que estão abaixo do ferimento (em coordenadas do mundo) dentro de um filete que alarga com a descida.
    static void Stain(MeshData d, List<Vector3> baked, Transform tf, Vector3 wp, Vector3 wr, Wound w, float reach, Color blood, float amount, float widen)
    {
        if (!d.hasColors) return;
        float reach2 = reach * reach;
        d.Query(wr, reach, stainBuf);
        for (int q = 0; q < stainBuf.Count; q++)
        {
            int i = stainBuf[q];
            if ((d.v[i] - wr).sqrMagnitude > reach2) continue;
            if (Vector3.Dot(d.n[i], w.localNormal) < -0.15f) continue;                // outro lado do membro
            var dl = tf.TransformPoint(baked[i]) - wp;
            float dd = -dl.y;
            if (dd < -0.012f || dd > w.flow) continue;
            float lateral = Mathf.Sqrt(dl.x * dl.x + dl.z * dl.z);
            float width = (0.009f + 0.0032f * Mathf.Sqrt(Mathf.Max(dd, 0f) * 100f)) * widen;
            if (lateral > width) continue;
            float k = Mathf.Pow(1f - lateral / width, 0.6f) * (1f - 0.45f * Mathf.Clamp01(dd / Mathf.Max(w.flow, 0.01f))) * amount;
            var c = d.c[i];
            d.c[i] = new Color(Mathf.Lerp(c.r, blood.r, k), Mathf.Lerp(c.g, blood.g, k), Mathf.Lerp(c.b, blood.b, k), c.a);
        }
    }
}
