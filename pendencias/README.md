# Pendências

> Plano de soluções pesquisadas: [`docs/plano-pesquisa-solucoes.md`](../docs/plano-pesquisa-solucoes.md).
> Atualizado em 28/10/2026 depois da rodada de correções (commits `896f89a`..). Marcado `[x]` = feito e verificado por
> teste automático ou captura; `[~]` = feito em parte; `[ ]` = falta.

| Arquivo | Tema | Situação |
|---|---|---|
| [07-bugs-reportados-no-teste.md](07-bugs-reportados-no-teste.md) | Fitas na explosão; tronco não abria nem cortava | **Feito** (teste `--blasttest`, 0 falhas) |
| [08-levantar-duro.md](08-levantar-duro.md) | Levantar duro | **Feito em grande parte** (`--getuptest`); falta aprovação jogando |
| [01-movimento-e-comportamento.md](01-movimento-e-comportamento.md) | Levantar, rastejar, estancar | Parte feita |
| [03-armas.md](03-armas.md) | Arpão, besta, armas novas | **Feito** (`--weapontest`, 0 falhas) |
| [04-corpo-e-feridas.md](04-corpo-e-feridas.md) | Tampa, buracos, tronco, sangue | Parte feita |
| [02-maos-e-pegada.md](02-maos-e-pegada.md) | Mãos e pegada | Parte feita |
| [05-desempenho-e-projeto.md](05-desempenho-e-projeto.md) | Desempenho, testes, versão | Parte feita |
| [06-modelos-e-assets.md](06-modelos-e-assets.md) | Modelos e assets | Decisão registrada |

## Como me passar o feedback com precisão
- Print com o painel médico visível (Tab), a arma na mão e a parte atingida.
- O que fez e o que esperava ("cortei a coxa esquerda com a foice, esperava X, aconteceu Y").
- Posição do boneco (em pé, de bruços, de costas) e, para movimento, o segundo aproximado.
- Em caso de travamento, o log: `%USERPROFILE%\AppData\LocalLow\Andraus\Boneco Gore Sandbox\Player.log`.
