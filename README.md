# Boneco Gore Sandbox (Unity)

Sandbox em primeira pessoa com bonecos **ragdoll anatômicos**: cada boneco tem ossos, órgãos, artérias e veias, sangra de verdade, perde membros, pega fogo, agoniza no chão, tenta estancar o próprio sangramento e se levanta quando consegue. Feito em **Unity 6 (6000.6.4f1)**, Built-in Render Pipeline, tudo montado por código (a única cena só chama `MainScene`).

> Conteúdo violento e anatômico. É um projeto de estudo de física, anatomia e animação procedural.

---

## Como rodar

1. Abra a pasta no **Unity 6000.6.4f1** (Unity Hub → *Add project from disk*).
2. Menu **Boneco Gore → Configurar projeto** (cria a cena `Assets/Scenes/Main.unity`) e aperte Play.
3. Ou gere o executável: **Boneco Gore → Gerar executável Windows** → `Build/BonecoGore.exe`.

Linha de comando (sem abrir o editor):
```
Unity -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindows
```

### Modos de teste (rodam sem som)
| Comando | O que faz |
|---|---|
| `BonecoGore.exe -autotest` | Cenário fixo de tiros, golpes e explosões + relatório no log |
| `BonecoGore.exe --phystest` | Estabilidade do ragdoll (queda, 40 tiros, arremesso, levantar) |
| `BonecoGore.exe --stress` | 10 bonecos + explosão, mede FPS |
| `BonecoGore.exe --shots=<pasta>` | Capturas de tela (bonecos, feridas, fogo, levantar, viewmodel de cada arma) |

O log fica em `%USERPROFILE%\AppData\LocalLow\Andraus\Boneco Gore Sandbox\Player.log`.

---

## Controles

| Tecla | Ação |
|---|---|
| WASD / Shift / Espaço / Ctrl | Mover / correr / pular / agachar |
| 1–9, 0 | Categoria de arma (**repita a tecla** para trocar dentro da categoria) · rodinha também troca |
| Clique esquerdo / direito | Ação principal / secundária (nas armas de fogo o direito **mira pela arma**) |
| Q | Recarregar (facas: recolher) |
| E (segurar) | Agarrar corpo ou membro (clique = arremessar, rodinha = distância) |
| F | Criar boneco na sua frente |
| R / K | Resetar laboratório / limpar sangue |
| X | Raio-X (órgãos, ossos, artérias e veias) |
| T | Câmera lenta |
| Tab | Painel médico |
| **O** | **Configurar mira** (estilo, cor, tamanho, espessura, abertura, contorno, opacidade, mira dinâmica, sensibilidade, FOV) |
| H / Esc | Esconder ajuda / soltar o mouse |

### Armas
| Tecla | Armas |
|---|---|
| 1 | Pistola 9mm · Revólver .357 · Pistola .50 |
| 2 | SMG · Fuzil · Carabina M4 · Bullpup (rajada de 3) |
| 3 | Escopeta · Cano serrado |
| 4 | Sniper .50 (direito = luneta) |
| 5 | Arco (segure para puxar) · Facas de arremesso (direito = leque de 3) |
| 6 | Faca (corte / facada) · Espada · Lança · Foice (ceifada em arco) |
| 7 | Machado · Marreta (contusão) · Montante (direito = varredura) |
| 8 | Granada · **Lança-chamas** |
| 9 | Bomba C4 (clique gruda, direito detona) |
| 0 | Kit médico (torniquete / bandagem) |

---

## Como funciona

### Boneco (`Assets/Scripts/Body`)
- **17 partes** (`DummyPart`) com `Rigidbody` + `ConfigurableJoint` e limites anatômicos (joelho e cotovelo só dobram para o lado certo). Os dados — posições, massas, juntas, ossos, órgãos e vasos de cada parte — estão em `Data/AnatomyData.cs`.
- **Pele contínua** (`SkinnedMeshRenderer`): um osso por parte; a pose vem das partes a cada quadro. **Ossos auxiliares** em ombros, cotovelos, quadris e joelhos giram a meio caminho entre pai e filho (evita a pele esticando em "asa" com o braço erguido).
- **Fisiologia** (`Dummy.cs`): 5 L de sangue, frequência cardíaca, pressão, SpO₂, choque hemorrágico, pneumotórax, parada cardíaca, hipóxia, dor, consciência, paralisia e causas de morte.
- **Dano** (`Dummy.Damage.cs`): balística por parte — o projétil atravessa a forma, perde energia em carne e osso, fratura ossos, perfura órgãos, rompe vasos e pode sair do outro lado. Amputação e destruição de partes, evisceração (`Fx/Guts.cs`).
- **Fogo** (`Dummy.Fire.cs`): dose de queimadura por parte, fogo que se espalha para as vizinhas, pele carbonizando no shader, pânico (cai e rola para abafar) e morte por queimadura extensa.
- **Em pé** (`Dummy.Stand.cs`): animação **procedural** — equilíbrio, reação por região do golpe, passo de recuperação (ponto de captura), marcha com IK nas pernas (rotação do quadril, contra-rotação dos ombros, peso no pé de apoio, rolamento calcanhar-ponta), respiração no ritmo da frequência respiratória, olhar para o jogador, medo (mãos para cima), mancar e segurar a ferida.
- **Levantar**: detecta se caiu **de bruços** (flexão → quatro apoios → um joelho → em pé) ou **de costas** (senta com as mãos atrás → recolhe os joelhos → um joelho → em pé), sempre apoiado no chão; mais lento, tremendo ou falhando se estiver fraco.
- **No chão** (`Dummy.Ground.cs`): agonia, rolar, rastejar, levar a mão à ferida, torniquete em si mesmo, espasmos ao morrer.

