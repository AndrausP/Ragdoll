using UnityEngine;

/// Aparência individual do boneco (corpo v2/v3): tom de pele, camisa, short, cabelo e olhos.
/// Aplicada nas cores de vértice de cada malha (corpo e peças separadas), então um membro arrancado
/// mantém a roupa e a pele do dono. O corpo original não usa isto.
///
/// Convenção do alfa nas cores de vértice de human_v2.bytes (ver tools/build_human_v2.py):
///   1,00 pele ou camisa | 0,50 short | 0,25 íris | 0,90 olho/pupila (não é pele nem roupa)
public class Appearance
{
    public Color skinTint, shirt, shorts, hair, iris;
    public bool bald;
    public float sheen;            // brilho da pele (suor / oleosidade)
    // formato do corpo (só visual; a física não muda) e do rosto
    public float girth = 1f, headScale = 1f, jaw, neckThick = 1f;
    public float beard;            // 0 barbeado, ~0.5 barba por fazer, ~0.9 barba cheia
    // postura e jeito de andar (usados pela animação em pé)
    public float slouch, headTilt, gaitMul = 1f;

    // tons de pele: multiplicam a cor base (0,60 0,40 0,30)
    static readonly Color[] SKIN = {
        new Color(1.14f, 1.06f, 1.03f), new Color(1.06f, 1.00f, 0.97f), new Color(0.98f, 0.92f, 0.88f),
        new Color(0.94f, 0.86f, 0.74f), new Color(0.80f, 0.70f, 0.62f), new Color(0.66f, 0.54f, 0.46f),
        new Color(0.50f, 0.40f, 0.36f), new Color(0.36f, 0.29f, 0.27f),
    };
    static readonly float[] SKIN_W = { 1.2f, 2f, 2f, 1.5f, 1.2f, 1f, 0.8f, 0.5f };
    static readonly Color[] SHIRT = {
        new Color(0.30f, 0.30f, 0.25f), new Color(0.62f, 0.60f, 0.55f), new Color(0.06f, 0.06f, 0.07f),
        new Color(0.08f, 0.10f, 0.22f), new Color(0.35f, 0.07f, 0.07f), new Color(0.18f, 0.22f, 0.10f),
        new Color(0.45f, 0.34f, 0.08f), new Color(0.22f, 0.14f, 0.09f), new Color(0.07f, 0.25f, 0.25f),
        new Color(0.42f, 0.42f, 0.45f),
    };
    static readonly Color[] SHORTS = {
        new Color(0.05f, 0.05f, 0.10f), new Color(0.35f, 0.30f, 0.18f), new Color(0.04f, 0.04f, 0.04f),
        new Color(0.20f, 0.20f, 0.20f), new Color(0.12f, 0.15f, 0.07f), new Color(0.10f, 0.16f, 0.30f),
    };
    static readonly Color[] HAIR = {
        new Color(0.03f, 0.025f, 0.025f), new Color(0.08f, 0.05f, 0.03f), new Color(0.18f, 0.11f, 0.06f),
        new Color(0.50f, 0.38f, 0.18f), new Color(0.40f, 0.40f, 0.40f), new Color(0.30f, 0.09f, 0.04f),
    };
    static readonly float[] HAIR_W = { 3f, 3f, 2f, 1f, 0.7f, 0.5f };
    static readonly Color[] IRIS = {
        new Color(0.20f, 0.12f, 0.06f), new Color(0.25f, 0.18f, 0.08f), new Color(0.20f, 0.35f, 0.55f),
        new Color(0.20f, 0.40f, 0.25f), new Color(0.40f, 0.42f, 0.45f),
    };

    static T Pick<T>(System.Random r, T[] a, float[] w = null)
    {
        if (w == null) return a[r.Next(a.Length)];
        float tot = 0; foreach (var x in w) tot += x;
        float t = (float)r.NextDouble() * tot;
        for (int i = 0; i < a.Length; i++) { t -= w[i]; if (t <= 0) return a[i]; }
        return a[a.Length - 1];
    }

