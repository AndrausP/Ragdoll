# 04 — Corpo e feridas

Feito: corte em membros e pescoço, buracos, amassados, sangue escorrendo, rosto por shader (ver `docs/corpo-procedural.md`).

- [ ] Tampa do corte: hoje é geométrica e lisa (anéis de cor). Falta textura/relevo de carne, osso saliente e fibras.
- [ ] Buracos na roupa deixam fiapos claros nas bordas (a roupa é removida por raio, não pelo contorno do buraco).
- [ ] Cortar tronco (cintura/peito): só membros e pescoço são cortáveis; a cabeça solta pelo pescoço.
- [ ] Sangue escorrendo: filete reto na direção da gravidade; falta contornar o corpo, pingar no chão a partir do filete e
      deixar a roupa encharcada de forma mais gradual.
- [ ] Ferida na pele sob a roupa: o decalque antigo (`WoundFx`) ainda segue o corpo rígido da parte, não a pele
      deformada; perto das juntas pode descolar.
- [ ] Rosto: sobrancelhas e lábios são pintados por shader; "cara de humano" ainda fica clara (modelo sem textura).
      Cabelo, cílios, dentes e olhos mais realistas não foram feitos.
- [ ] Arrancar membro já cortado (cortes encadeados) está pouco testado.
