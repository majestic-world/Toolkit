# 11 — Endurecer guardas do build

Status: ready-for-agent
Type: task
Blocked by: 04, 05, 06, 07, 08, 09, 10

Spec: `../spec.md`, seção [Diagnósticos do build].

## Objetivo

Com a migração completa, texto hardcoded novo passa a quebrar o build. Isso protege as features futuras.

## Mudanças

- L2LOC007 e L2LOC008 passam de aviso a erro em `Localization.targets`.
- Resolver todos os L2LOC009 (chaves sem uso): remover a chave ou corrigir a referência.
- Revisar `NonTranslatable.txt`: cada linha precisa de motivo claro (nome de produto, termo de domínio, formato, número, endônimo). Agrupar com comentários `#`.
- Revisar os `// loc-ok`: cada um precisa ser texto não exibido ao usuário.

## Aceite

- `dotnet build` e `make build` sem nenhum aviso ou erro L2LOC.
- Prova descartável: adicionar `Text="Teste"` numa View e `StatusText.Text = "Teste";` num code-behind quebra o build com L2LOC007 e L2LOC008, apontando arquivo e linha.

## Comments

### Implementação (branch `i18n/11-guards`)

**Guardas.** `Localization.targets`: L2LOC007 (atributo e texto de elemento) e L2LOC008 reportam como erro; L2LOC009 continua aviso (spec). Lacuna do ticket 09: `PlaceholderText` (ComboBox) entrou na lista de atributos, em `.PlaceholderText` como property element e no gatilho C# de atribuição, igual a `Watermark`. Os dois `PlaceholderText` existentes (LiveData, SkinBuilder) já usam `DynamicResource`, sem achados. Spec (tabela de diagnósticos) atualizada.

**L2LOC009.** `Common.Done` removida dos dois catálogos: nenhum uso real (os "Pronto" de status já têm chaves próprias por área).

**NonTranslatable.txt.** Regra no cabeçalho: só nome de produto/ferramenta, termo de domínio do glossário, formato/versão, número/tamanho, endônimo e valor dos dados do jogo; rótulo, título, aba e cabeçalho de coluna vão para o catálogo mesmo com texto igual. Cada grupo tem o motivo em `#`.

Viraram chaves (pt-BR = literal atual, D11):

| Literal | Chave | en |
| --- | --- | --- |
| `Logs` (aba) | `CreateMultisell.LogsTabButton`, `PrimeShop.LogsTabButton`, `SkinBuilder.LogsTabButton` | Logs |
| `Skins` / `Status` (abas) | `SkinBuilder.SkinsTabButton` / `SkinBuilder.StatusTabButton` | Skins / Stats (a aba mostra atributos; o log en já usa "stats") |
| `Preset` (rótulo) | `LiveData.PresetLabel`, `SkinBuilder.PresetLabel` | Preset |
| `Set Item GRP` (aba) | `LiveData.SetItemGrpTabButton` | Set Item GRP (as abas vizinhas `GrpTabButton`/`XmlTabButton` já eram chaves) |
| `MIN COLOR`, `RADIANCE`, `RING`, `PART.` (cabeçalhos) | `EnchantEffect.MinColorTitle`, `RadianceTitle`, `RingTitle`, `ParticleTitle` | iguais (`MaxColorTitle`/`MinEnchantTitle` já eram chaves) |
| `Icon`, `Icon Panel` (rótulos) | `SearchIcon.IconLabel`, `SearchIcon.IconPanelLabel` | iguais (`NameLabel` já era chave) |
| `Build`, `L2DAT BUILD` | `Settings.BuildButton`, `Settings.BuildTitle` | iguais (os outros títulos de card são chaves) |
| `BRUSH` (seção) | `SplashCompose.BrushSectionTitle` | BRUSH (as outras seções já eram chaves) |
| `PRESETS`, `Start L2`, `ID`, `HEX` | `SystemMsgColor.PresetsTitle`, `StartL2Button`, `IdColumnTitle`, `HexColumnTitle` | iguais |

Ficaram (com motivo): `L2 Toolkit…` (produto); `Live Data`, `Prime Shop Generator`, `Skin Builder`, `Splash Screen`, `Upgrade Normal System` (nome da ferramenta, glossário); `Armor`/`Items`/`Skills`/`Weapons`/`Weapon` (tipos do domínio, D9); `Normal (00000000)`/`Augmented (00000040)` (tipo lido do .dat); categorias do Prime Shop (dado, D9); animações do PawnData (nome interno do jogo); `ProductName_Classic-eu.dat`, `prime_shop.xml` (arquivo); `512 px`…`5000 px` (tamanho); `PNG`, formatos do Geodata, `Lineage2Ver111/121` (formato/envelope); `English`, `Português (Brasil)` (endônimos). Novo: `v{version}` (Main, formato de versão).

**`// loc-ok`.** Ficaram só os que não chegam à tela: extensões padrão `"bmp"`/`"png"` no `PickSaveAsync` (SplashScreen) e chave de tema + geometria no `PathIcon` do botão de excluir preset (SystemMsgColor). Removidos:

- `MainWindow` versão do app (`"v" + …`, visível): agora `$"v{version}"` com `v{version}` em NonTranslatable (formato de versão; exemplo já previsto no guia).
- `MainWindow` `UpdateVersions` (`"{atual}  →  {tag}"`): o marcador não tinha efeito (o texto visível não tem letras); só removido.
- `AppSettingsControl` arquivo em compilação (`fileName + ".txt"`): `Path.GetFileName(inputPath)`, sem literal.
- `SystemMsgColor` cor padrão `"799BB0"` (valor visível na caixa): `const DefaultPresetHex`, usada também nos dois fallbacks que repetiam o literal.

Guia (`_notes/localization-howto.md`) atualizado: critérios do NonTranslatable, `loc-ok` só para texto fora da tela, 007/008 como erro.

### Verificação

- Red/green do `PlaceholderText`: `PlaceholderText="Teste"` no LiveData passava sem diagnóstico; após a mudança, `LiveData.axaml(232,47): warning L2LOC007`.
- Prova descartável (revertida), com 007/008 já como erro: `Text="Teste"` em `BrushVariantsWindow.axaml`, `StatusText.Text = "Teste";` em `BrushVariantsWindow.axaml.cs` e `PlaceholderText="Teste"` em `LiveData.axaml` → build falha com `BrushVariantsWindow.axaml(89,40): error L2LOC007`, `BrushVariantsWindow.axaml.cs(58,27): error L2LOC008`, `LiveData.axaml(232,47): error L2LOC007`.
- `dotnet build L2Toolkit.sln`: 0 avisos, 0 erros (nenhum L2LOC).
- `make build` (Native AOT, win-x64): sucesso, 0 L2LOC. Avisos não-L2LOC: 22 `IL2026` (trim, `ReflectionBinding` em AXAML de SplashScreen, BrushGeneratorPage e SplashComposeWindow), em linhas que este ticket não toca; fora do escopo.
