# 05 — Desempenho e projeto

- [ ] Quadros acima de 50 ms no `--autotest`: ~3% (antes ~1%), média igual (~16–19 ms). Causa provável: reenvio completo
      da malha em cenas com muitos buracos (`FlushMesh`) e `BakeMesh` do sangue. Otimizar (envio parcial, só no buraco).
- [ ] `--stress` com 10 bonecos feridos: medir de novo com tiros/cortes e comparar com o baseline anterior.
- [ ] O Unity alterou `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt` e
      outros ao abrir o projeto (editor 6000.6.7f1; o projeto estava em 6000.6.4f1). Decidir a versão e commitar de propósito.
- [ ] Testes automáticos (`--cuttest`, `--woundtest`) só geram capturas e logs; falta checagem com critério de falha.
- [ ] Revisar a licença do corpo anatômico original (já listada em `CREDITOS.md`).
- [ ] Merge da branch `corpo-procedural` na `master` depois de jogar e aprovar.
