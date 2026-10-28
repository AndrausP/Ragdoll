using System.Collections.Generic;
using UnityEngine;

/// Boneco: ragdoll com fisiologia (sangue, coração, pulmões, cérebro, dor, consciência), dano anatômico,
/// amputação, IA de agonia/autotratamento no chão e, em pé, locomoção com objetivo (Dummy.Stand.cs).
public partial class Dummy : MonoBehaviour
{
    public enum State { STANDING, DOWN, UNCONSCIOUS, DEAD }

    static readonly HashSet<string> UPPER = new HashSet<string> { "abdomen", "chest", "neck", "head", "upper_arm_l", "forearm_l", "hand_l", "upper_arm_r", "forearm_r", "hand_r" };

    /// No Unity o ângulo X do ConfigurableJoint tem sinal oposto à rotação local da peça; ajustável se o teste mostrar o contrário.
    public static float HingeSign = -1f;

    public readonly Dictionary<string, DummyPart> parts = new Dictionary<string, DummyPart>();
    SkinnedMeshRenderer skinBody, skinCloth;
    Material skinShader, skinXray;
    readonly float[] bloodLevels = new float[17];
    readonly Dictionary<string, Transform> boneProxy = new Dictionary<string, Transform>();
    public State state = State.STANDING;
    public string dummyName = "Boneco";

    // ---- fisiologia
    public float blood = Anatomy.BLOOD_TOTAL, heartRate = 76f, systolic = 120f, diastolic = 80f, spo2 = 98f, respRate = 16f;
    public float consciousness = 1f, pain, acutePain, hypoxiaTime;
    public bool cardiacArrest, apnea, dead;
    public string paralysis = "", deathCause = "";
    public float deathTime, timeAlive, heartPhase, concussion, totalLost, bleedRateNow;
    public int tourniquetsLeft = 2;

    // ---- chão
    public string action = "none";
    float actionTime, actionDur, poseTimer, reachTimer, pressTimer, rollSign = 1f, twitchCd = 0.3f;
    public string curPose = "limp";
    DummyPart reachHand;
    Wound reachWound;
    public float muscleLevel;
    float physAcc;
    readonly Dictionary<string, float> chainInertia = new Dictionary<string, float>();

    public string lastHitSummary = "";
    public Vector3 entryLast;

    public Xf Root => new Xf(transform.rotation, transform.position);

    /// Aparência individual (corpo v2/v3); nula no corpo original.
    public Appearance look;

    public void Build()
    {
        var defs = Anatomy.Parts();
        var root = Root;
        if (MeshStore.UseV2) look = Appearance.Roll(dummyName);
        foreach (var id in Anatomy.ORDER)
        {
            var d = defs[id];
            var go = new GameObject(id);
            go.transform.SetParent(transform, false);
            var p = go.AddComponent<DummyPart>();
            p.Setup(this, id, d);
            p.Freeze = true;
            var w = root * Xf.At(d.pos);
            go.transform.SetPositionAndRotation(w.p, w.q);
            parts[id] = p;
            if (d.parent != "")
            {
                p.parentPart = parts[d.parent];
                p.parentPart.childrenParts.Add(p);
            }
        }
        // partes do mesmo boneco não colidem entre vizinhas (como as juntas do Godot)
        foreach (var id in Anatomy.ORDER)
            if (parts[id].def.joint != null) MakeJoint(parts[id]);
        foreach (var id in Anatomy.ORDER)
        {
            chainInertia[id] = ComputeChainInertia(parts[id]);
            prevPos[id] = parts[id].transform.position; prevRot[id] = parts[id].transform.rotation;
        }
        standRoot = root;
        BuildSkin();
        SetXray(Game.xray);
        InitBrain();
    }

    // ------------------------------------------------------------------ pele com esqueleto

