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
        // pequena variação individual dentro do tom
        float v = 1f + ((float)r.NextDouble() - 0.5f) * 0.12f;
        a.skinTint = new Color(a.skinTint.r * v, a.skinTint.g * v, a.skinTint.b * v);
        return a;
    }

    /// Recolore cores de vértice no formato do human_v2 (devolve um array novo).
    public Color[] Recolor(Color[] src)
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
            else o[i] = new Color(Mathf.Clamp01(c.r * skinTint.r), Mathf.Clamp01(c.g * skinTint.g), Mathf.Clamp01(c.b * skinTint.b), al);
        }
        return o;
    }
}
