# Agonia, marcha e armadilhas (v3)

## 1. Agonia (`Dummy.Ground.cs`)
Antes: a agonia alternava duas poses fixas (`writhe_a`/`writhe_b`) a cada 0,45–1,1 s, com impulsos aleatórios e sem fases.
Agora, em camadas:
- **Pose-base contínua**: o contorcer vai e volta entre as duas poses por um seno (velocidade ligada à dor), em vez de trocar de uma vez.
- **Ruído por junta**: vagar lento (Perlin, ~0,8 Hz, até 0,2 rad nos membros) + tremor de choque (≈50 Hz, 0,03 rad) proporcional à dor e à proximidade da morte.
- **Fases por `Dying()`** (0..1, de perda de sangue, hipóxia e parada cardíaca): contorcer vigoroso → fraco com tremor → suspiros e espasmos.
- **Reflexos do tronco cerebral** (`Reflexes`), que também valem inconsciente e morto (só impulsos, sem PD):
  - *suspiro agônico*: irregular, cada vez mais espaçado (2,2 s a 7 s), cabeça e pescoço jogados para trás (~6 rad/s), peito sobe, som sintetizado (`gasp`);
  - *espasmo clônico*: rajada de ≈5 Hz que decai, com extensão em arco e **postura flexora** (braços fletidos junto ao peito, pernas estendidas, pés em ponta).
- **Gemido** (`groan`, sintetizado, vogal "ah") enquanto consciente e com dor.
- Poses novas: `arch` e `gasp`.

Base técnica: o aviso é que a pesquisa web rendeu pouco material técnico concreto. Usei: movimento procedural em camadas (a talk "Animation Bootcamp", de David Rosen, citada em um post do Unity; **não li a talk**), o add-on "Rigor Mortis" do Garry's Mod (espasmos musculares, mão que busca o ferimento), e descrições médicas de respiração agônica (reflexo do tronco cerebral, suspiros irregulares, tremores). A fisiologia dos espasmos é uma aproximação, não uma simulação médica.

Medida (`--agonytest`, contorcer consciente forçado por 8 s, 5 rodadas cada; o dado varia por sorteio):
| | pico de velocidade (m/s) | junta aberta máx. | observação |
|---|---|---|---|
| Antiga (`--agony=legacy`) | 4,2 / 14,3 / 21,8 / 12,3 / 2,3 | até 3,1 cm | |
| Nova | 9,3 / 10,9 / 5,8 / 2,1 / 7,0 | até 2,7 cm | |
Numa bateria posterior (3 rodadas) apareceram picos isolados de 26–30 m/s e junta de 8,9 cm em 1 de 3. **Os picos esporádicos não foram eliminados** (a versão antiga também os tinha).
Bug encontrado e corrigido no caminho: o impulso de torque do suspiro era proporcional à massa da cabeça e gerava centenas de rad/s; agora é uma mudança de velocidade fixa de ~6 rad/s.
`--agony=legacy` volta ao comportamento antigo (para comparar).

## 2. Andar e correr (`Dummy.Stand.cs`, `tools/build_human_v2.py`)
Problema ("peito de pombo"): tronco rígido e vertical (0,2° de inclinação andando), peito projetado e cabeça recuada (-0,7 cm atrás do peito).
Causas: (a) a cadeia física da coluna/pescoço é toda vertical e o conversor "endireitava" o pescoço do modelo (rotação de 21°), apagando a cifose; (b) nenhuma inclinação ao andar; (c) braços quase retos.
Correções:
- Conversor: cifose torácica + cabeça à frente (pescoço +1,4 cm, cabeça +3,6 cm; rotação do pescoço 21° → 9°). Só visual.
- Inclinação do tronco ao andar (~3°) e a cabeça compensa (olhar nivelado).
- Braços: cotovelo flexionado ao andar (e mais ao correr), balanço maior (0,25→0,36 rad andando).
Medido (`--gaittest`): andando, tronco 0,2° → 3,4°, cabeça 0,8 cm à frente do peito; correndo 12,5° → 15,6°. Conferido nas capturas laterais. **Não é animação real**: segue sendo marcha procedural (IK + ciclo), sem mocap.

## 3. Armadilhas (`Assets/Scripts/World/Hazards.cs`)
Zona de testes ao fundo do galpão (z ≈ 24; esquerda: guilhotina x=-26, prensa x=-18, serra x=-10; direita: martelo x=10, espinhos x=18, lança-chamas x=26). **G** aciona a que você está mirando (a menos de 14 m e 26°). Dá para agarrar um corpo com E e deitar na máquina.
| Máquina | Como funciona | Dano |
|---|---|---|
| Guilhotina | lâmina cai por gravidade (16 m/s²) sobre o cepo, volta por guincho | corte fino pelo plano da lâmina, amputa |
| Prensa hidráulica | êmbolo desce a 0,55 m/s, segura 1,6 s, sobe | esmagamento contundente proporcional ao espaço; membros e cabeça viram gibs quando fecha (<7,5 cm) |
| Serra circular | carro corre no trilho a 0,55 m/s, disco gira | corte repetido; do 3.º golpe atravessa o membro |
| Martelo pendular | levanta devagar e solta; 3 balanços | contusão e empurrão pela velocidade real |
| Espinhos | sobem em ~0,1 s, seguram 2,8 s | até 2 perfurações por parte |
| Lança-chamas | jato horizontal por 4 s | fogo (`Flames`), o boneco pega fogo e rola |
Tudo usa a API de dano existente (`ApplyPenetration`, `SeverPart`, `DestroyPart`).
`--hazardtest --shots-hazard=<pasta>` deita/posiciona um boneco em cada máquina, aciona e mede. Resultado típico: guilhotina (pescoço cortado e cabeça rolando), prensa (54 golpes, 14 fraturas, mãos/pés destruídos), serra (3 cortes), martelo (parada cardíaca, 7 fraturas), espinhos (34 golpes), lança-chamas (boneco pega fogo).
Limitações conhecidas: nas capturas a guilhotina corta braços que caem estendidos no plano da lâmina (correto para a geometria, mas depende de como o corpo foi deitado); os espinhos ainda amputam mãos e pés; não há botão físico, só a tecla G; sem texto nas placas (a câmera é espelhada).

## 4. Testes executados nesta rodada
`--phystest` 0 falhas (corpo antigo e novo); `--autotest` completo, sem exceções (ambos); `--hazardtest` sem exceções;
`--stress` (10 bonecos): antigo 30 FPS, novo 43–46. **O novo caiu de ~65 para ~45 desde as primeiras medidas** (variedade/forma/barba por boneco na criação, camada de roupa, luzes e colisores das armadilhas); continua acima do antigo, mas a queda não foi investigada.