    /// Pele única que dobra nas juntas; as peças rígidas de cada parte só aparecem quando ela é separada.
    void BuildSkin()
    {
        var sm = MeshStore.Human("body");
        if (sm == null || !sm.skinned) return;
        var go = new GameObject("body");
        go.transform.SetParent(transform, false);
        var bones = new Transform[Anatomy.ORDER.Length];
        var bind = new Matrix4x4[bones.Length];
        var defs = Anatomy.Parts();
        for (int i = 0; i < bones.Length; i++)
        {
            string id = Anatomy.ORDER[i];
            var b = new GameObject("bone_" + id).transform;
            b.SetParent(transform, false);
            b.localPosition = defs[id].pos;
            bones[i] = b;
            boneProxy[id] = b;
            bind[i] = Matrix4x4.Translate(-defs[id].pos);   // repouso: osso na posição da parte, sem rotação
        }
        // ossos auxiliares nas juntas que giram muito (ombro, cotovelo, quadril, joelho): ficam na junta e giram
        // a meio caminho entre pai e filho. O peso que a malha dividia entre os dois (média linear de duas matrizes,
        // que encolhe e estica a pele — a "asa" da axila com o braço erguido) passa para uma rotação de verdade.
        var allBones = new List<Transform>(bones);
        var allBind = new List<Matrix4x4>(bind);
        foreach (var child in HELPER_JOINTS)
        {
            var hb = new GameObject("aux_" + child).transform;
            hb.SetParent(transform, false);
            var anchor = defs[child].joint.anchor;
            hb.localPosition = anchor;
            helperBones[child] = hb;
            allBones.Add(hb);
            allBind.Add(Matrix4x4.Translate(-anchor));
        }
        var mesh = Object.Instantiate(sm.mesh);
        if (look != null)
        {
            look.Shape(mesh);
            if (sm.hasColors) mesh.colors = look.Recolor(mesh.colors, mesh.vertices);
        }
        mesh.boneWeights = HelperWeights(mesh.boneWeights, defs);
        // posição/normal de repouso para o detalhe (tecido, poros) não escorregar com a deformação
        mesh.SetUVs(2, new List<Vector3>(sm.vertices));
        mesh.SetUVs(3, new List<Vector3>(sm.mesh.normals));
        mesh.bindposes = allBind.ToArray();
        // roupa como casca separada: precisa das cores originais, então sai antes de a pele ser repintada
        var clothMesh = sm.hasColors ? ClothLayer.Extract(mesh) : null;
        skinBones = new List<Transform>(allBones);
        skinBind = new List<Matrix4x4>(allBind);
        skinBody = go.AddComponent<SkinnedMeshRenderer>();
        skinBody.sharedMesh = mesh;
        skinBody.bones = allBones.ToArray();
        skinBody.rootBone = transform;
        skinBody.updateWhenOffscreen = true;
        skinBody.quality = SkinQuality.Bone4;
        if (clothMesh != null)
        {
            var cgo = new GameObject("roupa");
            cgo.transform.SetParent(transform, false);
            skinCloth = cgo.AddComponent<SkinnedMeshRenderer>();
            skinCloth.sharedMesh = clothMesh;
            skinCloth.bones = allBones.ToArray();
            skinCloth.rootBone = transform;
            skinCloth.updateWhenOffscreen = true;
            skinCloth.quality = SkinQuality.Bone4;
            var mpb = new MaterialPropertyBlock();
            mpb.SetFloat("_Push", ClothLayer.THICK);
            skinCloth.SetPropertyBlock(mpb);
        }
        skinShader = new Material(Mats.Skin);
        skinShader.SetColor("_SkinColor", DummyPart.SKIN);
        skinShader.SetColor("_BloodColor", DummyPart.BLOOD_DARK);
        skinShader.SetFloat("_UseVC", sm.hasColors ? 1 : 0);
        if (look != null) { skinShader.SetFloat("_Sheen", look.sheen); skinShader.SetColor("_HairColor", look.hair); }
        skinShader.SetFloatArray("_Blood", bloodLevels);
        skinShader.SetFloatArray("_Char", charLevels);
        var fabric = Resources.Load<Texture2D>("Textures/Fabric030_Color");
        if (fabric != null)
        {
            skinShader.SetTexture("_Fabric", fabric);
            skinShader.SetTexture("_Pores", PoreNoise);
            skinShader.SetFloat("_Detail", sm.hasColors ? 1 : 0);
        }
        skinXray = Mats.FadeMat(new Color(DummyPart.SKIN.r, DummyPart.SKIN.g, DummyPart.SKIN.b, 0.22f), 0.6f);
        skinBody.sharedMaterial = skinShader;
        if (skinCloth) skinCloth.sharedMaterial = skinShader;
        foreach (var p in parts.Values) p.bodyMesh.SetActive(false);
        UpdateSkin();
    }

