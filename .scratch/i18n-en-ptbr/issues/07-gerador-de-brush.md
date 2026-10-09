# 07 — Gerador de Brush e variações

Status: resolved
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

## Comments

**Feito (branch `i18n/07-brush`).**
- `BrushGeneratorPage.axaml`: título, subtítulo, seções (`SEMENTE`/`FORMA`/`SAÍDA`), rótulos de intensidade, `Tamanho`, tooltips e botões → `{DynamicResource BrushGenerator.*}`. Itens `512 px`…`5000 px` e o filtro `PNG` em `NonTranslatable.txt` (área BrushGenerator).
- `BrushGeneratorPage.axaml.cs`: picker `Exportar brush` → `Loc`; `InfoText` (semente/tamanho/prévia e progresso `Gerando …`), banners de sucesso e erro → `LiveText.Set` (mensagens de exceção ficam como recebidas; o fallback `Ocorreu um erro inesperado.` é traduzido ao vivo). `RunAsync`/`ShowSuccess` agora recebem `Func<string>`.
- `BrushVariantsWindow`: título, `Quantidade`, tooltip, botões → `{DynamicResource BrushVariants.*}`; picker de pasta → `Loc`; todo `StatusText` (quantidade inválida, gerando, pronto, exportando i/total, exportados, exceção) → `LiveText.Set`; rótulo `Semente {seed}` das miniaturas (criado em código) → `LiveText.Set`. Plurais: `GeneratingStatus`, `ReadyStatus`, `ExportedStatus` (`.One`/`.Other`; pt-BR `.Other` = literal original).
- `BrushGenerator.cs`: 3 exceções → `Brush.GrayscaleError`, `Brush.PngError`, `Brush.SizeRangeError` (esta última o scanner não listava no escopo, mas é texto de UI via `ex.Message`).

**Saída não depende de `Loc` (estático).** Em `BrushGenerator.cs`, `Loc.` aparece só nas linhas 38, 39 e 126, todas como argumento de `throw` (caminho de falha, nenhum byte gravado). Os bytes do PNG vêm de `Render(settings, …)` + `SKBitmap.Encode`, com `BrushSettings` numérico. Os nomes de arquivo continuam `$"l2brush_{_seed}.png"` (página, `SuggestedFileName`) e `$"l2brush_{settings.Seed}.png"` (variações, `Path.Combine`) — sem `Loc`. Chaves do `AppDatabase` (`brush_last_folder`, `brush_variants_count`) inalteradas.

**Verificação executada.** `dotnet build L2Toolkit.sln -tl:off -v:q -nologo`: 0 erros. L2LOC007/008 nos arquivos do escopo: antes 24+5 / 4+7+3 → depois 0/0. L2LOC009 novos: nenhum. Busca `grep -nP '"[^"]*[A-Za-zÀ-ú]{3,}'` nos 3 .cs: só chaves de settings, classes CSS, recursos de tema, nomes de arquivo e comentários.

**Deferido: GUI.** Troca de idioma com a janela de variações aberta e status visível; PNG idêntico em en/pt-BR com a mesma seed (estaticamente garantido acima).

**Para outros tickets.** `MainWindow.axaml(353)` "Gerador de Brush" (ticket 04, `Nav.*`) e `SplashComposeWindow.axaml.cs(235)` "Brush {_seed} enviado…" (ticket 06) ficam com eles.
