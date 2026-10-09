# 02 — Seletor de idioma e persistência

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `../spec.md`, decisões D5, D6 e D10.

## Objetivo

O usuário escolhe Português (Brasil) ou English em Configurações → Aparência. A troca vale na hora e fica salva. Sem escolha salva, o app abre em português.

## Mudanças

1. `Localization/AppLanguage.cs`:
   - `Saved` lê `app_language`: `en` vira `En`; `pt-BR`, ausente ou qualquer outro valor vira `PtBr`. Não grava nada na leitura.
   - `ApplySaved()`.
   - `Set(UiLanguage)`, que aplica e grava `app_language`.
2. `App.axaml.cs`: trocar o `Apply(UiLanguage.PtBr)` do ticket 01 por `AppLanguage.ApplySaved()`.
3. `Views/AppSettingsControl.axaml`: no card de aparência, logo abaixo da linha de tema, uma linha no mesmo layout (`Grid ColumnDefinitions="Auto,12,*"`):
   - `TextBlock Text="{DynamicResource Settings.LanguageLabel}"`;
   - `ComboBox x:Name="LanguageComboBox"` com dois `ComboBoxItem`, nesta ordem: `Content="Português (Brasil)" Tag="pt-BR"` e `Content="English" Tag="en"`. Os endônimos vão para `NonTranslatable.txt`.
4. `Views/AppSettingsControl.xaml.cs`: seleção inicial a partir de `AppLanguage.Current`. Em `SelectionChanged`, chamar `AppLanguage.Set` se o idioma mudou, no mesmo padrão de `ThemeComboBox_OnSelectionChanged`.
5. Catálogos: `Settings.LanguageLabel` (pt-BR `Idioma`, en `Language`).

## Aceite

- Perfil novo (renomear `%APPDATA%/L2Toolkit`) e perfil antigo sem `app_language`: o app abre em pt-BR e não grava `app_language`.
- Trocar para English muda na hora o rótulo `Idioma`/`Language` e o menu de contexto do `TextBox`, e grava `app_language=en`. Voltar para Português grava `app_language=pt-BR`.
- Uma página visitada antes da troca (em cache no `MainWindow`) mostra o idioma novo ao voltar. Uma janela secundária já aberta (`SplashLibraryWindow`) também muda, nas chaves que já estiverem migradas.
- Reiniciar mantém a escolha. Um valor inválido escrito à mão em `app_language` abre em pt-BR.

## Fora de escopo

O restante do texto da página Configurações (05).

## Comments

**Feito (Impl02Selector, branch `i18n/02-selector`)**

- `AppLanguage`: `Saved` (lê `app_language` via `Database.GetValue`, que é `_data.GetValueOrDefault` puro, sem gravar), `Parse` interno (`"en"` → `En`; qualquer outra coisa, incl. ausente, `EN`, ` en` → `PtBr`), `ApplySaved()`, `Set()` (aplica + `UpdateValue("app_language", "pt-BR"|"en")`).
- `App.axaml.cs`: `AppLanguage.ApplySaved()` no lugar de `Apply(UiLanguage.PtBr)`.
- `AppSettingsControl`: linha "Idioma" abaixo do tema (`LanguageComboBox`, itens `pt-BR`/`en` por `Tag`); seleção inicial por `AppLanguage.Current`; handler compara com `Current` antes de `Set` (mesmo padrão do tema, sem gravar na seleção inicial). Demais literais da página intocados (ticket 05).
- `Settings.LanguageLabel` (Idioma/Language); endônimos `English` e `Português (Brasil)` em `NonTranslatable.txt` (# ── Settings ──).
- Nenhum `Loc` chega a arquivo de saída: o único valor gravado é a constante `pt-BR`/`en` no settings.

**Verificação executada**

- `dotnet build L2Toolkit.sln`: 0 erros. L2LOC antes/depois: AppSettingsControl.axaml 43/43 L2LOC007, .xaml.cs 11/11 L2LOC008, L2LOC009 9/9 (a nova chave é usada).
- Check descartável fora do repo (`_scratch/02/parse`, console net10 com o `Parse` copiado do fonte e dicionário imitando `Database`): ausente→PtBr (dicionário continua com 0 entradas após a leitura), `pt-BR`→PtBr, `en`→En, `EN`/`xx`/` en`→PtBr.

**Deferred: GUI** — todos os itens de Aceite (perfil novo/antigo sem gravação, troca ao vivo do rótulo e menu do TextBox, gravação `en`/`pt-BR`, página em cache e `SplashLibraryWindow`, reinício, valor inválido).
