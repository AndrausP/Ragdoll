using System.Collections.Generic;
using UnityEngine;

/// Transformação rígida (rotação + posição + escala uniforme), equivalente ao Transform3D do Godot.
/// O jogo inteiro usa os MESMOS números do projeto Godot (mão direita); só a câmera é espelhada
/// (MirrorCamera), então a matemática de rotação/produto vetorial é idêntica.
public struct Xf
{
    public Quaternion q;
    public Vector3 p;
    public float s;

    public Xf(Quaternion q, Vector3 p, float s = 1f) { this.q = q; this.p = p; this.s = s; }
    public static Xf Identity => new Xf(Quaternion.identity, Vector3.zero);
    public static Xf At(Vector3 p) => new Xf(Quaternion.identity, p);

    public Vector3 Apply(Vector3 v) => q * (v * s) + p;
    public Vector3 Rotate(Vector3 v) => q * v;
    public static Xf operator *(Xf a, Xf b) => new Xf(a.q * b.q, a.Apply(b.p), a.s * b.s);
    public static Vector3 operator *(Xf a, Vector3 v) => a.Apply(v);
    public Xf Inverse()
    {
        var iq = Quaternion.Inverse(q);
        float invS = 1f / s;
        return new Xf(iq, iq * (-p) * invS, invS);
    }
    public Xf Translated(Vector3 d) => new Xf(q, p + d, s);
    public Xf InterpolateWith(Xf b, float t) => new Xf(Quaternion.Slerp(q, b.q, t), Vector3.Lerp(p, b.p, t), Mathf.Lerp(s, b.s, t));

    public static Xf Of(Transform t) => new Xf(t.rotation, t.position);
    public void ApplyTo(Transform t) { t.SetPositionAndRotation(p, q); }
}

public static class U
{
    public const float TAU = Mathf.PI * 2f;
    static System.Random rng = new System.Random(12345);

    public static float Randf() => (float)rng.NextDouble();
    public static float RandRange(float a, float b) => a + (b - a) * Randf();
    public static int RandInt(int a, int b) => rng.Next(a, b + 1);   // inclusivo
    public static float Randfn(float mean, float dev)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return mean + dev * (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
    }
    public static Vector3 RandomUnit() => new Vector3(RandRange(-1, 1), RandRange(-1, 1), RandRange(-1, 1)).normalized;
    public static T Pick<T>(IList<T> l) => l[rng.Next(l.Count)];

    public static float MoveToward(float a, float b, float d) => Mathf.Abs(b - a) <= d ? b : a + Mathf.Sign(b - a) * d;
    public static Vector3 MoveToward(Vector3 a, Vector3 b, float d) => Vector3.MoveTowards(a, b, d);
    public static float Smoothstep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }
    public static Vector3 LimitLength(Vector3 v, float l) => v.magnitude > l ? v.normalized * l : v;
    public static float Wrap(float v, float m) => ((v % m) + m) % m;
    public static int Wrapi(int v, int lo, int hi) { int r = hi - lo; return lo + (((v - lo) % r) + r) % r; }

    /// Basis.from_euler (ordem YXZ, radianos) — o Euler do Unity tem a mesma composição Y*X*Z.
    public static Quaternion Euler(Vector3 rad) => Quaternion.Euler(rad * Mathf.Rad2Deg);
    public static Quaternion Euler(float x, float y, float z) => Quaternion.Euler(x * Mathf.Rad2Deg, y * Mathf.Rad2Deg, z * Mathf.Rad2Deg);
    /// Basis(axis, angle) do Godot (radianos)
    public static Quaternion Axis(Vector3 axis, float rad) => Quaternion.AngleAxis(rad * Mathf.Rad2Deg, axis);
    /// Basis(x, y, z) com colunas ortonormais
    public static Quaternion FromBasis(Vector3 x, Vector3 y, Vector3 z) => Quaternion.LookRotation(z, y);
    /// DummyPart._basis_y: base com Y na direção dada
    public static Quaternion BasisY(Vector3 y)
    {
        y = y.normalized;
        Vector3 rf = Mathf.Abs(Vector3.Dot(y, Vector3.right)) < 0.9f ? Vector3.right : new Vector3(0, 0, -1);
        Vector3 x = Vector3.Cross(rf, y).normalized;
        Vector3 z = Vector3.Cross(x, y).normalized;
        return FromBasis(x, y, z);
    }
    /// Basis.looking_at(target): -Z aponta para o alvo
    public static Quaternion LookingAt(Vector3 dir, Vector3 up) => Quaternion.LookRotation(-dir, up);

    public static Xf RotAbout(Xf xf, Vector3 pivot, Quaternion b) => new Xf(b, pivot - b * pivot) * xf;

    public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);

    /// Godot Color.to_html sem alfa
    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}

/// Resultado de raio (equivalente ao Dictionary do intersect_ray)
public struct RayHit
{
    public bool hit;
    public Vector3 position, normal;
    public Collider collider;
    public Rigidbody body;
    public DummyPart part => collider ? collider.GetComponent<DummyPart>() : null;
}

public static class Phys
{
    static readonly RaycastHit[] buf = new RaycastHit[64];

    /// intersect_ray com máscara e exclusões (colliders)
    public static RayHit Ray(Vector3 from, Vector3 to, int mask, ICollection<Collider> exclude = null)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        var r = new RayHit();
        if (len < 1e-6f) return r;
        int n = Physics.RaycastNonAlloc(from, d / len, buf, len, mask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = buf[i];
            if (exclude != null && exclude.Contains(h.collider)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                r.hit = true; r.position = h.point; r.normal = h.normal; r.collider = h.collider; r.body = h.rigidbody;
            }
        }
        return r;
    }
}
