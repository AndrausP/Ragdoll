/// Parâmetros centrais de impacto, equilíbrio e queda (mesmos valores do impact_config.gd).
public static class ImpactConfig
{
    // empurrão físico
    public const float KNOCKBACK = 3.2f, KNOCKBACK_HIT_FRAC = 0.6f, KNOCKBACK_BODY_FRAC = 1.6f;
    // em pé: perda de equilíbrio por golpe
    public const float STAGGER_PER_IMPULSE = 0.03f, STAGGER_PER_DAMAGE = 0.005f, STAGGER_LEG_HIT = 0.2f, STAGGER_PUSH = 1.4f;
    public const float BALANCE_RECOVER = 0.3f, KNOCKDOWN_IMPULSE = 30f;
    // reação localizada
    public const float KICK_PER_IMPULSE = 0.022f, KICK_MAX = 0.7f, KICK_STIFF = 90f, KICK_DAMP = 11f, LEG_BUCKLE = 0.05f;
    // passos de recuperação
    public const float STEP_TRIGGER = 0.06f, STEP_MIN = 0.12f, STEP_MAX = 0.35f, STEP_TIME = 0.55f, STEP_LIFT = 0.07f;
    // limites para cair
    public const float PAIN_COLLAPSE = 1.45f, PAIN_HOLD_TIME = 1.2f, CONSCIOUS_COLLAPSE = 0.55f, BLOOD_LOSS_COLLAPSE = 0.35f;
    public const float CONCUSSION_COLLAPSE = 0.7f, BRAIN_HP_COLLAPSE = 0.7f;
    // amputação por explosão (pendência 07 / plano B3): só arranca o membro se ESTA explosão, sozinha, passar de
    // BLAST_TEAR_FRAC da vida da parte; mesmo assim com chance BLAST_TEAR_CHANCE. Abaixo disso o membro fica destroçado,
    // preso ao corpo. Tronco: abre com dano >= TORSO_OPEN_DMG e parte com >= TORSO_SPLIT_DMG (intensidade alta).
    public const float BLAST_TEAR_FRAC = 1.1f, BLAST_TEAR_CHANCE = 0.8f;
    public const float TORSO_OPEN_DMG = 150f, TORSO_SPLIT_DMG = 240f, TORSO_SPLIT_INTENSITY = 0.75f;
}
