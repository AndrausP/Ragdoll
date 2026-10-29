using UnityEngine;

/// Cena principal: monta mapa, sistemas, jogador, HUD e bonecos (equivale ao main.gd).
public class MainScene : MonoBehaviour
{
    static readonly Vector3[] SPAWNS = { new Vector3(-2.6f, 0.005f, -5.5f), new Vector3(0, 0.005f, -5.5f), new Vector3(2.6f, 0.005f, -5.5f), new Vector3(18, 4.005f, -5) };
    int count;

    public Vector3 ExitPoint => MapBuilder.EXIT;

    /// Próximo ponto de tarefa para um boneco (evita repetir o mesmo)
    public Vector3 TaskPoint(Dummy d)
    {
        var p = MapBuilder.TASKS[U.RandInt(0, MapBuilder.TASKS.Length - 1)];
        return p + new Vector3(U.RandRange(-0.6f, 0.6f), 0, U.RandRange(-0.6f, 0.6f));
    }

    /// Garante a cena mesmo que o projeto seja aberto sem a cena Main
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<MainScene>() == null)
            new GameObject("Main").AddComponent<MainScene>();
    }

    void Awake()
    {
        Game.Init();
        Game.main = this;
        var map = new GameObject("Mapa");
        MapBuilder.Build(map.transform);
        Game.blood = new GameObject("Sangue").AddComponent<BloodSystem>();
        Game.sfx = new GameObject("Sons").AddComponent<Sfx>();
        var pl = new GameObject("Jogador");
        pl.transform.position = new Vector3(0, 0.05f, 4);
        Game.player = pl.AddComponent<Player>();
        Game.hud = new GameObject("HUD").AddComponent<Hud>();
        SpawnDefaults();
        Game.LogEvent("Bem-vindo ao laboratório. H esconde a ajuda, Tab o painel médico.", new Color(0.7f, 0.9f, 1f));
        if (Game.autotest) gameObject.AddComponent<AutoTest>();
        if (Game.shotsDir != "") gameObject.AddComponent<Shots>();
        if (Game.physTest) gameObject.AddComponent<PhysTest>();
        if (Game.stressTest) gameObject.AddComponent<StressTest>();
        if (Game.agonyTest) gameObject.AddComponent<AgonyTest>();
        if (Game.hazardTest) gameObject.AddComponent<HazardTest>();
        if (Game.gaitTest) gameObject.AddComponent<GaitTest>();
        if (Game.cutTest) gameObject.AddComponent<CutTest>();
        if (Game.woundTest) gameObject.AddComponent<WoundTest>();
        if (Game.blastTest) gameObject.AddComponent<BlastTest>();
        if (Game.weaponTest) gameObject.AddComponent<WeaponTest>();
        if (Game.getUpTest) gameObject.AddComponent<GetUpTest>();
    }

    void SpawnDefaults()
    {
        foreach (var p in SPAWNS) SpawnDummy(p, Quaternion.identity);
    }

    public Dummy SpawnDummy(Vector3 pos, Quaternion rot)
    {
        count++;
        var go = new GameObject($"Boneco #{count}");
        go.transform.SetPositionAndRotation(pos, rot);
        var d = go.AddComponent<Dummy>();
        d.dummyName = $"Boneco #{count}";
        d.Build();
        Game.dummies.Add(d);
        return d;
    }

    void Update()
    {
        if (InputX.Down(InputX.A.Spawn))
        {
            var p = Game.player;
            var fwd = -(p.transform.rotation * Vector3.forward);
            fwd.y = 0; fwd.Normalize();
            var pos = p.transform.position + fwd * 2.6f;
            var h = Phys.Ray(pos + Vector3.up * 3f, pos + Vector3.down * 10f, Game.L_WORLD);
            if (h.hit) pos = h.position + Vector3.up * 0.005f;
            SpawnDummy(pos, U.LookingAt(fwd, Vector3.up));
            Game.LogEvent("Novo boneco criado", new Color(0.7f, 1f, 0.7f));
        }
        else if (InputX.Down(InputX.A.Reset)) ResetLab();
        else if (InputX.Down(InputX.A.Clean))
        {
            Game.blood.ClearAll();
        Flames.ClearAll();
            Game.LogEvent("Sangue limpo", new Color(0.7f, 0.9f, 1f));
        }
        else if (InputX.Down(InputX.A.Trap)) Hazard.ActivateAimed(Game.player.HeadPosition, Game.player.AimDirection);
        else if (InputX.Down(InputX.A.XRay))
        {
            Game.xray = !Game.xray;
            foreach (var d in Game.dummies) if (d) d.SetXray(Game.xray);
        }
        else if (InputX.Down(InputX.A.Slowmo))
        {
            Time.timeScale = Time.timeScale > 0.99f ? 0.25f : 1f;
            Time.fixedDeltaTime = Time.timeScale / 120f;
        }
    }

    public void ResetLab()
    {
        foreach (var d in Game.dummies) if (d) Destroy(d.gameObject);
        Game.dummies.Clear();
        Game.ClearTracked();
        Game.blood.ClearAll();
        Flames.ClearAll();
        count = 0;
        SpawnDefaults();
        Game.LogEvent("Laboratório resetado", new Color(0.7f, 0.9f, 1f));
    }
}
