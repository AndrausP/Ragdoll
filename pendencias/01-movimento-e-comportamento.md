# 01 — Movimento e comportamento

Ver também o relato do levantar duro em [08-levantar-duro.md](08-levantar-duro.md).

O sistema de levantar/rastejar/estancar é o anterior (`Dummy.Ground.cs`, `Dummy.Stand.cs`). Foi verificado só por capturas e
métricas: 36 s com a coxa cortada, junta aberta máxima 1,7 cm, sem exceções. A fluidez **não** foi avaliada jogando.

- [ ] Jogar e avaliar: cair, analisar a posição (de bruços / de costas) e levantar usando mãos e joelho de forma fluida.
- [ ] Sem um pé: tentar levantar, cair por não aguentar o peso e depois se arrastar com os braços (hoje, nos testes, ele
      aplica torniquete e fica pressionando a ferida; o rastejar aparece pouco).
- [ ] Perna cortada: as duas mãos estancando o toco de forma natural (hoje usa uma mão por ferida).
- [ ] Agonia: conferir se algum braço "bugado" ou dobrado de forma antinatural aparece em situações reais.
- [ ] Decidir se o boneco com a perna cortada deve tentar levantar antes de ir para o chão (o código atual só sabe a
      parte, não o comprimento restante do membro: `Dummy.Stand.cs`, checagem "perdeu a perna").
- [ ] Limites de junta do fragmento cortado: cones de tornozelo/pulso são recriados em pose dobrada (só a dobradiça
      recebe o ajuste de limites). Conferir visualmente pés/mãos soltos.
