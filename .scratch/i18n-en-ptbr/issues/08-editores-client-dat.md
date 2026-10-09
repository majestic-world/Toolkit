# 08 — Editores de client `.dat`

Status: resolved
Type: task
Blocked by: 01, 03

Spec: `../spec.md`, decisões D3–D8 e D11, [Glossário de tradução].

## Escopo

- `Views/SystemMsgColor.axaml/.xaml.cs`:
  - rótulos, watermark `Nome do preset...`;
  - picker `Selecionar SystemMsg.dat`, filtros `Lineage 2 DAT`/`All`;
  - MessageBox `Erro`/`Erro ao salvar` e `l2.exe não encontrado em:\n{path}`;
  - status (`Preset "{name}" salvo.`…);
  - rótulos de linhas e cards criados em código.
- `Views/EnchantEffect.axaml/.xaml.cs`:
  - rótulos (`Aplicar a todos os graus`, `MIN COLOR`…);
  - pickers `Selecionar WeaponEnchantEffectData.dat` / `FullArmorEnchantEffectData.dat`, filtros;
  - badges e textos de cards montados em código.

## Mudanças

- Chaves em `SystemMsgColor.*`, `EnchantEffect.*` e `Common.*` (Error, filtros).
- Rótulos fixos em controles criados em código usam `AppLanguage.Bind(LocKey…)`, para acompanhar a troca sem reconstruir as listas.
- Os rótulos de tipo `Normal (00000000)`/`Augmented (00000040)` continuam literais (D9), já desacoplados no ticket 03.
- Mensagens técnicas de `ClientDat/` (inglês) continuam sem tradução.

## Aceite

- Zero L2LOC007/008 nos arquivos do escopo.
- Com um `SystemMsg.dat` carregado e linhas selecionadas, trocar o idioma atualiza rótulos e cards sem perder seleção nem edições.
- Salvar o `.dat` em en gera arquivo byte a byte igual ao salvo em pt-BR com as mesmas edições.
- Enchant Effect carrega, edita e salva nos dois idiomas.

## Comments

### Feito

- `Strings.resx`/`Strings.en.resx`: 27 chaves `EnchantEffect.*` e 35 `SystemMsgColor.*` (com três plurais em `SystemMsgColor` e um em `EnchantEffect`), cada bloco logo abaixo da âncora da área, em ordem ordinal. pt-BR copia o literal atual (D11), exceto onde D8 troca `(s)`/`(ns)`/`(es)` por `.One`/`.Other` (`SearchResultsStatus`, `UniqueColorsStatus`, `ColorAppliedStatus`, `ColorsAppliedStatus`). Nenhuma chave `Common.*` nova: os filtros `All` passam a usar `Common.AllFilesFilter` (pt-BR agora mostra "Todos os arquivos", que é a correção de idioma misto citada na spec) e os títulos "Erro" usam `Common.Error`. O filtro `.dat` ficou por área (`SystemMsgColor.DatFilter` = "Lineage 2 DAT", `EnchantEffect.DatFilter` = "DAT"; en com "… files").
- `NonTranslatable.txt`: EnchantEffect `Augmented (00000040)`, `Normal (00000000)` (D9), `RADIANCE`, `RING`, `PART.`, `MIN COLOR` (nomes de campo do .dat); SystemMsgColor `PRESETS`, `Start L2`, `ID`, `HEX`.
- AXAML: todos os rótulos fixos viram `{DynamicResource …}`.
- C#, política D5:
  - Rótulos fixos em controles de código: `SaveLabel` alterna entre `AppLanguage.Bind(LocKey.SystemMsgColor.SavingStatus)` e `…SaveFileButton` (spinner de salvar); tooltip dos cards do modal "Cores no Arquivo" via `card[!ToolTip.TipProperty] = AppLanguage.Bind(LocKey.SystemMsgColor.UseForPresetTip)`. As linhas de mensagens e de graus não têm texto fixo (só `#id`, `+N`, hex e o texto da mensagem, que é dado), então nenhuma lista é reconstruída na troca.
  - Estado na tela com `LiveText.Set`: `StatsLabel` (contagem/resultados), `OverflowLabel`, `ClearSelectionLabel` ("Remover seleção (n)"), `ModalSubtitle` (cores únicas) e o tooltip dos presets (`#{hex} — clique para aplicar`, um `TextBlock` como conteúdo do ToolTip, porque `ToolTip.Tip` é `object`). `CloseFile_Click` chama `LiveText.Clear(StatsLabel)` antes de zerar o texto.
  - Transitório com `Loc` na emissão: MessageBox (`Common.Error`, `SystemMsgColor.SaveError`, `L2ExeNotFoundError`), toasts (`SavedStatus`, `PresetSavedStatus`, `ColorAppliedStatus`, `EnchantEffect.ColorsAppliedStatus`) e o banner de erro do EnchantEffect (`FileNotFoundError`, `SaveError(message)`). `ex.Message` vindo de `ClientDat/` continua sem tradução.
  - `// loc-ok` em `"799BB0"` (hex padrão) e no `PathIcon` do botão de excluir preset (`ThemeTextHint` e geometria).

