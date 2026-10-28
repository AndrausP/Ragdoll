# Plano de soluções — pesquisa para fechar as pendências

Pesquisa feita em 28/10/2026 (artigos, repositórios, documentação do Unity, fóruns, devlogs, páginas de jogos). Objetivo: achar
as **melhores soluções** para cada pendência da pasta [`pendencias/`](../pendencias/README.md) e transformar em um plano de
execução com critérios de aceite.

> **Como ler:** cada tema tem (1) o problema, (2) o que existe no mercado e na pesquisa, (3) a recomendação para este projeto e
> (4) passos. Afirmações marcadas com ⚠ são de fonte secundária (fórum, resenha) ou não foram confirmadas na fonte primária.

## 0. Limites desta pesquisa (honestidade)
- **Não consigo assistir a vídeos.** Usei texto, artigos e código. Para o levantar do chão, os vídeos de referência do usuário ainda são necessários (ver tema A).
- O **GDC Vault** estava em manutenção; a palestra do David Rosen (Overgrowth/Receiver) foi confirmada, mas **não li os slides**.
- Páginas bloqueadas (PMC/NIH, GitHub de um pacote, devlog do GutBox retornou 404): usei só o resumo da busca nesses casos e marquei ⚠.
- Não achei documentação técnica pública sobre o desmembramento do **Boneworks/Bonelab**, do **Blade & Sorcery**, do **Dead Space (original)** nem da **Euphoria** em si; o que está aqui é o que se sabe de fonte pública.
- Nada aqui foi implementado ainda: é plano.

## 1. Mapa: pendência → solução recomendada

| Pendência (pasta `pendencias/`) | Solução recomendada | Esforço | Risco |
|---|---|---|---|
| 08 / 01 Levantar duro, rastejar, estancar | **Active ragdoll** (juntas com motor PD) + **controlador de levantar por fases** (CoM sobre o polígono de apoio) + 3 estratégias humanas; depois, opcional, dados aprendidos (Tao et al.) | G | Alto (ajuste de ganhos) |
| 07-A Membros viram fitas na explosão | Separação rígida de pesos no corte (zerar influência cruzada) + cortes encadeados em ordem | P–M | Médio |
| 07-B Tórax não dissolve / não corta | Corte de tronco por eixo + destruição em pedaços rígidos já existentes (`skin_*`) + órgãos ejetados | M–G | Médio |
| 02 Mãos e pegada | Mão CC0 com dedos (**FPS arms, para / CC0**) + poses de dedos por arma + marcadores de pegada | M | Baixo |
| 04 Tampa do corte, sangue, buraco na roupa | Sangue em **textura UV** (não vértice) + fluxo; tampa com normal map; contorno do buraco por triângulos | M | Baixo |
| 05 Desempenho | Envio parcial da malha, grade espacial já feita, orçamento de ragdolls | P–M | Baixo |
| Estabilidade física (base de tudo) | Aplicar as regras de estabilidade do Unity (razão de massa, iterações, escrita de transform) | P | Baixo |

---

## 2. Tema A — Levantar do chão como um humano (pendências 08 e 01)