    static readonly string[] HELPER_JOINTS = { "upper_arm_l", "upper_arm_r", "forearm_l", "forearm_r", "thigh_l", "thigh_r", "shin_l", "shin_r" };
    readonly Dictionary<string, Transform> helperBones = new Dictionary<string, Transform>();

    /// Converte a parte dividida entre pai e filho (min dos dois pesos, dos dois lados) em peso do osso auxiliar.
    static BoneWeight[] HelperWeights(BoneWeight[] src, Dictionary<string, PartDef> defs)
    {
        int n = Anatomy.ORDER.Length;
        var o = new BoneWeight[src.Length];
        var idx = new int[4]; var w = new float[4];
        for (int i = 0; i < src.Length; i++)
        {
            var b = src[i];
            idx[0] = b.boneIndex0; idx[1] = b.boneIndex1; idx[2] = b.boneIndex2; idx[3] = b.boneIndex3;
            w[0] = b.weight0; w[1] = b.weight1; w[2] = b.weight2; w[3] = b.weight3;
            for (int h = 0; h < HELPER_JOINTS.Length; h++)
            {
                int c = Anatomy.OrderIndex(HELPER_JOINTS[h]);
                int p = Anatomy.OrderIndex(defs[HELPER_JOINTS[h]].parent);
                int sc = -1, sp = -1;
                for (int k = 0; k < 4; k++) { if (w[k] > 0f && idx[k] == c) sc = k; if (w[k] > 0f && idx[k] == p) sp = k; }
                if (sc < 0 || sp < 0) continue;
                float m = Mathf.Min(w[sc], w[sp]);
                w[sc] -= m; w[sp] -= m;
                int slot = w[sc] <= 1e-5f ? sc : sp;     // o que zerou vira o auxiliar
                idx[slot] = n + h; w[slot] = 2f * m;
            }
            // ordena por peso (o Unity espera do maior para o menor)
            for (int a = 1; a < 4; a++)
                for (int k = a; k > 0 && w[k] > w[k - 1]; k--)
                {
                    (w[k], w[k - 1]) = (w[k - 1], w[k]);
                    (idx[k], idx[k - 1]) = (idx[k - 1], idx[k]);
                }
            o[i] = new BoneWeight
            {
                boneIndex0 = idx[0], boneIndex1 = idx[1], boneIndex2 = idx[2], boneIndex3 = idx[3],
                weight0 = w[0], weight1 = w[1], weight2 = w[2], weight3 = w[3],
            };
        }
        return o;
    }

    readonly float[] charLevels = new float[17];

    /// Queimadura visível da parte (0..1): pele vermelha -> carbonizada.
    public void SetPartChar(string id, float v)
    {
        if (skinShader == null) return;
        charLevels[Anatomy.OrderIndex(id)] = Mathf.Clamp01(v);
        skinShader.SetFloatArray("_Char", charLevels);
    }

    static Texture2D poreNoise;
    /// Ruído fino (poros/irregularidade da pele), repetível
    static Texture2D PoreNoise
    {
        get
        {
            if (poreNoise) return poreNoise;
            const int S = 128;
            poreNoise = new Texture2D(S, S, TextureFormat.R8, true, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[S * S];
            var rng = new System.Random(7);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // ruído periódico (soma de senos com fases sorteadas) + grão
                    float v = 0.5f;
                    for (int k = 1; k <= 3; k++)
                        v += 0.12f / k * Mathf.Sin((x * k * 3 + y * k * 2) * U.TAU / S + k * 1.7f) * Mathf.Sin((y * k * 3 - x * k) * U.TAU / S + k);
                    v += ((float)rng.NextDouble() - 0.5f) * 0.35f;
                    px[y * S + x] = new Color(v, v, v);
                }
            poreNoise.SetPixels(px); poreNoise.Apply(true);
            return poreNoise;
        }
    }

    public void SetPartBlood(string id, float v)
    {
        if (skinShader == null) return;
        bloodLevels[Anatomy.OrderIndex(id)] = v;
        skinShader.SetFloatArray("_Blood", bloodLevels);
    }

