using System.Collections.Generic;
using UnityEngine;

/// Jogador em primeira pessoa: movimento, armas, agarrar ragdolls e kit médico.
/// Hierarquia (números do Godot): Player (yaw) → head (pitch) → camNode (recuo/tremor) → câmera real girada 180° + espelhada.
public class Player : MonoBehaviour
{
    const float WALK = 4.6f, SPRINT = 8f, JUMP = 4.6f, SENS = 0.0023f;

    CharacterController cc;
    public Transform head, camNode, vmRoot;
    Camera cam;
    readonly Dictionary<string, GameObject> vmModels = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Transform> muzzles = new Dictionary<string, Transform>();
    readonly Dictionary<string, Transform> bowParts = new Dictionary<string, Transform>();
    public int weaponI;
    float cooldown, recoil, vmKick, swingT = 1f, bowDraw, shake, bob, flashT, pitch, yaw;
    string swingKind = "";
    bool drawing;
    public bool zoom;
    public float BowDraw => bowDraw;
    Light flashLight;
    GameObject flashMesh;
    public readonly Dictionary<string, int> ammo = new Dictionary<string, int>();
    public float reloadT = -1f;
    Vector3 velocity;
    Vector3 vmPos; Vector3 vmRot;
    public Rigidbody grabBody;
    Vector3 grabLocal;
    float grabDist = 2f;
    Collider selfCol;
    List<Collider> selfEx;
    public List<Collider> SelfColliders => selfEx;
    int burstLeft;
    AudioSource flameLoop;
    float ads, drawT = 1f, spreadNow;
    float recoilYaw, windedT, breathT;
    Vector3 vmPosVel, vmRotVel;
    Vector2 sway, swayVel;
    /// 0..1: quanto está mirando pela arma (botão direito)
    public float Ads => ads;
    public bool forceAds;     // capturas de tela
    /// 0..1: abertura da mira dinâmica (movimento, pulo, recuo)
    public float SpreadNow => spreadNow;

    public Vector3 HeadPosition => camNode.position;
    Vector3 CamFwd => -(camNode.rotation * Vector3.forward);   // a câmera do Godot olha para -Z
    public Vector3 AimDirection => CamFwd;

    void Awake()
    {
        gameObject.layer = Game.LAYER_PLAYER;
        cc = gameObject.AddComponent<CharacterController>();
        cc.radius = 0.32f; cc.height = 1.75f; cc.center = new Vector3(0, 0.875f, 0);
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.02f;
        selfCol = cc;
        selfEx = new List<Collider> { selfCol };
        head = new GameObject("head").transform;
        head.SetParent(transform, false);
        head.localPosition = new Vector3(0, 1.62f, 0);
        camNode = new GameObject("camNode").transform;
        camNode.SetParent(head, false);
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(camNode, false);
        camGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 78f;
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
        camGo.AddComponent<MirrorCamera>();
        camGo.tag = "MainCamera";
        camNode.gameObject.AddComponent<AudioListener>();
        vmRoot = new GameObject("vm").transform;
        vmRoot.SetParent(camNode, false);
        BuildViewmodels();
        flashLight = Game.MakeLight(LightType.Point, camNode, Vector3.zero, Quaternion.identity, new Color(1, 0.8f, 0.45f), 0f, 7f);
        flashMesh = Prims.Visual("flash", Prims.Sphere(0.04f, 0.08f), Mats.Emissive(new Color(1, 0.75f, 0.3f), 6f), camNode, false);
        flashMesh.SetActive(false);
        if (!Game.autotest && !Game.physTest && !Game.stressTest && Game.shotsDir == "") InputX.Capture(true);
    }

    void Start() { SelectWeapon(0); }

    public WeaponDef Weapon() => Weapons.LIST[weaponI];

    // ------------------------------------------------------------------ viewmodels

