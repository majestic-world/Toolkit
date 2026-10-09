# 13 — Verificação final e revisão de tradução

Status: ready-for-human
Type: task
Blocked by: 11, 12

Spec: `../spec.md`, seção [Verificação].

## Objetivo

Validar o produto inteiro nos dois idiomas no executável Native AOT e revisar a qualidade do inglês.

## Roteiro

1. `make build`. Rodar `build/Release/win-x64/publish/L2 Toolkit.exe`.
2. Perfil novo (renomear `%APPDATA%/L2Toolkit`) e perfil antigo sem `app_language`: os dois abrem em pt-BR.
3. Em cada idioma, no tema Dark, passar por todas as páginas da sidebar e pelas três janelas secundárias. Conferir que não há texto no idioma errado, corte, sobreposição nem chave crua (`Area.Key`) visível.
4. Troca ao vivo: carregar um arquivo em SystemMsgColor, abrir a biblioteca de splash com troca pendente, deixar logs em SkinBuilder e um status em Settings; trocar o idioma; conferir que rótulos e estados mudam, que logs antigos ficam e que nada se perde.
5. Tema Light nos dois idiomas: smoke em três páginas.
6. Menu de contexto de `TextBox`: Recortar/Copiar/Colar em pt-BR e Cut/Copy/Paste em en.
7. Ler `Strings.en.resx` inteiro contra o glossário: termos consistentes, sentence case, `…` em ações com diálogo, sem tradução literal estranha.

## Aceite

Roteiro completo sem pendências. Problemas encontrados viram issues novas em `.scratch/i18n-en-ptbr/issues/` ou comentários no ticket da área.
