# 07 — Gerador de Brush e variações

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `../spec.md`, decisões D3–D8 e D11, [Glossário de tradução].

## Escopo

- `Views/BrushGeneratorPage.axaml/.axaml.cs`:
  - rótulos de intensidade (Pontas, Rachaduras, Garras, Estilhaços, Borda suave, Simetria), `SEMENTE`, `Tamanho` e botões;
  - picker `Exportar brush`;
  - banners.
- `Views/BrushVariantsWindow.axaml/.axaml.cs`:
  - título `Variações — Gerador de Brush`;
  - contador;
  - picker `Pasta para as variações`;
  - status (`Exportando {i}/{total} em {size} × {size} px…`, `{count} brushes exportados em {folder}.`).
- `Processing/Brush/BrushGenerator.cs`: exceções `Falha ao converter o brush…` e `Falha ao gerar o PNG.`

## Mudanças

- Chaves em `BrushGenerator.*`, `BrushVariants.*` e `Brush.*`.
- Os tamanhos (`512 px`…`5000 px`) entram em `NonTranslatable.txt`.
- Progresso e status persistente usam `LiveText.Set`.

## Aceite

- Zero L2LOC007/008 nos arquivos do escopo.
- Com a janela de variações aberta e o status visível, trocar o idioma atualiza página e janela.
- Exportar com a mesma seed em en e em pt-BR gera PNGs idênticos.