    GameObject Box(Transform parent, Vector3 size, Vector3 pos, Color col, Vector3 rot = default, float metal = 0f)
    {
        var go = Prims.Visual("box", Prims.Box(size), Mats.Mat(col, 0.5f, metal), parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = U.Euler(rot);
        return go;
    }

    GameObject Cyl(Transform parent, float r, float h, Vector3 pos, Color col, Vector3? rot = null, float metal = 0f)
    {
        var go = Prims.Visual("cyl", Prims.Cylinder(r, r, h), Mats.Mat(col, 0.45f, metal), parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = U.Euler(rot ?? new Vector3(Mathf.PI / 2, 0, 0));
        return go;
    }

    void BuildViewmodels()
    {
        var dark = new Color(0.12f, 0.12f, 0.13f);
        var gray = new Color(0.35f, 0.36f, 0.38f);
        foreach (var w in Weapons.LIST)
        {
            var root = new GameObject("vm_" + w.id);
            root.transform.SetParent(vmRoot, false);
            root.SetActive(false);
            vmModels[w.id] = root;
            var muzzle = new GameObject("muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzles[w.id] = muzzle;
            if (w.mag > 0) ammo[w.id] = w.mag;
            if (WeaponModel(w.id, root.transform, muzzle)) continue;
            var hold = new Vector3(0.22f, -0.2f, -0.42f);
            switch (w.id)
            {
                case "grenade":
                    var g = Prims.Visual("g", Prims.Sphere(0.045f, 0.1f), Mats.Mat(new Color(0.25f, 0.3f, 0.18f), 0.6f), root.transform, false);
                    g.transform.localPosition = hold;
                    Cyl(root.transform, 0.012f, 0.03f, hold + new Vector3(0, 0.055f, 0), dark, Vector3.zero, 0.6f);
                    break;
                case "c4":
                    Box(root.transform, new Vector3(0.09f, 0.05f, 0.14f), hold, new Color(0.72f, 0.65f, 0.45f));
                    Box(root.transform, new Vector3(0.05f, 0.1f, 0.03f), new Vector3(-0.2f, -0.22f, -0.4f), dark);
                    Box(root.transform, new Vector3(0.015f, 0.015f, 0.01f), new Vector3(-0.2f, -0.16f, -0.39f), new Color(1, 0.1f, 0.1f));
                    Arm(root.transform, "l", new Vector3(-0.2f, -0.24f, -0.4f), Vector3.up, new Vector3(-0.4f, -0.3f, 1f), new Vector3(-0.32f, -0.5f, 0.05f));
                    break;
                case "med":
                    var t = Prims.Visual("t", Prims.Torus(0.04f, 0.06f), Mats.Mat(new Color(0.1f, 0.1f, 0.1f), 0.6f), root.transform, false);
                    t.transform.localPosition = hold;
                    t.transform.localRotation = U.Euler(1.2f, 0, 0);
                    Cyl(root.transform, 0.04f, 0.08f, new Vector3(-0.2f, -0.22f, -0.42f), new Color(0.95f, 0.95f, 0.92f), new Vector3(0, 0, Mathf.PI / 2));
                    Arm(root.transform, "l", new Vector3(-0.2f, -0.22f, -0.42f), new Vector3(1, 0, 0), new Vector3(-0.3f, -1f, 0.4f), new Vector3(-0.32f, -0.5f, 0.05f));
                    break;
                case "flamer":
                    FlamerModel(root.transform, muzzle);
                    continue;
                default:
                    Cyl(root.transform, 0.014f, 0.8f, new Vector3(0.2f, -0.17f, -0.7f), gray, null, 0.5f);
                    muzzle.localPosition = new Vector3(0.2f, -0.17f, -1.1f);
                    hold = new Vector3(0.2f, -0.24f, -0.38f);
                    break;
            }
            Arm(root.transform, "r", hold + new Vector3(0, -0.02f, 0.01f), Vector3.up, new Vector3(0.25f, -0.3f, 1f), hold + new Vector3(0.12f, -0.3f, 0.42f));
        }
    }

    /// Lança-chamas (modelo procedural): tubo, tanque, bico com chama-piloto, cabo e alça da frente
    void FlamerModel(Transform root, Transform muzzle)
    {
        var dark = new Color(0.14f, 0.14f, 0.15f);
        var steel = new Color(0.5f, 0.5f, 0.52f);
        Cyl(root, 0.026f, 0.5f, new Vector3(0.2f, -0.19f, -0.62f), dark, null, 0.6f);
        Cyl(root, 0.034f, 0.08f, new Vector3(0.2f, -0.19f, -0.4f), steel, null, 0.8f);
        Cyl(root, 0.03f, 0.2f, new Vector3(0.2f, -0.245f, -0.6f), new Color(0.55f, 0.08f, 0.05f), null, 0.3f);    // tanque
        Cyl(root, 0.022f, 0.015f, new Vector3(0.2f, -0.245f, -0.71f), steel, null, 0.8f);
        Cyl(root, 0.018f, 0.12f, new Vector3(0.2f, -0.19f, -0.92f), steel, null, 0.9f);
        Cyl(root, 0.024f, 0.03f, new Vector3(0.2f, -0.19f, -0.97f), dark, null, 0.7f);
        Box(root, new Vector3(0.022f, 0.09f, 0.035f), new Vector3(0.2f, -0.25f, -0.42f), dark, new Vector3(0.25f, 0, 0));
        Box(root, new Vector3(0.02f, 0.08f, 0.03f), new Vector3(0.2f, -0.25f, -0.76f), dark);
        var pilot = Prims.Visual("piloto", Prims.Sphere(0.007f, 0.016f), Mats.Emissive(new Color(0.3f, 0.5f, 1f), 4f), root, false);
        pilot.transform.localPosition = new Vector3(0.2f, -0.207f, -0.99f);
        muzzle.localPosition = new Vector3(0.2f, -0.19f, -1.0f);
        Arm(root, "r", new Vector3(0.2f, -0.26f, -0.42f), new Vector3(0, Mathf.Cos(0.25f), -Mathf.Sin(0.25f)), new Vector3(0.2f, -0.3f, 1f), new Vector3(0.3f, -0.58f, 0.03f));
        Arm(root, "l", new Vector3(0.2f, -0.26f, -0.76f), Vector3.up, new Vector3(-0.6f, -0.4f, 0.8f), new Vector3(-0.05f, -0.6f, -0.35f));
    }

    // ------------------------------------------------------------------ pegada (braços do viewmodel)

    static Material armMat;

    /// Braço do viewmodel (pele contínua do próprio boneco, ombro -> mão, ver VmArms) segurando um cabo.
    /// grip: centro do cabo; thumb: para onde aponta o polegar ao longo do cabo; wrist: direção dedos -> punho;
    /// elbow: onde fica o cotovelo (fora da tela); curl: quanto a mão fecha (1 = punho, ~0.6 = pinça na corda).
    /// Devolve o nó do braço (origem no cabo) para animar.
    Transform Arm(Transform root, string side, Vector3 grip, Vector3 thumb, Vector3 wrist, Vector3 elbow, float curl = 1f)
    {
        var holder = new GameObject("braco_" + side).transform;
        holder.SetParent(root, false);
        holder.localPosition = grip;
        if (Game.shotsDir != "" && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--debug-grip") >= 0)
            Prims.Visual("dbg", Prims.Sphere(0.005f, 0.01f), Mats.Emissive(Color.red, 3f), holder, false);
        var am = VmArms.Get(side);
        if (am == null)
        {
            var skin = new Color(0.85f, 0.55f, 0.5f);
            var a0 = Box(holder, new Vector3(0.07f, 0.07f, Vector3.Distance(elbow, grip)), (elbow - grip) * 0.5f, skin);
            a0.transform.localRotation = U.LookingAt(grip - elbow, Vector3.up);
            Box(holder, new Vector3(0.075f, 0.06f, 0.09f), Vector3.zero, skin * 0.95f);
            return holder;
        }
        if (armMat == null)
        {
            armMat = Mats.Mat(Color.white, 0.6f);
            armMat.SetFloat("_UseVC", 1);
        }
        var defs = Anatomy.Parts();
        var hPos = defs["hand_" + side].pos;
        var fPos = defs["forearm_" + side].pos;
        var uPos = defs["upper_arm_" + side].pos;
        var wristRest = defs["hand_" + side].joint.anchor;
        var elbowRest = defs["forearm_" + side].joint.anchor;
        var shoulderRest = defs["upper_arm_" + side].joint.anchor;
        // base da mão: x local -> ao longo do cabo (direita: para longe do polegar; esquerda: para o polegar),
        // y local -> para o punho, z local (palma) = x × y
        var a = (side == "r" ? -thumb : thumb).normalized;
        var b = Vector3.ProjectOnPlane(wrist, a).normalized;
        var q = U.FromBasis(a, b, Vector3.Cross(a, b));
        var gc = am.gripCenter;
        if (curl < 1f) gc = Vector3.Lerp(new Vector3(gc.x, VmArms.KNUCKLE_Y - 0.03f, gc.z - VmArms.GRIP_R * 0.6f), gc, curl);
        var handO = -(q * gc);                                       // origem da mão no espaço do holder (cabo = 0)
        var wristW = handO + q * (wristRest - hPos);
        var qf = Quaternion.FromToRotation(q * (elbowRest - wristRest), (elbow - grip) - wristW) * q;
        var foreO = wristW - qf * (wristRest - fPos);
        var elbowW = foreO + qf * (elbowRest - fPos);
        // o braço (com a manga) continua para trás e para baixo, para fora da tela
        var back = ((elbow - grip) - wristW).normalized + new Vector3(0, -0.35f, 0.7f);
        var qu = Quaternion.FromToRotation(qf * (shoulderRest - elbowRest), back.normalized) * qf;
        var upperO = elbowW - qu * (elbowRest - uPos);
        var go = new GameObject("pele_" + side);
        go.transform.SetParent(holder, false);
        var bones = new Transform[3];
        var poses = new[] { (upperO, qu), (foreO, qf), (handO, q) };
        for (int i = 0; i < 3; i++)
        {
            bones[i] = new GameObject("osso_" + am.parts[i]).transform;
            bones[i].SetParent(holder, false);
            bones[i].localPosition = poses[i].Item1;
            bones[i].localRotation = poses[i].Item2;
        }
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = am.mesh;
        smr.bones = bones;
        smr.rootBone = holder;
        smr.sharedMaterial = armMat;
        smr.updateWhenOffscreen = true;
        smr.quality = SkinQuality.Bone4;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smr.SetBlendShapeWeight(0, Mathf.Clamp01(curl) * 100f);
        return holder;
    }

    /// Os modelos vêm com o cano em +X (armas de fogo) ou de pé em +Y (armas brancas/arco).
    static readonly Dictionary<string, float[]> GUN_VM = new Dictionary<string, float[]>
    {
        // comprimento, z da boca do cano, x, y do topo, fração da altura até o cano
        { "pistol", new[] { 0.21f, -0.56f, 0.2f, -0.13f, 0.18f } },
        { "revolver", new[] { 0.27f, -0.6f, 0.2f, -0.13f, 0.2f } },
        { "smg", new[] { 0.5f, -0.86f, 0.2f, -0.13f, 0.28f } },
        { "rifle", new[] { 0.66f, -0.95f, 0.2f, -0.13f, 0.3f } },
        { "shotgun", new[] { 0.86f, -1.0f, 0.2f, -0.14f, 0.3f } },
        { "sawedoff", new[] { 0.6f, -0.9f, 0.2f, -0.14f, 0.3f } },
        { "sniper", new[] { 1.1f, -1.28f, 0.2f, -0.08f, 0.5f } },
        { "deagle", new[] { 0.25f, -0.58f, 0.2f, -0.12f, 0.2f } },
        { "m4", new[] { 0.72f, -0.98f, 0.2f, -0.13f, 0.3f } },
        { "bullpup", new[] { 0.62f, -0.95f, 0.2f, -0.13f, 0.3f } },
    };

    /// Onde as mãos pegam: cabo (fração do cano até a coronha, fração do topo até a base), inclinação do cabo (graus)
    /// e telha da mão esquerda (fração z, fração y); telha < 0 = pistola com a esquerda apoiando a direita.
    /// O cabo é achado na malha: vértices entre as frações z0..z1 (do cano à coronha) e abaixo da fração y (do topo).
    static readonly Dictionary<string, float[]> GUN_GRIP = new Dictionary<string, float[]>
    {
        // z0, z1, y mínimo, inclinação do cabo (graus), fração z da telha (-1 = pistola com apoio da esquerda)
        { "pistol", new[] { 0.55f, 1f, 0.5f, 16f, -1f } },
        { "revolver", new[] { 0.6f, 1f, 0.45f, 22f, -1f } },
        { "smg", new[] { 0.45f, 0.85f, 0.55f, 10f, 0.25f } },
        { "rifle", new[] { 0.55f, 0.82f, 0.55f, 12f, 0.3f } },
        { "shotgun", new[] { 0.6f, 0.88f, 0.5f, 20f, 0.3f } },
        { "sawedoff", new[] { 0.65f, 1f, 0.45f, 25f, 0.4f } },
        { "sniper", new[] { 0.55f, 0.82f, 0.55f, 12f, 0.3f } },
        { "deagle", new[] { 0.5f, 1f, 0.45f, 16f, -1f } },
        { "m4", new[] { 0.56f, 0.72f, 0.5f, 12f, 0.3f } },
        { "bullpup", new[] { 0.36f, 0.52f, 0.5f, 12f, 0.13f } },
    };

    /// Centro dos vértices do modelo (no espaço de root) numa fatia: z0..z1 do cano à coronha, y abaixo de yMin (do topo).
    static Vector3? Slice(GameObject model, Transform root, float zMuzzle, float len, float yTop, float hgt, float z0, float z1, float yMin)
    {
        var mf = model.GetComponentInChildren<MeshFilter>();
        if (mf == null) return null;
        var xs = new List<float>(); var zs = new List<float>();
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in mf.sharedMesh.vertices)
        {
            var p = root.InverseTransformPoint(mf.transform.TransformPoint(v));
            float fz = (p.z - zMuzzle) / len, fy = (yTop - p.y) / hgt;
            if (fz < z0 || fz > z1 || fy < yMin) continue;
            xs.Add(p.x); zs.Add(p.z); lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y);
        }
        if (xs.Count < 3) return null;
        xs.Sort(); zs.Sort();
        return new Vector3(xs[xs.Count / 2], (lo + hi) * 0.5f, zs[zs.Count / 2]);
    }

    /// Armas brancas em pé (+Y): comprimento, fração da altura onde a mão pega, posição x, y, z, inclinação para a frente (rad)
    static readonly Dictionary<string, float[]> MELEE_VM = new Dictionary<string, float[]>
    {
        { "knife", new[] { 0.3f, 0.18f, 0.2f, -0.22f, -0.4f, 0.75f } },
        { "axe", new[] { 0.5f, 0.22f, 0.22f, -0.21f, -0.42f, 0.5f } },
        { "sword", new[] { 0.9f, 0.1f, 0.22f, -0.27f, -0.42f, 0.55f } },
        { "hammer", new[] { 0.5f, 0.22f, 0.22f, -0.21f, -0.42f, 0.45f } },
        { "claymore", new[] { 1.05f, 0.08f, 0.22f, -0.28f, -0.44f, 0.5f } },
        { "scythe", new[] { 1.2f, 0.3f, 0.24f, -0.3f, -0.5f, 0.35f } },
        { "throwknife", new[] { 0.28f, 0.18f, 0.2f, -0.22f, -0.4f, 0.9f } },
    };

    /// Arma cujo modelo tem outro nome no arquivo
    static readonly Dictionary<string, string> MODEL_OF = new Dictionary<string, string> { { "throwknife", "dagger" } };

    /// Mira pela arma (ADS): deslocamento do viewmodel que põe o topo da arma no centro da tela
    readonly Dictionary<string, Vector3> adsOffset = new Dictionary<string, Vector3>();

    bool WeaponModel(string id, Transform root, Transform muzzle)
    {
        string mid = MODEL_OF.TryGetValue(id, out var mo) ? mo : id;
        if (MeshStore.Weapon(mid) == null) return false;
        if (GUN_VM.TryGetValue(id, out var g))
        {
            var m = Game.Model(id, U.Axis(Vector3.up, Mathf.PI / 2), g[0], out var sz, false);
            m.transform.SetParent(root, false);
            m.transform.localPosition = new Vector3(g[2], g[3] - sz.y * 0.5f, g[1] + sz.z * 0.5f);
            muzzle.localPosition = new Vector3(g[2], g[3] - sz.y * g[4], g[1]);
            adsOffset[id] = new Vector3(-g[2], -0.035f - g[3], 0.07f);
            var gr = GUN_GRIP[id];
            float tilt = gr[3] * Mathf.Deg2Rad;
            var grip = Slice(m, root, g[1], sz.z, g[3], sz.y, gr[0], gr[1], gr[2]) ?? new Vector3(g[2], g[3] - sz.y * 0.65f, g[1] + sz.z * 0.75f);
            var up = new Vector3(0, Mathf.Cos(tilt), -Mathf.Sin(tilt));       // cabo inclinado: topo para a frente
            Arm(root, "r", grip, up, new Vector3(0.2f, -0.3f, 1f), grip + new Vector3(0.1f, -0.32f, 0.45f));
            if (gr[4] < 0f)
            {
                // pistola com as duas mãos: a esquerda abraça a direita por baixo/por fora
                var sup = grip + new Vector3(-0.03f, -0.014f, 0.004f);
                Arm(root, "l", sup, up, new Vector3(-0.6f, -0.45f, 0.8f), sup + new Vector3(-0.26f, -0.3f, 0.4f));
            }
            else
            {
                var fore = Slice(m, root, g[1], sz.z, g[3], sz.y, gr[4] - 0.05f, gr[4] + 0.05f, 0f) ?? new Vector3(g[2], g[3] - sz.y * 0.5f, g[1] + sz.z * gr[4]);
                Arm(root, "l", fore + new Vector3(-0.004f, -0.012f, 0), new Vector3(0, 0, -1), new Vector3(-1f, -0.3f, 0.25f), fore + new Vector3(-0.22f, -0.32f, 0.38f));
            }
            return true;
        }
        if (MELEE_VM.TryGetValue(id, out var mv))
        {
            var b = new GameObject("blade").transform;
            b.SetParent(root, false);
            b.localPosition = new Vector3(mv[2], mv[3], mv[4]);
            b.localRotation = U.Axis(Vector3.right, -mv[5]);            // ponta inclinada para a frente
            bowParts["blade_" + id] = b;
            var m = Game.Model(mid, Quaternion.identity, mv[0], out var sz, false);
            m.transform.SetParent(b, false);
            m.transform.localPosition = new Vector3(0, sz.y * (0.5f - mv[1]), 0);   // a mão (origem) fica no cabo
            // a cabeça do machado/marreta tira a caixa do centro: alinha o eixo do cabo (malha perto da mão) com a origem
            var mf = m.GetComponentInChildren<MeshFilter>();
            if (mf != null)
            {
                var xs = new List<float>(); var zs = new List<float>();
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = b.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (Mathf.Abs(p.y) < sz.y * 0.12f) { xs.Add(p.x); zs.Add(p.z); }
                }
                if (xs.Count > 3)
                {
                    xs.Sort(); zs.Sort();
                    m.transform.localPosition -= new Vector3(xs[xs.Count / 2], 0, zs[zs.Count / 2]);
                }
            }
            Arm(b, "r", Vector3.zero, Vector3.up, Quaternion.Inverse(b.localRotation) * new Vector3(0.25f, -0.3f, 1f),
                Quaternion.Inverse(b.localRotation) * new Vector3(0.1f, -0.3f, 0.42f));
            return true;
        }
        switch (id)
        {
            case "spear":
            {
                // na horizontal, as duas mãos por baixo, ponta para a frente
                var b = new GameObject("blade").transform;
                b.SetParent(root, false);
                b.localPosition = new Vector3(0.14f, -0.22f, -0.5f);
                b.localRotation = U.Axis(Vector3.right, -Mathf.PI / 2 + 0.08f);
                bowParts["blade_" + id] = b;
                var m = Game.Model(id, Quaternion.identity, 1.7f, out var sz, false);
                m.transform.SetParent(b, false);
                m.transform.localPosition = new Vector3(0, sz.y * 0.28f, 0);
                var inv = Quaternion.Inverse(b.localRotation);
                Arm(b, "r", Vector3.zero, Vector3.up, inv * new Vector3(1f, -0.3f, 0.3f), inv * new Vector3(0.12f, -0.3f, 0.42f));
                Arm(b, "l", new Vector3(0, 0.36f, 0), Vector3.up, inv * new Vector3(-1f, -0.3f, 0.25f), new Vector3(0, 0.36f, 0) + inv * new Vector3(-0.22f, -0.3f, 0.36f));
                return true;
            }
            case "bow":
            {
                var bow = new GameObject("bow").transform;
                bow.SetParent(root, false);
                bow.localPosition = new Vector3(0.12f, -0.12f, -0.55f);
                var m = Game.Model("bow", U.Axis(Vector3.up, -Mathf.PI / 2), 0.8f, out var sz, false, "White");
                m.transform.SetParent(bow, false);
                m.transform.localPosition = new Vector3(0, 0, 0.12f - sz.z * 0.5f);
                var str = Box(bow, new Vector3(0.003f, 0.76f, 0.003f), new Vector3(0, 0, 0.12f), new Color(0.9f, 0.9f, 0.85f));
                bowParts["string"] = str.transform;
                var arrow = new GameObject("arrow").transform;
                arrow.SetParent(bow, false);
                var am = Game.Model("arrow", U.Axis(Vector3.up, Mathf.PI), 0.7f, out _, false);
                am.transform.SetParent(arrow, false);
                bowParts["arrow"] = arrow;
                muzzle.localPosition = new Vector3(0.12f, -0.12f, -0.9f);
                // esquerda segura o arco; a direita puxa a corda (anda junto com ela)
                Arm(bow, "l", new Vector3(0, 0, 0.12f - sz.z * 0.5f), Vector3.up, new Vector3(-0.3f, -0.35f, 1f), new Vector3(-0.3f, -0.3f, 0.3f));
                var pull = Arm(bow, "r", new Vector3(0, 0, 0.12f), Vector3.up, new Vector3(0.4f, -0.2f, 1f), new Vector3(0.32f, -0.18f, 0.55f), 0.6f);
                bowParts["pull"] = pull;
                return true;
            }
        }
        return false;
    }

    readonly Dictionary<int, string> lastInSlot = new Dictionary<int, string>();

    public WeaponDef LastInSlot(int slot)
    {
        if (lastInSlot.TryGetValue(slot, out var id)) return Weapons.ById(id);
        foreach (var w in Weapons.LIST) if (w.slot == slot) return w;
        return null;
    }

    /// Tecla de categoria: se já está nela, passa para a próxima arma da mesma categoria.
    public void SelectSlot(int slot)
    {
        var inSlot = new List<int>();
        for (int i = 0; i < Weapons.LIST.Count; i++) if (Weapons.LIST[i].slot == slot) inSlot.Add(i);
        if (inSlot.Count == 0) return;
        int k = inSlot.IndexOf(weaponI);
        if (k >= 0) SelectWeapon(inSlot[(k + 1) % inSlot.Count]);
        else
        {
            var last = LastInSlot(slot);
            SelectWeapon(last != null ? Weapons.LIST.IndexOf(last) : inSlot[0]);
        }
    }

    public void SelectWeapon(int i)
    {
        weaponI = U.Wrapi(i, 0, Weapons.LIST.Count);
        foreach (var kv in vmModels) kv.Value.SetActive(kv.Key == Weapon().id);
        bowDraw = 0f; drawing = false; zoom = false; reloadT = -1f; ads = 0f;
        lastInSlot[Weapon().slot] = Weapon().id;
        cooldown = Mathf.Max(cooldown, 0.15f);
        drawT = 0f;     // animação de sacar
        if (Game.sfx) Game.sfx.PlayUI(Weapon().type == "melee" ? "draw_blade" : Weapon().type == "gun" || Weapon().type == "bow" ? "draw_gun" : "cloth", -12f, U.RandRange(0.95f, 1.05f));
        if (Game.hud) Game.hud.SetWeapon(weaponI);
    }

    public void AddShake(float a) { shake = Mathf.Min(shake + a, 2f); }

    // ------------------------------------------------------------------ entrada e movimento

    void Update()
    {
        if (InputX.Captured)
        {
            var md = InputX.MouseDelta;
            float s = SENS * Crosshair.sensitivity * (zoom ? 0.3f : 1f - ads * 0.3f);
            // inércia da arma: o viewmodel atrasa um pouco em relação à mira
            sway += new Vector2(-md.x, md.y) * 0.00045f * (1f - ads * 0.7f);
            yaw -= md.x * s;
            pitch = Mathf.Clamp(pitch + md.y * s, -1.5f, 1.5f);
            transform.rotation = U.Axis(Vector3.up, yaw);
            head.localRotation = U.Axis(Vector3.right, pitch);
        }
        int wheel = InputX.Wheel;
        if (wheel != 0)
        {
            if (grabBody) grabDist = Mathf.Clamp(grabDist + 0.25f * wheel, 1f, 4.5f);
            else SelectWeapon(weaponI - wheel);
        }
        for (int k = 0; k < 10; k++)
            if (InputX.SlotDown(k)) SelectSlot(k == 0 ? 10 : k);
        if (InputX.AnyMouseDown && !InputX.Captured && !Crosshair.menuOpen && !Game.autotest && Game.shotsDir == "") InputX.Capture(true);
        if (InputX.Down(InputX.A.Escape)) { InputX.Capture(false); Crosshair.menuOpen = false; }
        HandleWeapon(Time.deltaTime);
        HandleGrabInput();
        AnimateVm(Time.deltaTime);
        // o boneco sob a mira sente medo
        var h = AimRay(60f);
        if (h.hit && h.part != null) h.part.dummy.aimedAt = 0.25f;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float ix = (InputX.Held(InputX.A.Right) ? 1 : 0) - (InputX.Held(InputX.A.Left) ? 1 : 0);
        float iy = (InputX.Held(InputX.A.Back) ? 1 : 0) - (InputX.Held(InputX.A.Forward) ? 1 : 0);
        var inp = new Vector2(ix, iy);
        if (inp.magnitude > 1f) inp.Normalize();
        var dir = (transform.rotation * new Vector3(inp.x, 0, inp.y)).normalized;
        float speed = InputX.Held(InputX.A.Sprint) ? SPRINT : WALK;
        if (InputX.Held(InputX.A.Crouch))
        {
            speed *= 0.5f;
            head.localPosition = new Vector3(head.localPosition.x, U.MoveToward(head.localPosition.y, 1.1f, dt * 4f), 0);
        }
        else head.localPosition = new Vector3(head.localPosition.x, U.MoveToward(head.localPosition.y, 1.62f, dt * 4f), 0);
        bool onFloor = cc.isGrounded;
        if (!onFloor) velocity.y -= 12f * dt;
        else if (InputX.Held(InputX.A.Jump)) velocity.y = JUMP;
        else velocity.y = -0.5f;
        float accel = onFloor ? 12f : 3f;
        velocity.x = U.MoveToward(velocity.x, dir.x * speed, accel * speed * dt);
        velocity.z = U.MoveToward(velocity.z, dir.z * speed, accel * speed * dt);
        cc.Move(velocity * dt);
        bool moving = onFloor && dir.magnitude > 0.1f;
        if (moving)
        {
            float before = bob;
            bob += dt * speed * 1.6f;
            if (Mathf.FloorToInt(before / Mathf.PI) != Mathf.FloorToInt(bob / Mathf.PI))
                Game.sfx.Play("step", transform.position + new Vector3(0, 0.05f, 0), -14f + speed * 0.6f, U.RandRange(0.85f, 1.15f));
        }
        float bobAmt = moving ? (speed > WALK ? 0.012f : 0.007f) : 0f;
        head.localPosition = new Vector3(Mathf.Lerp(head.localPosition.x, Mathf.Sin(bob) * bobAmt, Mathf.Clamp01(dt * 10f)), head.localPosition.y, 0);
        HandleGrabPhysics();
    }

    public Vector3 Velocity => velocity;

    public RayHit AimRay(float maxDist = 400f)
    {
        var from = camNode.position;
        return Phys.Ray(from, from + CamFwd * maxDist, Game.L_WORLD | Game.L_DUMMY | Game.L_PROPS, selfEx);
    }

    void HandleWeapon(float dt)
    {
        cooldown -= dt;
        var w = Weapon();
        bool captured = InputX.Captured || Game.autotest;
        bool fire = captured && InputX.Fire && grabBody == null;
        bool fireJust = captured && InputX.FireDown && grabBody == null;
        bool altJust = captured && InputX.AltDown;
        bool alt = captured && InputX.Alt;
        switch (w.type)
        {
            case "gun":
                zoom = w.id == "sniper" && alt && reloadT < 0f;
                ads = U.MoveToward(ads, w.id != "sniper" && (alt || forceAds) && reloadT < 0f && drawT >= 1f ? 1f : 0f, dt / Mathf.Max(0.05f, WeaponFeels.Of(w.id).aimTime));
                if (reloadT >= 0f) ReloadStep(w, dt);
                else if (captured && InputX.Down(InputX.A.Reload) && ammo[w.id] < w.mag) StartReload(w);
                else if (burstLeft > 0 && cooldown <= 0f && ammo[w.id] > 0)
                {
                    // rajada em andamento
                    FireGun(w, camNode.position, CamFwd);
                    if (--burstLeft == 0) cooldown = 0.32f;
                }
                else if (cooldown <= 0f && ((w.auto && fire) || fireJust))
                {
                    if (w.burst > 0) burstLeft = w.burst - 1;
                    if (ammo[w.id] <= 0)
                    {
                        if (fireJust) Game.sfx.PlayUI("dry", -6f);
                        StartReload(w);
                    }
                    else FireGun(w, camNode.position, CamFwd);
                }
                if (Game.hud) Game.hud.SetAmmo(ammo[w.id], w.mag, reloadT);
                break;
            case "bow":
                if (fire && cooldown <= 0f)
                {
                    if (!drawing) Game.sfx.PlayUI("bow_draw", -9f, U.RandRange(0.95f, 1.05f));
                    drawing = true; bowDraw = Mathf.Min(1f, bowDraw + dt / 0.85f);
                }
                else if (drawing)
                {
                    ShootArrow(camNode.position + CamFwd * 0.3f, CamFwd, bowDraw);
                    drawing = false; bowDraw = 0f; cooldown = 0.35f;
                }
                if (Game.hud) Game.hud.SetAmmo(0, 0);
                break;
            case "melee":
                if (cooldown <= 0f && (fireJust || altJust))
                {
                    var atk = Weapons.Melee(w.id, !fireJust);
                    if (atk != null) { Melee(atk.prof, atk.energy, atk.reach, atk.anim, atk.sweep); cooldown = atk.cooldown; }
                }
                if (Game.hud) Game.hud.SetAmmo(0, 0);
                break;
            case "throwknife":
                if (reloadT >= 0f) ReloadStep(w, dt);
                else if (captured && InputX.Down(InputX.A.Reload) && ammo[w.id] < w.mag) StartReload(w);
                else if (cooldown <= 0f && (fireJust || altJust))
                {
                    if (ammo[w.id] <= 0) { Game.hud?.Toast("Sem facas — Q para recolher"); cooldown = 0.3f; }
                    else
                    {
                        int n = altJust ? Mathf.Min(3, ammo[w.id]) : 1;
                        for (int i = 0; i < n; i++)
                        {
                            float yawOff = n > 1 ? (i - 1) * 0.09f : 0f;
                            var d = U.Axis(Vector3.up, yawOff) * CamFwd;
                            ThrowKnife(camNode.position + CamFwd * 0.35f + camNode.rotation * new Vector3(0.12f, -0.05f, 0f), d);
                        }
                        ammo[w.id] -= n;
                        swingT = 0f; swingKind = "throw";
                        cooldown = altJust ? 0.7f : 0.4f;
                    }
                }
                if (Game.hud) Game.hud.SetAmmo(ammo[w.id], w.mag, reloadT);
                break;
            case "flame":
                if (reloadT >= 0f) ReloadStep(w, dt);
                else if (captured && InputX.Down(InputX.A.Reload) && ammo[w.id] < w.mag) StartReload(w);
                bool spraying = fire && reloadT < 0f && ammo[w.id] > 0;
                if (spraying)
                {
                    fuel -= dt * 22f;
                    while (fuel < 0f && ammo[w.id] > 0) { fuel += 1f; ammo[w.id]--; }
                    Flames.Spray(muzzles[w.id].position, (CamFwd * 6f + camNode.position - muzzles[w.id].position).normalized, velocity, dt);
                    vmKick = Mathf.Max(vmKick, 0.15f + Mathf.PerlinNoise(Time.time * 20f, 0) * 0.15f);
                    AddShake(dt * 0.4f);
                }
                else if (fire && ammo[w.id] <= 0 && fireJust) { Game.sfx.PlayUI("dry", -6f); StartReload(w); }
                FlameSound(spraying);
                if (Game.hud) Game.hud.SetAmmo(ammo[w.id], w.mag, reloadT);
                break;
            case "throw":
                if (cooldown <= 0f && (fireJust || altJust))
                {
                    ThrowGrenade(camNode.position + CamFwd * 0.4f, CamFwd, fireJust ? 15f : 6f);
                    cooldown = 0.8f;
                }
                if (Game.hud) Game.hud.SetAmmo(0, 0);
                break;
            case "c4":
                if (fireJust && cooldown <= 0f)
                {
                    var h = AimRay(4f);
                    if (h.hit) { PlaceC4(h); cooldown = 0.3f; }
                }
                if (altJust) DetonateC4();
                if (Game.hud) Game.hud.SetAmmo(0, 0);
                break;
            case "med":
                if ((fireJust || altJust) && cooldown <= 0f)
                {
                    var h2 = AimRay(3.5f);
                    if (h2.hit && h2.part != null)
                    {
                        var p = h2.part;
                        bool ok = fireJust ? p.dummy.ApplyTourniquet(p) : p.dummy.ApplyBandage(p);
                        if (!ok && Game.hud) Game.hud.Toast(fireJust ? "Não dá para aplicar aqui (só membros, ou já aplicado)" : "Já tem bandagem");
                    }
                    cooldown = 0.4f;
                }
                if (Game.hud) Game.hud.SetAmmo(0, 0);
                break;
        }
        if (w.type != "gun") ads = U.MoveToward(ads, 0f, dt * 6f);
        if (w.type != "flame") FlameSound(false);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, zoom ? 20f : Crosshair.fov - ads * GameFeel.AdsFovDelta, Mathf.Clamp01(dt * 14f));
        vmRoot.gameObject.SetActive(!zoom);
        // mira dinâmica: abre andando, no ar e com o recuo; fecha mirando pela arma
        float mv = new Vector2(velocity.x, velocity.z).magnitude / SPRINT;
        float target = Mathf.Clamp01(mv * 0.6f + (cc.isGrounded ? 0f : 0.35f) + recoil * 5f) * (1f - ads * 0.8f);
        spreadNow = Mathf.Lerp(spreadNow, target, Mathf.Clamp01(dt * 10f));
    }

