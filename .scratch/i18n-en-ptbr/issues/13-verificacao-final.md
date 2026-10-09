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

## Comments

### Passe automatizado (agente, 2026-10-09, `feat/i18n-en-ptbr` @ 81911d1)

Feito:

- Roteiro 1: `make build` (Native AOT win-x64) passou sem nenhum L2LOC. Os únicos avisos são 22 IL2026 de `ReflectionBinding` (`{Binding #Slider.Value}` em SplashScreen/BrushGeneratorPage/SplashComposeWindow). Eles já existiam em `main`. O executável publicado abre.
- Roteiro 2, parte do perfil novo: com `settings.properties` vazio, o executável AOT abre em pt-BR e não grava nada.
- Roteiro 3, parcial: build Debug no tema Dark, com as 17 páginas e Configurações em en. Não apareceu chave crua nem corte no estado vazio. As páginas em pt-BR batem com o texto anterior.
- Troca ao vivo (Debug e AOT): sidebar, páginas em cache e Configurações mudam na hora, nos dois sentidos, e os ComboBox acompanham (correção `ComboBoxLiveSelection`). Grava `app_language=en`/`pt-BR`, e reiniciar mantém a escolha. Trocar o idioma não grava `app_theme`.
- A revisão de código (padrões + spec) corrigiu termos do glossário em en (títulos das páginas, `Generate many…`), `…` e papéis de chave.

Pendente (humano):

- Roteiro 2: perfil antigo, com outras chaves e sem `app_language`.
- Roteiro 3: janelas secundárias (`SplashLibraryWindow`, `SplashComposeWindow`, `BrushVariantsWindow`) e páginas com dados carregados, nos dois idiomas.
- Roteiro 4: troca ao vivo com SystemMsg.dat carregado, biblioteca com troca pendente, logs no SkinBuilder e status em Configurações.
- Roteiro 5: tema Light.
- Roteiro 6: menu de contexto do `TextBox`. Os popups não aparecem na captura por janela.
- Roteiro 7: leitura humana de `Strings.en.resx`.
- Saída idêntica em en e pt-BR por ferramenta, com dados reais do client e do servidor. Os tickets 06–10 só provaram isso estaticamente, e no 06 por hash. Não há dados de jogo nesta máquina.
