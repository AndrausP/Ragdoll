using System.Collections.Generic;
using UnityEngine;

/// Cartuchos ejetados: cilindrinhos de latão (ou cartucho plástico de escopeta) com física, som ao bater no chão
/// e limite de quantidade (os mais antigos somem).
public class Casing : MonoBehaviour
{
    const int MAX = 90;
    static readonly Queue<GameObject> live = new Queue<GameObject>();
    static Material brass, shell, brassBase;
    float soundCd;
    int bounces;

    public static void Eject(Vector3 pos, Vector3 right, Vector3 up, Vector3 inherit, bool shotgun, float scale = 1f)
    {
        if (brass == null)
        {
            brass = Mats.Mat(new Color(0.78f, 0.58f, 0.25f), 0.3f, 0.95f);
            brassBase = Mats.Mat(new Color(0.7f, 0.5f, 0.22f), 0.35f, 0.9f);
            shell = Mats.Mat(new Color(0.6f, 0.06f, 0.05f), 0.55f);
        }
        while (live.Count >= MAX) { var old = live.Dequeue(); if (old) Destroy(old); }
        var go = new GameObject(shotgun ? "cartucho" : "estojo");
        go.layer = Game.LAYER_PROPS;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(right, up) * Quaternion.Euler(90, 0, 0);
        float r = (shotgun ? 0.0105f : 0.0048f) * scale, h = (shotgun ? 0.065f : 0.022f) * scale;
        Prims.Visual("corpo", Prims.Cylinder(r, r, h, 10), shotgun ? shell : brass, go.transform, false);
        var b = Prims.Visual("base", Prims.Cylinder(r * 1.05f, r * 1.05f, h * (shotgun ? 0.3f : 0.15f), 10), brassBase, go.transform, false);
        b.transform.localPosition = new Vector3(0, -h * (shotgun ? 0.35f : 0.42f), 0);
        var cc = go.AddComponent<CapsuleCollider>(); cc.radius = r; cc.height = h + r * 2f; cc.direction = 1;
        cc.sharedMaterial = new PhysicsMaterial { bounciness = 0.35f, dynamicFriction = 0.5f, staticFriction = 0.6f };
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = shotgun ? 0.02f : 0.01f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearVelocity = inherit + right * U.RandRange(1.6f, 2.6f) + up * U.RandRange(1.2f, 2.2f) + U.RandomUnit() * 0.3f;
        rb.angularVelocity = U.RandomUnit() * 25f;
        go.AddComponent<Casing>();
        live.Enqueue(go);
        Game.Track(go);
    }

    void OnCollisionEnter(Collision c)
    {
        if (soundCd > 0f || bounces > 4) return;
        float v = c.relativeVelocity.magnitude;
        if (v < 0.4f) return;
        bounces++;
        soundCd = 0.06f;
        Game.sfx.Play("shell", transform.position, -16f + Mathf.Min(v, 4f) * 2f, U.RandRange(0.9f, 1.25f));
    }

    void Update() { soundCd -= Time.deltaTime; }
}