    // ------------------------------------------------------------------ ações (também usadas pelo autoteste)

    bool recordedReload;

    public void StartReload(WeaponDef w)
    {
        if (reloadT >= 0f || w.mag <= 0) return;
        reloadT = 0f;
        // gravação da recarga inteira quando existe; senão os cliques sintetizados ao longo da recarga
        string rec = w.id == "shotgun" || w.id == "sawedoff" ? "" : (w.id == "rifle" || w.id == "sniper" ? "reload_rifle" : "reload_pistol");
        recordedReload = rec != "" && Game.sfx.Has(rec);
        if (recordedReload) Game.sfx.PlayUI(rec, -6f, U.RandRange(0.97f, 1.03f) * Mathf.Clamp(1.6f / w.reload, 0.75f, 1.1f));
        else Game.sfx.PlayUI("mag_out", -8f, U.RandRange(0.95f, 1.05f));
    }

    void ReloadStep(WeaponDef w, float dt)
    {
        float before = reloadT;
        reloadT += dt / w.reload;
        if (before < 0.55f && reloadT >= 0.55f && !recordedReload) Game.sfx.PlayUI("mag_in", -7f, U.RandRange(0.95f, 1.05f));
        if (before < 0.82f && reloadT >= 0.82f && (!recordedReload || w.id == "sniper")) Game.sfx.PlayUI("rack", -7f, U.RandRange(0.95f, 1.05f));
        if (reloadT >= 1f) { reloadT = -1f; ammo[w.id] = w.mag; cooldown = 0.1f; }
    }

