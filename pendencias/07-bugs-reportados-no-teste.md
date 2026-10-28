# 07 — Bugs reportados pelo usuário em teste (explosão com C4 e besta)

Origem: 3 prints do usuário. Ainda **não reproduzidos nem corrigidos**.

## A. Membros viram "fitas" esticadas depois da explosão (prints 1, 2 e 3)
Depois de C4 / estilhaços, braços e pernas aparecem como tubos compridos e finos, ligados ao corpo, em vez de pedaços
soltos com tampa. O log mostra "AMPUTAÇÃO: … cortado(a) a 9 cm / 15 cm da articulação" nos quatro membros.

- [ ] Reproduzir: C4 grudada no boneco (`Player.PlaceC4`) + detonar; capturar com `--cuttest`-like (explosão em
      `Dummy.ApplyBlast`).
- [ ] Hipótese 1: vários cortes seguidos na mesma explosão (`CutAt` chamado por parte, com `cutHint` do centro da parte e
      `ragged = 1`) — a região (`CutField`) usa os pesos de osso; depois do primeiro corte os vértices do lado distal passam a
      pesar no osso novo e o segundo corte, na parte vizinha, pode deixar vértices "no meio" ligados aos dois pedaços.
- [ ] Hipótese 2: o corte irregular (`ragged`) desloca o plano até 4 cm; triângulos esticados entre vértices dos dois lados
      não são separados quando a divisão cai fora da região de pesos.
- [ ] Hipótese 3: o pedaço cortado mantém pesos antigos no osso do pai nos vértices de transição (articulação), então a pele
      estica até o fragmento. Conferir `CutOptions.proxMap/distMap` e os pesos auxiliares (`aux_*`).
- [ ] Conferir também `Dummy.UpdateSkin` para fragmentos com `meshDetached` e `severed` simultâneos.

## B. Tórax não "dissolve" e o corpo não é cortado de verdade (prints 2 e 3)
O tronco (pelve, abdômen, tórax) continua inteiro com todos os órgãos "DESTRUÍDO"; só os membros saem. A explosão de C4 não
abre o corpo, e não existe corte de tronco (`Cuttable` aceita só membros e pescoço, ver `Dummy.Cut.cs`).

- [ ] Corte do tronco por explosão/lâmina grande: dividir pelve/abdômen/tórax em dois pedaços com tampa e corpo físico próprio
      (as partes do tronco são caixas, não cápsulas; falta definir o eixo e o plano de corte).
- [ ] "Dissolver" o tronco na explosão (destruição de partes): hoje `DestroyPart` só vale para folhas (mão, pé, cabeça).
      Definir gibs/pedaços para tórax e abdômen, com órgãos e sangue saindo.
- [ ] Órgãos destruídos devem sair do corpo quando o tronco abre (`Fx/Guts.cs` existe para evisceração; ligar ao novo corte).

## C. Detalhes visíveis nos prints
- [ ] Print 1: besta/virote e corda do arpão aparecem sobre o corpo morto; a mão e o braço do jogador atravessam o viewmodel
      de forma estranha (arco/besta de lado). Revisar a pose da besta.
- [ ] Print 2/3: o tronco fica coberto de manchas e "quadrados" coloridos (decalques de ferida antigos sobre a pele
      deformada). Revisar `WoundFx` com muitos ferimentos no mesmo ponto.