### Byte a byte: nenhum `Loc` chega ao .dat

Prova estática (grep):
- `src/ClientDat/` não referencia `Localization`, `Loc.`, `LocKey`, `AppLanguage` nem `LiveText` (0 ocorrências).
- Todos os usos de `Loc.`/`LocKey.`/`LiveText.` em `SystemMsgColor.xaml.cs` e `EnchantEffect.xaml.cs` vão para `FilePickerOpenOptions.Title`, `FilePickerFileType.Name`, `MessageBoxManager`, `ShowSuccessToast`/`ShowErrorBanner` (só `TextBlock.Text`), `TextBlock.Text` de rótulos de estado e `ToolTip.Tip`. Nenhum é lido de volta.
- Entradas da serialização: SystemMsgColor `SaveFile_Click` monta `Source.Color` a partir de `ColorRgb`/`ColorAlpha` (vindos do .dat, do `TextBox` de hex, do picker HSV ou do preset em `msg_color_presets.properties`) e chama `L2DatFile.SerializeSystemMsg` sobre os `DatSystemMsg` originais; `MessageText` é só exibição. EnchantEffect `SyncWeaponUiToDat`/`SyncArmorUiToDat` usam só `Primary`/`Secondary`/`MinColorRgb`/`MaxColorRgb` (hex) e os registros parseados; `TypeCombo` lê `Option.Id` (hex, ticket 03) e `GradeCombo` usa a string do .dat. `TypeLabel` é literal fixo, não passa pelo catálogo.
- Arquivos auxiliares também ficam fora: `.bak` é `File.Copy`, as chaves de settings (`systemmsg_last_path`, `weaponenchant_last_path`, `fullarmorenchant_last_path`) e o arquivo de presets (`nome=RRGGBBAA`) não mudaram.

Logo, com as mesmas edições o .dat salvo é idêntico nos dois idiomas. [INFERENCE] por análise estática; o teste de salvar nos dois idiomas é GUI e ficou para a passada consolidada.

### Verificação executada

- `dotnet build L2Toolkit.sln -tl:off -v:q -nologo` no worktree: 0 erros.
- L2LOC (linhas únicas) antes → depois:
  - `SystemMsgColor.axaml` L2LOC007 21 → 0; `SystemMsgColor.xaml.cs` L2LOC008 18 → 0.
  - `EnchantEffect.axaml` L2LOC007 27 → 0; `EnchantEffect.xaml.cs` L2LOC008 6 → 0.
  - L2LOC009: 9 → 7 (`Common.Error` e `Common.AllFilesFilter` passaram a ser usados; nenhuma chave nova sem uso).
- Caça manual (`"[^"]*[A-Za-zÀ-ú]{3,}` nos dois `.xaml.cs` e atributos de texto nos dois `.axaml`): encontrou e migrou o que o scanner não vê (toasts e banners que passam por `ShowSuccessToast`/`ShowErrorBanner`, `ToolTip.SetTip`, o toast de `ApplyAll`). Sobram só chaves de tema, fontes, geometria, hex e nomes de arquivo/settings. `Views/Controls/HsvColorPicker.cs` (usado pelas duas telas) não tem texto de UI.

### Deferred: GUI

- Com `SystemMsg.dat` carregado, linhas selecionadas e busca ativa, trocar o idioma: rótulos, `StatsLabel`, "Remover seleção (n)", overflow, tooltips de preset/cards e subtítulo do modal mudam sem perder seleção nem edições.
- Salvar o `SystemMsg.dat` em en e em pt-BR com as mesmas edições e comparar os arquivos (byte a byte).
- Enchant Effect: carregar, editar e salvar Weapon e FullArmor nos dois idiomas; trocar o idioma com o arquivo aberto.
- Layout em en: larguras dos botões ("Existing colors…", "Clear selection (n)") e cabeçalhos ("MIN ENCHANT" na coluna de 60 px do armor).

### Achados

- Toasts e o banner de erro do EnchantEffect são transitórios: se o idioma mudar durante os 2,8 s/5 s em que ficam visíveis, continuam no idioma da emissão (D5).