    public void FireGun(WeaponDef w, Vector3 from, Vector3 fwd)
    {
        cooldown = 60f / w.rpm;
        if (w.mag > 0) ammo[w.id] = Mathf.Max(0, ammo[w.id] - 1);
        var mz = muzzles[w.id].position;
        for (int i = 0; i < w.pellets; i++)
        {
            float spread = w.spread * (zoom ? 0.15f : 1f) * (1f - ads * 0.55f) * (1f + spreadNow * 1.5f);
            var d = (fwd + new Vector3(U.Randfn(0, spread), U.Randfn(0, spread), U.Randfn(0, spread))).normalized;
            var end = Weapons.FireRay(from, d, w.energy, w.prof, selfEx);
            Tracer(mz, end);
        }
        Game.sfx.PlayUI(w.sound, -4f, w.pitch * U.RandRange(0.93f, 1.07f));
        var feel = WeaponFeels.Of(w.id);
        recoil += w.recoil * feel.recoilPitch * (1f - ads * 0.35f);
        recoilYaw += U.RandRange(-1f, 1f) * w.recoil * feel.recoilYaw * (1f - ads * 0.35f);
        EjectCasing(w);
        // fumaça leve no cano
        Fx.Burst(mz, 4, 0.9f, 0.6f, new Color(0.6f, 0.6f, 0.6f, 0.25f), new Color(0.7f, 0.7f, 0.7f, 0f), 0.06f, false, new Vector3(0, 0.6f, 0), fwd, 25f, 0.2f, 1.5f, 0.6f, 2f);
        vmKick = 1f;
        flashT = 0.05f;
        AddShake(w.recoil * 1.5f);
    }

