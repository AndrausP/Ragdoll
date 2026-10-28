# Plano de melhorias — base: ragdoll-review.md

Verificado no código (commit 8145c0c) antes de planejar.

## Confirmado no código
- `Dummy.Stand.cs:537-543` — `AnimateStanding` escreve `rb.position/rotation` + `transform` de todas as partes; partes `Freeze` (kinemáticas) em pé.
- `Dummy.Stand.cs:917-944` — `Collapse()` libera, velocidade = delta de posição (limitada a 4 m/s), sem velocidade angular.
- `DummyPart.cs:64-65` — solver 16/6; `DummyPart.cs:74` — atrito único 0.75 em todas as partes.
- `Dummy.cs:297-299` — projeção PositionAndRotation (0.02 / 4°) já ativa.
- Testes existentes: `-autotest`, `--phystest`, `--stress` (`Assets/Scripts/Tests/Tests.cs`), sem métricas numéricas de estabilidade.

## Ordem de execução
| Fase | Entrega | Precisa Unity? | Risco |
|---|---|---|---|
| A | Métricas no `--phystest`: erro máx. de junta, energia, tempo até repouso, penetração, contatos, CPU. Baseline antes de mexer em física | só pra rodar | baixo |
| A2 | Materiais de atrito por região (sola/pele/roupa) em `DummyPart` | sim, pra ajustar | baixo |
| A3 | `Collapse()` preserva velocidade angular + velocidade da pose | sim | baixo |
| B | Active ragdoll mínimo: PD por junta em pelvis/abdomen/chest/coxas/canelas, partes não-kinemáticas em pé, `AnimateStanding` vira pose-alvo | **sim, iterar ganhos** | alto |
| C | Pés (CapsuleCast), centro de massa, ponto de captura, passo real | sim | alto |
| D | Impacto em camadas, torque `r × F`, histórico de eventos | sim | médio |
| E | Desmembramento / visual / áudio | sim | médio |

## Regra
Fases B-C só com Unity aberto: ganhos kp/kd precisam de iteração visual. Código sem poder rodar = chute.

## Sugestão
Começar por A (baseline) + A2 + A3 agora; B depois com Unity instalado.

## Status (feito sem Unity, NÃO compilado/testado)
- [x] A: `PhysTest` com energia cinética, penetração no chão, tempo até repouso (`Tests.cs`)
- [x] A2: atrito por região — pé 1.1, mão 0.85, cabeça 0.7, resto 0.6 (`DummyPart.cs`)
- [x] A3: `Collapse()` herda velocidade angular da pose, limite 8 rad/s (`Dummy.Stand.cs`, guarda `prevRot`)
- [ ] Ao abrir Unity: compilar, rodar `--phystest` e anotar baseline; comparar com antes (git stash) pra validar A2/A3
- [ ] B-E
