# 02 — Mãos e pegada das armas

Referência do usuário: viewmodels do CS 1.6 / CS:Source (luva sem dedos, dedos enrolados no cabo, braço baixo à direita).
Esses modelos são protegidos por direitos autorais e **não** foram baixados; usar assets CC0 ou feitos aqui.

Feito: luva preta sem dedos pintada nas cores de vértice (`Player/VmArms.cs`), foice de duas mãos com a lâmina para a frente.

- [ ] Modelo de mão com dedos articulados (hoje a mão é um único blend shape "punho"). Procurar mão CC0 (OpenGameArt,
      Quaternius, Kenney) ou modelar; definir poses por tipo de arma (pistola, fuzil, faca, cabo longo).
- [ ] Pegada por arma: revisar `GUN_VM` / `GUN_GRIP` / `MELEE_VM` em `Player/Player.cs`, conferindo cada `vm_<arma>.png`
      gerado por `BonecoGore.exe --shots=<pasta>`.
- [ ] Besta e arpão: a pegada foi só posicionada pelas tabelas; falta ajustar a mão esquerda no ponto certo do corpo da arma.
- [ ] Posição geral do viewmodel (mais baixo e à direita, como no CS) e animação de recarregar com a mão.
