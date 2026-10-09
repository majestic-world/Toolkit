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

## Comments

### Implementação (branch `i18n/10-server`)

- Chaves em `DescriptionFix.*` (20), `DoorGenerate.*` (9), `Geodata.*` (44, plurais `Converted`/`CopiedFile`/`Failed`/`FoundLog`/`ProgressStatus` em `.One`/`.Other`), `LogParse.*` (24), `Missions.*` (11), `PawnData.*` (8), `SpawnManager.*` (13), `UpgradeNormal.*` (7). Valores pt-BR = literal atual (D11); DescriptionFix ganhou pt-BR novo para o que era inglês (picker, filtro, salvar).
- `Common.*` novos no fim do bloco (genéricos, combinado com Impl09Items, que adiciona `CopiedStatus` e `UnexpectedError` com o mesmo nome/valor): `CopiedStatus` (Copiado!), `SelectFilePicker` (Selecione o arquivo), `UnexpectedError` (Ocorreu um erro inesperado.), `XmlFilesFilter` (Arquivos XML). Reusados: `Common.Browse`, `Common.Copy`, `Common.Cancel`, `Common.AllFilesFilter`.
- Resumo do Geodata: `Geodata.DoneLog` = `Concluído: {converted}, {copied}, {failed}` com cada parte vinda de um plural (`1 convertido`/`2 convertidos`…). Progresso: `Geodata.ProgressStatus(total, current)` via `LiveText` (texto de estado); botões que mudam de estado (`ConvertButtonText`, `ButtonGenerateText` do LogParse) via `LiveText.Set`.
- `GeodataProcessor` (logs `[SKIP]/[COPY]/[READ]/[CONV]/[OK]/[ERRO]`, mensagens de `FileResult.Error`) e as `ArgumentException` de `GeodataParsers`/`GeodataWriters` passam pelo catálogo (chegam ao log via `ex.Message`). Prefixos `[ERRO]` viram `[ERROR]` em en.
- `NonTranslatable.txt`: 20 nomes de animação do PawnData, `L2J (.l2j)`, `CONV_DAT (_conv.dat)`, `L2G (.l2g)`, `Upgrade Normal System` (título, glossário).
- DescriptionFix: filtros `Text files`/`TXT File` unificados em `DescriptionFix.TextFilesFilter`; mensagem de sucesso virou plural `DescriptionFix.ReplacedStatus`.

### Nenhum texto do catálogo chega a arquivo de saída

- DoorGenerate: o XML é montado só com grupos de regex do input (`ConvertToXml`); `Loc` só nas duas mensagens de erro que substituem o XML na caixa de saída (UI).
- PawnData: `PawnDataControl.xaml.cs` não usa `Loc` (saída só com nome e entradas).
- SpawnManager: `ProcessNpcLines` usa só input e IDs; `Loc` só em `SendNotify`.
- Missions / UpgradeNormal: saída a partir dos templates `const` (`onedayreward_begin…`, `upgradesystem_begin…`) e dos atributos do XML; `DAILY`/`WEEKLY`/`MONTHLY`/`SINGLE` intocados; `Loc` só em exceções de validação, título/filtros do picker e fallback da notificação.
- DescriptionFix: linhas gravadas = input + `H5Names.Descriptions`; `SuggestedFileName` (`_modified`) inalterado; `Loc` só em picker/filtro/notificações.
- LogParse: arquivo = linhas do log filtradas; nome `Log-{key}[-{evento}]-{dd-MM}.log` inalterado; `Loc` só em logs/notificações/botão.
- Geodata: escrita via `GeodataWriter.Create(region, format)` e nome via `GeoConstants.GetOutputFileName`; `Loc` só no callback `log`, em `FileResult.Error` (não lido pela UI) e em exceções.
- Verificado com `grep -n "Loc\." ` nos 9 arquivos `.cs`: 56 ocorrências, todas nas posições acima.

### Verificação executada

- `dotnet build L2Toolkit.sln -tl:off -v:q -nologo` (após `touch src/Localization/Strings.resx`, ver achado abaixo): 0 erros.
- L2LOC007/008 nos arquivos do escopo: antes 168 (170 linhas filtradas, menos 2 do `MainWindow.axaml` que são do ticket 04); depois 0.
- L2LOC009: nenhuma chave nova sem uso; restam só as pré-existentes `Format.DateTime`, `Common.Close`, `Common.Done`, `Common.Error`, `Common.Save` (`Common.Cancel` e `Common.AllFilesFilter` passaram a ser usadas).
- Busca manual de texto fora do alcance dos scanners (`grep -nP '"[^"]*[A-Za-zÀ-ú]{3,}'` nos `.cs`, atributos/elementos nos `.axaml`): restam só nomes de setting, padrões de regex, templates de saída e nomes de elemento XML.

### Adiado: GUI

- Cada ferramenta com entrada real em en vs pt-BR, comparando a saída (Geodata: uma região pequena).
- Cancelamento de Geodata e erro de validação (campo vazio) no idioma ativo.
- Troca ao vivo: `ProgressStatus`, botão Iniciar/Cancelar do Geodata e Gerar/Processando do LogParse.

### Achados

- A validação L2LOC é incremental pelo `.resx`: editar só `.cs`/`.axaml` e rebuildar mostra os avisos antigos. Para recontar, `touch src/Localization/Strings.resx` antes do build.
- Ferramentas de edição com caminho relativo resolvem a partir de `C:/Workspace/Toolkit` (checkout da integração), não da worktree; usar caminhos absolutos da worktree.
