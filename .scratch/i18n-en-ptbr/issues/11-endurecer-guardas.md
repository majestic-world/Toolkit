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
