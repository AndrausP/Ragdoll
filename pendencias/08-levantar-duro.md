# 08 — Levantar do chão está duro (reportado pelo usuário)

Relato: o boneco cai de bruços e levanta "todo duro".

## Feito (`Dummy.Stand.cs`, teste `--getuptest`)
- [x] Poses-chave em **curva de Hermite contínua** (a velocidade não zera em cada pose). Antes: etapas travadas com smoothstep.
- [x] **Atraso por região**: cabeça e tronco saem na frente, braços empurram, coxa/canela/pé vêm depois; lados defasados.
- [x] **Apoios presos**: mão/pé que toca o chão fica no ponto (IK de dois ossos) até a curva tirá-lo; sai pelo ar (passo).
- [x] Respiração de esforço; fraco: mais lento, tremendo, **pausa ajoelhado** com a mão no joelho, pode falhar e tentar de novo.
- [x] Limite de giro por quadro (11 rad/s) contra troca brusca de solução da IK.
- [x] Métricas: 8/8 levantadas, tempo 3,4–3,5 s, giro máx 11 rad/s, 0–1 quadro parado no meio do movimento; capturas a
      cada 0,5 s (`levantar_brucos_*.png`, `levantar_costas_*.png`).
- [x] Sem um pé: tenta levantar, não aguenta, cai e passa a rastejar (01).

## Falta
- [ ] Deslize de apoio: pico de 1,66 m/s em **um quadro** ao recolher o pé sentado (de costas); de bruços 0,68 m/s. Critério
      do teste é 0,6 m/s, por isso ele ainda acusa 1 falha.
- [ ] **Active ragdoll** de verdade (motores PD nas juntas, CoM sobre o polígono de apoio). A pose ainda é escrita direto nos
      corpos (cinemáticos), como na animação em pé; trocar exige ajustar ganhos jogando.
- [ ] Estratégia "sentar de lado" (side-sit) para ferido.
- [ ] Aprovação do usuário jogando. Vídeos de referência ainda ajudam a ajustar as poses.
