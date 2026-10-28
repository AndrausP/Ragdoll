using System.Collections.Generic;
using UnityEngine;

/// Roupa como casca separada da pele: os vértices de cor "tecido" da malha do corpo viram uma segunda malha
/// um pouco acima da superfície, com os mesmos ossos. A pele embaixo fica com cor de pele.
public static class ClothLayer
{
    public const float THICK = 0.006f;     // m acima da pele

    // pele é bem mais vermelha que azul (mesmo critério do shader BG_Skin)
    public static bool IsCloth(Color c) => c.r - c.b < 0.15f;

    /// Devolve a malha da roupa (ou null se não houver) e pinta de pele os vértices de roupa em `body`.
    public static Mesh Extract(Mesh body, Color skin)
    {
        var cols = body.colors;
        if (cols == null || cols.Length == 0) return null;
        int n = body.vertexCount;
        var cloth = new bool[n];
        for (int i = 0; i < n; i++) cloth[i] = IsCloth(cols[i]);

        var tris = body.triangles;
        var keep = new List<int>();
        for (int i = 0; i < tris.Length; i += 3)
            if (cloth[tris[i]] && cloth[tris[i + 1]] && cloth[tris[i + 2]])
            { keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]); }
        if (keep.Count == 0) return null;

        // reaproveita todos os vértices (os atributos ficam alinhados com os pesos dos ossos) e só desloca os de roupa
        var v = body.vertices; var nrm = body.normals;
        for (int i = 0; i < n; i++) v[i] += nrm[i] * THICK;
        var m = new Mesh { name = body.name + "_roupa", indexFormat = body.indexFormat };
        m.vertices = v; m.normals = nrm; m.colors = cols;
        m.boneWeights = body.boneWeights; m.bindposes = body.bindposes;
        var uv = new List<Vector2>(); body.GetUVs(1, uv); if (uv.Count > 0) m.SetUVs(1, uv);
        var u3 = new List<Vector3>(); body.GetUVs(2, u3); if (u3.Count > 0) m.SetUVs(2, u3);
        var u4 = new List<Vector3>(); body.GetUVs(3, u4); if (u4.Count > 0) m.SetUVs(3, u4);
        m.SetTriangles(keep, 0);
        m.RecalculateBounds();

        // pele embaixo da roupa: sem a cor do tecido
        for (int i = 0; i < n; i++) if (cloth[i]) cols[i] = skin;
        body.colors = cols;
        return m;
    }
}