### Feridas (`Fx/WoundFx.cs`, shader `BG_Wound`)
Decalques recortados da própria malha do corpo em volta do ponto atingido, com textura procedural por tipo: entrada de bala (furo, colar de abrasão, hematoma), saída (rasgo com carne e gordura), corte, golpe de machado/espada (gordura e osso), perfuração, queimadura e contusão. As que sangram ganham um **filete de sangue** que escorre e cresce conforme o sangramento.

### Jogador e armas (`Assets/Scripts/Player`, `Data`, `Weapons`)
- `Weapons.cs`: perfis de dano (`Prof`) e definição das armas; tiros são *hitscan* com penetração (`Weapons.FireRay`), flecha e facas são projéteis que cravam (`Projectiles.cs`), granada/C4 explodem com estilhaços, lança-chamas em `Flames.cs`.
- **Viewmodel**: o braço é recortado da pele contínua do corpo (`VmArms.cs`, sem costura no punho) com um *blend shape* "punho" que fecha os dedos em volta do cabo. O cabo e a telha de cada arma são achados na própria malha da arma.
- Mira pela arma (ADS), inércia da arma ao mexer o mouse, animação de sacar, recuo, estojos ejetados com física (`Fx/Casings.cs`), fumaça no cano.

### Mundo e visual
- `World/MapBuilder.cs`: laboratório gerado por código (câmara de azulejo, plataforma alta, barris, saída), luzes, reflexos e **eco** (`AudioReverbZone`).
- Shaders em `Assets/Resources/Shaders`: `BG_Lit` (padrão), `BG_Pbr` (textura PBR em projeção triplanar para o mapa), `BG_Skin` (pele com sangue e queimadura por parte, trama de tecido e poros projetados na posição de repouso), `BG_Wound`, `BG_XRay`.
- **A câmera é espelhada** (`MirrorCamera`): o jogo usa os mesmos números do projeto original em Godot (mão direita); só a imagem é invertida.

---

## Modelos, sons e texturas

| Recurso | De onde vem | Licença |
|---|---|---|
| Corpo humano, ossos e órgãos | `Male human with separated anatomical systems.blend` (fornecido pelo autor) | confirmar antes de distribuir |
| Armas de fogo | Quaternius — Ultimate Gun Pack | CC0 |
| Armas brancas e arco | Quaternius — LowPoly Medieval Weapons | CC0 |
| Tiros | The Free Firearm Sound Library | CC0 |
| Recargas, osso/carne, armas medievais, passos, impactos | OpenGameArt (vários) e Kenney | CC0 |
| Concreto, azulejo, chapa de aço, tecido | ambientCG | CC0 |

Lista completa com links: [`CREDITOS.md`](CREDITOS.md).

### Formato das malhas
As malhas ficam em `Assets/Resources/Models/human.bytes` e `weapons.bytes` (formato próprio `BGM1`, lido por `Core/MeshStore.cs`): posições, normais, cores por vértice, índice da parte (UV2), pesos de osso e submalhas por material.

### Regenerar (pipeline em Blender, no projeto Godot irmão `BonecoGore/tools`)
```
# humano realista (pele anatômica vestida, olhos, mão fechada) -> .glb
blender -b "Male human with separated anatomical systems.blend" --python tools/build_human.py -- human.glb --real

# armas novas: FBX do Quaternius -> assets/weapons/<id>.glb (com prévias opcionais)
blender -b --python tools/import_weapons.py -- <pasta dos fbx> [pasta de prévias]

# exportar para o Unity (human.glb opcional; "skip" exporta só as armas)
blender -b --python tools/export_unity.py -- <pasta deste projeto> [human.glb | skip]
```
- `real_human.py`: veste a pele do modelo anatômico (camiseta e bermuda com barras cortadas na malha), remove a genitália, cria os olhos.
- `build_human.py`: corta a pele, o esqueleto e os órgãos nas 17 partes, calcula os pesos (rotulagem geodésica braço/tronco + transição larga em ombro e quadril) e gera a mão fechada do viewmodel.
- Os sons recortados (um disparo por arquivo, mono 44,1 kHz) foram gerados por um script Python a partir das bibliotecas CC0 e ficam em `Assets/Resources/Sounds/<nome>/`; o `Sfx.cs` sorteia uma variação e usa síntese para o que não tiver gravação.

---

## Estrutura

```
Assets/
  Editor/          BuildTools (configurar/gerar build), TextureImport (normal maps)
  Resources/
    Models/        human.bytes, weapons.bytes
    Shaders/       BG_*.shader
    Sounds/        gravações CC0 por categoria
    Textures/      PBR do ambientCG
  Scripts/
    Body/          Dummy (+ Damage, Ground, Stand, Fire), DummyPart, Wound, ImpactConfig
    Core/          Game, InputX, Mats (materiais/primitivas), MeshStore, Util
    Data/          Anatomy, AnatomyData, Weapons, WeaponData
    Fx/            BloodSystem, WoundFx, Casings, Guts, Sfx, Fx
    Player/        Player, VmArms
    UI/            Hud (painel médico, barra de armas), Crosshair
    Weapons/       Projectiles (flecha/faca, granada, C4, explosões), Flames
    World/         MainScene, MapBuilder
    Tests/         AutoTest, PhysTest, StressTest, Shots
```

## Ajustes rápidos
- Força das armas: `Data/Weapons.cs` e `Data/WeaponData.cs` (energia, `struct` = dano para amputar, `bleed`, `impulse`).
- Quanto cada vaso sangra: `Data/AnatomyData.cs`.
- Equilíbrio, queda e passos: `Body/ImpactConfig.cs`.
- Limites de sangue na tela: `MAX_DROPS`, `MAX_SPLATS`, `MAX_POOLS` em `Fx/BloodSystem.cs`.
