using UnityEngine;

/// Uma ferida numa parte do corpo.
public class Wound
{
    public DummyPart part;
    public Vector3 localPos;               // posição local na parte
    public Vector3 localNormal = Vector3.up;   // normal local (para onde o sangue jorra)
    public string kind = "bala";           // bala, saída, corte, perfuração, flecha, estilhaço, queimadura, toco, contusão
    public float bleed = 0.5f;             // ml/s base (externo)
    public float internalBleed;            // ml/s sangramento interno (órgãos)
    public bool arterial;
    public string vessel = "";
    public float clot = 1f;                // 1 = sem coágulo
    public float pain = 0.05f;
    public float age;
    public bool plugged;                   // flecha cravada tampa parcialmente
    public float emitAcc;
    public GameObject visual;
    public Material trickle;               // filete de sangue escorrendo (WoundFx)
    public float trickleGrow;

    /// Taxa externa efetiva em ml/s antes de pressão arterial/cardíaca.
    public float ExternalRate()
    {
        float r = bleed * clot;
        if (plugged) r *= 0.35f;
        return r * part.TreatmentFactor(this);
    }

    public float InternalRate() => internalBleed * (part.HasTourniquetAbove() ? 0.3f : 1f);

    public void Update(float dt)
    {
        age += dt;
        if (!arterial)
        {
            float target = bleed > 6f ? 0.25f : 0.08f;
            clot = U.MoveToward(clot, target, dt * 0.012f);
        }
        else clot = U.MoveToward(clot, 0.85f, dt * 0.002f);
    }
}
