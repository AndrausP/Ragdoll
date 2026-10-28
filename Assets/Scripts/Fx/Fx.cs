using UnityEngine;

/// Rajadas de partículas descartáveis (equivale aos CPUParticles3D one_shot do Godot).
public static class Fx
{
    static Material normalMat, addMat;

    /// pos, quantidade, vida, velocidade máx., cor inicial/final, tamanho, aditivo, gravidade, direção, abertura (graus),
    /// fração mínima da velocidade, amortecimento (m/s²), escala mín/máx do tamanho
    public static void Burst(Vector3 pos, int amount, float life, float speed, Color c0, Color c1, float size, bool additive,
        Vector3 grav, Vector3 dir, float spreadDeg, float minSpeedFrac = 0.3f, float damping = 0f, float sizeMin = 0.5f, float sizeMax = 1.6f)
    {
        var go = new GameObject("fx");
        go.transform.position = pos;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
        go.transform.rotation = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) < 0.99f ? Vector3.up : Vector3.right);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * minSpeedFrac, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * sizeMin, size * sizeMax);
        main.startColor = Color.white;
        main.gravityModifier = -grav.y / 9.81f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(1, amount);
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)amount) });
        var sh = ps.shape;
        if (spreadDeg >= 89f) { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.02f; }
        else { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = spreadDeg; sh.radius = 0.01f; }
        if (damping > 0f)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = speed;
            lv.drag = Mathf.Clamp(damping / Mathf.Max(speed, 0.1f), 0f, 20f);
            lv.multiplyDragByParticleSize = false;
            lv.multiplyDragByParticleVelocity = true;
        }
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c0, 0), new GradientColorKey(c1, 1) },
                  new[] { new GradientAlphaKey(c0.a, 0), new GradientAlphaKey(c1.a, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = additive ? (addMat ? addMat : addMat = Mats.Unlit(Color.white, Mats.SoftDot, true))
                                    : (normalMat ? normalMat : normalMat = Mats.Unlit(Color.white, Mats.SoftDot, false));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        Object.Destroy(go, life + 1.5f);
    }

    /// Luz que apaga sozinha (clarão de explosão)
    public static void FlashLight(Vector3 pos, Color c, float range, float intensity, float time)
    {
        var go = new GameObject("flash");
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
        l.shadows = LightShadows.Hard;
        go.AddComponent<FadeLight>().time = time;
    }
}

public class FadeLight : MonoBehaviour
{
    public float time = 0.45f;
    float t, start;
    Light l;
    void Start() { l = GetComponent<Light>(); start = l.intensity; }
    void Update()
    {
        t += Time.deltaTime;
        l.intensity = Mathf.Lerp(start, 0f, t / time);
        if (t >= time) Destroy(gameObject);
    }
}
