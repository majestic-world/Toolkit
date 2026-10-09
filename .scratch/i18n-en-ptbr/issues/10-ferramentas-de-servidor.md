# 10 — Ferramentas de servidor (XML, geodata, logs)

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `../spec.md`, decisões D3–D8 e D11, [Glossário de tradução].

## Escopo

- `Views/DoorGenerateControl.axaml/.xaml.cs`.
- `Views/GeodataConverterControl.axaml/.xaml.cs`:
  - `Selecione um formato de saída`, `PROGRESSO`;
  - logs (`Iniciando conversão para {format}...`, `Entrada: {dir}`, `Concluído: {converted} convertido(s), {copied} copiado(s), {failed} erro(s)`, `Conversão cancelada pelo usuário.`, `Erro fatal: {message}`).
- `Processing/Geodata/GeodataProcessor.cs`: mensagens de progresso e erro que chegam ao log da página. `GeodataParsers`/`GeodataWriters` já estão em inglês; manter, ou passar ao catálogo se aparecerem na UI.
- `Views/PawnDataControl.axaml`: rótulos. Os nomes de animação (`1 — Walk`…`33 — Special Attack 06`) são não traduzíveis.
- `Views/SpawnManager.axaml/.xaml.cs`, `Views/Missions.axaml/.xaml.cs`, `Views/UpgradeNormalSystem.axaml/.xaml.cs`:
  - rótulos;
  - picker `Selecione o arquivo` e filtros `Arquivos XML`/`Todos os arquivos`;
  - erros (`Preencha o caminho do arquivo`, `O campo category não esta presente`…);
  - `Copiado!`.
- `Views/DescriptionFix.axaml/.xaml.cs`: hoje mistura inglês (`Select file to modify descriptions`, `Save modified file`, `Text files`) com português. Os dois idiomas ficam completos.
- `Views/LogParse.axaml/.xaml.cs`: rótulos, filtro `Log files`, erros e logs (`Iniciando o processo...`, `Logs encontrados: {count}`, `Pronto, o arquivo {fileName} foi criado com sucesso`).

## Mudanças

- Chaves em `DoorGenerate.*`, `Geodata.*`, `PawnData.*`, `SpawnManager.*`, `Missions.*`, `UpgradeNormal.*`, `DescriptionFix.*` e `LogParse.*`. Filtros genéricos vão para `Common.*`.
- Os plurais de Geodata viram `.One`/`.Other`.
- Valores de XML de entrada (`DAILY`, `WEEKLY`…) e fragmentos de XML de saída não passam pelo catálogo.

## Aceite

- Zero L2LOC007/008 nos arquivos do escopo.
- Cada ferramenta processa uma entrada real em en com saída idêntica à de pt-BR. Para a conversão de geodata, basta uma região pequena.
- Um cancelamento de geodata e um erro de validação (campo vazio) aparecem no idioma ativo.