    void Melee(Prof prof, float energy, float reach, string kind, bool sweep = false)
    {
        swingT = 0f;
        swingKind = kind;
        Game.sfx.PlayUI(kind == "chop" || kind == "smash" || kind == "reap" ? "swing" : "slash", -6f, U.RandRange(0.9f, 1.1f));
        if (sweep)
        {
            // golpe em arco: leque de raios da direita para a esquerda, uma vez por parte atingida
            var hitParts = new HashSet<DummyPart>();
            bool any = false;
            for (int i = -3; i <= 3; i++)
            {
                var d = U.Axis(Vector3.up, i * 0.17f) * CamFwd;
                var hs = Phys.Ray(camNode.position, camNode.position + d * reach, Game.L_WORLD | Game.L_DUMMY | Game.L_PROPS, selfEx);
                if (!hs.hit || hs.part == null || !hitParts.Add(hs.part)) continue;
                var cut = (U.Axis(Vector3.up, -0.5f) * d).normalized;     // lâmina vem de lado
                hs.part.dummy.ApplyPenetration(hs.part, hs.position, (d + cut).normalized, energy * 0.85f, prof, hs.normal);
                if (Game.hud) Game.hud.Hitmarker(hs.part.dummy, hs.part);
                any = true;
            }
            if (any) AddShake(0.3f);
            return;
        }
        var h = AimRay(reach);
        if (!h.hit) return;
        var dir = CamFwd;
        if (h.part != null)
        {
            h.part.dummy.ApplyPenetration(h.part, h.position, dir, energy, prof, h.normal);
            if (Game.hud) Game.hud.Hitmarker(h.part.dummy, h.part);
            if (kind == "chop" || kind == "smash") AddShake(kind == "smash" ? 0.4f : 0.25f);
            if (kind == "smash") Game.sfx.Play("impact", h.position, 0f, 0.6f);
        }
        else
        {
            Game.sfx.Play("impact", h.position, -3f, 0.7f);
            if (h.body != null) h.body.AddForceAtPosition(dir * 6f, h.position, ForceMode.Impulse);
        }
    }

