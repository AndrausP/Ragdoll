using System.Collections.Generic;

/// Parâmetros globais de sensação (câmera, arma, movimento). Centralizados aqui em vez de espalhados por Player.cs.
public static class GameFeel
{
    // câmera
    public const float AdsFovDelta = 20f;          // FOV ao mirar = FOV normal - isto (78 -> 58)
    public const float ImpactCameraKick = 0.018f;
    // arma
    public const float WeaponPositionLag = 0.06f;  // s
    public const float WeaponRotationLag = 0.09f;  // s
    public const float BreathRest = 0.003f;        // m
    public const float BreathWinded = 0.009f;      // m, logo após correr
    public const float SprintRecoverTime = 2.5f;   // s para o fôlego voltar
}

/// Sensação de peso de uma arma: massa percebida, estabilidade, tempos de saque e recuo.
public class WeaponFeel
{
    public float mass = 1f;            // 1 = pistola 9 mm; escala a inércia do viewmodel
    public float aimStability = 0.7f;  // 0..1; menor = mais balanço
    public float raiseTime = 0.32f;    // s para sacar
    public float aimTime = 0.18f;      // s para entrar na mira (ADS)
    public float recoilPitch = 1f;     // multiplicador do recuo vertical (sobre WeaponDef.recoil)
    public float recoilYaw = 0.15f;    // desvio lateral por tiro, em fração do recuo
    public float recovery = 1f;        // multiplicador da velocidade de volta ao centro
    public float sway = 1f;            // multiplicador do balanço
    public bool twoHanded;
}

public static class WeaponFeels
{
    static readonly WeaponFeel DEFAULT = new WeaponFeel();

    // valores do documento de design (seção 3); armas fora da tabela usam DEFAULT
    static readonly Dictionary<string, WeaponFeel> TABLE = new Dictionary<string, WeaponFeel>
    {
        { "pistol",   new WeaponFeel { mass = 0.8f, aimStability = 0.78f, raiseTime = 0.18f, aimTime = 0.15f, recoilPitch = 1.0f, recoilYaw = 0.15f, recovery = 1.3f, sway = 0.8f } },
        { "revolver", new WeaponFeel { mass = 1.2f, aimStability = 0.64f, raiseTime = 0.24f, aimTime = 0.18f, recoilPitch = 1.0f, recoilYaw = 0.25f, recovery = 0.9f, sway = 1.0f } },
        { "deagle",   new WeaponFeel { mass = 1.5f, aimStability = 0.52f, raiseTime = 0.28f, aimTime = 0.20f, recoilPitch = 1.15f, recoilYaw = 0.3f, recovery = 0.7f, sway = 1.2f } },
        { "smg",      new WeaponFeel { mass = 1.4f, aimStability = 0.70f, raiseTime = 0.25f, aimTime = 0.18f, recoilPitch = 1.0f, recoilYaw = 0.2f, recovery = 1.0f, sway = 1.0f, twoHanded = true } },
        { "m4",       new WeaponFeel { mass = 2.0f, aimStability = 0.76f, raiseTime = 0.32f, aimTime = 0.20f, recoilPitch = 1.0f, recoilYaw = 0.18f, recovery = 1.0f, sway = 0.9f, twoHanded = true } },
        { "bullpup",  new WeaponFeel { mass = 2.0f, aimStability = 0.74f, raiseTime = 0.32f, aimTime = 0.20f, recoilPitch = 1.0f, recoilYaw = 0.18f, recovery = 1.0f, sway = 0.9f, twoHanded = true } },
        { "rifle",    new WeaponFeel { mass = 2.6f, aimStability = 0.58f, raiseTime = 0.38f, aimTime = 0.22f, recoilPitch = 1.1f, recoilYaw = 0.22f, recovery = 0.75f, sway = 1.1f, twoHanded = true } },
        { "shotgun",  new WeaponFeel { mass = 3.0f, aimStability = 0.44f, raiseTime = 0.42f, aimTime = 0.22f, recoilPitch = 1.2f, recoilYaw = 0.3f, recovery = 0.6f, sway = 1.3f, twoHanded = true } },
        { "sawedoff", new WeaponFeel { mass = 2.2f, aimStability = 0.46f, raiseTime = 0.30f, aimTime = 0.20f, recoilPitch = 1.25f, recoilYaw = 0.35f, recovery = 0.6f, sway = 1.3f, twoHanded = true } },
        { "sniper",   new WeaponFeel { mass = 4.5f, aimStability = 0.35f, raiseTime = 0.55f, aimTime = 0.30f, recoilPitch = 1.3f, recoilYaw = 0.2f, recovery = 0.45f, sway = 1.5f, twoHanded = true } },
    };

    public static WeaponFeel Of(string id) => TABLE.TryGetValue(id, out var f) ? f : DEFAULT;
}
