using UnityEngine;

/// Fogo: queimadura por parte (dose acumulada), parte pegando fogo por alguns segundos (e passando para as vizinhas),
/// pele carbonizando no shader, dor, pânico (cai e rola para abafar) e morte por queimadura extensa.
public partial class Dummy
{
    float fireCrackle, burnDeathTimer, rollTimer;
    bool everBurned;
    Light fireLight;

    /// Fração do corpo queimada (por massa, 0..1)
    public float BurnFraction()
    {
        float m = 0f, b = 0f;
        foreach (var p in parts.Values)
        {
            if (!p || p.severed) continue;
            m += p.mass; b += p.mass * Mathf.Clamp01(p.burn);
        }
        return m > 0f ? b / m : 0f;
    }

    public bool OnFire()
    {
        foreach (var p in parts.Values) if (p && p.onFire > 0f) return true;
        return false;
    }

    /// Chama atingindo a parte no ponto atW. amount ~ dose (0.05 por bolha do lança-chamas em cheio).
    public void Burn(DummyPart p, Vector3 atW, float amount)
    {
        if (p == null || p.destroyed || amount <= 0f) return;
        p.burn += amount;
        p.onFire = Mathf.Max(p.onFire, 2.5f + Mathf.Min(p.burn, 1f) * 5f);
        SetPartChar(p.partId, p.burn * 0.85f);
        if (p.severed && p.skinMat) Mats.SetColor(p.skinMat, Color.Lerp(Mats.GetColor(p.skinMat), new Color(0.06f, 0.04f, 0.03f), Mathf.Clamp01(amount * 2f)));
        p.hp -= amount * 30f;
        if (!dead) { acutePain = Mathf.Min(1.2f, acutePain + amount * 1.5f); fear = 1f; }
        if (!everBurned)
        {
            everBurned = true;
            Game.LogEvent($"{dummyName} pegou fogo!", new Color(1f, 0.55f, 0.2f));
        }
        // a cada 0.3 de dose aparece uma queimadura visível perto do ponto atingido
        if (p.burn >= p.burnWoundAt)
        {
            p.burnWoundAt += 0.3f;
            var inv = p.Xform.Inverse();
            var lp = inv.Apply(atW);
            var ln = lp.sqrMagnitude > 1e-6f ? lp.normalized : Vector3.forward;
            p.AddWound(new Wound { kind = "queimadura", localPos = lp, localNormal = ln, bleed = 0.04f, pain = 0.3f });
        }
        if (state == State.STANDING && !gettingUp)
        {
            Stagger(U.RandomUnit(), 0.04f);
            // pânico: com o fogo pegando, se joga no chão para rolar
            if (BurnFraction() > 0.12f && consciousness > 0.3f)
            {
                fallReason = "pegando fogo";
                Game.LogEvent($"{dummyName} se joga no chão para apagar o fogo", new Color(1f, 0.7f, 0.4f));
                Collapse();
            }
        }
    }

    void FireTick(float dt)
    {
        bool any = false;
        Vector3 c = Vector3.zero; int nc = 0;
        bool rolling = state == State.DOWN && consciousness > 0.35f && paralysis == "" && !dead;
        foreach (var p in parts.Values)
        {
            if (!p || p.destroyed || p.onFire <= 0f) continue;
            any = true;
            // rolar no chão abafa o fogo bem mais rápido
            p.onFire -= dt * (rolling ? 2.6f : 1f);
            p.burn += dt * 0.05f;
            SetPartChar(p.partId, p.burn * 0.85f);
            if (!dead) acutePain = Mathf.Min(1.2f, acutePain + dt * 0.25f);
            Flames.EmitFire(p.transform.position, p.PartRadius() * 0.9f, dt * 45f);
            c += p.transform.position; nc++;
            // espalha para as vizinhas
            if (p.onFire > 1.5f && U.Randf() < dt * 0.35f)
            {
                var nb = U.Randf() < 0.5f && p.parentPart != null ? p.parentPart : (p.childrenParts.Count > 0 ? p.childrenParts[U.RandInt(0, p.childrenParts.Count - 1)] : null);
                if (nb != null && nb && !nb.destroyed && nb.onFire <= 0f) Burn(nb, nb.transform.position, 0.03f);
            }
        }
        if (any)
        {
            fireCrackle -= dt;
            if (fireCrackle <= 0f) { fireCrackle = U.RandRange(0.35f, 0.7f); Game.sfx.Play("fire", c / nc, -6f, U.RandRange(0.85f, 1.15f)); }
            if (fireLight == null)
            {
                var lg = new GameObject("fogo no corpo");
                lg.transform.SetParent(transform, false);
                fireLight = lg.AddComponent<Light>();
                fireLight.type = LightType.Point; fireLight.color = new Color(1f, 0.5f, 0.18f); fireLight.range = 5f;
            }
            fireLight.enabled = true;
            fireLight.transform.position = c / nc + Vector3.up * 0.2f;
            fireLight.intensity = 1.6f + Mathf.PerlinNoise(Time.time * 11f, nc) * 1.6f;
            if (rolling)
            {
                rollTimer -= dt;
                if (rollTimer <= 0f) { rollTimer = 0.7f; rollSign = -rollSign; Roll(rollSign * 9f); }
            }
        }
        else if (fireLight != null) fireLight.enabled = false;
        // queimadura extensa: choque e morte depois de alguns segundos
        if (!dead && everBurned)
        {
            float bf = BurnFraction();
            if (bf > 0.5f)
            {
                burnDeathTimer += dt * (1f + (bf - 0.5f) * 4f);
                if (burnDeathTimer > 9f) Die("Queimaduras extensas");
            }
        }
    }
}