    void ThrowKnife(Vector3 from, Vector3 dir)
    {
        var a = new GameObject("faca").AddComponent<Arrow>();
        a.Launch(from, dir * 22f + velocity + Vector3.up * 0.8f, 135f, selfEx, Weapons.P_THROW_KNIFE, "dagger");
        Game.sfx.PlayUI("throw", -6f, U.RandRange(1.1f, 1.3f));
    }

    float fuel;

    void FlameSound(bool on)
    {
        if (flameLoop == null)
        {
            var go = new GameObject("som do lança-chamas");
            go.transform.SetParent(camNode, false);
            flameLoop = go.AddComponent<AudioSource>();
            flameLoop.loop = true; flameLoop.spatialBlend = 0f; flameLoop.playOnAwake = false;
            flameLoop.clip = Game.sfx.Clip("flame");
        }
        if (on && !flameLoop.isPlaying) { flameLoop.volume = 0f; flameLoop.Play(); }
        flameLoop.volume = Mathf.MoveTowards(flameLoop.volume, on ? 0.55f : 0f, Time.deltaTime * 4f);
        if (!on && flameLoop.isPlaying && flameLoop.volume <= 0.01f) flameLoop.Stop();
    }

    /// Estojo saindo pela janela de ejeção (à direita, um pouco atrás do meio da arma)
    void EjectCasing(WeaponDef w)
    {
        if (w.id == "revolver" || w.id == "sawedoff" || !GUN_VM.TryGetValue(w.id, out var g)) return;
        var port = muzzles[w.id].position + camNode.rotation * new Vector3(0, 0.02f, g[0] * 0.55f);
        var right = camNode.rotation * Vector3.right;
        bool shell = w.id == "shotgun";
        float scale = w.id == "sniper" ? 1.6f : (w.id == "rifle" || w.id == "m4" || w.id == "bullpup" ? 1.35f : (w.id == "deagle" ? 1.2f : 1f));
        Casing.Eject(port, right, camNode.rotation * Vector3.up, velocity, shell, scale);
    }

