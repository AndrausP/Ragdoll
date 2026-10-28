# Corpo procedural: corte, buracos, sangue e rosto

Branch `corpo-procedural`. O esqueleto físico (17 corpos rígidos + juntas, `AnatomyData.cs`) continua sendo o que cai,
equilibra e levanta; o que mudou é **a pele**: uma malha única editável em tempo de execução (corpo v2, 72 mil vértices) e
a roupa como casca separada.

## O que o código faz

| Arquivo | Função |
|---|---|
| `Body/MeshCut.cs` | `MeshData` (cópia editável da malha com pesos), `MeshCut.Cut` (corta por um plano/função, divide triângulos, tampa os dois lados), `MeshCut.Crater` (fura a pele e fecha com funil de carne), grade espacial para achar vértices perto de um ponto |
| `Body/Dummy.Cut.cs` | `CutAt(parte, ponto, direção, irregularidade)`: corta a pele e a roupa, cria um osso novo para o pedaço distal, divide o corpo rígido (mesmo referencial da parte de origem), religa os filhos ao fragmento, reparte ossos/órgãos/vasos, abre o toco que sangra. `TryMeshCut` liga isso ao `SeverPart` (todo golpe que amputa corta onde bateu; mão/pé cortam no pulso/tornozelo; cabeça corta no pescoço) |
| `Body/Dummy.Deform.cs` | `DeformSkin`: afunda a pele na entrada, abre lábio na saída, hematoma, sangue no tecido e, em ferimento grande, buraco de verdade (pele e roupa) |
| `Body/Dummy.Blood.cs` | Sangue que **escorre**: cada ferida que ainda sangra desce um filete na direção da gravidade (pose atual, via `BakeMesh`) e mancha a cor de vértice da pele e da roupa. O tom vermelho da parte inteira foi reduzido a quase nada |
| `Resources/Shaders/BG_Skin.shader` | Rosto procedural (sobrancelhas, pálpebras, olheiras, narinas, lábios, bochechas) calculado por pixel na posição de repouso |

### Como o corte funciona
1. Cada vértice recebe um valor `f` = distância ao plano do corte (com ruído se `ragged > 0`); só entram os vértices cujos
   pesos pertencem à parte cortada e aos seus descendentes.
2. Triângulos que cruzam são divididos; o anel do corte é soldado **por posição** (a malha tem costuras: sem isso o anel vira
   vários pedaços e a tampa fica transparente).
3. Cada lado ganha uma tampa em anéis (carne, carne escura, osso, centro afundado) com irregularidade.
4. Os vértices do lado distal passam a pesar no osso novo; o corpo rígido é dividido na mesma altura.
5. As juntas dos filhos são refeitas; a dobradiça ganha um deslocamento de limites porque o Unity trata a pose atual como zero.

## Como testar (executável gerado por *Boneco Gore → Gerar executável Windows*)
```
BonecoGore.exe --cuttest   --shots-cut=<pasta>      # corta coxa, antebraço, canela; depois uma perna perdida por 36 s
BonecoGore.exe --woundtest --shots-wound=<pasta>    # tiros no peito/perna, foice, machado, marreta, besta e arpão
BonecoGore.exe --shots=<pasta>                      # viewmodels de todas as armas, inventário (inventario.png) e HUD
```

## Limites conhecidos (honestos)
- Corte só em membros e pescoço (cabeça e tronco continuam inteiros; cabeça solta pelo pescoço).
- A tampa do corte é geométrica, sem textura própria; a irregularidade é determinística por posição.
- Buraco com borda de roupa ainda pode deixar pequenos fiapos claros (a roupa é removida por raio, não por contorno).
- Sangue escorre pela gravidade com o corpo na pose atual; não há simulação de fluido nem escorrimento que "contorna" o corpo.
- Levantar/rastejar/estancar continuam sendo o sistema anterior (`Dummy.Ground.cs`, `Dummy.Stand.cs`); foi verificado que ele
  funciona com membros cortados no meio (estabilidade, junta aberta máx. 1,7 cm em 36 s), mas a fluidez do movimento só se
  avalia jogando.
- Desempenho: média igual à de antes nos testes; há mais quadros acima de 50 ms em cenas com muitos buracos (envio completo da malha).
