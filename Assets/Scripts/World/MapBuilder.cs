using UnityEngine;

/// Laboratório gerado por código (mesmos números do map_builder.gd) + saída sinalizada e pontos de tarefa.
public static class MapBuilder
{
    static Texture2D concrete, tiles;

    public static readonly Vector3 EXIT = new Vector3(0, 0, 38.6f);
    public static readonly Vector3[] TASKS =
    {
        new Vector3(-5f, 0, -1.8f),   // mesa de autópsia
        new Vector3(9.5f, 0, 6.2f),   // barris
        new Vector3(-6.5f, 0, 6.6f),  // caixas
        new Vector3(12.2f, 0, 7.5f),  // pé da escada
        new Vector3(0, 0, 12f),       // centro do salão
        new Vector3(-17f, 0, 0f),     // colunas
    };

    public static void Build(Transform root)
    {
        MakeTextures();
        Environment(root);
        var conc = Mats.Mat(Color.white, 0.85f);
        conc.SetTexture("_MainTex", concrete);
        conc.SetFloat("_Tri", 1); conc.SetFloat("_TriScale", 0.25f); conc.SetFloat("_RoughTex", 1);
        var tile = Mats.Mat(Color.white, 0.12f);
        tile.SetTexture("_MainTex", tiles);
        tile.SetFloat("_Tri", 1); tile.SetFloat("_TriScale", 0.5f);
        var dark = Mats.Mat(new Color(0.22f, 0.23f, 0.26f), 0.8f);
        // texturas reais (ambientCG, CC0) quando presentes; senão as procedurais acima
        var floorM = Mats.Pbr("Concrete047A", 0.35f, new Color(0.78f, 0.78f, 0.8f), 1.2f) ?? conc;
        var wallM = Mats.Pbr("Concrete048", 0.4f, new Color(0.85f, 0.85f, 0.86f)) ?? conc;
        tile = Mats.Pbr("Tiles133A", 0.8f, Color.white, 1f, 0.8f) ?? tile;
        var plate = Mats.Pbr("DiamondPlate008C", 0.8f, new Color(0.75f, 0.75f, 0.78f), 1f, 1f, 0.6f) ?? conc;
        var yellow = Mats.Mat(new Color(0.95f, 0.68f, 0.08f), 0.6f);
        var metal = Mats.Mat(new Color(0.4f, 0.42f, 0.45f), 0.4f, 0.7f);

        // chão e paredes externas
        Block(root, new Vector3(90, 1, 90), new Vector3(0, -0.5f, 0), floorM);
        foreach (int s in new[] { -1, 1 })
        {
            Block(root, new Vector3(90, 7, 1), new Vector3(0, 3.5f, 40.5f * s), wallM);
            Block(root, new Vector3(1, 7, 90), new Vector3(40.5f * s, 3.5f, 0), wallM);
        }
        // câmara de testes (azulejo branco)
        Block(root, new Vector3(18, 4.5f, 0.4f), new Vector3(0, 2.25f, -10), tile);
        Block(root, new Vector3(0.4f, 4.5f, 9), new Vector3(-9, 2.25f, -5.5f), tile);
        Block(root, new Vector3(0.4f, 4.5f, 9), new Vector3(9, 2.25f, -5.5f), tile);
        Block(root, new Vector3(18.8f, 0.3f, 9.4f), new Vector3(0, 4.65f, -5.5f), dark);
        // ralo e faixas amarelas
        var drain = Prims.Visual("ralo", Prims.Cylinder(0.25f, 0.25f, 0.01f, 24), metal, root);
        drain.transform.localPosition = new Vector3(0, 0.005f, -6);
        for (int i = 0; i < 9; i++) Deco(root, new Vector3(1.2f, 0.01f, 0.18f), new Vector3(-8 + i * 2, 0.006f, -0.6f), yellow);
        for (int i = 0; i < 14; i++)
        {
            float a = i * U.TAU / 14f;
            var m = Deco(root, new Vector3(0.9f, 0.01f, 0.16f), new Vector3(Mathf.Cos(a) * 7f, 0.006f, Mathf.Sin(a) * 7f + 14f), yellow);
            m.transform.localRotation = U.Axis(Vector3.up, -a + Mathf.PI / 2);
        }
        // plataforma alta com escada
        Block(root, new Vector3(7, 0.4f, 8), new Vector3(18, 3.8f, -4), plate);
        foreach (var c in new[] { new Vector3(15, 1.8f, -7.5f), new Vector3(21, 1.8f, -7.5f), new Vector3(15, 1.8f, -0.5f), new Vector3(21, 1.8f, -0.5f) })
            Block(root, new Vector3(0.4f, 3.6f, 0.4f), c, dark);
        for (int i = 0; i < 16; i++) Block(root, new Vector3(2f, 0.25f, 0.5f), new Vector3(13.5f, 0.125f + i * 0.25f, 5.5f - i * 0.5f), plate);
        Deco(root, new Vector3(7, 0.02f, 0.2f), new Vector3(18, 4.01f, -0.1f), yellow);
        // colunas
        foreach (var p in new[] { new Vector3(-14, 3, 6), new Vector3(-14, 3, -6), new Vector3(-22, 3, 6), new Vector3(-22, 3, -6) })
            Block(root, new Vector3(1, 6, 1), p, wallM);
        // caixas estáticas
        var wood = Mats.Mat(new Color(0.45f, 0.33f, 0.18f), 0.9f);
        Block(root, Vector3.one * 1.2f, new Vector3(-6, 0.6f, 8), wood);
        Block(root, Vector3.one * 1.2f, new Vector3(-7.3f, 0.6f, 8.2f), wood);
        Block(root, Vector3.one * 1.2f, new Vector3(-6.6f, 1.8f, 8.1f), wood);
        // mesa de autópsia
        Block(root, new Vector3(2.2f, 0.1f, 0.9f), new Vector3(-5, 0.9f, -3), metal);
        foreach (var c in new[] { new Vector3(-5.9f, 0.43f, -3.3f), new Vector3(-4.1f, 0.43f, -3.3f), new Vector3(-5.9f, 0.43f, -2.7f), new Vector3(-4.1f, 0.43f, -2.7f) })
            Block(root, new Vector3(0.08f, 0.86f, 0.08f), c, metal);
        // barris soltos
        var barrelCols = new[] { new Color(0.1f, 0.3f, 0.6f), new Color(0.6f, 0.12f, 0.08f), new Color(0.2f, 0.4f, 0.2f) };
        for (int i = 0; i < 6; i++)
        {
            var b = new GameObject("barril");
            b.layer = Game.LAYER_PROPS;
            b.transform.SetParent(root, false);
            var cc = b.AddComponent<CapsuleCollider>(); cc.radius = 0.3f; cc.height = 0.9f;
            var bx = b.AddComponent<BoxCollider>(); bx.size = new Vector3(0.42f, 0.9f, 0.42f);
            Prims.Visual("m", Prims.Cylinder(0.3f, 0.3f, 0.9f, 20), Mats.Mat(barrelCols[i % 3], 0.5f, 0.4f), b.transform);
            var rb = b.AddComponent<Rigidbody>(); rb.mass = 25f;
            b.transform.position = new Vector3(10 + (i % 3) * 0.7f, 0.46f, 8 + (i / 3) * 0.7f);
        }
        // saída: batente na parede do fundo e placa verde
        var frame = Mats.Mat(new Color(0.3f, 0.3f, 0.32f), 0.5f, 0.6f);
        Block(root, new Vector3(0.2f, 2.4f, 0.3f), new Vector3(-0.85f, 1.2f, 39.9f), frame);
        Block(root, new Vector3(0.2f, 2.4f, 0.3f), new Vector3(0.85f, 1.2f, 39.9f), frame);
        Block(root, new Vector3(1.9f, 0.2f, 0.3f), new Vector3(0, 2.5f, 39.9f), frame);
        Deco(root, new Vector3(1.5f, 2.3f, 0.05f), new Vector3(0, 1.15f, 39.97f), Mats.Mat(new Color(0.1f, 0.12f, 0.1f), 0.4f, 0.5f));
        var sign = Prims.Visual("SAÍDA", Prims.Box(new Vector3(0.9f, 0.3f, 0.05f)), Mats.Emissive(new Color(0.15f, 1f, 0.3f), 3f), root, false);
        sign.transform.localPosition = new Vector3(0, 2.95f, 39.95f);
        Game.MakeLight(LightType.Point, root, new Vector3(0, 2.9f, 39.3f), Quaternion.identity, new Color(0.3f, 1f, 0.4f), 1.2f, 4f);

        // luzes (rotações do Godot; MakeLight cuida da diferença de eixo)
        Game.MakeLight(LightType.Directional, root, Vector3.zero, U.Euler(-48 * Mathf.Deg2Rad, 35 * Mathf.Deg2Rad, 0), new Color(1f, 0.85f, 0.7f), 0.55f, 0, true);
        foreach (float x in new[] { -5f, 0f, 5f })
        {
            Game.MakeLight(LightType.Point, root, new Vector3(x, 4.2f, -5.5f), Quaternion.identity, new Color(0.92f, 0.95f, 1f), 2.2f, 9f, true);
            var lamp = Prims.Visual("lâmpada", Prims.Box(new Vector3(1.2f, 0.05f, 0.25f)), Mats.Emissive(new Color(0.95f, 0.97f, 1f), 3f), root, false);
            lamp.transform.localPosition = new Vector3(x, 4.48f, -5.5f);
        }
        var fill = Game.MakeLight(LightType.Spot, root, new Vector3(0, 3.2f, 1.5f), U.Euler(-28 * Mathf.Deg2Rad, 0, 0), new Color(1f, 0.96f, 0.9f), 3f, 16f);
        fill.spotAngle = 100f;
        Game.MakeLight(LightType.Point, root, new Vector3(8.5f, 3.5f, -9.5f), Quaternion.identity, new Color(1, 0.15f, 0.1f), 1.6f, 5f);
        Game.MakeLight(LightType.Point, root, new Vector3(18, 7, -4), Quaternion.identity, Color.white, 1.4f, 10f);
        // acústica: galpão de concreto (eco longo) e câmara de azulejo (reverberação curta e brilhante)
        var hall = new GameObject("eco do galpão").AddComponent<AudioReverbZone>();
        hall.transform.SetParent(root, false);
        hall.reverbPreset = AudioReverbPreset.User;     // galpão: cauda ~2 s (o preset Hangar embola os tiros)
        hall.room = -900; hall.roomHF = -400; hall.decayTime = 2.2f; hall.decayHFRatio = 0.6f;
        hall.reflections = -500; hall.reflectionsDelay = 0.03f; hall.reverb = -300; hall.reverbDelay = 0.04f;
        hall.minDistance = 30f; hall.maxDistance = 55f;
        var room = new GameObject("eco da câmara").AddComponent<AudioReverbZone>();
        room.transform.SetParent(root, false);
        room.transform.localPosition = new Vector3(0, 2f, -5.5f);
        room.reverbPreset = AudioReverbPreset.Bathroom;
        room.minDistance = 5f; room.maxDistance = 8f;
                // reflexos da câmara de testes
        var probeGo = new GameObject("reflexos");
        probeGo.transform.SetParent(root, false);
        probeGo.transform.localPosition = new Vector3(0, 2.3f, -5.5f);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.size = new Vector3(18.5f, 4.8f, 9.5f);
        probe.boxProjection = true;
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.resolution = 128;
    }

