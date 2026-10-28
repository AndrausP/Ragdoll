// GERADO por tools/dump_cs.gd a partir de scripts/anatomy.gd (Godot). Não edite à mão.
using System.Collections.Generic;
using UnityEngine;

public static partial class Anatomy
{
    static Dictionary<string, PartDef> Build()
    {
        var d = new Dictionary<string, PartDef>();
        {
            var p = new PartDef { id = "pelvis", name = "Pelve", parent = "", shape = "box", pos = new Vector3(0.0f, 0.92f, -0.015f), mass = 11.0f, hp = 140.0f, severable = false, leaf = false, limb = "", baseBleed = 1.4f };
            p.size = new Vector3(0.33f, 0.2f, 0.21f);
            p.organs.Add(new OrganDef { name = "Bexiga", c = new Vector3(0.0f, -0.0333f, 0.0525f), s = new Vector3(0.0776f, 0.0666f, 0.063f), vital = "bladder", bleed = 2.0f, color = new Color(0.9f, 0.85f, 0.5f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Intestino grosso", c = new Vector3(0.0f, 0.0333f, 0.042f), s = new Vector3(0.2328f, 0.0777f, 0.084f), vital = "gut", bleed = 2.5f, color = new Color(0.92f, 0.66f, 0.58f), hp = 100.0f });
            p.bones.Add(new BoneDef { name = "Ílio esquerdo", a = new Vector3(0.1164f, 0.0555f, -0.021f), b = new Vector3(0.0776f, -0.0666f, 0.0315f), r = 0.04f, hp = 90.0f });
            p.bones.Add(new BoneDef { name = "Ílio direito", a = new Vector3(-0.1164f, 0.0555f, -0.021f), b = new Vector3(-0.0776f, -0.0666f, 0.0315f), r = 0.04f, hp = 90.0f });
            p.bones.Add(new BoneDef { name = "Sacro", a = new Vector3(0.0f, 0.0555f, -0.0735f), b = new Vector3(0.0f, -0.0555f, -0.084f), r = 0.03f, hp = 80.0f });
            p.vessels.Add(new VesselDef { name = "Artéria ilíaca esquerda", a = new Vector3(0.0291f, 0.0999f, -0.0105f), b = new Vector3(0.0873f, -0.0999f, 0.0315f), r = 0.012f, artery = true, bleed = 30.0f });
            p.vessels.Add(new VesselDef { name = "Artéria ilíaca direita", a = new Vector3(-0.0291f, 0.0999f, -0.0105f), b = new Vector3(-0.0873f, -0.0999f, 0.0315f), r = 0.012f, artery = true, bleed = 30.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "abdomen", name = "Abdômen", parent = "pelvis", shape = "box", pos = new Vector3(0.0f, 1.1f, 0.0f), mass = 9.0f, hp = 140.0f, severable = false, leaf = false, limb = "", baseBleed = 1.2f };
            p.size = new Vector3(0.3f, 0.16f, 0.2f);
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.0f, 1.02f, -0.005f), dir = new Vector3(0.0f, 1.0f, 0.0f), swing = 0.6f, twist = 0.5f, lower = 0.0f, upper = 0.0f, muscle = 170.0f };
            p.organs.Add(new OrganDef { name = "Fígado", c = new Vector3(-0.0658f, 0.0335f, 0.021f), s = new Vector3(0.141f, 0.0603f, 0.126f), vital = "liver", bleed = 9.0f, color = new Color(0.42f, 0.1f, 0.08f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Estômago", c = new Vector3(0.0658f, 0.0335f, 0.042f), s = new Vector3(0.094f, 0.0536f, 0.084f), vital = "stomach", bleed = 3.0f, color = new Color(0.9f, 0.6f, 0.5f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Baço", c = new Vector3(0.1128f, 0.0402f, -0.0315f), s = new Vector3(0.047f, 0.0469f, 0.0525f), vital = "spleen", bleed = 7.0f, color = new Color(0.45f, 0.12f, 0.25f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Rim esquerdo", c = new Vector3(0.0564f, 0.0f, -0.063f), s = new Vector3(0.047f, 0.0536f, 0.042f), vital = "kidney", bleed = 5.0f, color = new Color(0.55f, 0.15f, 0.12f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Rim direito", c = new Vector3(-0.0564f, -0.0067f, -0.063f), s = new Vector3(0.047f, 0.0536f, 0.042f), vital = "kidney", bleed = 5.0f, color = new Color(0.55f, 0.15f, 0.12f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Intestino delgado", c = new Vector3(0.0f, -0.0335f, 0.042f), s = new Vector3(0.2068f, 0.067f, 0.0945f), vital = "gut", bleed = 2.0f, color = new Color(0.92f, 0.66f, 0.58f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Medula espinhal lombar", c = new Vector3(0.0f, 0.0f, -0.08925f), s = new Vector3(0.01504f, 0.1608f, 0.0168f), vital = "spinal_l", bleed = 0.5f, color = new Color(1.0f, 0.95f, 0.7f), hp = 40.0f });
            p.bones.Add(new BoneDef { name = "Vértebras lombares", a = new Vector3(0.0f, 0.0804f, -0.084f), b = new Vector3(0.0f, -0.0804f, -0.084f), r = 0.022f, hp = 70.0f });
            p.vessels.Add(new VesselDef { name = "Aorta abdominal", a = new Vector3(0.01128f, 0.0804f, -0.0525f), b = new Vector3(0.01128f, -0.0804f, -0.0525f), r = 0.012f, artery = true, bleed = 70.0f });
            p.vessels.Add(new VesselDef { name = "Veia cava inferior", a = new Vector3(-0.0188f, 0.0804f, -0.0525f), b = new Vector3(-0.0188f, -0.0804f, -0.0525f), r = 0.013f, artery = false, bleed = 30.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "chest", name = "Tórax", parent = "abdomen", shape = "box", pos = new Vector3(0.0f, 1.32f, 0.0f), mass = 17.0f, hp = 160.0f, severable = false, leaf = false, limb = "", baseBleed = 1.3f };
            p.size = new Vector3(0.34f, 0.28f, 0.22f);
            p.ribs = true;
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.0f, 1.18f, 0.0f), dir = new Vector3(0.0f, 1.0f, 0.0f), swing = 0.45f, twist = 0.45f, lower = 0.0f, upper = 0.0f, muscle = 170.0f };
            p.organs.Add(new OrganDef { name = "Coração", c = new Vector3(0.0267f, -0.0175f, 0.0315f), s = new Vector3(0.0801f, 0.09625f, 0.084f), vital = "heart", bleed = 70.0f, color = new Color(0.65f, 0.05f, 0.08f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Pulmão esquerdo", c = new Vector3(0.08455f, 0.00875f, 0.0f), s = new Vector3(0.089f, 0.1925f, 0.1365f), vital = "lung", bleed = 4.0f, color = new Color(0.95f, 0.55f, 0.6f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Pulmão direito", c = new Vector3(-0.0801f, 0.00875f, 0.0f), s = new Vector3(0.0979f, 0.1925f, 0.1365f), vital = "lung", bleed = 4.0f, color = new Color(0.95f, 0.55f, 0.6f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Esôfago", c = new Vector3(0.0f, 0.04375f, -0.063f), s = new Vector3(0.0178f, 0.175f, 0.021f), vital = "esophagus", bleed = 1.0f, color = new Color(0.92f, 0.66f, 0.58f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Medula espinhal torácica", c = new Vector3(0.0f, 0.0f, -0.09975f), s = new Vector3(0.01424f, 0.28f, 0.0168f), vital = "spinal_t", bleed = 0.5f, color = new Color(1.0f, 0.95f, 0.7f), hp = 40.0f });
            p.bones.Add(new BoneDef { name = "Esterno", a = new Vector3(0.0f, 0.11375f, 0.09975f), b = new Vector3(0.0f, -0.07f, 0.09975f), r = 0.02f, hp = 70.0f });
            p.bones.Add(new BoneDef { name = "Clavícula esquerda", a = new Vector3(0.0178f, 0.13125f, 0.0735f), b = new Vector3(0.1602f, 0.13125f, 0.0315f), r = 0.013f, hp = 45.0f });
            p.bones.Add(new BoneDef { name = "Clavícula direita", a = new Vector3(-0.0178f, 0.13125f, 0.0735f), b = new Vector3(-0.1602f, 0.13125f, 0.0315f), r = 0.013f, hp = 45.0f });
            p.bones.Add(new BoneDef { name = "Escápula esquerda", a = new Vector3(0.0979f, 0.0875f, -0.09975f), b = new Vector3(0.1068f, -0.04375f, -0.09975f), r = 0.03f, hp = 60.0f });
            p.bones.Add(new BoneDef { name = "Escápula direita", a = new Vector3(-0.0979f, 0.0875f, -0.09975f), b = new Vector3(-0.1068f, -0.04375f, -0.09975f), r = 0.03f, hp = 60.0f });
            p.bones.Add(new BoneDef { name = "Vértebras torácicas", a = new Vector3(0.0f, 0.13125f, -0.0945f), b = new Vector3(0.0f, -0.13125f, -0.0945f), r = 0.022f, hp = 70.0f });
            p.vessels.Add(new VesselDef { name = "Aorta", a = new Vector3(0.0f, 0.0875f, -0.021f), b = new Vector3(0.0f, -0.14f, -0.0525f), r = 0.015f, artery = true, bleed = 120.0f });
            p.vessels.Add(new VesselDef { name = "Artéria subclávia esquerda", a = new Vector3(0.0178f, 0.11375f, 0.0f), b = new Vector3(0.1691f, 0.105f, 0.0f), r = 0.009f, artery = true, bleed = 30.0f });
            p.vessels.Add(new VesselDef { name = "Artéria subclávia direita", a = new Vector3(-0.0178f, 0.11375f, 0.0f), b = new Vector3(-0.1691f, 0.105f, 0.0f), r = 0.009f, artery = true, bleed = 30.0f });
            p.vessels.Add(new VesselDef { name = "Veia cava superior", a = new Vector3(-0.02225f, 0.1225f, 0.0f), b = new Vector3(-0.02225f, -0.0175f, 0.0f), r = 0.012f, artery = false, bleed = 28.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "neck", name = "Pescoço", parent = "chest", shape = "capsule", pos = new Vector3(0.0f, 1.515f, -0.015f), mass = 1.5f, hp = 45.0f, severable = true, leaf = false, limb = "head", baseBleed = 2.0f };
            p.r = 0.05f;
            p.len = 0.13f;
            p.stumpBleed = 60.0f; p.stumpVessel = "Artérias carótidas";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.0f, 1.46f, -0.01f), dir = new Vector3(0.0f, 1.0f, 0.0f), swing = 0.6f, twist = 0.6f, lower = 0.0f, upper = 0.0f, muscle = 30.0f };
            p.organs.Add(new OrganDef { name = "Traqueia", c = new Vector3(0.0f, 0.0f, 0.02912f), s = new Vector3(0.02002f, 0.1302f, 0.0182f), vital = "trachea", bleed = 2.0f, color = new Color(0.85f, 0.8f, 0.75f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Esôfago", c = new Vector3(0.0f, 0.0f, 0.00455f), s = new Vector3(0.01638f, 0.1302f, 0.01638f), vital = "esophagus", bleed = 1.0f, color = new Color(0.92f, 0.66f, 0.58f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Medula espinhal cervical", c = new Vector3(0.0f, 0.0f, -0.0273f), s = new Vector3(0.01365f, 0.1302f, 0.01365f), vital = "spinal_c", bleed = 0.5f, color = new Color(1.0f, 0.95f, 0.7f), hp = 35.0f });
            p.bones.Add(new BoneDef { name = "Vértebras cervicais", a = new Vector3(0.0f, 0.0651f, -0.0273f), b = new Vector3(0.0f, -0.0651f, -0.0273f), r = 0.019f, hp = 55.0f });
            p.vessels.Add(new VesselDef { name = "Artéria carótida esquerda", a = new Vector3(0.0273f, 0.0651f, 0.01274f), b = new Vector3(0.0273f, -0.0651f, 0.01274f), r = 0.009f, artery = true, bleed = 45.0f });
            p.vessels.Add(new VesselDef { name = "Artéria carótida direita", a = new Vector3(-0.0273f, 0.0651f, 0.01274f), b = new Vector3(-0.0273f, -0.0651f, 0.01274f), r = 0.009f, artery = true, bleed = 45.0f });
            p.vessels.Add(new VesselDef { name = "Veia jugular esquerda", a = new Vector3(0.03913f, 0.0651f, 0.0f), b = new Vector3(0.03913f, -0.0651f, 0.0f), r = 0.01f, artery = false, bleed = 15.0f });
            p.vessels.Add(new VesselDef { name = "Veia jugular direita", a = new Vector3(-0.03913f, 0.0651f, 0.0f), b = new Vector3(-0.03913f, -0.0651f, 0.0f), r = 0.01f, artery = false, bleed = 15.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "head", name = "Cabeça", parent = "neck", shape = "sphere", pos = new Vector3(0.0f, 1.632f, -0.012f), mass = 5.0f, hp = 85.0f, severable = true, leaf = true, limb = "head", baseBleed = 3.0f };
            p.r = 0.1f;
            p.stumpBleed = 20.0f; p.stumpVessel = "Artérias vertebrais";
            p.skull = true;
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.0f, 1.57f, -0.012f), dir = new Vector3(0.0f, 1.0f, 0.0f), swing = 0.5f, twist = 0.7f, lower = 0.0f, upper = 0.0f, muscle = 22.0f };
            p.organs.Add(new OrganDef { name = "Cérebro", c = new Vector3(0.0f, 0.02275f, -0.00455f), s = new Vector3(0.1365f, 0.1092f, 0.1456f), vital = "brain", bleed = 6.0f, color = new Color(0.93f, 0.62f, 0.68f), hp = 100.0f });
            p.organs.Add(new OrganDef { name = "Tronco encefálico", c = new Vector3(0.0f, -0.0455f, -0.0182f), s = new Vector3(0.0273f, 0.0455f, 0.0273f), vital = "brainstem", bleed = 3.0f, color = new Color(1.0f, 0.95f, 0.7f), hp = 30.0f });
            p.organs.Add(new OrganDef { name = "Olho esquerdo", c = new Vector3(0.03185f, 0.0f, 0.08372f), s = new Vector3(0.02184f, 0.02184f, 0.02002f), vital = "eye", bleed = 0.5f, color = new Color(0.95f, 0.95f, 0.95f), hp = 15.0f });
            p.organs.Add(new OrganDef { name = "Olho direito", c = new Vector3(-0.03185f, 0.0f, 0.08372f), s = new Vector3(0.02184f, 0.02184f, 0.02002f), vital = "eye", bleed = 0.5f, color = new Color(0.95f, 0.95f, 0.95f), hp = 15.0f });
            p.bones.Add(new BoneDef { name = "Mandíbula", a = new Vector3(-0.0455f, -0.0637f, 0.05005f), b = new Vector3(0.0455f, -0.0637f, 0.05005f), r = 0.022f, hp = 45.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "upper_arm_l", name = "Braço esquerdo", parent = "chest", shape = "capsule", pos = new Vector3(0.195f, 1.25f, -0.02f), mass = 2.2f, hp = 70.0f, severable = true, leaf = false, limb = "arm_l", baseBleed = 1.0f };
            p.r = 0.048f;
            p.len = 0.31f;
            p.stumpBleed = 22.0f; p.stumpVessel = "Artéria braquial";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.175f, 1.39f, -0.022f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 2.2f, twist = 0.9f, lower = 0.0f, upper = 0.0f, muscle = 55.0f };
            p.bones.Add(new BoneDef { name = "Úmero esquerdo", a = new Vector3(0.0f, 0.14f, 0.0f), b = new Vector3(0.0f, -0.14f, 0.0f), r = 0.017f, hp = 70.0f });
            p.vessels.Add(new VesselDef { name = "Artéria braquial esquerda", a = new Vector3(-0.024f, 0.13f, 0.0096f), b = new Vector3(-0.0192f, -0.14f, 0.0192f), r = 0.009f, artery = true, bleed = 18.0f });
            p.vessels.Add(new VesselDef { name = "Veia basílica esquerda", a = new Vector3(-0.0336f, 0.13f, -0.0048f), b = new Vector3(-0.0288f, -0.14f, 0.0f), r = 0.008f, artery = false, bleed = 5.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "forearm_l", name = "Antebraço esquerdo", parent = "upper_arm_l", shape = "capsule", pos = new Vector3(0.232f, 1.0f, -0.015f), mass = 1.4f, hp = 55.0f, severable = true, leaf = false, limb = "arm_l", baseBleed = 0.9f };
            p.r = 0.04f;
            p.len = 0.25f;
            p.stumpBleed = 14.0f; p.stumpVessel = "Artérias radial e ulnar";
            p.joint = new JointDef { type = "hinge", anchor = new Vector3(0.215f, 1.115f, -0.02f), dir = new Vector3(0.0f, 0.0f, 0.0f), swing = 0.0f, twist = 0.0f, lower = 0.0f, upper = 2.5f, muscle = 35.0f };
            p.bones.Add(new BoneDef { name = "Rádio esquerdo", a = new Vector3(0.01116f, 0.1116f, 0.0093f), b = new Vector3(0.01395f, -0.1116f, 0.0093f), r = 0.011f, hp = 45.0f });
            p.bones.Add(new BoneDef { name = "Ulna esquerda", a = new Vector3(-0.01116f, 0.1116f, -0.0093f), b = new Vector3(-0.0093f, -0.1116f, -0.0093f), r = 0.011f, hp = 45.0f });
            p.vessels.Add(new VesselDef { name = "Artéria radial esquerda", a = new Vector3(0.01395f, 0.093f, 0.0279f), b = new Vector3(0.0186f, -0.1209f, 0.0279f), r = 0.007f, artery = true, bleed = 7.0f });
            p.vessels.Add(new VesselDef { name = "Artéria ulnar esquerda", a = new Vector3(-0.01395f, 0.093f, 0.02604f), b = new Vector3(-0.01674f, -0.1209f, 0.02604f), r = 0.007f, artery = true, bleed = 6.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "hand_l", name = "Mão esquerda", parent = "forearm_l", shape = "box", pos = new Vector3(0.27f, 0.805f, -0.005f), mass = 0.5f, hp = 30.0f, severable = true, leaf = true, limb = "arm_l", baseBleed = 0.6f };
            p.size = new Vector3(0.075f, 0.16f, 0.04f);
            p.stumpBleed = 5.0f; p.stumpVessel = "Arco palmar";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.25f, 0.885f, -0.01f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 0.8f, twist = 0.7f, lower = 0.0f, upper = 0.0f, muscle = 9.0f };
            p.bones.Add(new BoneDef { name = "Ossos da mão esquerda", a = new Vector3(0.0f, 0.07f, 0.0f), b = new Vector3(0.0f, -0.07f, 0.0f), r = 0.02f, hp = 30.0f });
            p.vessels.Add(new VesselDef { name = "Arco palmar esquerdo", a = new Vector3(-0.025f, 0.03f, 0.01368f), b = new Vector3(0.025f, 0.03f, 0.01368f), r = 0.006f, artery = true, bleed = 3.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "upper_arm_r", name = "Braço direito", parent = "chest", shape = "capsule", pos = new Vector3(-0.195f, 1.25f, -0.02f), mass = 2.2f, hp = 70.0f, severable = true, leaf = false, limb = "arm_r", baseBleed = 1.0f };
            p.r = 0.048f;
            p.len = 0.31f;
            p.stumpBleed = 22.0f; p.stumpVessel = "Artéria braquial";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(-0.175f, 1.39f, -0.022f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 2.2f, twist = 0.9f, lower = 0.0f, upper = 0.0f, muscle = 55.0f };
            p.bones.Add(new BoneDef { name = "Úmero direito", a = new Vector3(0.0f, 0.14f, 0.0f), b = new Vector3(0.0f, -0.14f, 0.0f), r = 0.017f, hp = 70.0f });
            p.vessels.Add(new VesselDef { name = "Artéria braquial direita", a = new Vector3(0.024f, 0.13f, 0.0096f), b = new Vector3(0.0192f, -0.14f, 0.0192f), r = 0.009f, artery = true, bleed = 18.0f });
            p.vessels.Add(new VesselDef { name = "Veia basílica direita", a = new Vector3(0.0336f, 0.13f, -0.0048f), b = new Vector3(0.0288f, -0.14f, 0.0f), r = 0.008f, artery = false, bleed = 5.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "forearm_r", name = "Antebraço direito", parent = "upper_arm_r", shape = "capsule", pos = new Vector3(-0.232f, 1.0f, -0.015f), mass = 1.4f, hp = 55.0f, severable = true, leaf = false, limb = "arm_r", baseBleed = 0.9f };
            p.r = 0.04f;
            p.len = 0.25f;
            p.stumpBleed = 14.0f; p.stumpVessel = "Artérias radial e ulnar";
            p.joint = new JointDef { type = "hinge", anchor = new Vector3(-0.215f, 1.115f, -0.02f), dir = new Vector3(0.0f, 0.0f, 0.0f), swing = 0.0f, twist = 0.0f, lower = 0.0f, upper = 2.5f, muscle = 35.0f };
            p.bones.Add(new BoneDef { name = "Rádio direito", a = new Vector3(-0.01116f, 0.1116f, 0.0093f), b = new Vector3(-0.01395f, -0.1116f, 0.0093f), r = 0.011f, hp = 45.0f });
            p.bones.Add(new BoneDef { name = "Ulna direita", a = new Vector3(0.01116f, 0.1116f, -0.0093f), b = new Vector3(0.0093f, -0.1116f, -0.0093f), r = 0.011f, hp = 45.0f });
            p.vessels.Add(new VesselDef { name = "Artéria radial direita", a = new Vector3(-0.01395f, 0.093f, 0.0279f), b = new Vector3(-0.0186f, -0.1209f, 0.0279f), r = 0.007f, artery = true, bleed = 7.0f });
            p.vessels.Add(new VesselDef { name = "Artéria ulnar direita", a = new Vector3(0.01395f, 0.093f, 0.02604f), b = new Vector3(0.01674f, -0.1209f, 0.02604f), r = 0.007f, artery = true, bleed = 6.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "hand_r", name = "Mão direita", parent = "forearm_r", shape = "box", pos = new Vector3(-0.27f, 0.805f, -0.005f), mass = 0.5f, hp = 30.0f, severable = true, leaf = true, limb = "arm_r", baseBleed = 0.6f };
            p.size = new Vector3(0.075f, 0.16f, 0.04f);
            p.stumpBleed = 5.0f; p.stumpVessel = "Arco palmar";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(-0.25f, 0.885f, -0.01f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 0.8f, twist = 0.7f, lower = 0.0f, upper = 0.0f, muscle = 9.0f };
            p.bones.Add(new BoneDef { name = "Ossos da mão direita", a = new Vector3(0.0f, 0.07f, 0.0f), b = new Vector3(0.0f, -0.07f, 0.0f), r = 0.02f, hp = 30.0f });
            p.vessels.Add(new VesselDef { name = "Arco palmar direito", a = new Vector3(-0.025f, 0.03f, 0.01368f), b = new Vector3(0.025f, 0.03f, 0.01368f), r = 0.006f, artery = true, bleed = 3.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "thigh_l", name = "Coxa esquerda", parent = "pelvis", shape = "capsule", pos = new Vector3(0.082f, 0.67f, -0.015f), mass = 8.0f, hp = 95.0f, severable = true, leaf = false, limb = "leg_l", baseBleed = 1.3f };
            p.r = 0.068f;
            p.len = 0.44f;
            p.stumpBleed = 30.0f; p.stumpVessel = "Artéria femoral";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.088f, 0.885f, -0.015f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 1.15f, twist = 0.5f, lower = 0.0f, upper = 0.0f, muscle = 140.0f };
            p.bones.Add(new BoneDef { name = "Fêmur esquerdo", a = new Vector3(0.0095f, 0.1995f, 0.0f), b = new Vector3(0.0f, -0.1995f, 0.0f), r = 0.02f, hp = 90.0f });
            p.vessels.Add(new VesselDef { name = "Artéria femoral esquerda", a = new Vector3(-0.03325f, 0.1995f, 0.03325f), b = new Vector3(-0.0285f, -0.1575f, -0.0095f), r = 0.012f, artery = true, bleed = 28.0f });
            p.vessels.Add(new VesselDef { name = "Veia femoral esquerda", a = new Vector3(-0.04465f, 0.1995f, 0.0266f), b = new Vector3(-0.0399f, -0.1575f, -0.0152f), r = 0.011f, artery = false, bleed = 9.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "shin_l", name = "Canela esquerda", parent = "thigh_l", shape = "capsule", pos = new Vector3(0.078f, 0.265f, -0.015f), mass = 3.6f, hp = 75.0f, severable = true, leaf = false, limb = "leg_l", baseBleed = 0.9f };
            p.r = 0.05f;
            p.len = 0.38f;
            p.stumpBleed = 12.0f; p.stumpVessel = "Artéria tibial";
            p.joint = new JointDef { type = "hinge", anchor = new Vector3(0.075f, 0.455f, -0.015f), dir = new Vector3(0.0f, 0.0f, 0.0f), swing = 0.0f, twist = 0.0f, lower = -2.4f, upper = 0.0f, muscle = 80.0f };
            p.bones.Add(new BoneDef { name = "Tíbia esquerda", a = new Vector3(0.00455f, 0.168f, 0.0182f), b = new Vector3(0.0f, -0.168f, 0.01365f), r = 0.018f, hp = 70.0f });
            p.bones.Add(new BoneDef { name = "Fíbula esquerda", a = new Vector3(0.02275f, 0.1575f, -0.0091f), b = new Vector3(0.02275f, -0.1575f, -0.0091f), r = 0.009f, hp = 35.0f });
            p.vessels.Add(new VesselDef { name = "Artéria poplítea esquerda", a = new Vector3(0.0f, 0.189f, -0.0273f), b = new Vector3(0.0f, 0.1155f, -0.0273f), r = 0.01f, artery = true, bleed = 20.0f });
            p.vessels.Add(new VesselDef { name = "Artéria tibial esquerda", a = new Vector3(-0.0091f, 0.1155f, -0.0182f), b = new Vector3(0.0f, -0.1575f, -0.0182f), r = 0.007f, artery = true, bleed = 8.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "foot_l", name = "Pé esquerdo", parent = "shin_l", shape = "box", pos = new Vector3(0.105f, 0.04f, 0.03f), mass = 1.1f, hp = 35.0f, severable = true, leaf = true, limb = "leg_l", baseBleed = 0.6f };
            p.size = new Vector3(0.095f, 0.08f, 0.25f);
            p.stumpBleed = 4.0f; p.stumpVessel = "Artéria dorsal do pé";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(0.08f, 0.08f, -0.025f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 0.6f, twist = 0.3f, lower = 0.0f, upper = 0.0f, muscle = 18.0f };
            p.bones.Add(new BoneDef { name = "Ossos do pé esquerdo", a = new Vector3(0.0f, 0.0f, -0.0832f), b = new Vector3(0.0f, 0.0f, 0.104f), r = 0.022f, hp = 35.0f });
            p.vessels.Add(new VesselDef { name = "Artéria dorsal do pé esquerdo", a = new Vector3(0.0f, 0.033f, -0.052f), b = new Vector3(0.0f, 0.022f, 0.104f), r = 0.006f, artery = true, bleed = 3.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "thigh_r", name = "Coxa direita", parent = "pelvis", shape = "capsule", pos = new Vector3(-0.082f, 0.67f, -0.015f), mass = 8.0f, hp = 95.0f, severable = true, leaf = false, limb = "leg_r", baseBleed = 1.3f };
            p.r = 0.068f;
            p.len = 0.44f;
            p.stumpBleed = 30.0f; p.stumpVessel = "Artéria femoral";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(-0.088f, 0.885f, -0.015f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 1.15f, twist = 0.5f, lower = 0.0f, upper = 0.0f, muscle = 140.0f };
            p.bones.Add(new BoneDef { name = "Fêmur direito", a = new Vector3(-0.0095f, 0.1995f, 0.0f), b = new Vector3(0.0f, -0.1995f, 0.0f), r = 0.02f, hp = 90.0f });
            p.vessels.Add(new VesselDef { name = "Artéria femoral direita", a = new Vector3(0.03325f, 0.1995f, 0.03325f), b = new Vector3(0.0285f, -0.1575f, -0.0095f), r = 0.012f, artery = true, bleed = 28.0f });
            p.vessels.Add(new VesselDef { name = "Veia femoral direita", a = new Vector3(0.04465f, 0.1995f, 0.0266f), b = new Vector3(0.0399f, -0.1575f, -0.0152f), r = 0.011f, artery = false, bleed = 9.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "shin_r", name = "Canela direita", parent = "thigh_r", shape = "capsule", pos = new Vector3(-0.078f, 0.265f, -0.015f), mass = 3.6f, hp = 75.0f, severable = true, leaf = false, limb = "leg_r", baseBleed = 0.9f };
            p.r = 0.05f;
            p.len = 0.38f;
            p.stumpBleed = 12.0f; p.stumpVessel = "Artéria tibial";
            p.joint = new JointDef { type = "hinge", anchor = new Vector3(-0.075f, 0.455f, -0.015f), dir = new Vector3(0.0f, 0.0f, 0.0f), swing = 0.0f, twist = 0.0f, lower = -2.4f, upper = 0.0f, muscle = 80.0f };
            p.bones.Add(new BoneDef { name = "Tíbia direita", a = new Vector3(-0.00455f, 0.168f, 0.0182f), b = new Vector3(0.0f, -0.168f, 0.01365f), r = 0.018f, hp = 70.0f });
            p.bones.Add(new BoneDef { name = "Fíbula direita", a = new Vector3(-0.02275f, 0.1575f, -0.0091f), b = new Vector3(-0.02275f, -0.1575f, -0.0091f), r = 0.009f, hp = 35.0f });
            p.vessels.Add(new VesselDef { name = "Artéria poplítea direita", a = new Vector3(0.0f, 0.189f, -0.0273f), b = new Vector3(0.0f, 0.1155f, -0.0273f), r = 0.01f, artery = true, bleed = 20.0f });
            p.vessels.Add(new VesselDef { name = "Artéria tibial direita", a = new Vector3(0.0091f, 0.1155f, -0.0182f), b = new Vector3(0.0f, -0.1575f, -0.0182f), r = 0.007f, artery = true, bleed = 8.0f });
            d[p.id] = p;
        }
        {
            var p = new PartDef { id = "foot_r", name = "Pé direito", parent = "shin_r", shape = "box", pos = new Vector3(-0.105f, 0.04f, 0.03f), mass = 1.1f, hp = 35.0f, severable = true, leaf = true, limb = "leg_r", baseBleed = 0.6f };
            p.size = new Vector3(0.095f, 0.08f, 0.25f);
            p.stumpBleed = 4.0f; p.stumpVessel = "Artéria dorsal do pé";
            p.joint = new JointDef { type = "cone", anchor = new Vector3(-0.08f, 0.08f, -0.025f), dir = new Vector3(0.0f, -1.0f, 0.0f), swing = 0.6f, twist = 0.3f, lower = 0.0f, upper = 0.0f, muscle = 18.0f };
            p.bones.Add(new BoneDef { name = "Ossos do pé direito", a = new Vector3(0.0f, 0.0f, -0.0832f), b = new Vector3(0.0f, 0.0f, 0.104f), r = 0.022f, hp = 35.0f });
            p.vessels.Add(new VesselDef { name = "Artéria dorsal do pé direito", a = new Vector3(0.0f, 0.033f, -0.052f), b = new Vector3(0.0f, 0.022f, 0.104f), r = 0.006f, artery = true, bleed = 3.0f });
            d[p.id] = p;
        }
        return d;
    }
}