    void ShootArrow(Vector3 from, Vector3 fwd, float draw)
    {
        var a = new GameObject("flecha").AddComponent<Arrow>();
        float spd = 18f + 58f * draw;
        a.Launch(from, fwd * spd, 40f + 170f * draw, selfEx);
        Game.sfx.PlayUI("bow", -4f, 0.9f + draw * 0.25f);
    }

    void ThrowGrenade(Vector3 from, Vector3 fwd, float spd)
    {
        var g = new GameObject("granada").AddComponent<Grenade>();
        g.transform.position = from;
        g.rb.linearVelocity = fwd * spd + Vector3.up * (spd > 10f ? 3f : 0.5f) + velocity;
        g.rb.angularVelocity = U.RandomUnit() * 8f;
        Game.sfx.PlayUI("swing", -8f, 0.8f);
    }

    public void PlaceC4(RayHit h)
    {
        var c = new GameObject("C4").AddComponent<C4>();
        if (h.body != null) c.transform.SetParent(h.body.transform, true);
        c.transform.SetPositionAndRotation(h.position + h.normal * 0.005f, U.BasisY(h.normal));
        Game.sfx.Play("beep", h.position, -4f);
        if (Game.hud) Game.hud.Toast($"C4 grudada{(h.part != null ? " no boneco" : "")}. Botão direito para detonar.");
    }

    public void DetonateC4()
    {
        if (Game.c4s.Count == 0) { if (Game.hud) Game.hud.Toast("Nenhuma C4 colocada"); return; }
        Game.sfx.PlayUI("beep", -2f, 1.3f);
        foreach (var c in new List<C4>(Game.c4s)) if (c) c.Detonate();
    }

    static Material tracerMat;
    void Tracer(Vector3 a, Vector3 b)
    {
        if (Vector3.Distance(a, b) < 0.5f) return;
        var go = new GameObject("tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, a); lr.SetPosition(1, b);
        lr.startWidth = lr.endWidth = 0.006f;
        lr.sharedMaterial = tracerMat ? tracerMat : (tracerMat = Mats.Unlit(new Color(1f, 0.85f, 0.5f), null, true));
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go, 0.035f);
    }

    // ------------------------------------------------------------------ agarrar

    bool grabFirePressed;
    void HandleGrabInput()
    {
        if (InputX.Held(InputX.A.Grab))
        {
            if (grabBody == null)
            {
                var h = AimRay(3.5f);
                if (h.hit && h.body != null)
                {
                    var part = h.body.GetComponent<DummyPart>();
                    if (part != null && part.Freeze) part.dummy.Collapse();
                    if (!h.body.isKinematic)
                    {
                        grabBody = h.body;
                        grabLocal = Quaternion.Inverse(h.body.rotation) * (h.position - h.body.position);
                        grabDist = Vector3.Distance(camNode.position, h.position);
                    }
                }
            }
            if (grabBody && InputX.FireDown) grabFirePressed = true;
        }
        else grabBody = null;
    }

    void HandleGrabPhysics()
    {
        if (!grabBody) { grabBody = null; return; }
        var target = camNode.position + CamFwd * grabDist;
        var p = grabBody.position + grabBody.rotation * grabLocal;
        var f = (target - p) * 2600f - grabBody.linearVelocity * 110f;
        f = U.LimitLength(f, grabBody.GetComponent<DummyPart>() ? 1700f : 400f);
        grabBody.AddForceAtPosition(f, p);
        grabBody.WakeUp();
        if (grabFirePressed)
        {
            grabBody.AddForce(CamFwd * 20f * Mathf.Min(grabBody.mass, 10f), ForceMode.Impulse);
            grabBody = null;
        }
        grabFirePressed = false;
    }

    // ------------------------------------------------------------------ animação da arma