    static GameObject Block(Transform root, Vector3 size, Vector3 pos, Material mat)
    {
        var go = Prims.Visual("bloco", Prims.Box(size), mat, root);
        go.layer = Game.LAYER_WORLD;
        go.isStatic = true;
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static GameObject Deco(Transform root, Vector3 size, Vector3 pos, Material mat)
    {
        var go = Prims.Visual("deco", Prims.Box(size), mat, root, false);
        go.transform.localPosition = pos;
        return go;
    }

    static void Environment(Transform root)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.26f, 0.27f, 0.33f);
        RenderSettings.ambientEquatorColor = new Color(0.25f, 0.22f, 0.22f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.09f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = new Color(0.12f, 0.11f, 0.12f);
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        QualitySettings.shadowDistance = 60f;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.pixelLightCount = 8;
        QualitySettings.antiAliasing = 2;
    }

    static void MakeTextures()
    {
        if (concrete != null) return;
        int s = 256;
        concrete = new Texture2D(s, s, TextureFormat.RGB24, true);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float v = 0.5f + (Mathf.PerlinNoise(x * 0.03f + 3.1f, y * 0.03f + 7.7f) - 0.5f) * 0.36f
                               + (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) - 0.5f) * 0.12f;
                if (x % 128 < 2 || y % 128 < 2) v *= 0.65f;
                px[y * s + x] = new Color(v * 0.95f, v * 0.95f, v);
            }
        concrete.SetPixels(px); concrete.Apply(true);
        concrete.wrapMode = TextureWrapMode.Repeat;
        tiles = new Texture2D(128, 128, TextureFormat.RGB24, true);
        var tp = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                bool g = x % 64 < 2 || y % 64 < 2;
                float v2 = g ? 0.62f : 0.92f + (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) - 0.5f) * 0.06f;
                tp[y * 128 + x] = new Color(v2, v2, v2 * 1.02f);
            }
        tiles.SetPixels(tp); tiles.Apply(true);
        tiles.wrapMode = TextureWrapMode.Repeat;
    }
}
