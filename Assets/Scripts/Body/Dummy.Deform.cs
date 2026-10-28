using System.Collections.Generic;
using UnityEngine;

/// Deformação procedural da pele e da roupa onde o golpe pegou: afundamento na entrada, lábio na saída, hematoma, sangue
/// no tecido e, nos ferimentos grandes, um buraco de verdade (faces removidas + funil de carne). Tudo no espaço de repouso
/// da malha, então acompanha o corpo quando ele dobra.
public partial class Dummy
{
    bool meshVertsDirty, meshTopoDirty;

    void EnsureData()
    {
        if (bodyData != null) return;
        bodyData = MeshData.From(skinBody.sharedMesh);
        if (skinCloth) clothData = MeshData.From(skinCloth.sharedMesh);
    }

    void FlushMesh()
    {
        if (!meshVertsDirty && !meshTopoDirty) return;
        if (meshTopoDirty)
        {
            var bind = skinBind.ToArray();
            bodyData.Apply(skinBody.sharedMesh, bind);
            if (clothData != null) clothData.Apply(skinCloth.sharedMesh, bind);
        }
        else
        {
            bodyData.ApplyVerts(skinBody.sharedMesh);
            if (clothData != null) clothData.ApplyVerts(skinCloth.sharedMesh);
        }
        meshVertsDirty = meshTopoDirty = false;
    }

    void Dent(MeshData d, Vector3 at, Vector3 n, float r, float depth, Color tint, float tintAmount, bool cloth)
    {
        float r2 = r * 1.4f; r2 *= r2;
        for (int i = 0; i < d.Count; i++)
        {
            var off = d.v[i] - at;
            float dd2 = off.sqrMagnitude;
            if (dd2 > r2) continue;
            if (Vector3.Dot(d.n[i], n) < -0.2f) continue;           // lado de trás do membro / outro membro encostado
            float dist = Mathf.Sqrt(dd2);
            float t = 1f - (dist / r) * (dist / r);
            if (t > 0f && Mathf.Abs(depth) > 1e-5f)
            {
                float disp = depth * t * t * (cloth ? 1.05f : 1f);
                d.v[i] -= n * disp;
                var radial = Vector3.ProjectOnPlane(off, n);
                if (radial.sqrMagnitude > 1e-8f)
                {
                    float slope = depth * (-4f * t * dist / (r * r));
                    var nn = d.n[i] + radial.normalized * slope * 6f;
                    d.n[i] = nn.normalized;
                }
            }
            if (d.hasColors && tintAmount > 0f)
            {
                float k = Mathf.Pow(Mathf.Clamp01(1f - dist / (r * 1.3f)), 1.4f) * tintAmount;
                if (k > 0f) { var c = d.c[i]; d.c[i] = new Color(Mathf.Lerp(c.r, tint.r, k), Mathf.Lerp(c.g, tint.g, k), Mathf.Lerp(c.b, tint.b, k), c.a); }
            }
        }
    }

    /// Chamado a cada ferida (entrada e saída) por ApplyPenetration.
    public void DeformSkin(DummyPart p, Wound w, Prof prof, float energy)
    {
        if (skinBody == null || w == null) return;
        EnsureData();
        var at = w.localPos + p.def.pos;
        var n = w.localNormal.sqrMagnitude > 1e-6f ? w.localNormal.normalized : Vector3.up;
        float r, depth, tintAmt = 0.9f; bool crater = false; float craterR = 0f;
        var tint = new Color(0.34f, 0.025f, 0.035f);
        switch (w.kind)
        {
            case "saída": r = 0.05f; depth = -0.007f; if (energy > 90f) { crater = true; craterR = 0.014f + Mathf.Min(energy / 2500f, 0.03f); } break;
            case "machado": r = 0.055f; depth = 0.013f; crater = energy > 120f; craterR = 0.03f; break;
            case "corte": r = 0.03f; depth = 0.003f; break;
            case "perfuração": r = 0.022f; depth = 0.007f; break;
            case "contusão": r = 0.075f; depth = 0.018f; tint = new Color(0.36f, 0.18f, 0.30f); tintAmt = 0.6f; break;
            case "queimadura": r = 0.06f; depth = 0f; tint = new Color(0.16f, 0.07f, 0.05f); tintAmt = 0.85f; break;
            case "estilhaço": r = 0.018f; depth = 0.005f; break;
            default:
                r = 0.03f + prof.radius * 2f; depth = 0.005f + Mathf.Min(energy / 700f, 1f) * 0.008f;
                crater = energy > 170f && prof.radius >= 0.0045f; craterR = 0.012f + prof.radius * 1.5f; break;
        }
        Dent(bodyData, at, n, r, depth, tint, tintAmt, false);
        if (clothData != null) Dent(clothData, at, n, r, depth, new Color(0.20f, 0.05f, 0.06f), 0.75f, true);
        meshVertsDirty = true;
        if (crater && craterR > 0f)
        {
            var opt = new CutOptions { capDepth = Mathf.Min(0.03f, craterR * 1.1f), irregular = 1f };
            MeshCut.Crater(bodyData, at, n, craterR, opt, true);
            if (clothData != null) MeshCut.Crater(clothData, at, n, craterR * 1.15f, new CutOptions(), false);
            meshTopoDirty = true;
        }
    }
}
