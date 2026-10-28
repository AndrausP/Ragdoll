using System;
using System.Collections.Generic;
using UnityEngine;

/// Equivalente ao autoload "Game" do Godot: referências globais, camadas de física, log de eventos.
public static class Game
{
    // camadas do Unity (índices) e máscaras equivalentes às do Godot
    public const int LAYER_WORLD = 8, LAYER_DUMMY = 9, LAYER_PLAYER = 10, LAYER_PROPS = 11;
    public const int L_WORLD = 1 << LAYER_WORLD;
    public const int L_DUMMY = 1 << LAYER_DUMMY;
    public const int L_PLAYER = 1 << LAYER_PLAYER;
    public const int L_PROPS = 1 << LAYER_PROPS;

    public static MainScene main;
    public static BloodSystem blood;
    public static Sfx sfx;
    public static Hud hud;
    public static Player player;
    public static readonly List<Dummy> dummies = new List<Dummy>();
    public static readonly List<GameObject> projectiles = new List<GameObject>();
    public static readonly List<C4> c4s = new List<C4>();
    public static bool xray;
    public static bool autotest;
    public static string shotsDir = "";
    public static bool physTest, stressTest;
    static bool? bodyV2;      // argumento de linha de comando; sem ele vale a preferência salva

    public static event Action<string, Color> EventLogged;

    public static void Init()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var a in args)
        {
            if (a == "--autotest" || a == "-autotest") autotest = true;
            else if (a.StartsWith("--shots=")) shotsDir = a.Substring(8);
            else if (a == "--phystest") physTest = true;
            else if (a == "--stress") stressTest = true;
            else if (a == "--body=v2") bodyV2 = true;
            else if (a == "--body=v1") bodyV2 = false;
        }
        MeshStore.SelectBody(bodyV2 ?? PlayerPrefs.GetInt("corpo_v2", 0) == 1);
        // testes automáticos e capturas rodam sem som
        AudioListener.volume = autotest || physTest || stressTest || shotsDir != "" ? 0f : 1f;
        // matriz de colisão = máscaras do Godot
        for (int i = 0; i < 32; i++)
            Physics.IgnoreLayerCollision(LAYER_PLAYER, i, i != LAYER_WORLD);
        Physics.IgnoreLayerCollision(LAYER_PLAYER, LAYER_WORLD, false);
        Physics.gravity = new Vector3(0, -9.8f, 0);
        Time.fixedDeltaTime = 1f / 120f;
        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 4;
        Physics.defaultContactOffset = 0.004f;
        Physics.sleepThreshold = 0.01f;
        Physics.defaultMaxAngularSpeed = 60f;
        dummies.Clear(); projectiles.Clear(); c4s.Clear();
    }

    public static void LogEvent(string text, Color? color = null)
    {
        var c = color ?? new Color(1, 0.85f, 0.8f);
        EventLogged?.Invoke(text, c);
        if (autotest)
            Debug.Log("[EVENTO] " + text);
    }

    public static Vector3 RandomUnit() => U.RandomUnit();

    public static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    /// Luz com orientação do Godot: no Godot a luz aponta para -Z do nó; no Unity para +Z.
    /// O nó recebe a rotação do Godot e um filho girado 180° carrega o componente Light.
    public static Light MakeLight(LightType type, Transform parent, Vector3 pos, Quaternion godotRot, Color color, float energy, float range = 10f, bool shadows = false)
    {
        var node = new GameObject(type + " light");
        node.transform.SetParent(parent, false);
        node.transform.localPosition = pos;
        node.transform.localRotation = godotRot;
        var lgo = new GameObject("light");
        lgo.transform.SetParent(node.transform, false);
        lgo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var l = lgo.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = energy;
        l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        if (shadows) { l.shadowBias = 0.03f; l.shadowNormalBias = 0.3f; }
        return l;
    }

    /// Registra um objeto que o "resetar" deve apagar (grupo "projectiles" do Godot)
    public static void Track(GameObject go) { projectiles.Add(go); }

    public static void ClearTracked()
    {
        foreach (var g in projectiles) if (g) UnityEngine.Object.Destroy(g);
        projectiles.Clear();
        c4s.Clear();
    }

    /// Game.model: instancia uma arma (malha de weapons.bytes) girada por rot, com o maior lado = length,
    /// centralizada na origem. size (já escalado) volta em sizeOut.
    public static GameObject Model(string id, Quaternion rot, float length, out Vector3 sizeOut, bool shadows = true, string hideMaterial = null)
    {
        var holder = new GameObject("model_" + id);
        sizeOut = Vector3.zero;
        var mm = MeshStore.Weapon(id);
        if (mm == null) return holder;
        var pivot = new GameObject("pivot");
        pivot.transform.SetParent(holder.transform, false);
        var inst = new GameObject(id);
        inst.transform.SetParent(pivot.transform, false);
        inst.AddComponent<MeshFilter>().sharedMesh = mm.mesh;
        var mr = inst.AddComponent<MeshRenderer>();
        var mats = MeshStore.WeaponMaterials(mm, hideMaterial);
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        // AABB da malha girada
        var b = mm.mesh.bounds;
        Vector3 mn = Vector3.one * float.MaxValue, mx = Vector3.one * float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            var r = rot * c;
            mn = Vector3.Min(mn, r); mx = Vector3.Max(mx, r);
        }
        var size = mx - mn;
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float s = length / Mathf.Max(longest, 1e-5f);
        var center = (mn + mx) * 0.5f;
        pivot.transform.localRotation = rot;
        pivot.transform.localScale = Vector3.one * s;
        pivot.transform.localPosition = -center * s;
        sizeOut = size * s;
        return holder;
    }
}
