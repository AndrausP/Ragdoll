# Corpo v2 (male base mesh CC0): versão de teste, reversível

O corpo original (`Assets/Resources/Models/human.bytes`) **não foi alterado**. O corpo novo vive em
`human_v2.bytes` e é escolhido em tempo de execução.

## Como ativar / voltar
- Menu **O** → seção CORPO → "Modelo novo" (vale ao reiniciar o jogo), ou
- linha de comando: `BonecoGore.exe --body=v2` (novo) / `--body=v1` (original). Sem argumento vale a preferência salva.
- Para voltar ao original: desmarcar a opção, ou `--body=v1`. Nada mais precisa ser desfeito.

## Pipeline (o original não estava no repositório; este é novo)
```
python tools/build_human_v2.py \
  --base tools/fontes/male-base-mesh-base_male.glb \
  --old  Assets/Resources/Models/human.bytes \
  --anat Assets/Scripts/Data/AnatomyData.cs \
  --out  Assets/Resources/Models/human_v2.bytes
```
- `tools/bgm.py`: leitor/escritor do formato BGM1 (espelho de `Core/MeshStore.cs`).
- `tools/gltf_min.py`: leitor mínimo de GLB (numpy).
- `build_human_v2.py` copia do `human.bytes` tudo o que não é pele (órgãos, ossos, cabelo, mãos do viewmodel, esfera) e
  gera `body` (pele única com pesos) e `skin_*` (peças rígidas por parte, usadas em membros arrancados).
- Formato preservado: UV2.x = (índice da parte + 0,5)/32, pesos 4×(u8 índice + f32), submalha `skin`, enrolamento dos triângulos
  igual ao antigo (verificado pelo script).
- Os scripts originais (`build_human.py`, `export_unity.py`, `import_weapons.py`) **não estão neste repositório**; o
  `human.bytes` original veio de um `.blend` do autor. Por isso o v2 foi feito do zero, sem tocar no pipeline antigo.

## Modelo de origem
- `male-base-mesh-base_male.glb`: 1,786 m, pose A, rig de 163 ossos, Y para cima, frente +Z, esquerda +X (mesma convenção do
  projeto, sem espelhar). 7 primitivas: pele (63.750 v), aréolas, boca, unhas das mãos, unhas dos pés, orelhas; malha
  `high-poly` = olhos. Sem textura e sem cor de vértice (só "clay").
- Resultado: 72.771 vértices (o corpo antigo tinha 308.632) e 109.064 triângulos.

## Mapeamento de ossos → 17 partes físicas
| Parte física | Ossos do rig novo |
|---|---|
| pelvis | `root`, `spine05`, `pelvis.L/R` |
| abdomen | `spine04`, `spine03` |
| chest | `spine02`, `spine01`, `breast.*`, `clavicle.*`, `shoulder01.*` |
| neck | `neck01..03` |
| head | `head`, mandíbula, língua, olhos, todos os músculos faciais |
| upper_arm_l/r | `upperarm01/02.*` |
| forearm_l/r | `lowerarm01/02.*` |
| hand_l/r | `wrist.*`, `metacarpal*`, `finger*` |
| thigh_l/r | `upperleg01/02.*` |
| shin_l/r | `lowerleg01/02.*` |
| foot_l/r | `foot.*`, `toe*` |

Os ossos de dedos, face e torções **não** viram juntas físicas: seus pesos são fundidos na parte correspondente.
Rigidbody, ConfigurableJoint e limites anatômicos não foram tocados.

## Retargeting (por quê não é só importar)
O esqueleto físico tem proporções diferentes do modelo (canela e antebraço mais curtos, pelve mais baixa) e braços caídos
em vez de pose A. Cada parte recebe uma transformação de segmento (rotação + escala axial + escala perpendicular) que leva as
juntas do modelo às âncoras dos `ConfigurableJoint`. Como o pivô de cada segmento coincide com a junta física, a pele
dobra no lugar certo. Para não esticar demais, os pontos de articulação intermediários (cotovelo, joelho, coluna) são
deslocados 60% em direção à distorção igual entre segmentos. Escala axial resultante: pelve 1,14, abdômen 1,08, tórax 0,91,
antebraço 0,91, coxa 0,93, canela 0,87.

## Roupa
O modelo não tem roupa. A camisa e o short são pintados por região na conversão (cor de vértice) e depois separados em
uma casca de tecido (`ClothLayer`), empurrada pelo shader (`_Push`) ao longo da normal já deformada.

## Problemas encontrados
- A pele estourava na roupa com os braços levantados; resolvido empurrando a casca no shader, não na malha.
- Vértices dominados pelo abdômen abaixo da barra da camisa ficavam sem cor de roupa (mancha de pele na cintura); corrigido.
- O modelo tem lado esquerdo em +X, igual ao projeto. O modelo `torso-with-removable-organs.glb` tem o lado esquerdo em **−X**.
- Cabelo antigo foi reaproveitado (encaixa, mas é o do corpo antigo).
- Olhos ainda parecem grandes demais de perto.

## Órgãos (`torso-with-removable-organs.glb`): avaliado, NÃO aplicado
CC0, 56 malhas nomeadas (coração, pulmões, fígado, estômago, intestinos, rins, vasos, traqueia), sem esqueleto, só o tronco
(0–0,64 m). Comparado com as `OrganDef` do projeto (centro e tamanho em espaço do mundo):

| Órgão | Deslocamento vertical | Razão de tamanho (x, y, z) |
|---|---|---|
| Fígado | +0,844 | 1,83 / 1,77 / 0,95 |
| Estômago | +0,886 | 1,46 / 3,26 / 1,76 |
| Rins | +0,88 | 1,11 / 2,05 / 0,76 |
| Intestino delgado | +0,955 | 0,75 / 1,81 / 0,97 |
| Intestino grosso | +0,822 | 1,09 / 2,64 / 0,77 |

Não dá para trocar sem (1) espelhar X, (2) ajustar cada órgão à sua caixa de dano, (3) separar quais malhas são só visuais.
Hoje as malhas de órgãos do projeto são **só visuais** (`organs_*`, no raio-X); a colisão/dano usa as elipsoides de `OrganDef`,
que não mudam. Recomendação: se for usar, fazer num passo próprio, órgão a órgão.

## Testes executados (build Windows, Unity 6000.6.7f1)
| Teste | Corpo antigo | Corpo novo |
|---|---|---|
| Compila | ok | ok |
| `--phystest` (queda, 40 tiros, arremesso, levantar) | 0 falhas | 0 falhas |
| `--stress` (10 bonecos + explosão), FPS médio (mín.) | 31 (2) / 31 (14) | 72 (29) / 64 (29) / 59 (24) |
| `--autotest` (tiros, machado, flecha, faca, C4, granada, torniquete) | completo, sem exceções | completo, sem exceções |
| `--shots` (43 capturas: neutro, braços levantados, agachado, caído, levantando, feridas, fogo, raio-X, amputação) | revisadas | revisadas |

Resultados determinísticos do `--autotest` são idênticos (sniper na cabeça, machado, flecha cravada, C4 com as mesmas 14
partes arrancadas, torniquete 21,7 → 0,4 ml/s). Fuzil e escopeta diferem por dispersão aleatória.
Não testado: partidas longas, os dois corpos misturados na mesma cena, escadas/bordas.
`--phystest -nographics` imprime `InvalidOperationException: Instancing is not supported` do `BloodSystem` (não existe sem GPU;
acontece igual no corpo antigo).
