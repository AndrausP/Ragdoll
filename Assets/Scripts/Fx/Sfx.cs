using System.Collections.Generic;
using UnityEngine;

/// Sons: gravações CC0 em Resources/Sounds/<nome>/ (várias variações, sorteadas; ver CREDITOS_SONS.txt) e,
/// para o que não tiver gravação, síntese procedural (mesmas fórmulas do sfx.gd).
public class Sfx : MonoBehaviour
{
    const int RATE = 22050;
    readonly Dictionary<string, AudioClip> streams = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, AudioClip[]> recorded = new Dictionary<string, AudioClip[]>();
    static readonly string[] RECORDED =
    {
        "pistol", "revolver", "smg", "rifle", "shotgun", "sawedoff", "sniper", "reload_pistol", "reload_rifle", "rack",
        "bone", "flesh", "sever", "gib", "swing", "slash", "bow", "bow_draw", "draw_blade", "draw_gun", "explosion",
        "step", "impact", "thunk", "dry", "cloth", "shell",
    };
    // sem gravação nem síntese própria: usa a de outro som
    static readonly Dictionary<string, string> ALIAS = new Dictionary<string, string>
    {
        { "revolver", "sniper" }, { "smg", "pistol" }, { "sawedoff", "shotgun" },
    };

    public bool Has(string name) => recorded.ContainsKey(name) || streams.ContainsKey(name);

    public AudioClip Clip(string name)
    {
        if (recorded.TryGetValue(name, out var arr)) return arr[rng.Next(arr.Length)];
        if (streams.TryGetValue(name, out var c)) return c;
        return ALIAS.TryGetValue(name, out var a) ? Clip(a) : null;
    }
    System.Random rng = new System.Random(42);
    int active;

    float N() => (float)(rng.NextDouble() * 2.0 - 1.0);
    float[] Buf(float sec) => new float[(int)(sec * RATE)];

    void Awake()
    {
        streams["pistol"] = Gun(0.45f, 28f, 95f, 0.55f);
        streams["rifle"] = Gun(0.5f, 32f, 80f, 0.7f);
        streams["shotgun"] = Gun(0.75f, 16f, 60f, 0.9f);
        streams["sniper"] = Gun(1.2f, 12f, 50f, 1f);
        streams["flesh"] = Flesh(0.16f);
        streams["gasp"] = Gasp(0.9f);
        streams["groan"] = Groan(1.1f);
        streams["bone"] = Bone();
        streams["sever"] = Sever();
        streams["gib"] = GibS();
        streams["slash"] = Swish(0.22f, 2.2f);
        streams["swing"] = Swish(0.35f, 1f);
        streams["bow"] = Twang();
        streams["thunk"] = Thunk();
        streams["explosion"] = Explosion();
        streams["beep"] = Tone(1400f, 0.07f);
        streams["click"] = Tone(2600f, 0.015f);
        streams["tourniquet"] = Ratchet();
        streams["impact"] = Impact();
        streams["mag_out"] = Mech(new[] { 0f, 0.035f }, new[] { 2200f, 900f }, 0.18f);
        streams["mag_in"] = Mech(new[] { 0f, 0.05f, 0.075f }, new[] { 1800f, 3200f, 1200f }, 0.2f);
        streams["rack"] = Mech(new[] { 0f, 0.09f, 0.16f }, new[] { 1500f, 2600f, 3400f }, 0.3f);
        streams["dry"] = Mech(new[] { 0f }, new[] { 4200f }, 0.05f);
        streams["step"] = Step();
        streams["fire"] = Crackle(0.8f);
        streams["shell"] = Mech(new[] { 0f }, new[] { 5200f }, 0.08f);
        streams["flame"] = Roar(1.2f);
        streams["throw"] = Swish(0.25f, 1.6f);
        streams["crossbow"] = CrossbowShot();
        streams["harpoon"] = HarpoonShot();
        streams["nailgun"] = NailShot();
        streams["winch"] = Winch();
        streams["crossbow_load"] = Crank(1.9f, 11);
        streams["harpoon_load"] = Crank(2.8f, 5);
        foreach (var n in RECORDED)
        {
            var arr = Resources.LoadAll<AudioClip>("Sounds/" + n);
            if (arr != null && arr.Length > 0) recorded[n] = arr;
        }
        if (Game.autotest) Debug.Log($"[SOM] {recorded.Count}/{RECORDED.Length} sons gravados carregados");
    }