### 2.1 O problema no nosso código
`Dummy.Stand.cs` grava `rb.position/rotation` e o `transform` das partes a cada passo; as partes ficam cinemáticas (`Freeze`)
durante o levantar. A documentação do Unity pede **não** mexer direto no `Transform` de corpos cinemáticos ligados por juntas a
corpos dinâmicos, porque isso pula o cálculo interno de velocidade do PhysX e dá resultados instáveis
([Unity — estabilidade de ragdoll](https://docs.unity.com/en-us/engine/current/manual/physics-section/physics-overview/ragdoll/ragdoll-stability)).
É uma causa provável do aspecto "duro": o corpo é arrastado por poses, não por músculos.

### 2.2 O que existe (da mais barata à mais cara)

**Opção 1 — Fases procedurais com motores (PD) e polígono de apoio — _recomendada_.**
Ben Kenwright, *Real-Time Character Rise Motions* ([arXiv 2304.05056](https://arxiv.org/abs/2304.05056); texto completo em [ar5iv](https://ar5iv.labs.arxiv.org/html/2304.05056)) descreve um controlador em tempo real, sem keyframes e sem otimização offline, que levanta
de várias posições deitadas:
- **Fase 0:** depois que o corpo para (velocidades abaixo de um limiar), rola para a frente ou de lado; mãos e pés recebem alvos confortáveis à esquerda/direita para não cruzar membros.
- **Fase 1:** solta uma mão; os pés + a mão de apoio formam o polígono de apoio; um modelo simples (massa pontual com pernas e braços telescópicos, tipo pêndulo invertido) coloca o **centro de massa (CoM) o mais perto possível dos pés**. Se não alcança, a mão de apoio troca de lado e repete.
- **Fase 2:** com o CoM sobre os pés, solta as duas mãos e sobe na vertical estendendo as pernas; braços relaxam.
- Trajetórias de mãos e pés em **splines de Bézier** (altura e velocidade definem o "estilo"); IK analítico de duas articulações; torque por PD `τ = kp(θd − θ) − kd·θ̇` com ganhos ajustados à mão; contatos de mãos e pés fixos (sem escorregar); realimentação periódica corrige perturbações; simulação a 100 fps.
- Restrição de prioridade: primeiro manter o CoM dentro do apoio e os membros dentro dos comprimentos mín/máx; depois, a postura-alvo.
- **Por que serve a nós:** (a) é exatamente o que o usuário descreveu (apoia as mãos, traz o joelho, levanta fluido); (b) trata comprimentos de membro como parâmetro, então um membro ausente vira "polígono de apoio menor" e o controlador decide **tentar, falhar e rastejar**; (c) não precisa de mocap nem de treino.
- Há um repositório de referência de comportamento procedural em Unity: [active-ragdolls (sergioabreu-g)](https://github.com/sergioabreu-g/active-ragdolls) (Apache-2.0): corpo animado oculto + corpo físico; cada `ConfigurableJoint` persegue a rotação do osso correspondente. ⚠ A página não traz valores de ganho nem equilíbrio.
- No Unity, o motor vem do `ConfigurableJoint`: *Target Rotation* + *Angular Drive* (mola = termo P, amortecedor = termo D, *Maximum Force* limita o torque) — ver o [manual do ConfigurableJoint](https://docs.unity3d.com/Manual/class-ConfigurableJoint.html).

**Opção 2 — Animação de levantar + ressincronização (blend físico ↔ animação).**
Fluxo comum (guias de [mocaponline](https://mocaponline.com/blogs/mocap-news/ragdoll-physics-animation-guide), [Imphenzia/Puppet Master](https://imphenzia.itch.io/thats-racing/devlog/36403/ragdoll-knockdown-getup) e um guia de Unreal ⚠): esperar o corpo parar → ler a orientação da pelve para saber se está de **frente ou de costas** → tirar uma foto da pose → misturar para o primeiro quadro da animação de levantar (peso da física de 1→0 em ~0,2–0,5 s) → IK para mãos/pés no chão real. Precisa de **pelo menos dois clipes** (de bruços e de costas). Armadilhas listadas: ragdoll estourando por colisores sobrepostos, tremor por motores ligados, pés afundando, articulações apertadas demais deixando tudo "de madeira", saltos sem a pose-match.
Serve se tivermos clipes bons. Hoje não temos e não há como "assistir" referências; por isso fica como **segunda etapa** (fonte de poses-chave para a Opção 1).

**Opção 3 — Aprendido por reforço (longo prazo).**
- [*Learning to Get Up* (Tao, Wilson, Gou, van de Panne — SIGGRAPH 2022)](https://ar5iv.labs.arxiv.org/html/2205.00307): levantar **sem mocap**, em 3 etapas — (1) personagem "forte" descobre a estratégia, (2) enfraquece gradualmente os torques (fator 0,95) para ficar natural, (3) um controlador de imitação aprende a mesma coisa **até 5× mais devagar**. Recompensas limitadas a [0,1] e multiplicadas. Funciona também com variantes **com perna/braço engessado ou sem um braço** — o que casa com o nosso caso de membros cortados. Custo: ~24 h por política em 32 CPUs + GPU; simulação a 800 Hz, controle a 40 Hz. Código: [tianxintao/get_up_control](https://github.com/tianxintao/get_up_control) (dm_control/MuJoCo, Python, testado em Ubuntu; ⚠ licença não declarada na página). Saída útil: **ângulos de junta em `.npy`** (`--to_file`), que poderiam virar clipes para o nosso esqueleto.
- [*Synthesizing Get-Up Motions for Physics-based Characters* (Frezzato et al., CGF 2022)](https://diglib.eg.org/items/d9302d75-fe63-4a27-a476-d52b543d54a3/full) ⚠ (só o resumo da busca): usa mocap + espaço latente com curvas de "estilo", de bruços e de costas.
- [DReCon (Bergamin et al., Ubisoft La Forge, SIGGRAPH Asia 2019)](https://www.ubisoft.com/en-us/studio/laforge/news/VjEIwquaIyEZZSw5RZI0V/drecon-datadriven-responsive-control-of-physicsbased-characters): Motion Matching dá o alvo cinemático e uma política RL corrige com pequenas ações. Precisa de mocap + treino pesado; ⚠ não achei código público.
- [Unity ML-Agents](https://pypi.org/project/mlagents/0.30.0) (Apache-2.0) tem exemplo de ragdoll andando; não achei política de levantar pronta ⚠.
- **Veredito:** não agora. Vale como Fase 3 se o controlador procedural ficar abaixo do esperado.

**Opção 4 — Comprar um pacote.** [Self-Balancing Active Ragdoll (FrostPunchGames)](https://frostpunchgames.itch.io/active-ragdoll) (US$ 19,99, Unity 2022.3+; levantar procedural, equilíbrio por CoM, IK de pés) e o Puppet Master (RootMotion; ⚠ não li a documentação). **Não recomendo:** exigem rig Humanoid padrão e o nosso esqueleto tem 17 partes próprias, com cortes que dividem corpos em tempo de execução. Servem como **referência de comportamento**, não como base.

### 2.3 Como humanos realmente levantam (para as estratégias)
Estudos de movimento descrevem três técnicas de levantar do chão: **sentar de frente (sit-up)**, **sentar de lado (side-sit)** e **rolar (roll-over)**. Em um estudo com 20 adultos 65+ (3D mocap, 2023), o sit-up foi o mais comum (12/20) e exigiu mais flexão de quadril e joelho ([resumo na busca; PMC bloqueado ⚠](https://pmc.ncbi.nlm.nih.gov/articles/PMC10558536)); em outro (congresso de fisioterapia, 18 participantes, 60+) apareceram sit-up (6), side-sit (9) e roll-over (3), e o tempo de levantar se associou ao teste de sentar-levantar de 5 repetições ([World Physiotherapy](https://world.physio/congress-proceeding/floor-recovery-biometric-analysis-what-does-it-take-get-floor)). São dados de idosos; servem para **variar** a estratégia (ferido/cansado → side-sit; de costas → sit-up; de bruços → mãos e joelhos).

### 2.4 Estabilidade física (pré-requisito de tudo)
Regras do manual do Unity ([estabilidade de ragdoll](https://docs.unity.com/en-us/engine/current/manual/physics-section/physics-overview/ragdoll/ragdoll-stability)) aplicadas ao nosso esqueleto:
- **Razão de massa entre corpos ligados:** 2:1 ok, **10:1 pode tremer**. Calculado de `AnatomyData.cs`: tórax 17 kg → pescoço 1,5 kg = **11,3:1**; tórax → braço 2,2 kg = **7,7:1**; antebraço → mão 2,8:1. → Ação: teste A/B elevando massas de pescoço/braços (não precisa ser fiel ao real; pesa mais na estabilidade) e medir tremor no `--phystest`.
- **Iterações do solver** 10–20 (já usamos 12/4 global e 16/6 por corpo — manter) e **limites angulares Y/Z ≥ 5–15°** ou zero.
- **`enablePreprocessing = false`** e **projeção** ligadas (já usamos) — ok.
- **Limitar velocidades iniciais** e `maxDepenetrationVelocity` (relatado como a correção que mais resolveu projeção de junta em [How we made our active ragdolls somewhat convincing](https://discussions.unity.com/t/wip-how-we-made-our-active-ragdolls-somewhat-convincing/848337) ⚠ fórum). Hoje a queda limita a 4 m/s (`Collapse`); falta `maxDepenetrationVelocity`.
- **Escrever pose só pela física:** usar `Rigidbody.MovePosition/MoveRotation` ou motores de junta, nunca `transform` em cinemáticos ligados.
- ⚠ **ArticulationBody vs. ConfigurableJoint:** não achei comparação publicada para humanoides. Fazer um **teste local** (mesmo rig, mesmos cenários) antes de decidir; só migrar se o ganho for claro.

### 2.5 Lições de jogos
- **Euphoria (GTA IV/V, RDR, Max Payne 3)** — "Dynamic Motion Synthesis": comportamento procedural em vez de animação enlatada ([Wikipedia](https://en.wikipedia.org/wiki/Euphoria_(software))). Lição: GTA V reduziu propriedades físicas para o personagem deslizar mais e **levantar mais rápido** ⚠. → Não perseguir "100% físico": o jogo precisa se recuperar em tempo razoável.
- **Overgrowth/Receiver (David Rosen, GDC 2014, [vídeo](https://discussions.unity.com/t/an-indie-approach-to-procedural-animation-gdc-video-talk/538228))** — poucos keyframes + técnicas procedurais; "primeiro tornar a física responsiva, depois pôr a animação por cima" ⚠ (resumo de fórum); o jogo mistura animações pré-definidas com física ([PC Gamer](https://www.pcgamer.com/au/overgrowth-preview)). → **Híbrido** é o caminho: poucas poses-chave por fase + motores + correção.
- **Boneworks/Bonelab (Stress Level Zero, engine Marrow)** — "física primeiro" ([resenha](https://mixed-news.com/en/bonelabs-avatar-swapping-gameplay-could-make-for-a-unique-vr-experience/) ⚠); sem material técnico público.

### 2.6 Plano de execução (tema A)
| Passo | Entrega | Critério de aceite |
|---|---|---|
| A0 | Estabilidade: teste A/B de massas, `maxDepenetrationVelocity`, trocar escrita de `transform` por `MovePosition/MoveRotation` | `--phystest` sem falhas; tremor (velocidade angular p95) ≤ baseline |
| A1 | **Active ragdoll mínimo**: `ConfigurableJoint` com `slerpDrive`/`angularXDrive` em pelve, coluna, coxas, canelas, ombros, cotovelos; alvo = pose da máquina de fases | Boneco em pé se sustenta e reage a empurrão sem "congelar" |
| A2 | **Levantar de bruços** (Fases 0→2 do Kenwright): mãos → joelho → pé → corpo; CoM sobre o polígono de apoio; mãos/pés presos (atrito alto + alvo de IK) | Tempo 2–4 s; sem junta aberta > 2 cm; 20/20 tentativas em chão plano |
| A3 | **Levantar de costas** (sit-up) e **de lado** (side-sit, ferido) | Estratégia escolhida por estado; capturas em sequência a cada 0,5 s revisadas |
| A4 | **Membros ausentes**: polígono de apoio recalculado; se inviável → tenta, cai, rasteja (cotovelos/antebraços); duas mãos no toco | Sem pé: cai e rasteja; sem mão: usa o outro braço; nunca "levanta do nada" |
| A5 | Polimento: easing por fase, respiração, tremor por fraqueza, variação aleatória | Aprovação do usuário jogando |
| A6 (opcional) | Gerar poses-chave com Tao et al. ou mocap CC0 e comparar com A2 | Só se A2 não convencer |

**Material que o usuário pode mandar para acelerar:** 2–3 vídeos curtos/GIFs de pessoas levantando de bruços e de costas (ou prints a cada 0,5 s) — extraio poses e tempos por fase.

---

## 3. Tema B — Corte de malha, membros esticados e tronco (pendências 07-A e 07-B)

### 3.1 Estado da arte (ferramentas)
| Ferramenta | O que faz | Serve a nós? |
|---|---|---|
| [MeshSlicerFree (hanzemeng)](https://github.com/hanzemeng/MeshSlicerFree) / [Asset Store](https://assetstore-fallback.unity.com/packages/tools/utilities/mesh-slicer-free-283149) | Corta malhas côncavas/abertas/furadas, **inclui skinned mesh** e triangulação de Delaunay restrita; ⚠ <1000 vértices fatiam em <100 ms; licença não aparece na página | Boa **referência de código** para tampa; nossa malha tem 72 mil vértices |
| [OpenFracture (dgreenheck)](https://github.com/unitycoder/OpenFracture) | Fratura/fatia; a tampa usa triangulação de Delaunay restrita e exige polígono fechado e sem autointerseção; ⚠ skinned/runtime variam por versão | Referência para **gibs** (Voronoi) do tronco destruído |
| Dynamic Mesh Cutter (Asset Store, pago) | Corte por plano, respeita pesos de osso, ⚠ cortes hierárquicos | Não: já temos corte próprio |
| Slice SkeletalMesh (Unreal, pago) | Corte com superfície selada; o plano é **infinito e não se limita a um osso** | Mostra o mesmo problema que tivemos: é preciso **restringir por osso/região** |

### 3.2 Técnica alternativa barata: "buraco por shader" + tampa
Devlog do GutBox ⚠ (resumo da busca; a página dá 404): clipa/descarta a região no shader, preenche com uma "gore cap" e gibs falsos; o shader do personagem lê os dados do corte, e as decalques são limpas e redesenhadas para seguir a animação. Outro dev duplica o ragdoll, zera a escala dos ossos de cada lado e põe peças de tampa na emenda ([Unity Forum — Character Body Part Dismemberment](https://discussions.unity.com/t/character-body-part-dismemberment/9967)). Vantagem: **sem refazer topologia** (barato). Desvantagem: precisa de duas malhas e tampa por sobreposição.
→ **Mantemos o corte geométrico** (já funciona e fecha a tampa), mas podemos usar o truque do shader para **cortes rápidos em cenas com muitos bonecos**.

### 3.3 Bug 07-A: membros viram "fitas" — causa provável e correção
Hipótese principal (a confirmar reproduzindo com C4): **vértices de transição (articulação) mantêm pesos nos dois lados do corte** e a pele estica entre o corpo e o fragmento. Em Unity, a malha não se reencaixa sozinha em ossos quebrados; é preciso reatribuir pesos explicitamente (ver [fórum: skinned mesh esticando com juntas quebradas](https://discussions.unity.com/t/skin-mesh-renderer-is-stretching-in-a-ragdoll-with-broken-joints/899515)). Hoje só o lado distal troca pesos (`distMap`).
**Correção:**
1. **Separação rígida:** lado proximal: **todo** peso em ossos da cadeia distal vira o osso proximal; lado distal: **todo** peso em ossos proximais/ancestrais vira o osso do fragmento (incluindo `aux_*`). Renormalizar.
2. **Cortes encadeados** (explosão): ordenar do **mais distal para o mais proximal** e recalcular a região após cada corte; não reaproveitar `cutHint` do centro da parte (usar o ponto do impacto/fragmento).
3. **Triângulos esticados:** depois do corte, remover/triangular de novo qualquer triângulo cujos 3 vértices tenham ossos dominantes em lados diferentes.
4. API de pesos do Unity: `Mesh.GetAllBoneWeights/SetBoneWeights` ([docs](https://docs.unity3d.com/kr/2023.2/ScriptReference/Mesh-boneWeights.html)) permitem mais de 4 influências, se precisarmos.
5. Teste automático: `--cuttest` com explosão; métrica = maior comprimento de aresta de triângulo após o corte (deve ficar < 3× o original).

### 3.4 07-B: tronco que "dissolve" e corte de verdade
O que os jogos fazem:
- **Soldier of Fortune (GHOUL, Raven)** — modelo dividido em **26 zonas** de dano independentes (36 na sequência); cada uma sofre dano à parte ([Wikipedia](https://en.wikipedia.org/wiki/Soldier_of_Fortune_(video_game))). *Payback* subdivide mais (pé, tornozelo, canela, duas seções de coxa) ⚠. → Nosso corte em qualquer ponto já supera isso nos membros; **falta zona no tronco**.
- **Dead Space** — "desmembramento estratégico" como mecânica; no *remake* (2023) há um sistema de **"descascar"**: camadas de pele, músculo e osso reveladas conforme o dano ([Epic](https://store.epicgames.com/news/inside-dead-space-2-the-new-necromorph-nightmare?lang=uk)); e o modelo é realmente cortado em pedaços ⚠.
- **Blade & Sorcery** — desmembramento só com **ângulo e local corretos**, ajustado para "não ser frequente nem caricato" e com opção de desligar ([GamesBeat](https://gamesbeat.com/blade-sorcery-interview-one-developer-redefines-physics-for-vr-melee-combat/) ⚠). → Adicionar **limiares configuráveis** (probabilidade, energia mínima, ângulo).

**Recomendação para o nosso tronco (pelve, abdômen, tórax — são caixas):**
1. **Corte do tronco por plano horizontal** (cintura/peito): generalizar `LimbAxis` para eixo Y de caixa e permitir dividir `abdomen`/`chest` em dois corpos (mesmo referencial, como nos membros). Órgãos e vasos repartidos por altura (já existe `SplitAnatomy`).
2. **Destruição do tronco (explosão forte):** usar as **malhas rígidas por parte `skin_*` que o projeto já tem** (hoje só para membros arrancados): esconder a região da pele contínua e lançar pedaços rígidos com tampa (via `MeshCut`) + **órgãos, ossos e sangue** (`Fx/Guts.cs`). Chunks extras por Voronoi (referência: OpenFracture) para o efeito de "dissolver".
3. **Camadas (opcional, estilo Dead Space remake):** pele → músculo → osso como submalhas com *clip* por ferida; mais caro e fica para depois.

### 3.5 Plano de execução (tema B)
| Passo | Entrega | Aceite |
|---|---|---|
| B1 | Reproduzir 07-A (teste com C4) e medir aresta máxima | Falha reproduzida e métrica registrada |
| B2 | Separação rígida de pesos + cortes encadeados em ordem distal→proximal | Sem fitas; aresta máx < 3× |
| B3 | Limiares de amputação (energia/ângulo/probabilidade) em `ImpactConfig` | Configurável e documentado |
| B4 | Corte de tronco (cintura/peito) com dois corpos | Boneco cortado ao meio cai em duas metades com órgãos |
| B5 | Destruição do tronco por explosão com pedaços rígidos e gibs | Sem tórax "inteiro e destruído"; capturas antes/depois |
| B6 (opcional) | Camadas de pele/músculo/osso | — |

---

## 4. Tema C — Mãos, pegada e armas (pendências 02 e 03)

- **Mão com dedos (CC0):** [FPS arms (rigged only) — para, CC0](https://opengameart.org/content/fps-arms-rigged-only): ~4 000 vértices, 1024² de textura, `.blend` + `.fbx`, **ossos de dedos com restrição para curvar rapidamente** (T-pose + animação crua). É a melhor candidata encontrada; download direto: `https://opengameart.org/sites/default/files/fps%20arms.7z`. Alternativa: [Low Poly FPS Rifle and Hands (Robin Lamb, CC0)](https://opengameart.org/content/low-poly-fps-rifle-and-hands) ⚠ (a página não diz se há ossos de dedos). Foram **ignorados de propósito** os modelos de CS (direitos autorais) e os pagos.
- **Técnica de pegada:** a Mecanim/IK não controla dedos; solução comum é dados por arma (ScriptableObject) com a posição da mão relativa à arma + rotação dos ossos dos dedos, e *marcadores* de empunhadura/apoio no modelo ([fórum Unity](https://discussions.unity.com/t/mecanim-ik-ik-with-hand-to-grab-objects-naturally/491731)). Para nós: trocar o único *blend shape* "punho" por **poses de dedo por tipo de pegada** (pistola, fuzil, cabo longo, faca, apoio sob o cano, pinça da corda).
- **Passos:** C1 baixar e converter a mão para `.bytes`/malha com ossos; C2 poses de dedos (4–6 tipos); C3 tabela por arma (`GUN_GRIP`/`MELEE_VM`) com **marcadores no modelo** (empunhadura, apoio, cano); C4 revisar `vm_<arma>.png` de todas as armas; C5 besta e arpão (mão esquerda no ponto certo).
- **Armas novas:** a procura por modelos CC0 de arpão/speargun não achou nada pronto; o arpão fica **feito por código** (`tools/add_weapons.py`). Para mais armas, candidatos: pacotes CC0 do Quaternius (medieval e armas de fogo, já usados) e [OpenGameArt](https://opengameart.org/) (verificar licença de cada arquivo).

## 5. Tema D — Ferimentos, sangue e tampa (pendência 04)

- **Sangue persistente e escorrendo:** hoje usamos cor de vértice (resolução ~1,5 cm). Alternativas: (a) **mapa de respingos em espaço UV2** (como [SkinnedMeshDecals — naelstrof, MIT](https://github.com/naelstrof/SkinnedMeshDecals): decalques pintados em RenderTexture por modelo, dilatação nas costuras, orçamento de VRAM de 512 MB padrão ⚠ não cobre sangue especificamente); (b) **máscaras por vértice/textura** ([Procedural Wound Generator](https://superhivemarket.com/products/procedural-wound-generator), ⚠ produto pago, só referência); (c) discussão que prefere pintar vértice a empilhar decalques ([fórum UE](https://forums.unrealengine.com/t/blood-floor/459704) ⚠). **Escoamento realista:** modelo de fluxo sobre a superfície (trabalho de visualização de sangue em coxa, [SPIE 2012 ⚠](https://odu.academia.edu/RifatAras)). → **Recomendação:** textura de sangue 512² por boneco em UV2 + passo de "fluxo" que empurra tinta para baixo pelo gradiente de altura (mundo); manter vértice só para o encharcado da roupa.
- **Amassados de bala:** pesquisa de *denting* ([Patkar et al., 2014](https://diglib.eg.org/handle/10.2312/sca.20141126.087-096)) usa mapa de amassado do projétil em Z-buffer; para pele animada, o prático é **deslocamento no espaço de repouso** (o que já fazemos) + **normal map/parallax** para calibres pequenos. Sem referência pública específica para pele skinada ⚠.
- **Tampa do corte:** textura de carne/osso em espaço de planta (UV planar na tampa), normal map e **anel de osso saliente**; irregularidade já existe.
- **Buraco na roupa:** remover triângulos pelo **mesmo contorno** da pele (hoje por raio) e adicionar franja; ou buraco por shader (descarte por máscara radial) e funil só na pele.
- **Passos:** D1 buraco de roupa por contorno; D2 tampa texturizada; D3 mapa de sangue UV + fluxo; D4 normal map de ferida; D5 corrigir decalques antigos (`WoundFx`) para seguir a pele deformada.

## 6. Tema E — Desempenho (pendência 05)
- Já feito: grade espacial para busca de vértices; envio de malha com limite de 8/s.
- Próximo: **envio parcial** (`Mesh.SetVertexBufferData` só da faixa alterada), `BakeMesh` só para o filete ativo (orçamento por quadro), **orçamento de ragdolls** (o guia da mocaponline sugere ~8–12 ativos ⚠), LOD físico à distância.
- Medir com `--autotest` (frames > 50 ms) e `--stress` com bonecos feridos.

## 7. Roadmap sugerido (ordem)
1. **A0 + B1/B2** (estabilidade e bug de fitas) — baixo risco, destrava o resto.
2. **A1–A4** (levantar/rastejar/estancar) — maior ganho percebido; exige o usuário testando.
3. **B3–B5** (tronco e explosão).
4. **C1–C5** (mãos e pegadas).
5. **D1–D5** e **E** (acabamento e desempenho).
6. **A6** (aprendido) só se necessário.

**Decisões que dependem do usuário:** vídeos de referência do levantar; prioridade entre tronco (B) e movimento (A); licença para baixar `FPS arms` (CC0) — pode ir direto; se aceita comprar um pacote (não recomendado).

## 8. Fontes
Levantar / físico
- Kenwright — [Real-Time Character Rise Motions (arXiv 2304.05056)](https://arxiv.org/abs/2304.05056), [ar5iv](https://ar5iv.labs.arxiv.org/html/2304.05056)
- Tao et al. — [Learning to Get Up](https://ar5iv.labs.arxiv.org/html/2205.00307), [código](https://github.com/tianxintao/get_up_control)
- Frezzato et al. — [Synthesizing Get-Up Motions](https://diglib.eg.org/items/d9302d75-fe63-4a27-a476-d52b543d54a3/full)
- Bergamin et al. — [DReCon (Ubisoft La Forge)](https://www.ubisoft.com/en-us/studio/laforge/news/VjEIwquaIyEZZSw5RZI0V/drecon-datadriven-responsive-control-of-physicsbased-characters)
- [Unity — Estabilidade de ragdoll](https://docs.unity.com/en-us/engine/current/manual/physics-section/physics-overview/ragdoll/ragdoll-stability), [ConfigurableJoint](https://docs.unity3d.com/Manual/class-ConfigurableJoint.html)
- [active-ragdolls (sergioabreu-g, Apache-2.0)](https://github.com/sergioabreu-g/active-ragdolls), [Active Ragdoll (FrostPunchGames)](https://frostpunchgames.itch.io/active-ragdoll)
- [Como blendar ragdoll e animação (mocaponline)](https://mocaponline.com/blogs/mocap-news/ragdoll-physics-animation-guide), [Imphenzia — Ragdoll knockdown/getup](https://imphenzia.itch.io/thats-racing/devlog/36403/ragdoll-knockdown-getup), [Unity Forum — active ragdolls convincentes](https://discussions.unity.com/t/wip-how-we-made-our-active-ragdolls-somewhat-convincing/848337)
- [Euphoria (Wikipedia)](https://en.wikipedia.org/wiki/Euphoria_(software)), [Rosen — GDC 2014](https://discussions.unity.com/t/an-indie-approach-to-procedural-animation-gdc-video-talk/538228), [PC Gamer — Overgrowth](https://www.pcgamer.com/au/overgrowth-preview)
- [Levantar do chão em idosos (IJERPH 2023, PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10558536), [World Physiotherapy — floor recovery](https://world.physio/congress-proceeding/floor-recovery-biometric-analysis-what-does-it-take-get-floor)

Corte, desmembramento, sangue
- [MeshSlicerFree](https://github.com/hanzemeng/MeshSlicerFree), [OpenFracture](https://github.com/unitycoder/OpenFracture), [Dynamic Mesh Cutter](https://assetstore.unity.com/packages/tools/modeling/dynamic-mesh-cutter-208384)
- [Character Body Part Dismemberment (Unity Forum)](https://discussions.unity.com/t/character-body-part-dismemberment/9967), [skinned mesh esticando](https://discussions.unity.com/t/skin-mesh-renderer-is-stretching-in-a-ragdoll-with-broken-joints/899515), [Mesh.boneWeights](https://docs.unity3d.com/kr/2023.2/ScriptReference/Mesh-boneWeights.html)
- [Soldier of Fortune / GHOUL (Wikipedia)](https://en.wikipedia.org/wiki/Soldier_of_Fortune_(video_game)), [Dead Space remake (Epic)](https://store.epicgames.com/news/inside-dead-space-2-the-new-necromorph-nightmare?lang=uk), [Blade & Sorcery (GamesBeat)](https://gamesbeat.com/blade-sorcery-interview-one-developer-redefines-physics-for-vr-melee-combat/)
- [SkinnedMeshDecals](https://github.com/naelstrof/SkinnedMeshDecals), [Procedural Wound Generator](https://superhivemarket.com/products/procedural-wound-generator), [Denting (Patkar et al.)](https://diglib.eg.org/handle/10.2312/sca.20141126.087-096)

Mãos e armas
- [FPS arms (rigged only), CC0](https://opengameart.org/content/fps-arms-rigged-only), [Low Poly FPS Rifle and Hands, CC0](https://opengameart.org/content/low-poly-fps-rifle-and-hands), [Wooden crossbow, CC0](https://opengameart.org/content/wooden-crossbow), [Mecanim IK para pegar objetos](https://discussions.unity.com/t/mecanim-ik-ik-with-hand-to-grab-objects-naturally/491731)