    /// Pose de cada osso = movimento da parte desde o repouso. Partes separadas colapsam no coto.
    void UpdateSkin()
    {
        if (skinBody == null) return;
        var inv = Root.Inverse();
        foreach (var kv in parts)
        {
            var p = kv.Value;
            if (!p || !boneProxy.TryGetValue(kv.Key, out var bt)) continue;
            Xf xf;
            if (!Collapsed(p))
                xf = inv * p.Xform;     // osso = transformação da parte (a posição de repouso já está no bindpose)
            else
            {
                // encolhe no ponto da junta do primeiro ancestral que ainda tem pele (corpo ou fragmento de corte)
                var r = p;
                while (r.parentPart != null && Collapsed(r.parentPart)) r = r.parentPart;
                var par = r.parentPart;
                if (par == null || !par)
                {
                    bt.localScale = Vector3.one * 0.001f;
                    continue;
                }
                var a = r.def.joint.anchor;
                // colapsa no ponto da junta do pai: par * T(a - par.pos) * S(0.001) * T(p.pos - a)
                var parX = inv * par.Xform;
                var pivot = parX.Apply(a - par.def.pos);
                xf = new Xf(parX.q, pivot + parX.q * ((p.def.pos - a) * 0.001f), 0.001f);
            }
            bt.localPosition = xf.p;
            bt.localRotation = xf.q;
            bt.localScale = Vector3.one * xf.s;
        }
        // fragmentos de corte: o osso extra segue o corpo do fragmento
        foreach (var kv in fragBone)
        {
            if (!kv.Key) continue;
            var fx = inv * kv.Key.Xform;
            kv.Value.localPosition = fx.p; kv.Value.localRotation = fx.q; kv.Value.localScale = Vector3.one * (kv.Key.destroyed ? 0.001f : fx.s);
        }
        // auxiliares: na junta, metade da rotação entre pai e filho (filho separado: segue o pai)
        foreach (var kv in helperBones)
        {
            if (!parts.TryGetValue(kv.Key, out var c) || !c) continue;
            var par = c.parentPart;
            if (par == null || !par) continue;
            if (Collapsed(par) && boneProxy.TryGetValue(par.partId, out var pb))
            {
                // pai separado: imita o osso (colapsado) do pai — bind do auxiliar é T(-âncora), o do pai T(-pos)
                kv.Value.localRotation = pb.localRotation;
                kv.Value.localScale = pb.localScale;
                kv.Value.localPosition = pb.localPosition + pb.localRotation * Vector3.Scale(pb.localScale, c.def.joint.anchor - par.def.pos);
                continue;
            }
            var px = inv * par.Xform;
            var anchor = c.def.joint.anchor;
            var q = Collapsed(c) ? px.q : Quaternion.Slerp(px.q, (inv * c.Xform).q, 0.5f);
            // ombro e quadril: o auxiliar também leva pele do peito/virilha; com o membro jogado ao extremo (explosão,
            // arremesso) ele arrastaria essa pele junto. Até 60° do tronco cobre as poses normais.
            if (c.def.parent == "chest" || c.def.parent == "pelvis") q = Quaternion.RotateTowards(px.q, q, 60f);
            kv.Value.localPosition = px.Apply(anchor - par.def.pos);
            kv.Value.localRotation = q;
            kv.Value.localScale = Vector3.one;
        }
    }

    float flowTimer;
    void LateUpdate()
    {
        UpdateSkin();
        flowTimer += Time.deltaTime;
        if (flowTimer > 0.5f) { FlowStep(flowTimer); flowTimer = 0f; }
        FlushMesh();
    }

    // ------------------------------------------------------------------ juntas

