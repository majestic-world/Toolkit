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
