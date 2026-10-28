using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Corte em qualquer ponto de um membro: a pele (e a roupa) é partida por um plano, cada lado ganha uma tampa de
/// carne/osso, o pedaço distal vira um corpo rígido novo (mesmo referencial da parte de origem) e as partes
/// penduradas nele são religadas ao fragmento.
public partial class Dummy
{
    MeshData bodyData, clothData;
    List<Transform> skinBones;
    List<Matrix4x4> skinBind;
    readonly Dictionary<DummyPart, Transform> fragBone = new Dictionary<DummyPart, Transform>();
    int fragCount;

    /// Direção "para a ponta" do membro no espaço de repouso (a mesma dos colisores: Y, ou Z no pé).
    static Vector3 LimbAxis(PartDef d)
    {
        if (d.id.StartsWith("foot")) return Vector3.forward;
        return d.pos.y >= d.joint.anchor.y ? Vector3.up : Vector3.down;
    }

    static int AuxIndex(string id)
    {
        int h = System.Array.IndexOf(HELPER_JOINTS, id);
        return h < 0 ? -1 : Anatomy.ORDER.Length + h;
    }

    /// Pode ser cortada em qualquer ponto? (membros e pescoço; a cabeça é só destruída)
    public bool Cuttable(DummyPart p) =>
        skinBody != null && p != null && !p.destroyed && p.def.joint != null && (p.def.severable || Torso(p)) && p.def.shape != "sphere"
        && !Collapsed(p);

    /// Tronco cortável (pendência 07-B): cintura (abdômen) e peito (tórax). A pelve é a raiz e não tem junta.
    static bool Torso(DummyPart p) => p.partId == "abdomen" || p.partId == "chest";

    /// Parte separada pelo corte na junta (a pele dela está encolhida no coto) ou destruída.
    static bool Collapsed(DummyPart p) => p.destroyed || p.severed && !p.meshDetached;

    /// Cadeia ainda presa por juntas (sem os galhos que já foram arrancados antes).
    List<DummyPart> AttachedChain(DummyPart p)
    {
        var o = new List<DummyPart> { p };
        foreach (var c in p.childrenParts) if (c && !c.cutHere && !c.destroyed) o.AddRange(AttachedChain(c));
        return o;
    }