    void AnimateVm(float dt)
    {
        var feel = WeaponFeels.Of(Weapon().id);
        recoil = U.MoveToward(recoil, 0f, dt * 0.6f * feel.recovery);
        recoilYaw = U.MoveToward(recoilYaw, 0f, dt * 0.6f * feel.recovery);
        vmKick = U.MoveToward(vmKick, 0f, dt * 9f);
        shake = U.MoveToward(shake, 0f, dt * 2.5f);
        flashT -= dt;
        var w = Weapon();
        float steady = 1f - ads * 0.85f;
        bool isGun = w.type == "gun";
        // fôlego: correr cansa e a respiração desloca a arma até recuperar
        if (new Vector2(velocity.x, velocity.z).magnitude > WALK + 0.5f) windedT = 1f;
        else windedT = Mathf.Max(0f, windedT - dt / GameFeel.SprintRecoverTime);
        breathT += dt * Mathf.Lerp(1.6f, 3.2f, windedT);
        float b = Mathf.Sin(bob) * 0.012f * steady;
        var pos = new Vector3(b, Mathf.Abs(Mathf.Cos(bob)) * 0.01f * steady, vmKick * (0.06f - ads * 0.03f));
        var rot = new Vector3(vmKick * (0.12f - ads * 0.06f), 0, 0);
        // mirar pela arma: traz o topo da arma para o centro da tela
        if (ads > 0f && adsOffset.TryGetValue(w.id, out var ao)) pos += ao * U.Smoothstep(0, 1, ads);
        // inércia da mira (mola) e inclinação ao andar de lado
        swayVel += (-sway * 160f - swayVel * 18f) * dt;
        sway += swayVel * dt;
        sway = Vector2.ClampMagnitude(sway, 0.12f);
        var localVel = Quaternion.Inverse(transform.rotation) * velocity;
        rot += new Vector3(sway.y, sway.x, sway.x * 0.6f - localVel.x * 0.006f * steady) * (isGun ? feel.sway : 1f);
        pos += new Vector3(sway.x * 0.12f, -sway.y * 0.12f, 0);
        pos.y += Mathf.Sin(breathT) * Mathf.Lerp(GameFeel.BreathRest, GameFeel.BreathWinded, windedT) * steady * feel.sway;
        // sacar: a arma sobe de baixo
        if (drawT < 1f)
        {
            drawT = Mathf.Min(1f, drawT + dt / Mathf.Max(0.1f, feel.raiseTime));
            float d = 1f - U.Smoothstep(0, 1, drawT);
            pos += new Vector3(0.04f * d, -0.28f * d, 0.05f * d);
            rot += new Vector3(0.7f * d, 0, -0.3f * d);
        }
        if ((w.type == "melee" || w.type == "throwknife") && swingT < 0.5f)
        {
            swingT += dt;
            float s = Mathf.Sin(Mathf.Clamp01(swingT / 0.25f) * Mathf.PI);
            // antecipação: recua um pouco antes do golpe
            float wind = swingT < 0.07f ? Mathf.Sin(swingT / 0.07f * Mathf.PI) : 0f;
            switch (swingKind)
            {
                case "slash": rot += new Vector3(-0.25f * s + 0.2f * wind, 1.0f * s - 0.3f * wind, 0.7f * s); pos += new Vector3(-0.17f * s, 0, -0.1f * s); break;
                case "stab": pos += new Vector3(-0.05f * s, 0.03f * s, -0.25f * s + 0.06f * wind); break;
                case "thrust": pos += new Vector3(-0.06f * s, 0.02f * s, -0.42f * s + 0.12f * wind); rot += new Vector3(0.05f * s, 0, 0); break;
                case "chop": rot += new Vector3(-1.1f * s + 0.4f * wind, 0.2f * s, 0); pos += new Vector3(-0.08f * s, 0.08f * s, -0.15f * s); break;
                case "reap": rot += new Vector3(0.1f * s, 1.5f * s - 0.5f * wind, 0.5f * s); pos += new Vector3(-0.28f * s + 0.08f * wind, -0.04f * s, -0.12f * s); break;
                case "throw": rot += new Vector3(-0.7f * s + 0.5f * wind, 0, 0); pos += new Vector3(-0.04f * s, 0.06f * s + 0.05f * wind, -0.22f * s + 0.08f * wind); break;
                case "smash": rot += new Vector3(-1.45f * s + 0.6f * wind, 0.15f * s, 0.1f * s); pos += new Vector3(-0.1f * s, 0.12f * s - 0.05f * wind, -0.2f * s); break;
            }
        }
        if (reloadT >= 0f)
        {
            float r = Mathf.Sin(Mathf.Clamp01(reloadT) * Mathf.PI);
            pos += new Vector3(-0.04f * r, -0.12f * r, 0.05f * r);
            rot += new Vector3(0.45f * r, 0.15f * r, 0.7f * r);
        }
        if (w.id == "bow" && bowParts.ContainsKey("string"))
        {
            var st = bowParts["string"];
            st.localPosition = new Vector3(0, 0, 0.12f + bowDraw * 0.22f);
            var ar = bowParts["arrow"];
            ar.gameObject.SetActive(cooldown <= 0f);
            ar.localPosition = new Vector3(0, 0, -0.18f + bowDraw * 0.22f);
            if (bowParts.TryGetValue("pull", out var pull)) pull.localPosition = new Vector3(0, 0, 0.12f + bowDraw * 0.22f);
            pos += new Vector3(-0.05f * bowDraw, 0.02f * bowDraw, 0f);
        }
        // a arma persegue a câmera com atraso proporcional à massa (mola amortecida)
        float lagMul = isGun ? Mathf.Sqrt(feel.mass) : 0.7f;
        vmPos = Vector3.SmoothDamp(vmPos, pos, ref vmPosVel, GameFeel.WeaponPositionLag * lagMul, float.PositiveInfinity, dt);
        vmRot = Vector3.SmoothDamp(vmRot, rot, ref vmRotVel, GameFeel.WeaponRotationLag * lagMul, float.PositiveInfinity, dt);
        vmRoot.localPosition = vmPos;
        vmRoot.localRotation = U.Euler(vmRot);
        camNode.localRotation = U.Euler(recoil + U.RandRange(-1, 1) * shake * 0.02f, recoilYaw + U.RandRange(-1, 1) * shake * 0.02f, 0);
        var mz = muzzles[w.id];
        flashLight.intensity = flashT > 0f ? 5f : 0f;
        bool fv = flashT > 0f && w.type == "gun";
        flashMesh.SetActive(fv);
        if (fv)
        {
            flashLight.transform.parent.position = mz.position;
            flashMesh.transform.position = mz.position;
        }
    }
}

/// Espelha a imagem na horizontal (o jogo usa os números do Godot, de mão direita; o Unity é de mão esquerda).
/// Com a projeção invertida em X e o culling invertido, a imagem fica idêntica à do Godot.
public class MirrorCamera : MonoBehaviour
{
    Camera cam;
    void Awake() { cam = GetComponent<Camera>(); }
    void OnPreCull()
    {
        cam.ResetProjectionMatrix();
        cam.projectionMatrix = cam.projectionMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));
    }
    void OnPreRender() { GL.invertCulling = true; }
    void OnPostRender() { GL.invertCulling = false; }
}