    /// A junta trata a pose relativa do momento da criação como zero. `restRelative` (religar uma parte a um fragmento de
    /// corte com o corpo já dobrado): cria a junta com a parte na pose de repouso em relação ao pai e devolve a pose
    /// real em seguida, para os limites (dobradiça e cones de ombro, pulso, tornozelo, cabeça) ficarem anatômicos.
    void MakeJoint(DummyPart p, bool restRelative = false)
    {
        var jd = p.def.joint;
        var par = p.parentPart;
        Vector3 keepP = p.transform.position; Quaternion keepR = p.transform.rotation;
        if (restRelative)
        {
            p.transform.SetPositionAndRotation(par.Xform.Apply(p.def.pos - par.def.pos), par.transform.rotation);
            p.rb.position = p.transform.position; p.rb.rotation = p.transform.rotation;
            Physics.SyncTransforms();
        }
        var j = p.gameObject.AddComponent<ConfigurableJoint>();
        j.connectedBody = par.rb;
        j.autoConfigureConnectedAnchor = false;
        j.anchor = jd.anchor - p.def.pos;
        j.connectedAnchor = jd.anchor - par.def.pos;
        j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
        j.projectionMode = JointProjectionMode.PositionAndRotation;
        j.projectionDistance = 0.02f;
        j.projectionAngle = 4f;
        j.enablePreprocessing = false;
        if (jd.type == "hinge")
        {
            // dobradiça em torno do X do boneco; no Godot ângulo = -rotação X da peça
            j.axis = Vector3.right;
            j.secondaryAxis = Vector3.up;
            float rotMin = -jd.upper, rotMax = -jd.lower;         // faixa da rotação X da peça (rad)
            float lo = HingeSign > 0 ? rotMin : -rotMax, hi = HingeSign > 0 ? rotMax : -rotMin;
            j.angularXMotion = ConfigurableJointMotion.Limited;
            j.angularYMotion = ConfigurableJointMotion.Locked;
            j.angularZMotion = ConfigurableJointMotion.Locked;
            j.lowAngularXLimit = new SoftJointLimit { limit = Mathf.Clamp(lo * Mathf.Rad2Deg, -177f, 177f) };
            j.highAngularXLimit = new SoftJointLimit { limit = Mathf.Clamp(hi * Mathf.Rad2Deg, -177f, 177f) };
        }
        else
        {
            j.axis = jd.dir.y > 0 ? Vector3.up : Vector3.down;   // eixo de torção ao longo do osso
            j.secondaryAxis = Vector3.forward;
            float tw = Mathf.Clamp(jd.twist * Mathf.Rad2Deg, 3f, 177f);
            float sw = Mathf.Clamp(jd.swing * Mathf.Rad2Deg, 5f, 177f);
            j.angularXMotion = ConfigurableJointMotion.Limited;
            j.angularYMotion = ConfigurableJointMotion.Limited;
            j.angularZMotion = ConfigurableJointMotion.Limited;
            j.lowAngularXLimit = new SoftJointLimit { limit = -tw };
            j.highAngularXLimit = new SoftJointLimit { limit = tw };
            j.angularYLimit = new SoftJointLimit { limit = sw };
            j.angularZLimit = new SoftJointLimit { limit = sw };
        }
        p.joint = j;
        if (restRelative)
        {
            p.transform.SetPositionAndRotation(keepP, keepR);
            p.rb.position = keepP; p.rb.rotation = keepR;
            Physics.SyncTransforms();
        }
    }

    float ComputeChainInertia(DummyPart p)
    {
        if (p.def.joint == null) return 1f;
        var anchor = p.def.joint.anchor;
        float i = 0f;
        foreach (var c in Chain(p))
        {
            float dist = Vector3.Distance(c.def.pos, anchor);
            i += c.mass * (dist * dist + 0.004f);
        }
        return i;
    }

    public List<DummyPart> Chain(DummyPart p)
    {
        var o = new List<DummyPart> { p };
        foreach (var c in p.childrenParts) o.AddRange(Chain(c));
        return o;
    }

    public List<DummyPart> ValidParts()
    {
        var o = new List<DummyPart>();
        foreach (var p in parts.Values) if (p && !p.destroyed) o.Add(p);
        return o;
    }

    public void SetXray(bool on)
    {
        foreach (var p in ValidParts()) p.SetXray(on);
        if (skinBody) skinBody.sharedMaterial = on ? skinXray : skinShader;
        if (skinCloth) skinCloth.enabled = !on;
    }

