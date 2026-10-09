# 12 — Documentação

Status: ready-for-agent
Type: task
Blocked by: 11

Spec: `../spec.md`, seções [Convenção de chaves], [Fluxo para uma feature nova] e [Adicionar um terceiro idioma].

## Mudanças

- `docs/agents/l2toolkit-engineering.md`:
  - Nova seção `## Localization`, em inglês como o resto do arquivo e curta: arquivos, convenção de chaves, API (`Loc`, `LocKey`, `AppLanguage.Bind`, `LiveText`), política D5, plural, formato de data, `NonTranslatable.txt` e `// loc-ok`, códigos L2LOC, checklist para feature nova e passos para um terceiro idioma.
  - Linha `Localization/` na tabela de pastas.
  - As referências a rótulos de UI em português (`Configurações → Aparência`, `Verificar atualizações`, `Gerar vários…` etc.) continuam, porque pt-BR é o idioma padrão. Conferir que cada uma bate com o valor em `Strings.resx`.
  - Em "Avalonia conventions", uma linha: nunca texto literal em View ou code-behind; usar o catálogo.
- `AGENTS.md`: na linha de L2Toolkit engineering, incluir "textos de interface" entre os gatilhos de leitura do guia.
- `GLOSSARY.md`, novos termos com `_Avoid_`:
  - **Idioma da interface** (`pt-BR` padrão | `en`, `app_language`);
  - **Catálogo de strings** (`Strings*.resx`);
  - **Chave de string** (`Área.Elemento`).
- `README.md`: em Estrutura, `src/Localization/` e uma frase sobre o idioma (pt-BR padrão, inglês opcional, troca em Configurações).
- `docs/index.html`: mencionar a opção de idioma na seção de configurações, em pt-BR como o resto da página.

## Aceite

- Um agente que só leia `AGENTS.md` e o guia de engenharia consegue adicionar uma string nova nos dois idiomas sem consultar esta spec.
- Nenhum rótulo citado no guia diverge do texto en real da UI.
