# 06 — Splash Screen e janelas

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `../spec.md`, decisões D3–D8 e D11, [Glossário de tradução].

## Escopo

- `Views/SplashScreen.axaml/.xaml.cs`:
  - rótulos e banners;
  - combos de formato e criptografia: `Lineage2Ver121` e `Lineage2Ver111` são não traduzíveis, `Sem criptografia` e as descrições de formato são traduzíveis;
  - títulos de picker (`Abrir splash screen`, `Salvar splash screen`, `Exportar PNG`, `Substituir imagem`) e filtros (`Splash screen (BMP)`, `Imagens`);
  - mensagens de sucesso e erro (`{fileName} salva — …`, `Original preservado em …`, `{count} pixels recortados.`…).
- `Views/SplashLibraryWindow.axaml/.axaml.cs`:
  - título da janela;
  - botões;
  - status (`Gravando {count} arquivos…`, `{count} arquivos salvos`, `Trocas descartadas.`);
  - legenda de tile `Trocado · W × H · não salvo`;
  - pickers.
- `Views/SplashComposeWindow.axaml/.axaml.cs`:
  - título e controles: Contorno, posição Fora/Dentro/Centro, Sombra projetada, sliders, `Ocupar a arte toda (−10 px)`;
  - legenda (`{name} · {w} × {h} px`, `· contorno {px} px`);
  - pickers.
- `Processing/Splash/{RgbaImage,SplashBmp,SplashEnvelope,SplashFile}.cs`: mensagens de exceção exibidas ao usuário.

## Mudanças

- Chaves em `SplashScreen.*`, `SplashLibrary.*`, `SplashCompose.*` e `Splash.*` (exceções do Processing).
- A legenda dos tiles com troca pendente e os textos de status persistentes usam `LiveText.Set` ou `AppLanguage.Bind`. Banners temporários usam `Loc` direto.
- Combos continuam lidos por `SelectedIndex`.

## Aceite

- Zero L2LOC007/008 nos arquivos do escopo.
- Com um BMP aberto, a biblioteca aberta com uma troca pendente e a janela de composição aberta, trocar o idioma atualiza as três superfícies, inclusive a legenda `Swapped · … · unsaved` / `Trocado · … · não salvo`.
- Um BMP truncado (cópia cortada, descartável) mostra o erro no idioma ativo.
- Salvar e exportar em en gera arquivos byte a byte iguais aos gerados em pt-BR com as mesmas entradas.