    /// Determinística pelo nome (o mesmo "Boneco #n" sempre tem a mesma cara).
    public static Appearance Roll(string seedText)
    {
        int h = 17;
        foreach (char c in seedText) h = h * 31 + c;
        var r = new System.Random(h);
        var a = new Appearance
        {
            skinTint = Pick(r, SKIN, SKIN_W), shirt = Pick(r, SHIRT), shorts = Pick(r, SHORTS),
            hair = Pick(r, HAIR, HAIR_W), iris = Pick(r, IRIS), bald = r.NextDouble() < 0.12,
            sheen = Mathf.Lerp(0.1f, 0.5f, (float)r.NextDouble()),
        };
        a.girth = Mathf.Lerp(0.97f, 1.045f, (float)r.NextDouble());
        a.headScale = Mathf.Lerp(0.95f, 1.06f, (float)r.NextDouble());
        a.jaw = Mathf.Lerp(-0.05f, 0.09f, (float)r.NextDouble());
        a.neckThick = Mathf.Lerp(0.92f, 1.10f, (float)r.NextDouble());
        double bd = r.NextDouble();
        a.beard = bd < 0.45 ? 0f : bd < 0.80 ? Mathf.Lerp(0.35f, 0.65f, (float)r.NextDouble()) : Mathf.Lerp(0.8f, 1f, (float)r.NextDouble());
        a.slouch = Mathf.Lerp(0f, 0.16f, Mathf.Pow((float)r.NextDouble(), 1.6f));
        a.headTilt = Mathf.Lerp(-0.09f, 0.09f, (float)r.NextDouble());
        a.gaitMul = Mathf.Lerp(0.9f, 1.12f, (float)r.NextDouble());
        // pequena variação individual dentro do tom
        float v = 1f + ((float)r.NextDouble() - 0.5f) * 0.12f;
        a.skinTint = new Color(a.skinTint.r * v, a.skinTint.g * v, a.skinTint.b * v);
        return a;
    }

    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a)));

    /// Deforma a malha do corpo inteiro (espaço do mundo em repouso): cabeça, mandíbula, pescoço e tronco.
    /// Os deslocamentos são de poucos milímetros e suaves pelos pesos dos ossos, para os decalques de ferida não descolarem.
    public void Shape(Mesh mesh)
    {
        var v = mesh.vertices; var bw = mesh.boneWeights;
        var head = Anatomy.Parts()["head"].pos;
        for (int i = 0; i < v.Length; i++)
        {
            float wH = 0, wN = 0, wT = 0;
            Acc(bw[i].boneIndex0, bw[i].weight0, ref wH, ref wN, ref wT); Acc(bw[i].boneIndex1, bw[i].weight1, ref wH, ref wN, ref wT);
            Acc(bw[i].boneIndex2, bw[i].weight2, ref wH, ref wN, ref wT); Acc(bw[i].boneIndex3, bw[i].weight3, ref wH, ref wN, ref wT);
            var p = v[i];
            if (wT > 0f)
            {
                float g = Mathf.Lerp(1f, girth, wT);
                p.x *= g; p.z = -0.01f + (p.z + 0.01f) * g;
            }
            if (wN > 0f)
            {
                float g = Mathf.Lerp(1f, neckThick, wN);
                p.x *= g; p.z = head.z + (p.z - head.z) * g;
            }
            if (wH > 0f)
            {
                var d = p - head;
                float lowFace = 1f - Smooth(1.56f, 1.64f, p.y);                    // 1 no queixo, 0 no alto da cabeça
                d.x *= 1f + jaw * lowFace;                                          // largura da mandíbula
                d *= Mathf.Lerp(1f, headScale, wH);
                p = head + d;
            }
            v[i] = p;
        }
        mesh.vertices = v;
        mesh.RecalculateBounds();
    }

    // índices de osso: 4 cabeça, 3 pescoço, 0-2 pelve/abdômen/tórax
    static void Acc(int b, float w, ref float wH, ref float wN, ref float wT)
    {
        if (b == 4) wH += w; else if (b == 3) wN += w; else if (b <= 2) wT += w;
    }

    /// Mesma deformação para a peça rígida de cabeça/pescoço (cabeça arrancada), em espaço local da parte.
    public void ShapePart(string partId, Mesh mesh)
    {
        if (partId != "head" && partId != "neck") return;
        var v = mesh.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            if (partId == "head")
            {
                float lowFace = 1f - Smooth(-0.07f, 0.01f, v[i].y);
                v[i].x *= 1f + jaw * lowFace;
                v[i] *= headScale;
            }
            else { v[i].x *= neckThick; v[i].z *= neckThick; }
        }
        mesh.vertices = v;
        mesh.RecalculateBounds();
    }

    /// Recolore cores de vértice no formato do human_v2 (devolve um array novo).
    /// verts/offset (opcional): posições e deslocamento para o mundo, usados para a barba no rosto.
    public Color[] Recolor(Color[] src, Vector3[] verts = null, Vector3 offset = default)
    {
        var o = new Color[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            var c = src[i]; float al = c.a;
            if (al > 0.2f && al < 0.3f) { o[i] = new Color(iris.r, iris.g, iris.b, al); continue; }     // íris
            if (al > 0.85f && al < 0.95f) { o[i] = c; continue; }                                         // olho/pupila
            float lum = (c.r + c.g + c.b) / 3f;
            if (ClothLayer.IsCloth(c))
            {
                bool sh = al < 0.75f;
                var b = sh ? shorts : shirt;
                float k = lum / (sh ? 0.067f : 0.283f);
                o[i] = new Color(b.r * k, b.g * k, b.b * k, al);
            }
            else
            {
                var sc = new Color(Mathf.Clamp01(c.r * skinTint.r), Mathf.Clamp01(c.g * skinTint.g), Mathf.Clamp01(c.b * skinTint.b), al);
                if (beard > 0.01f && verts != null)
                {
                    var p = verts[i] + offset;
                    // máscara do rosto: queixo, maxilar e bochechas (abaixo do nariz), à frente do crânio, sem passar das orelhas
                    float m = Smooth(1.595f, 1.52f, p.y) * Smooth(1.40f, 1.48f, p.y) * Smooth(-0.02f, 0.045f, p.z) * (1f - Smooth(0.075f, 0.105f, Mathf.Abs(p.x)));
                    float grain = 0.75f + 0.5f * Mathf.PerlinNoise(p.x * 260f, p.y * 260f);      // pelos irregulares
                    float k = Mathf.Clamp01(m * beard * grain * 0.85f);
                    var hc = new Color(hair.r * 0.8f + 0.04f, hair.g * 0.8f + 0.035f, hair.b * 0.8f + 0.03f, al);
                    sc = Color.Lerp(sc, hc, k);
                }
                o[i] = sc;
            }
        }
        return o;
    }
}
