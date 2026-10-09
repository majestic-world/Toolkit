# 04 — Shell: `MainWindow` e modal de update

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `../spec.md`, decisões D3–D7 e D11, [Convenção de chaves], [Glossário de tradução] e [Fluxo para uma feature nova].

## Escopo

- `Views/MainWindow.axaml`: tooltip `Configurações`, seções `FERRAMENTAS` e `UTILITÁRIOS`, os 17 nomes de ferramenta da sidebar e o modal de update (títulos, rótulos, aviso, botões).
- `Views/MainWindow.xaml.cs`: textos do `PromptUpdate` e do download (`Atualizando para a versão {tag}`, `Conectando…`, `{done} de {total} MB`, `Abrindo o instalador…`, `Falha ao atualizar: {message}`, `Sem notas para esta versão.`, `Cancelar`/`Fechar`) e a data em `Format.DateTime` (linha 90).
- `Utilities/AppUpdater.cs`: mensagens de exceção que chegam ao usuário (`Download incompleto…`, `O instalador baixado não confere…`, `Tag da release fora do formato…`, `A release {tag} não tem instalador (.exe).`).

## Mudanças

- Nomes de ferramenta em `Nav.*`, que o ticket 05 reutiliza nos chips de Settings. Shell em `Main.*`. Modal e updater em `Update.*`.
- O modal cobre a janela inteira, então o idioma não pode mudar com ele aberto. Textos do modal usam `Loc.Update.*` direto, sem `LiveText`.
- `L2 Toolkit` e `L2 Toolkit (Fafurion) By Mk` continuam literais (`NonTranslatable.txt`).
- pt-BR: os valores são os literais atuais. en: segue o glossário.

## Aceite

- Zero L2LOC007/008 nos três arquivos.
- `make run`: com a sidebar visível, trocar o idioma muda todos os nomes, as seções e o tooltip.
- O modal de update aparece nos dois idiomas, com a data no formato de cada um. Para provocar o modal sem release nova, usar um teste descartável que chame `PromptUpdate` com um `AppRelease` fictício; não commitar.
- Nenhum nome de ferramenta corta na largura atual da sidebar em en nem em pt-BR.

## Comments

- Feito: `Main.SettingsTip/ToolsSection/UtilitiesSection`; 17 chaves `Nav.*` (BrushGenerator, CreateMultisell, DescriptionFix, DoorGenerate, EnchantEffect, GeodataConverter, LiveData, LogParse, Missions, PawnData, PrimeShop, SearchIcon, SkinBuilder, SpawnManager, SplashScreen, SystemMsgColor, UpgradeNormalSystem), inclusive as que são iguais nos dois idiomas, para o ticket 05 reutilizar; 23 chaves `Update.*` (modal, progresso e exceções do `AppUpdater`). Sidebar e modal via `{DynamicResource}`; textos do modal em C# via `Loc.Update.*` direto (sem `LiveText`); data via `Loc.Format.DateTime`; `Cancelar`/`Fechar` via `Common.Cancel/Close`. `v{versão}` e `{atual} → {tag}` marcados `// loc-ok`.
- Nenhum `Loc` chega a arquivo de saída: as exceções do `AppUpdater` só viram `ex.Message` em `UpdateProgressText`; o nome do instalador (`L2 Toolkit Installer {tag}.exe`), a pasta temp e o User-Agent seguem literais.
- Verificação: `dotnet build L2Toolkit.sln` 0 erros. L2LOC007 MainWindow.axaml 33→0; L2LOC008 MainWindow.xaml.cs 11→0, AppUpdater.cs 4→0. Totais 463/224/9 → 430/209/6 (`Format.DateTime`, `Common.Cancel`, `Common.Close` deixaram de ser L2LOC009; nenhum L2LOC009 novo).
- Data: catálogo gerado tem `dd/MM/yyyy HH:mm` (pt-BR) e `yyyy-MM-dd HH:mm` (en); teste descartável fora do repo formatou 2026-10-09 14:05 como `09/10/2026 14:05` e `2026-10-09 14:05`.
- Largura (estimativa estática, Segoe UI Semibold 14 px): espaço do texto ≈ 240 − 1 borda − 12 margem − 28 padding − 28 ícone = 171 px. Mais longos: `Upgrade Normal System` 153 px (ambos), en `Animations (PawnData)` 148, pt `Animações (PawnData)` 145. Cabe.
- Adiado (GUI): troca de idioma ao vivo na sidebar/tooltip; modal nos dois idiomas com `PromptUpdate` fictício; confirmação visual de que nenhum nome corta.
