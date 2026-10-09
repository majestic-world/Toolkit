# 05 — Página Settings

Status: ready-for-agent
Type: task
Blocked by: 01, 02

Spec: `../spec.md`, decisões D3–D8 e D11, [Glossário de tradução].

## Escopo

- `Views/AppSettingsControl.axaml`:
  - títulos de card e seção (`APLICATIVO`, `L2DAT BUILD`…), `Tema`, `Escuro (padrão)`/`Claro`;
  - qualidade `Minimum`…`Maximum`, hoje só em inglês: pt-BR novo `Mínima`/`Média`/`Alta`/`Máxima`;
  - rótulos, watermarks, botões e chips de ferramentas, que usam `Nav.*` do ticket 04.
- `Views/AppSettingsControl.xaml.cs`:
  - títulos de picker (`Selecionar pasta de origem (.txt)`, `Selecionar pasta de saída (.l2dat)`);
  - status do build (`Selecione uma pasta de origem válida.`, `Nenhum arquivo .txt encontrado…`, `Compilando...`, `Build concluído: …`, `{ok} ok, {failed} erro(s) — …`, `Erro: {message}`);
  - status de "Verificar atualizações";
  - a exceção `Round-trip falhou para {fileName}`.

## Mudanças

- Chaves em `Settings.*`.
- `BuildStatusText` e `UpdateStatusText` mostram estado e passam a usar `LiveText.Set`.
- `arquivo(s)` e `erro(s)` viram plurais `.One`/`.Other`.
- O combo de tema mantém a lógica por `SelectedIndex`.

## Aceite

- Zero L2LOC007/008 nos dois arquivos.
- Com um build L2DAT concluído e o status visível, trocar o idioma atualiza o status para o idioma novo, com os mesmos números.
- "Verificar atualizações" mostra o resultado no idioma ativo.
- A página não corta texto nos dois idiomas, inclusive nos chips.