    public void Play(string name, Vector3 pos, float volDb = 0f, float pitch = 1f)
    {
        var clip = Clip(name);
        if (clip == null || active > 40) return;
        var go = new GameObject("snd_" + name);
        go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = Mathf.Clamp01(U.DbToLinear(volDb) * 0.6f);
        s.pitch = pitch;
        s.spatialBlend = 1f;
        s.minDistance = 5f;
        s.maxDistance = 120f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.dopplerLevel = 0f;
        s.Play();
        active++;
        StartCoroutine(Release(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f));
    }

    public void PlayUI(string name, float volDb = 0f, float pitch = 1f)
    {
        var clip = Clip(name);
        if (clip == null) return;
        var go = new GameObject("ui_" + name);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = Mathf.Clamp01(U.DbToLinear(volDb) * 0.6f);
        s.pitch = pitch;
        s.spatialBlend = 0f;
        s.Play();
        Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    System.Collections.IEnumerator Release(GameObject go, float t)
    {
        yield return new WaitForSecondsRealtime(t);
        active--;
        if (go) Destroy(go);
    }

    // ------------------------------------------------------------------ síntese

    AudioClip Wav(float[] s, string name)
    {
        float peak = 0.0001f;
        foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
        float norm = 0.9f / peak;
        for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i] * norm, -1f, 1f);
        var c = AudioClip.Create(name, s.Length, 1, RATE, false);
        c.SetData(s, 0);
        return c;
    }

    AudioClip Gun(float sec, float decay, float thumpHz, float body)
    {
        var s = Buf(sec); float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            float crack = N() * Mathf.Exp(-t * decay * 2.5f);
            float alpha = Mathf.Clamp(0.9f - t * 2f, 0.05f, 0.9f);
            lp += (N() - lp) * alpha;
            float boom = lp * Mathf.Exp(-t * decay) * body;
            float thump = Mathf.Sin(U.TAU * thumpHz * t * (1f - t)) * Mathf.Exp(-t * decay * 0.8f) * body;
            float tail = N() * 0.08f * Mathf.Exp(-t * 4f);
            s[i] = crack * 0.9f + boom + thump * 0.8f + tail;
        }
        return Wav(s, "gun");
    }

    AudioClip Flesh(float sec)
    {
        var s = Buf(sec); float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            lp += (N() - lp) * 0.08f;
            s[i] = lp * 3f * Mathf.Exp(-t * 30f) + Mathf.Sin(U.TAU * 70f * t) * Mathf.Exp(-t * 40f) * 0.7f;
        }
        return Wav(s, "flesh");
    }

    /// Suspiro agônico: ar áspero filtrado (ataque rápido, queda lenta) com um sopro vozeado fraco por baixo.
    AudioClip Gasp(float sec)
    {
        var s = Buf(sec); float lp = 0, lp2 = 0, ph = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            float env = (1f - Mathf.Exp(-t * 22f)) * Mathf.Exp(-t * 3.4f);
            lp += (N() - lp) * 0.22f; lp2 += (lp - lp2) * 0.10f;
            float band = (lp - lp2) * 5f;                                  // faixa ~500-1800 Hz: o "chiado" da garganta
            ph += U.TAU * (150f + 25f * Mathf.Sin(t * 9f)) / RATE;
            float voice = Mathf.Sin(ph) * 0.08f * Mathf.Exp(-t * 2f);
            s[i] = (band + voice) * env * 0.9f;
        }
        return Wav(s, "gasp");
    }

    /// Gemido: vogal "ah" (formantes ~700 e 1100 Hz) sobre um tom grave com vibrato e instabilidade.
    AudioClip Groan(float sec)
    {
        var s = Buf(sec); float ph = 0, f1 = 0, f1v = 0, f2 = 0, f2v = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            float env = Mathf.Min(1f, t * 8f) * Mathf.Exp(-Mathf.Max(0f, t - 0.25f) * 2.4f);
            float f0 = 118f - 22f * t + 5f * Mathf.Sin(t * 38f) + N() * 1.5f;
            ph += U.TAU * f0 / RATE;
            float src = (Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.3f * Mathf.Sin(3f * ph)) * 0.5f + N() * 0.12f;
            // dois ressonadores (filtro de dois polos)
            f1v += (src - f1) * 0.30f; f1v *= 0.93f; f1 += f1v * 0.30f;
            f2v += (src - f2) * 0.46f; f2v *= 0.90f; f2 += f2v * 0.46f;
            s[i] = (f1 * 1.3f + f2 * 0.7f) * env * 0.9f;
        }
        return Wav(s, "groan");
    }

    AudioClip Bone()
    {
        var s = Buf(0.14f);
        for (int k = 0; k < 5; k++)
        {
            int c = rng.Next(0, (int)(0.07f * RATE) + 1);
            for (int j = 0; j < 70; j++)
                if (c + j < s.Length) s[c + j] += N() * Mathf.Exp(-j / 12f) * 1.2f;
        }
        float prev = 0;
        for (int i = 0; i < s.Length; i++) { float v = s[i]; s[i] = v - prev * 0.95f; prev = v; }
        return Wav(s, "bone");
    }

    AudioClip Sever()
    {
        var s = Buf(0.6f); float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            lp += (N() - lp) * (0.05f + 0.1f * Mathf.Abs(Mathf.Sin(t * 40f)));
            s[i] = lp * 2.5f * Mathf.Exp(-t * 6f) + N() * Mathf.Exp(-t * 60f) + Mathf.Sin(U.TAU * 55f * t) * Mathf.Exp(-t * 15f) * 0.6f;
        }
        return Wav(s, "sever");
    }

    AudioClip GibS()
    {
        var s = Buf(0.8f); float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            lp += (N() - lp) * (0.04f + 0.12f * Mathf.Abs(Mathf.Sin(t * 23f + Mathf.Sin(t * 7f))));
            s[i] = lp * 3f * Mathf.Exp(-t * 4.5f) + N() * 0.6f * Mathf.Exp(-t * 30f);
        }
        return Wav(s, "gib");
    }

    AudioClip Swish(float sec, float bright)
    {
        var s = Buf(sec); float lp = 0, lp2 = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / sec / RATE;
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 2);
            lp += (N() - lp) * Mathf.Clamp(0.05f + t * 0.3f * bright, 0.02f, 0.9f);
            lp2 += (lp - lp2) * 0.5f;
            s[i] = (lp - lp2) * env * 3f;
        }
        return Wav(s, "swish");
    }

    AudioClip Twang()
    {
        var s = Buf(0.45f); float ph = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            float f = 105f * (1f + 0.02f * Mathf.Sin(t * 40f));
            ph += f / RATE;
            float saw = (ph % 1f) * 2f - 1f;
            s[i] = saw * Mathf.Exp(-t * 9f) * 0.6f + N() * Mathf.Exp(-t * 80f) * 0.5f;
        }
        return Wav(s, "twang");
    }

    AudioClip Thunk()
    {
        var s = Buf(0.18f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            s[i] = Mathf.Sin(U.TAU * 160f * t * (1f - t * 2f)) * Mathf.Exp(-t * 28f) + N() * Mathf.Exp(-t * 90f) * 0.6f;
        }
        return Wav(s, "thunk");
    }

    AudioClip Impact()
    {
        var s = Buf(0.1f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            s[i] = N() * Mathf.Exp(-t * 70f) + Mathf.Sin(U.TAU * 900f * t) * Mathf.Exp(-t * 50f) * 0.3f;
        }
        return Wav(s, "impact");
    }

    AudioClip Explosion()
    {
        var s = Buf(2.6f); float lp = 0, lp2 = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            lp += (N() - lp) * Mathf.Clamp(0.5f * Mathf.Exp(-t * 3f), 0.01f, 0.5f);
            lp2 += (lp - lp2) * 0.1f;
            float boom = Mathf.Sin(U.TAU * 38f * t * (1f - t * 0.15f)) * Mathf.Exp(-t * 2.5f);
            s[i] = N() * Mathf.Exp(-t * 40f) * 1.2f + lp * 2.2f * Mathf.Exp(-t * 1.6f) + lp2 * 3f * Mathf.Exp(-t * 1f) + boom * 0.8f;
        }
        return Wav(s, "explosion");
    }

    AudioClip Mech(float[] times, float[] freqs, float sec)
    {
        var s = Buf(sec);
        for (int k = 0; k < times.Length; k++)
        {
            int i0 = (int)(times[k] * RATE);
            for (int i = i0; i < s.Length; i++)
            {
                float t = (float)(i - i0) / RATE;
                float env = Mathf.Exp(-t * 90f);
                if (env < 0.002f) break;
                s[i] += (N() * 0.6f + Mathf.Sin(U.TAU * freqs[k] * t) * 0.8f + Mathf.Sin(U.TAU * freqs[k] * 1.53f * t) * 0.4f) * env;
            }
        }
        return Wav(s, "mech");
    }

    AudioClip Step()
    {
        var s = Buf(0.12f); float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            lp += (N() - lp) * 0.18f;
            s[i] = (lp * 0.9f + Mathf.Sin(U.TAU * 70f * t) * 0.5f) * Mathf.Exp(-t * 38f);
        }
        return Wav(s, "step");
    }

    /// Estalos de fogo: cliques curtos aleatórios sobre um chiado grave
    AudioClip Crackle(float sec)
    {
        var s = Buf(sec); float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            lp += (N() - lp) * 0.05f;
            s[i] = lp * 0.5f;
        }
        for (int k = 0; k < 40; k++)
        {
            int c = rng.Next(0, s.Length - 200);
            float a = 0.3f + (float)rng.NextDouble();
            for (int j = 0; j < 120; j++) s[c + j] += N() * a * Mathf.Exp(-j / 18f);
        }
        return Wav(s, "crackle");
    }

    /// Ronco do lança-chamas (laço): ruído grave filtrado com pulsação lenta, começo e fim casando
    AudioClip Roar(float sec)
    {
        var s = Buf(sec); float lp = 0, lp2 = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            lp += (N() - lp) * 0.12f;
            lp2 += (lp - lp2) * 0.2f;
            s[i] = (lp2 * 2.2f + (lp - lp2) * 0.6f) * (0.8f + 0.2f * Mathf.Sin(U.TAU * 3f * t));
        }
        int f = RATE / 20;
        for (int i = 0; i < f; i++) { float w = (float)i / f; s[s.Length - f + i] = s[s.Length - f + i] * (1f - w) + s[i] * w; }
        return Wav(s, "roar");
    }

    AudioClip Tone(float hz, float sec)
    {
        var s = Buf(sec);
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            s[i] = Mathf.Sin(U.TAU * hz * t) * Mathf.Min(1f, (sec - t) * 80f);
        }
        return Wav(s, "tone");
    }

    /// Besta: estalo seco da corda + baque de madeira + zumbido curto
    AudioClip CrossbowShot()
    {
        var s = Buf(0.5f); float ph = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            ph += 180f * (1f - t * 0.6f) / RATE;
            float saw = (ph % 1f) * 2f - 1f;
            s[i] = N() * Mathf.Exp(-t * 140f) * 1.1f + saw * Mathf.Exp(-t * 14f) * 0.45f
                 + Mathf.Sin(U.TAU * 95f * t) * Mathf.Exp(-t * 30f) * 0.7f;
        }
        return Wav(s, "besta");
    }

    /// Arpão de elástico: borracha soltando (sopro grave) + metal batendo na guia
    AudioClip HarpoonShot()
    {
        var s = Buf(0.7f); float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            lp += (N() - lp) * 0.08f;
            float ring = Mathf.Sin(U.TAU * 1450f * t) * 0.5f + Mathf.Sin(U.TAU * 2310f * t) * 0.3f;
            s[i] = lp * 2.5f * Mathf.Exp(-t * 9f) + ring * Mathf.Exp(-t * 22f) * 0.5f + Mathf.Sin(U.TAU * 70f * t) * Mathf.Exp(-t * 18f) * 0.8f;
        }
        return Wav(s, "arpao");
    }

    /// Pistola de pregos: pistão pneumático (chiado curto) + batida metálica
    AudioClip NailShot()
    {
        var s = Buf(0.22f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = (float)i / RATE;
            s[i] = N() * Mathf.Exp(-t * 35f) * 0.7f + Mathf.Sin(U.TAU * 520f * t) * Mathf.Exp(-t * 60f) * 0.8f
                 + Mathf.Sin(U.TAU * 3100f * t) * Mathf.Exp(-t * 90f) * 0.3f;
        }
        return Wav(s, "prego");
    }

    /// Corda sendo recolhida: catraca rápida em laço
    AudioClip Winch()
    {
        var s = Buf(0.5f);
        for (int k = 0; k < 14; k++)
        {
            int c = (int)(k * 0.035f * RATE);
            for (int j = 0; j < 200; j++)
                if (c + j < s.Length)
                {
                    float t = (float)j / RATE;
                    s[c + j] += (N() * 0.5f + Mathf.Sin(U.TAU * 1700f * t)) * Mathf.Exp(-t * 220f);
                }
        }
        return Wav(s, "recolher");
    }

    /// Recarga com manivela/elástico: `clicks` estalos espaçados ao longo de `sec` e uma trava no fim
    AudioClip Crank(float sec, int clicks)
    {
        var s = Buf(sec);
        for (int k = 0; k < clicks; k++)
        {
            int c = (int)((0.1f + k * (sec * 0.75f / clicks)) * RATE);
            for (int j = 0; j < 400; j++)
                if (c + j < s.Length)
                {
                    float t = (float)j / RATE;
                    s[c + j] += (N() * 0.4f + Mathf.Sin(U.TAU * (1300f + k * 40f) * t)) * Mathf.Exp(-t * 150f) * 0.7f;
                }
        }
        int latch = (int)(sec * 0.88f * RATE);
        for (int j = 0; j < 900; j++)
            if (latch + j < s.Length)
            {
                float t = (float)j / RATE;
                s[latch + j] += (Mathf.Sin(U.TAU * 900f * t) + N() * 0.6f) * Mathf.Exp(-t * 60f);
            }
        return Wav(s, "recarga");
    }

    AudioClip Ratchet()
    {
        var s = Buf(0.45f);
        for (int k = 0; k < 6; k++)
        {
            int c = (int)((0.03f + k * 0.065f) * RATE);
            for (int j = 0; j < 260; j++)
                if (c + j < s.Length)
                {
                    float t = (float)j / RATE;
                    s[c + j] += (N() * 0.6f + Mathf.Sin(U.TAU * 2200f * t)) * Mathf.Exp(-t * 180f);
                }
        }
        return Wav(s, "ratchet");
    }
}
