# 08 — Levantar do chão está duro (reportado pelo usuário)

Relato: o boneco cai de bruços e levanta "todo duro". Quer o levantar modelado como um humano de verdade, com base em vídeos
de referência.

Onde está o código: `Dummy.Stand.cs` (`TryGetUp`, `GetUpStep`, ~977 linhas) e `Dummy.Ground.cs`. Hoje o levantar é uma
sequência de poses-alvo aplicada nos corpos rígidos (flexão → quatro apoios → um joelho → em pé). A animação em pé
escreve posição/rotação dos corpos diretamente (`AnimateStanding`), o que dá o aspecto rígido. O `docs/plano-melhorias.md`
já listava como fase B (active ragdoll: motores PD por junta) e C (pés/centro de massa/passo real), **não feitas**.

## O que fazer
- [ ] Substituir poses interpoladas por **active ragdoll**: junta com motor/alvo (PD) em pelve, coluna, coxas, canelas, ombros
      e cotovelos, em vez de gravar posição. Ajustar ganhos kp/kd jogando (só dá para fazer com o Unity aberto).
- [ ] Levantar de bruços, em fases com sobreposição (não em passos travados): (1) cabeça e peito sobem, mãos viram palmas no
      chão na altura dos ombros; (2) quadril sobe e um joelho vem para baixo do corpo; (3) pé da frente apoia, tronco se ergue;
      (4) endireita por último. O peso passa para o pé de apoio antes de soltar as mãos.
- [ ] Levantar de costas: senta com as mãos atrás, recolhe os joelhos, rola o peso para a frente, um joelho no chão, em pé.
- [ ] Variação por estado: ferido, tonto, cansado (mais lento, tremendo, mão apoiada no joelho, falha e tenta de novo).
- [ ] Timing e easing humanos: acelerar e desacelerar cada fase (ease in/out), pequenos atrasos entre membros, respiração.
- [ ] Mãos e pés presos ao chão durante o apoio (sem escorregar): atrito alto e alvo de IK no ponto de contato.
- [ ] Métricas no `--phystest`: velocidade angular máxima por junta, tempo total do levantar (alvo ≈ 2 a 4 s), e uma
      captura em sequência (`--shots` levantar_0..5) para revisar.

## Referências (a obter)
Não consigo assistir a vídeos; preciso de descrição escrita ou de quadros. Sugestão: o usuário envia 2 ou 3 vídeos/GIFs curtos
de pessoas levantando de bruços e de costas (ou prints a cada 0,5 s) e eu extraio as poses e os tempos de cada fase. Fontes
úteis: estudos de "rising from the floor" (movimento humano de levantar do chão), referências de animação de levantar de
jogos (Euphoria/Natural Motion), bibliotecas de mocap livres (CMU Graphics Lab Motion Capture Database, CC0/uso livre).
- [ ] Verificar licença antes de usar qualquer mocap; preferir CMU/CC0 e converter só as poses-chave (não o clipe inteiro).
