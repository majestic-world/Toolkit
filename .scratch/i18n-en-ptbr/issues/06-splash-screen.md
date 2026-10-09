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

## Comments

### Implementação (branch `i18n/06-splash`)

- 139 entradas (×2 idiomas) em `Splash.*` (18), `SplashCompose.*` (47), `SplashLibrary.*` (34, com plurais `ApplyingStatus`, `FailedStatus`, `ListedStatus`, `SavedStatus`, `SavingStatus`, `SwappedStatus`) e `SplashScreen.*` (40, com plural `KeyedOutStatus`). pt-BR = literal atual (D11); os `.One` são texto novo, já que o literal antigo era sempre plural. Reuso de `Common.Save` no botão Salvar.
- `NonTranslatable.txt`: `Splash Screen`, `Lineage2Ver111`, `Lineage2Ver121`, `PNG` (SplashScreen); `BRUSH`, `PNG` (SplashCompose). Extensões `"bmp"`/`"png"` do `PickSaveAsync` marcadas `// loc-ok`.
- AXAML: tudo em `{DynamicResource}`, inclusive `Window.Title` das duas janelas e os `ComboBoxItem` (Formato, `Sem criptografia`, Fora/Dentro/Centro). Combos seguem lidos por `SelectedIndex` (sem mudança de lógica).
- D5:
  - `LiveText`: rodapé `InfoText` do editor (`Details()` lê o estado atual: `nova imagem`, `no disco: …`, `prévia com N cores`, `não salva`, e o `Describe()` com `bits (paleta)`/`sem criptografia`); `StatusText` das duas janelas (todo texto passa por um `SetStatus(Func<string>)`; mensagens de exceção ficam capturadas como estão); `ArtText` da composição; legenda dos tiles (`Trocado · W × H · não salvo` e `W × H · N bits · Ver121/sem criptografia`); tooltip do tile trocado (vira um `TextBlock` com `LiveText`).
  - `Loc` na emissão: banners de sucesso/erro do editor, títulos/filtros de picker, mensagens de exceção de `Processing/Splash`.
- `SplashComposeWindow.SetArt(art, string? name)`: `null` = arte vinda do editor sem arquivo; o rótulo exibido usa `SplashCompose.EditorImageLabel` (ao vivo). O nome de arquivo sugerido no export continua `imagem do editor_brush<seed>.png` nos dois idiomas (nome de saída fica fora do catálogo, spec "Fora de escopo").

### Nenhum texto do catálogo chega a arquivo gerado

- `grep -rn "Loc\.\|LiveText" src/Processing/` fora de `throw new`: 0 linhas. Em `Processing/Splash`, `Loc.` só aparece como argumento de exceção.
- Caminho de gravação: `SplashFile.Save` → `SplashConverter.Convert` (pixels/formato/cor-chave/dither) → `SplashBmp.Encode` → `SplashEnvelope.Seal(bitmap, encryption, Path.GetFileName(path))` → `File.WriteAllBytes`; export: `SplashFile.ExportPng` (Skia). Nenhum recebe string da UI além do caminho escolhido no picker. Na biblioteca, `Save` recebe formato/criptografia do próprio arquivo. Os nomes sugeridos (`sp_256_01.bmp`, `sp_32b_01.bmp`, `splash.png`, `…_brush<seed>.png`) são literais fixos.

### Verificação executada

- `dotnet build L2Toolkit.sln -tl:off -v:q -nologo` (worktree): 0 erros, "Compilação com êxito".
- L2LOC nos arquivos do escopo (dedupe pelo comando do guia), antes → depois:
  - L2LOC007: SplashScreen.axaml 29 → 0, SplashLibraryWindow.axaml 9 → 0, SplashComposeWindow.axaml 38 → 0.
  - L2LOC008: SplashScreen.xaml.cs 11 → 0, SplashLibraryWindow.axaml.cs 12 → 0, SplashComposeWindow.axaml.cs 7 → 0, SplashBmp 13 → 0, SplashFile 4 → 0, RgbaImage 3 → 0, SplashEnvelope 1 → 0.
  - L2LOC009: nenhuma chave nova sem uso (total 9 → 8: `Common.Save` passou a ser usado).
  - Projeto inteiro: L2LOC007 463 → 386, L2LOC008 224 → 172.
- Busca manual (`grep -nP '"[^"]*[A-Za-zÀ-ú]{3,}'` nos `.cs` do escopo): sobram só nomes de arquivo, classes CSS (`current`), `Lineage2Ver*`/`Ver*`, `Brush {seed}` (glossário), `px`/`KB` e números.
- Console descartável em `C:/Workspace/Toolkit-wt/_scratch/06/check` (fora do repo), referenciando `build/Debug/L2 Toolkit.dll`; o idioma é trocado publicando `LocCatalog.En`/`PtBr` em `AppLanguage` por reflexão (o mesmo estado que `Apply` publica, sem o slot do Avalonia):
  - `SplashFile.Save` em 3 formatos × 3 criptografias × dither on/off + `ExportPng`, mesma imagem 320×200 com alpha, em pt-BR e em en: `outputs compared: 19, identical: 19, different: 0` (SHA-256).
  - BMP truncado (cópia cortada pela metade): pt `O BMP está truncado: faltam linhas de pixels.` / en `The BMP is truncated: pixel rows are missing.`; envelope Ver121 cortado: `O arquivo não é um bitmap (BMP) válido.` / `The file is not a valid bitmap (BMP).`; `Import` de `.txt`: `Formato de imagem não suportado…` / `Unsupported image format…`.

### Deferido: GUI

- Troca de idioma ao vivo com BMP aberto + biblioteca com troca pendente + composição aberta (três superfícies, legenda `Swapped · … · unsaved`, tooltip do tile trocado, `Window.Title` das duas janelas, rodapé do editor, status da composição).
- Banner de erro do BMP truncado aberto pela UI no idioma ativo.
- Conferir que os `ComboBoxItem` com `DynamicResource` mantêm o item selecionado ao trocar o idioma.
