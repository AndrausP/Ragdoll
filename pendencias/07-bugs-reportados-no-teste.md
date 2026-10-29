# 07 — Bugs reportados pelo usuário em teste (explosão com C4 e besta)

Origem: 3 prints do usuário. **Reproduzidos e corrigidos.** Teste: `BonecoGore.exe --blasttest` (17 casos: C4 no peito,
coxa, barriga, braço e cabeça, granada nos pés e um controle sem corte) — 0 falhas em duas rodadas seguidas.

## A. Membros viram "fitas" esticadas depois da explosão
Causas encontradas (todas confirmadas medindo o triângulo mais esticado e os ossos dele):
- [x] Lado proximal do corte ainda pesava nos ossos da cadeia distal → **separação rígida dos pesos** (`proxMap`/`distMap`).
- [x] Corte num membro já com galho arrancado religava esse galho ao fragmento com junta nova → galho só troca de dono.
- [x] Mão/pé destruído depois do corte ficava cinemático e preso ao fragmento (segurava o pedaço no ar) → solta a junta;
      pesos divididos vão inteiros para um lado (`SnapWeightsOff`).
- [x] Buraco da explosão/tiro no tronco pegava faces do braço/pescoço vizinhos e a tampa ligava os dois → o buraco só fura
      a pele da própria parte e o funil fica preso ao osso dela.
- [x] Tampa do corte do tronco copiava os pesos dos ombros (borda nas axilas) → miolo da tampa preso ao osso do pedaço;
      corte do peito fica abaixo das axilas.
- [x] Junta religada ao fragmento usava a pose dobrada como zero (limites dobrados) → criada a partir da pose de repouso.
- [x] Fragmento/toco de 2–4 cm girava solto (inércia mínima) → travado; razão de massa mínima 6:1 entre vizinhos.
- [x] Auxiliares de ombro/quadril arrastavam a pele do peito com o braço jogado ao extremo → limitados a 60°.
- [x] Explosão processa das pontas para o tronco e corta perto do ponto da explosão.
- Observação: parte do que parecia "fita" nos prints eram as **alças de intestino** (cor de pele). Agora são vermelho-escuras e com gomos.

## B. Tórax não "dissolve" e o corpo não é cortado de verdade
- [x] Corte do tronco (cintura = abdômen, peito = tórax) com tampa, dois corpos e órgãos repartidos; morte imediata.
- [x] Explosão forte no tronco abre a parede (buraco com tampa de carne na pele e na roupa), joga órgãos destruídos e alças
      para fora; perto o bastante parte o corpo. Limiares em `ImpactConfig` (`TORSO_OPEN_DMG`, `TORSO_SPLIT_*`).
- [x] Amputação por explosão com limiar configurável (`BLAST_TEAR_FRAC`, `BLAST_TEAR_CHANCE`).
- [ ] "Dissolver" o tronco em vários pedaços (gibs Voronoi). Hoje: abre e parte em dois, não esfarela.

## C. Detalhes visíveis nos prints
- [x] Virote/arpão cravados seguem o pedaço cortado e caem quando a parte é destruída; corda solta ao trocar de arma e faz
      barriga quando frouxa.
- [x] Pose da besta: menor, mais à frente e mais baixa, segurada pelas duas mãos (capturas `vm_crossbow.png`).
- [x] "Quadrados" no tronco: decalques antigos eram cópia rígida da malha original. Agora seguem a pele deformada deste
      boneco, somem sobre buracos/cortes e não empilham no mesmo ponto.