    /// Ponta distal (mundo) de uma peça cortável; usada para cortar o pulso/tornozelo/pescoço quando a mão/pé/cabeça é arrancada.
    Vector3 DistalTip(DummyPart p, float back)
    {
        var axis = LimbAxis(p.def);
        float half = (p.def.shape == "capsule" ? p.def.len : Vector3.Dot(p.def.size, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)))) * 0.5f;
        float aC = Vector3.Dot(p.def.pos, axis);
        float hi = p.segInit ? p.segHi : aC + half;
        return p.Xform.Apply(axis * (hi - back - aC));
    }

    /// Amputação pelo golpe: corta onde bateu (membros), no pulso/tornozelo (mão/pé) ou no pescoço (cabeça).
    bool TryMeshCut(DummyPart p, Vector3 dirW)
    {
        if (skinBody == null || p.cutHere || p.destroyed) return false;
        var target = p;
        Vector3 pt = cutHint ?? p.transform.position;
        if (p.partId == "head")
        {
            if (!parts.TryGetValue("neck", out var neck) || !neck || Collapsed(neck)) return p.meshDetached;
            target = neck; pt = DistalTip(neck, 0.035f);
        }
        else if (p.def.leaf && p.parentPart != null && !p.parentPart.destroyed)
        {
            target = p.parentPart; pt = DistalTip(target, 0.02f);
        }
        if (!Cuttable(target) || target.cutHere)
            return p.meshDetached;                      // pedaço solto que não dá para cortar de novo: nada a fazer
        if (target.segInit && target.segHi - target.segLo < 0.09f) return p.meshDetached;
        bool ok = CutAt(target, pt, dirW, cutRagged);
        cutHint = null;
        return ok;
    }

    /// `below` (só no tronco): ossos pendurados na parte cortada. Os vértices deles vão inteiros para o lado distal, sem
    /// passar pelo plano — com o boneco em repouso as mãos ficam na altura da cintura e seriam cortadas junto.
    static float[] CutField(MeshData d, HashSet<int> region, Vector3 axis, float s, float ragged, Vector3 center, float seed, HashSet<int> below = null)
    {
        var f = new float[d.Count];
        var ex = Vector3.Cross(axis, Vector3.up); if (ex.sqrMagnitude < 1e-4f) ex = Vector3.Cross(axis, Vector3.forward);
        ex.Normalize();
        var ez = Vector3.Cross(ex, axis);
        for (int i = 0; i < f.Length; i++)
        {
            var b = d.bw[i];
            float share = 0f;
            if (region.Contains(b.boneIndex0)) share += b.weight0;
            if (region.Contains(b.boneIndex1)) share += b.weight1;
            if (region.Contains(b.boneIndex2)) share += b.weight2;
            if (region.Contains(b.boneIndex3)) share += b.weight3;
            if (share < 0.5f) { f[i] = -1f; continue; }
            float ax = Vector3.Dot(d.v[i], axis) - s;
            if (below != null)
            {
                float sb = 0f;
                if (below.Contains(b.boneIndex0)) sb += b.weight0;
                if (below.Contains(b.boneIndex1)) sb += b.weight1;
                if (below.Contains(b.boneIndex2)) sb += b.weight2;
                if (below.Contains(b.boneIndex3)) sb += b.weight3;
                if (sb >= 0.5f) { f[i] = Mathf.Max(ax, 1e-3f); continue; }
            }
            if (ragged > 0f)
            {
                var rel = d.v[i] - center;
                float ang = Mathf.Atan2(Vector3.Dot(rel, ex), Vector3.Dot(rel, ez));
                float nz = Mathf.PerlinNoise(Mathf.Cos(ang) * 1.6f + seed, Mathf.Sin(ang) * 1.6f + 3.1f) - 0.5f;
                ax -= ragged * 0.04f * nz;
            }
            f[i] = Mathf.Abs(ax) < 1e-6f ? 1e-6f : ax;
        }
        return f;
    }

    static bool Crosses(MeshData d, float[] f)
    {
        var t = d.tris;
        for (int i = 0; i < t.Count; i += 3)
        {
            bool a = f[t[i]] > 0f, b = f[t[i + 1]] > 0f, c = f[t[i + 2]] > 0f;
            if (a != b || b != c) return true;
        }
        return false;
    }

    static void ResizeCollider(Collider col, PartDef def, Vector3 axis, float aC, float a, float b)
    {
        float len = Mathf.Max(b - a, 0.02f);
        var center = axis * ((a + b) * 0.5f - aC);
        if (col is CapsuleCollider cc)
        {
            cc.radius = Mathf.Min(def.r, Mathf.Max(len * 0.5f, 0.012f));
            cc.height = Mathf.Max(len, cc.radius * 2f);
            cc.center = center;
        }
        else if (col is BoxCollider bc)
        {
            var sz = def.size;
            if (Mathf.Abs(axis.y) > 0.5f) sz.y = len; else if (Mathf.Abs(axis.z) > 0.5f) sz.z = len; else sz.x = len;
            bc.size = sz;
            bc.center = center;
        }
    }

    /// Corta a parte `p` no ponto do mundo `worldPoint` (projetado no eixo do membro). `ragged` 0..1: corte irregular
    /// (machado, escopeta, explosão); 0 = lâmina limpa. Devolve false se não for possível cortar ali.
    public bool CutAt(DummyPart p, Vector3 worldPoint, Vector3 dirW, float ragged = 0f)
    {
        if (!Cuttable(p) || p.cutHere || p.parentPart == null && !p.fragment) return false;
        var def = p.def;
        var axis = LimbAxis(def);
        float half = (def.shape == "capsule" ? def.len : Vector3.Dot(def.size, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)))) * 0.5f;
        float aC = Vector3.Dot(def.pos, axis);
        if (!p.segInit) { p.segLo = aC - half; p.segHi = aC + half; p.segInit = true; }
        if (p.segHi - p.segLo < 0.09f) return false;

        var rest = p.Xform.Inverse().Apply(worldPoint) + def.pos;
        float s = Mathf.Clamp(Vector3.Dot(rest, axis), p.segLo + 0.035f, p.segHi - 0.035f);
        // peito: abaixo das axilas (a pele de lá segue os braços e a tampa esticaria com eles)
        if (p.partId == "chest") s = Mathf.Min(s, p.segLo + 0.08f);
        var centerRest = def.pos + axis * (s - aC);

        // ---- pele e roupa
        EnsureData();
        var chain = Chain(p);
        var region = new HashSet<int>();
        foreach (var c in chain)
        {
            region.Add(c.boneIndex);
            if (c != p) { int ax = AuxIndex(c.partId); if (ax >= 0) region.Add(ax); }
        }
        float seed = Random.value * 50f;
        bool torso = Torso(p);
        var below = new HashSet<int>(region); below.Remove(p.boneIndex);
        var f = CutField(bodyData, region, axis, s, ragged, centerRest, seed, torso ? below : null);
        if (!Crosses(bodyData, f)) return false;

        // separação rígida dos pesos (pendência 07-A): nenhum vértice pode ficar preso aos dois pedaços, senão a pele
        // estica do corpo até o fragmento ("fita"). Lado proximal: peso nos ossos da cadeia distal vai para a parte
        // cortada; lado distal: peso em qualquer osso de fora da cadeia (pai, auxiliar do ombro/quadril) vai para o fragmento.
        int newBone = skinBones.Count;
        var distMap = new int[newBone];
        var proxMap = new int[newBone];
        for (int i = 0; i < newBone; i++)
        {
            distMap[i] = below.Contains(i) ? -1 : newBone;
            proxMap[i] = below.Contains(i) ? p.boneIndex : -1;
        }

        var opt = new CutOptions { capNormal = axis, distMap = distMap, proxMap = proxMap, capDepth = torso ? 0.03f : 0.014f, proxCapBone = p.boneIndex, distCapBone = newBone };
        if (torso) { opt.boneRadius = 0.022f; opt.irregular = 1f; }
        var info = MeshCut.Cut(bodyData, f, opt, out _);
        if (clothData != null)
        {
            var fc = CutField(clothData, region, axis, s, ragged, centerRest, seed, torso ? below : null);
            if (Crosses(clothData, fc))
            {
                var oc = new CutOptions { capNormal = axis, distMap = distMap, proxMap = proxMap, cap = false };
                MeshCut.Cut(clothData, fc, oc, out _);
            }
        }
        skinBind.Add(Matrix4x4.Translate(-def.pos));
        var nbT = new GameObject($"bone_{p.partId}_cut{fragCount + 1}").transform;
        nbT.SetParent(transform, false);
        nbT.localPosition = def.pos;
        skinBones.Add(nbT);
        var bind = skinBind.ToArray();
        bodyData.Apply(skinBody.sharedMesh, bind);
        if (clothData != null) clothData.Apply(skinCloth.sharedMesh, bind);
        meshVertsDirty = meshTopoDirty = false;
        skinBody.bones = skinBones.ToArray();
        if (skinCloth) skinCloth.bones = skinBones.ToArray();

        // ---- física: o corpo cai e o fragmento ganha vida própria
        if (state == State.STANDING) Collapse();
        bool wasAttached = !p.severed;
        float lo = p.segLo, hi = p.segHi;
        float frac = (hi - s) / (hi - lo);

        var go = new GameObject($"{p.partId}_corte{++fragCount}");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(p.transform.position, p.transform.rotation);
        var frag = go.AddComponent<DummyPart>();
        frag.Setup(this, p.partId, def);
        frag.fragment = true; frag.boneIndex = newBone; frag.segInit = true; frag.segLo = s; frag.segHi = hi;
        frag.bodyMesh.SetActive(false);
        if (frag.hairMesh) frag.hairMesh.SetActive(false);
        parts[p.partId + "#" + fragCount] = frag;
        fragBone[frag] = nbT;

        float m0 = p.rb.mass;
        frag.rb.mass = Mathf.Max(0.05f, m0 * frac);
        p.rb.mass = Mathf.Max(0.05f, m0 * (1f - frac));
        frag.limbBlood = p.limbBlood * frac; p.limbBlood -= frag.limbBlood;
        frag.maxHp = Mathf.Max(10f, p.maxHp * frac); frag.hp = frag.maxHp * 0.5f;
        p.hp = Mathf.Max(p.hp, p.maxHp * 0.35f);
        ResizeCollider(p.col, def, axis, aC, lo, s);
        if (p.parentPart != null) p.rb.mass = Mathf.Max(p.rb.mass, p.parentPart.mass / 6f);
        // toco curto: sem músculo nem alavanca para girar; solto na junta ele rodopia e torce a pele do ombro/quadril
        if (s - lo < 0.08f && p.joint is ConfigurableJoint pj)
            pj.angularXMotion = pj.angularYMotion = pj.angularZMotion = ConfigurableJointMotion.Locked;
        ResizeCollider(frag.col, def, axis, aC, s, hi);
        p.segHi = s;
        // ossos, órgãos e vasos ficam com o lado em que estão
        SplitAnatomy(p, frag, axis, aC, s);

        frag.rb.linearVelocity = p.rb.linearVelocity; frag.rb.angularVelocity = p.rb.angularVelocity;
        Physics.IgnoreCollision(p.col, frag.col, true);
        StartCoroutine(RestoreCollision(p.col, frag.col, 0.45f));
        // o que fica pendurado no fragmento (braços no tórax, por exemplo) começa encostado no pedaço que ficou: sem isso
        // a física separa os dois com um tranco que arrebenta as juntas
        foreach (var c in p.childrenParts)
            if (c && c.col && !c.destroyed)
            {
                Physics.IgnoreCollision(p.col, c.col, true);
                StartCoroutine(RestoreCollision(p.col, c.col, 0.6f));
            }

        // partes penduradas no pedaço cortado passam a depender do fragmento
        var kids = new List<DummyPart>(p.childrenParts);
        p.childrenParts.Clear();
        foreach (var c in kids)
        {
            if (c.cutHere || c.destroyed)
            {
                // galho já arrancado: só muda de dono (o coto encolhido passa a seguir o fragmento), sem junta nova
                c.parentPart = frag; frag.childrenParts.Add(c);
                continue;
            }
            if (c.joint != null) DestroyImmediate(c.joint);
            c.parentPart = frag; frag.childrenParts.Add(c);
            MakeJoint(c, true);
            // fragmento curto (pulso, tornozelo, pescoço logo abaixo da cabeça): sem trava, o pedaço de 2-4 cm gira solto
            // em volta da junta e vira a pele do avesso; fica preso ao filho como uma peça só
            if ((hi - s < 0.07f || p.partId == "neck") && c.joint is ConfigurableJoint cj)
                cj.angularXMotion = cj.angularYMotion = cj.angularZMotion = ConfigurableJointMotion.Locked;
            // razão de massa entre corpos ligados acima de ~6:1 deixa a junta instável (manual do Unity, plano A0)
            frag.rb.mass = Mathf.Max(frag.rb.mass, c.mass / 6f);
        }

        foreach (var c in AttachedChain(frag))
        {
            c.severed = true; c.meshDetached = true; c.Freeze = false; c.pressure = 0f;
        }
        frag.cutHere = true; frag.hasStump = true;
        frag.stumpLocal = axis * (s - aC); frag.stumpNormal = -axis;

        // ---- fisiologia e efeitos
        var cutLocal = (info.cutTriangles > 0 ? info.proxCenter : centerRest) - def.pos;
        var cw = p.Xform.Apply(cutLocal);
        var nw = (p.transform.rotation * axis).normalized;
        if (wasAttached)
        {
            float lost = 0f;
            foreach (var c in AttachedChain(frag)) lost += c.limbBlood;
            blood = Mathf.Max(0f, blood - lost * 0.55f);
            totalLost += lost * 0.55f;
            frag.drainBlood = lost * 0.45f;
            float sb = torso ? 140f : def.stumpBleed;
            string vessel = torso ? (p.partId == "chest" ? "Aorta torácica e veia cava" : "Aorta abdominal e veia cava") : def.stumpVessel;
            var w = new Wound { kind = "toco", localPos = cutLocal, localNormal = axis, bleed = sb * Mathf.Lerp(1f, 0.75f, ragged), arterial = true, vessel = vessel, pain = 0.35f };
            p.AddWound(w);
            frag.stumpParentWound = w;
            acutePain += 0.7f;
        }
        var dv = dirW != Vector3.zero ? dirW : nw;
        frag.rb.AddForce((dv.normalized * 6f + U.RandomUnit() * 2f) * Mathf.Min(frag.mass, 6f) * 0.6f, ForceMode.Impulse);
        p.impactGrace = 0.5f; frag.impactGrace = 0.5f;
        Game.blood.Spray(cw, nw, 40, 4f, 0.5f, p.col);
        Game.blood.Spray(cw, -nw, 20, 2.5f, 0.6f, frag.col);
        Game.blood.Mist(cw, dv, 2f);
        Game.sfx.Play("sever", cw, 2f);
        if (torso)
        {
            // corpo partido: vísceras saem das duas metades
            Guts.Spill(p, cutLocal, axis, 2.2f);
            Guts.Spill(frag, cutLocal, -axis, 1.6f);
            Guts.Eject(p, cw, nw, 3);
            Game.LogEvent($"[b]CORPO PARTIDO:[/b] {def.name} seccionado(a) — aorta e veia cava rompidas", new Color(1, 0.2f, 0.2f));
            Die(p.partId == "chest" ? "Tórax seccionado" : "Corpo partido na cintura");
        }
        else Game.LogEvent($"[b]AMPUTAÇÃO:[/b] {def.name} cortado(a) a {(s - p.segLo) * 100f:F0} cm da articulação — {def.stumpVessel} jorrando", new Color(1, 0.25f, 0.25f));
        if (p.partId == "neck" || p.partId == "head") Die("Decapitação");
        return true;
    }

    /// A parte deixa de ter pele própria (destruída ou arrancada na junta de dentro de um fragmento): cada vértice vai
    /// inteiro para ela (encolhe com o osso) ou sai dela (fica no vizinho). Sem isso, vértices divididos esticam do coto
    /// até o ponto onde ela encolheu.
    void SnapWeightsOff(DummyPart p)
    {
        if (skinBody == null) return;
        EnsureData();
        var gone = new HashSet<int> { p.boneIndex };
        foreach (var c in Chain(p)) gone.Add(c.boneIndex);
        Snap(bodyData, gone); if (clothData != null) Snap(clothData, gone);
        meshTopoDirty = true;
    }

    static void Snap(MeshData d, HashSet<int> gone)
    {
        if (!d.hasBW) return;
        var idx = new int[4]; var w = new float[4];
        for (int i = 0; i < d.Count; i++)
        {
            var b = d.bw[i];
            idx[0] = b.boneIndex0; idx[1] = b.boneIndex1; idx[2] = b.boneIndex2; idx[3] = b.boneIndex3;
            w[0] = b.weight0; w[1] = b.weight1; w[2] = b.weight2; w[3] = b.weight3;
            float share = 0f; int best = -1;
            for (int k = 0; k < 4; k++)
                if (w[k] > 0f && gone.Contains(idx[k])) { share += w[k]; if (best < 0 || w[k] > w[best]) best = k; }
            if (share <= 0f || share >= 0.999f) continue;
            if (share >= 0.5f)
            {
                d.bw[i] = new BoneWeight { boneIndex0 = idx[best], weight0 = 1f };
                continue;
            }
            for (int k = 0; k < 4; k++) if (gone.Contains(idx[k])) w[k] = 0f;
            float sum = w[0] + w[1] + w[2] + w[3];
            for (int k = 0; k < 4; k++) w[k] /= sum;
            for (int a = 1; a < 4; a++)
                for (int k = a; k > 0 && w[k] > w[k - 1]; k--) { (w[k], w[k - 1]) = (w[k - 1], w[k]); (idx[k], idx[k - 1]) = (idx[k - 1], idx[k]); }
            d.bw[i] = new BoneWeight
            {
                boneIndex0 = idx[0], boneIndex1 = idx[1], boneIndex2 = idx[2], boneIndex3 = idx[3],
                weight0 = w[0], weight1 = w[1], weight2 = w[2], weight3 = w[3],
            };
        }
    }

    /// Explosão forte no tronco (pendência 07-B): abre a parede (buraco com tampa de carne na pele e na roupa), joga
    /// órgãos para fora e, perto o bastante, parte o corpo na altura da explosão (cintura ou peito).
    void TorsoBlast(DummyPart p, Vector3 centerW, Vector3 dirW, float intensity, float dmg)
    {
        if (skinBody == null || p.destroyed || p.severed && !p.meshDetached) return;
        if (intensity < 0.45f || dmg < ImpactConfig.TORSO_OPEN_DMG) return;
        EnsureData();
        if (p.blastHoles < 2)
        {
            var lc = p.Xform.Inverse().Apply(centerW);
            var nl = lc.sqrMagnitude > 1e-6f ? lc.normalized : Vector3.forward;
            var lp = nl * p.PartRadius();
            if (MeshStore.ProjectOnSkin(p.partId, lp, nl, out var pp, out var pn)) { lp = pp; nl = pn; }
            float r = Mathf.Lerp(0.05f, 0.1f, Mathf.Clamp01((intensity - 0.45f) / 0.5f));
            var at = lp + p.def.pos;
            var opt = new CutOptions { capDepth = 0.05f, irregular = 1f, coreColor = new Color(0.36f, 0.05f, 0.06f) };
            bool holed = MeshCut.Crater(bodyData, at, nl, r, opt, true, p.boneIndex);
            if (clothData != null) MeshCut.Crater(clothData, at, nl, r * 1.3f, new CutOptions(), false, p.boneIndex);
            if (holed)
            {
                p.blastHoles++;
                meshTopoDirty = true;
                var posW = p.Xform.Apply(lp); var nW = (p.transform.rotation * nl).normalized;
                p.guts = Mathf.Min(p.guts, Guts.MAX_PER_PART - 1);      // o buraco da explosão sempre deixa vísceras à mostra
                Guts.Spill(p, lp, nl, 2f + intensity);
                Guts.Eject(p, posW, nW, intensity > 0.8f ? 3 : 1);
                Game.blood.Gibs(posW, 6 + (int)(intensity * 8f), nW, false);
                Game.LogEvent($"[b]{p.def.name} ABERTO(A)[/b] pela explosão", new Color(1, 0.3f, 0.25f));
            }
        }
        // perto o bastante: o tronco parte (a pelve não tem junta: parte-se a cintura)
        if (intensity > ImpactConfig.TORSO_SPLIT_INTENSITY && dmg > ImpactConfig.TORSO_SPLIT_DMG)
        {
            var target = p.partId == "pelvis" && parts.TryGetValue("abdomen", out var ab) ? ab : p;
            if (target && Cuttable(target) && !target.cutHere) CutAt(target, centerW, dirW, 1f);
        }
    }

    /// Pior esticamento da pele em relação ao repouso (aresta posada / aresta de repouso) e quantos triângulos viraram
    /// "fita" (aresta posada > 12 cm e > 4× a de repouso).
    /// Teste da pendência 07-A ("membros viram fitas").
    public float SkinStretch(out int bad)
    {
        bad = 0;
        if (skinBody == null) return 0f;
        var baked = new Mesh();
        skinBody.BakeMesh(baked, true);
        var pv = baked.vertices; var rv = skinBody.sharedMesh.vertices; var t = skinBody.sharedMesh.triangles;
        Object.Destroy(baked);
        if (pv.Length != rv.Length) return 0f;
        var bw = skinBody.sharedMesh.boneWeights;
        float worst = 0f;
        var who = new Dictionary<string, int>();
        for (int i = 0; i < t.Length; i += 3)
        {
            // vértices de parte destruída/arrancada encolhem no coto de propósito: isso não é "fita"
            if (bw.Length == rv.Length && (skinBones[bw[t[i]].boneIndex0].localScale.x < 0.01f
                || skinBones[bw[t[i + 1]].boneIndex0].localScale.x < 0.01f || skinBones[bw[t[i + 2]].boneIndex0].localScale.x < 0.01f)) continue;
            float tw = 0f; bool ribbon = false;
            for (int e = 0; e < 3; e++)
            {
                int a = t[i + e], b = t[i + (e + 1) % 3];
                float r = (rv[a] - rv[b]).magnitude;
                if (r < 0.004f) continue;
                float len = (pv[a] - pv[b]).magnitude;
                tw = Mathf.Max(tw, len / r);
                // fita visível: aresta posada longa (> 12 cm) e muito maior que a de repouso
                if (len > 0.12f && len > r * 4f) ribbon = true;
            }
            if (ribbon)
            {
                bad++;
                if (bad == 1 && Game.blastTest && bw.Length == rv.Length)
                {
                    string dump = "";
                    for (int e = 0; e < 3; e++)
                    {
                        int vi = t[i + e]; var bt = skinBones[bw[vi].boneIndex0];
                        dump += $" v{vi} rest={rv[vi]} pose={pv[vi]} osso={bt.name}@{bt.localPosition} s={bt.localScale.x:F3};";
                    }
                    Debug.Log("[fita]" + dump);
                }
                if (bw.Length == rv.Length)
                {
                    string k = "";
                    for (int e = 0; e < 3; e++) { var w = bw[t[i + e]]; k += skinBones[w.boneIndex0].name + "/" + (w.weight1 > 0.01f ? skinBones[w.boneIndex1].name : "-") + " "; }
                    who[k] = who.TryGetValue(k, out var c) ? c + 1 : 1;
                }
            }
            worst = Mathf.Max(worst, tw);
        }
        if (Game.blastTest && who.Count > 0)
        {
            var l = new List<KeyValuePair<string, int>>(who); l.Sort((x, y) => y.Value.CompareTo(x.Value));
            for (int i = 0; i < Mathf.Min(4, l.Count); i++) Debug.Log($"[esticado] {l[i].Value}x {l[i].Key}");
        }
        return worst;
    }

    static IEnumerator RestoreCollision(Collider a, Collider b, float t)
    {
        yield return new WaitForSeconds(t);
        if (a && b) Physics.IgnoreCollision(a, b, false);
    }

    /// Reparte ossos, órgãos e vasos entre o lado proximal (p) e o distal (frag) pela posição ao longo do eixo.
    static void SplitAnatomy(DummyPart p, DummyPart frag, Vector3 axis, float aC, float s)
    {
        // coordenada axial no espaço local da parte: a_local + aC (o referencial local é o de repouso, sem a posição)
        bool Distal(Vector3 a, Vector3 b) => Vector3.Dot((a + b) * 0.5f, axis) + aC > s;
        bool DistalPt(Vector3 c) => Vector3.Dot(c, axis) + aC > s;
        p.bones.RemoveAll(b => !b.def.shell && Distal(b.def.a, b.def.b));
        frag.bones.RemoveAll(b => b.def.shell || !Distal(b.def.a, b.def.b));
        p.organs.RemoveAll(o => DistalPt(o.def.c));
        frag.organs.RemoveAll(o => !DistalPt(o.def.c));
        p.vessels.RemoveAll(v => Distal(v.def.a, v.def.b));
        frag.vessels.RemoveAll(v => !Distal(v.def.a, v.def.b));
    }
}