    // ------------------------------------------------------------------ laço

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (gettingUp) GetUpStep(dt);
        else if (state == State.STANDING) AnimateStanding(dt);
        else { RagdollStep(dt); TryGetUp(dt); }
        heartPhase = (heartPhase + dt * heartRate / 60f * U.TAU) % U.TAU;
        physAcc += dt;
        if (physAcc >= 0.1f) { Physiology(physAcc); physAcc = 0f; }
        BleedFx(dt);
        FireTick(dt);
    }

    public string StatusText()
    {
        switch (state)
        {
            case State.STANDING: return "EM PÉ";
            case State.DOWN: return pain * consciousness > 0.35f ? "AGONIZANDO" : "CAÍDO (CONSCIENTE)";
            case State.UNCONSCIOUS: return "INCONSCIENTE";
            default: return "MORTO";
        }
    }

    public int HealthPct()
    {
        if (dead) return 0;
        float h = Mathf.Clamp01((blood - 1500f) / 3500f);
        var brain = OrganOf("head", "brain");
        if (brain != null) h = Mathf.Min(h, brain.hp / brain.max);
        var heart = OrganOf("chest", "heart");
        if (cardiacArrest) h = Mathf.Min(h, 0.05f);
        else if (heart != null) h = Mathf.Min(h, 0.2f + 0.8f * heart.hp / heart.max);
        h = Mathf.Min(h, Mathf.Clamp01((spo2 - 50f) / 48f));
        h *= 1f - Mathf.Clamp01(hypoxiaTime / 14f);
        h *= 1f - Mathf.Clamp(pain * 0.15f, 0f, 0.15f);
        return Mathf.Max(1, Mathf.RoundToInt(h * 100f));
    }

    OrganState OrganOf(string partId, string vital)
    {
        if (!parts.TryGetValue(partId, out var p) || !p) return null;
        return p.Organ(vital);
    }

    public Vector3 Center()
    {
        var p = parts.TryGetValue("chest", out var c) ? c : null;
        return p ? p.transform.position : transform.position;
    }

    // ------------------------------------------------------------------ fisiologia

    public float PressureFactor()
    {
        if (dead) return 0.06f * Mathf.Exp(-(timeAlive - deathTime) / 25f);
        if (cardiacArrest) return 0.07f;
        return Mathf.Clamp(systolic / 120f, 0.07f, 1.3f);
    }

    void Physiology(float dt)
    {
        timeAlive += dt;
        float lossRate = 0f, chronic = 0f;
        foreach (var p in parts.Values)
        {
            if (!p || p.severed || p.destroyed) continue;
            foreach (var w in p.wounds)
            {
                w.Update(dt);
                WoundFx.Grow(w, dt, w.ExternalRate() * PressureFactor());
                lossRate += w.ExternalRate() + w.InternalRate();
                chronic += w.pain;
            }
            foreach (var b in p.bones) if (b.fractured) chronic += 0.16f;
            foreach (var o in p.organs)
                if (o.hp < o.max && o.vital != "brain" && o.vital != "brainstem") chronic += 0.08f * (1f - o.hp / o.max);
            if (p.tourniquet) chronic += 0.12f;
            foreach (var c in p.childrenParts) if (c && c.cutHere) chronic += 0.3f;
            // bandagem encharcando
            if (p.bandage > 0f && p.bandageMat)
            {
                float soak = Mathf.Clamp01(p.TotalBleed() / 6f);
                Mats.SetColor(p.bandageMat, Color.Lerp(Mats.GetColor(p.bandageMat), new Color(0.55f, 0.03f, 0.04f), soak * dt * 0.5f));
            }
        }
        float pf = PressureFactor();
        bleedRateNow = lossRate * pf;
        float lost = Mathf.Min(blood, lossRate * pf * dt);
        blood -= lost;
        totalLost += lost;

        var heart = OrganOf("chest", "heart");
        float heartEff = 1f;
        if (heart == null || heart.destroyed || heart.hp <= 6f)
        {
            if (!cardiacArrest && !dead) Game.LogEvent($"{dummyName}: PARADA CARDÍACA", new Color(1, 0.3f, 0.3f));
            cardiacArrest = true;
        }
        else heartEff = heart.hp / heart.max;
        // pulmões (hemopneumotórax progressivo)
        float lungF = 0f; int nL = 0;
        if (parts.TryGetValue("chest", out var ch) && ch)
            foreach (var o in ch.organs)
                if (o.vital == "lung")
                {
                    nL++;
                    if (o.hp < o.max) o.pneumo = Mathf.Min(1f, o.pneumo + dt * 0.05f * (1f - o.hp / o.max));
                    lungF += (o.hp / o.max) * (1f - o.pneumo * 0.85f);
                }
        lungF /= Mathf.Max(1f, nL);
        float airway = 1f;
        if (!parts.TryGetValue("neck", out var neck) || !neck || neck.severed) airway = 0f;
        else
        {
            var tr = neck.Organ("trachea");
            if (tr != null) airway = Mathf.Clamp01(tr.hp / tr.max * 1.3f);
            var sc = neck.Organ("spinal_c");
            if (sc != null && sc.hp <= 0f && paralysis != "tetraplegia")
            {
                paralysis = "tetraplegia";
                apnea = true;
                Game.LogEvent($"{dummyName}: medula cervical seccionada — TETRAPLEGIA e parada respiratória", new Color(1, 0.4f, 0.4f));
            }
        }
        if (paralysis == "")
            foreach (var pid in new[] { "chest", "abdomen" })
                if (parts.TryGetValue(pid, out var pp) && pp)
                    foreach (var o in pp.organs)
                        if (o.vital.StartsWith("spinal") && o.hp <= 0f && paralysis == "")
                        {
                            paralysis = "paraplegia";
                            Game.LogEvent($"{dummyName}: medula espinhal lesionada — PARAPLEGIA", new Color(1, 0.5f, 0.4f));
                        }
        if (!dead)
        {
            if (!parts.TryGetValue("head", out var head) || !head || head.severed || head.destroyed) Die("Decapitação");
            else
            {
                if (head.Organ("brain").hp <= 0f) Die("Destruição cerebral");
                if (head.Organ("brainstem").hp <= 0f) Die("Lesão do tronco encefálico");
            }
        }

        float loss = 1f - blood / Anatomy.BLOOD_TOTAL;
        float painFelt = pain * consciousness;
        if (cardiacArrest || dead) heartRate = U.MoveToward(heartRate, 0f, dt * 70f);
        else
        {
            float t = 74f + Mathf.Clamp01(painFelt) * 35f + fear * 30f;
            if (loss > 0.12f) t += (loss - 0.12f) * 280f;
            t += (1f - Mathf.Clamp01(spo2 / 97f)) * 70f;
            if (loss > 0.38f) t = Mathf.Lerp(t, 34f, Mathf.Clamp01((loss - 0.38f) / 0.12f));
            t = Mathf.Min(t, 178f);
            heartRate = U.MoveToward(heartRate, t, dt * 22f);
        }
        if (cardiacArrest || dead) systolic = U.MoveToward(systolic, 0f, dt * 90f);
        else
        {
            float volF = Mathf.Clamp01(1f - Mathf.Max(0f, loss - 0.1f) * 2f);
            float t2 = 122f * volF * (0.35f + 0.65f * heartEff) + Mathf.Clamp01(painFelt) * 10f;
            systolic = U.MoveToward(systolic, t2, dt * 30f);
        }
        diastolic = systolic * 0.66f;
        if (apnea || dead || airway <= 0f)
        {
            respRate = U.MoveToward(respRate, 0f, dt * 20f);
            spo2 = U.MoveToward(spo2, 0f, dt * 3.2f);
        }
        else
        {
            respRate = Mathf.Clamp(14f + loss * 45f + painFelt * 10f + (1f - lungF) * 20f + fear * 8f, 6f, 48f);
            float targetSpo2 = 98f * Mathf.Clamp01(lungF * 1.25f) * Mathf.Clamp01(airway);
            if (loss > 0.3f) targetSpo2 *= Mathf.Clamp(1f - (loss - 0.3f), 0.6f, 1f);
            spo2 = U.MoveToward(spo2, targetSpo2, dt * (targetSpo2 < spo2 ? 3f : 6f));
        }
        float perf = Mathf.Clamp(systolic / 95f, 0f, 1.2f);
        float o2 = perf * Mathf.Clamp01(spo2 / 94f);
        if (o2 < 0.4f) hypoxiaTime += dt * (1f + (0.4f - o2) * 4f);
        else hypoxiaTime = Mathf.Max(0f, hypoxiaTime - dt * 0.4f);
        acutePain = Mathf.Min(U.MoveToward(acutePain, 0f, dt * 0.1f), 1.2f);
        pain = Mathf.Clamp(1.1f * (1f - Mathf.Exp(-chronic * 0.9f)) + acutePain, 0f, 1.5f);
        concussion = U.MoveToward(concussion, 0f, dt * 0.02f);
        if (!dead)
        {
            float cT = 1f;
            var br2 = OrganOf("head", "brain");
            if (br2 != null) cT = Mathf.Min(cT, Mathf.Clamp01(br2.hp / br2.max * 1.4f - 0.1f));
            cT = Mathf.Min(cT, Mathf.Clamp01((o2 - 0.38f) / 0.4f));
            if (concussion > 0.6f) cT = Mathf.Min(cT, 0.15f);
            if (pain > 1.3f) cT = Mathf.Min(cT, 0.3f);
            consciousness = U.MoveToward(consciousness, cT, dt * (cT < consciousness ? 0.3f : 0.08f));
        }
        if (!dead)
        {
            if (hypoxiaTime > 14f)
            {
                if (cardiacArrest) Die("Parada cardíaca");
                else if (apnea) Die("Asfixia (parada respiratória)");
                else if (loss > 0.33f) Die("Choque hemorrágico");
                else if (lungF < 0.35f || airway < 0.4f) Die("Insuficiência respiratória (pulmões colapsados)");
                else Die("Hipóxia cerebral");
            }
            else if (blood < 1300f) Die("Exsanguinação");
        }
        if (!dead)
        {
            if (state == State.STANDING) { if (ShouldCollapse()) Collapse(); }
            else if (state == State.DOWN && consciousness < 0.25f)
            {
                state = State.UNCONSCIOUS;
                SetEyes(false);
                Game.LogEvent($"{dummyName} perdeu a consciência", new Color(1, 0.85f, 0.4f));
            }
            else if (state == State.UNCONSCIOUS && consciousness > 0.35f)
            {
                state = State.DOWN;
                SetEyes(true);
                ChooseAction();
                Game.LogEvent($"{dummyName} recobrou a consciência", new Color(0.9f, 1f, 0.6f));
            }
        }
    }

    public void Die(string cause)
    {
        if (dead) return;
        dead = true;
        deathCause = cause;
        deathTime = timeAlive;
        if (state == State.STANDING) Collapse();
        state = State.DEAD;
        consciousness = 0f;
        muscleLevel = 0f;
        twitchCd = 0.15f;
        SetEyes(false, true);
        Game.LogEvent($"☠ {dummyName} morreu — {cause} (tempo: {timeAlive:F1}s, sangue perdido: {(int)totalLost} ml)", new Color(1, 0.15f, 0.15f));
    }

    void SetEyes(bool open, bool deadEyes = false)
    {
        if (!parts.TryGetValue("head", out var h) || !h || h.deco == null) return;
        foreach (Transform e in h.deco.transform)
            e.localScale = deadEyes ? new Vector3(1, 0.65f, 1) : new Vector3(1, open ? 1 : 0.2f, 1);
    }

    // ------------------------------------------------------------------ sangue visual

    void BleedFx(float dt)
    {
        float pf = PressureFactor();
        float pulse = 0.3f + 0.7f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(heartPhase)), 2f);
        if (cardiacArrest || dead) pulse = 0.35f;
        foreach (var p in parts.Values)
        {
            if (!p || p.destroyed) continue;
            if (!p.severed)
            {
                foreach (var w in p.wounds)
                {
                    float rate = w.ExternalRate() * pf;
                    if (rate > 0.02f) Game.blood.EmitWound(w, rate, dt, w.arterial && !dead && !cardiacArrest, pulse * pf);
                }
            }
            else if (p.cutHere && p.drainBlood > 1f && p.hasStump)
            {
                float r = p.drainBlood * 0.09f;
                p.drainBlood -= r * dt;
                Game.blood.EmitPoint(p, p.stumpLocal, p.stumpNormal, r, dt, 0.6f);
                foreach (var w in p.wounds)
                    if (w.kind != "toco") Game.blood.EmitWound(w, Mathf.Min(w.bleed * 0.3f, r * 0.2f), dt, false, 0.3f);
            }
        }
    }
}
