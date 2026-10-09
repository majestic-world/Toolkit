# 09 — Ferramentas de dados de item

Status: ready-for-agent
Type: task
Blocked by: 01, 03

Spec: `../spec.md`, decisões D3–D9 e D11, [Glossário de tradução].

## Escopo

- `Views/LiveData.axaml/.xaml.cs`:
  - rótulos e botões (`Copiar`…);
  - erros de validação (`Preencha todos os campos`, `Informe o próximo ID do Item Set…`);
  - logs.
- `Views/SkinBuilder.axaml/.xaml.cs`:
  - rótulos;
  - logs (`Recuperando status do equipamento...`, `Cache de nomes criados, {count} nomes`, `Processando weapons...`…);
  - erros.
- `Views/CreateMultisell.axaml/.xaml.cs`: rótulos, logs (`Nomes de itens carregados, total de {count}`), chips criados em código.
- `Views/PrimeShopGenerator.axaml/.xaml.cs`: rótulos, notificações (`Atenção: Arquivo '{file}' não encontrado!…`), `ID {id} sem nome`.
- `Views/SearchIcon.axaml/.xaml.cs`: rótulos (`PESQUISA`…), status (`CARREGANDO...`), `Arquivo não encontrado: {file}`.
- `Utilities/TableManager.cs` (`Tabela não encontrada: {name}`) e `Utilities/Parser.cs` (mensagem exibida).

## Mudanças

- Chaves em `LiveData.*`, `SkinBuilder.*`, `CreateMultisell.*`, `PrimeShop.*`, `SearchIcon.*` e `Tables.*`.
- Os nomes de tipo (`Skills`, `Weapons`, `Armor`, `Items`, `Weapon`) e as categorias do Prime Shop (dados) continuam literais (D9) e entram em `NonTranslatable.txt`.
- Logs usam `Loc` no momento da emissão (D5).
- Nenhum texto que vai para XML ou arquivo de saída passa pelo catálogo.

## Aceite

- Zero L2LOC007/008 nos arquivos do escopo.
- Cada ferramenta processa uma entrada real em en com saída idêntica à de pt-BR:
  - LiveData: Skills, Weapons, Armor, Items;
  - SkinBuilder: Weapons, Armor;
  - CreateMultisell;
  - Prime Shop;
  - SearchIcon.
- Trocar o idioma com logs na tela: os logs antigos ficam, os rótulos mudam e os próximos logs saem no idioma novo.

## Comments

### Feito (branch `i18n/09-items`)

- AXAML das 5 telas em `{DynamicResource …}`: títulos, subtítulos, rótulos, watermarks, tooltips, abas, botões. Também o `PlaceholderText="Selecione um preset..."` (LiveData/SkinBuilder), que o scanner não vê.
- C#: logs do SkinBuilder/CreateMultisell, notificações (`ShowNotification`/`SendNotify`), exceções exibidas (`throw new Exception(...)` → banner) usam `Loc` na emissão (D5). O fallback `"Ocorreu um erro inesperado."` virou `Common.UnexpectedError`.
- SearchIcon: o título de estado `StatusBox` (`RESULTADO` / `CARREGANDO...`) usa `LiveText.Set`. Assim acompanha a troca de idioma.
- `Utilities/TableManager.cs` → `Tables.NotFoundError(name)`. `Utilities/Parser.cs` (`Dados de parse inválidos!`, que hoje só chega ao log do CreateMultisell via `Parser.ParseId`) → `Tables.InvalidIdsError`. Usei a área `Tables` para as mensagens de `Utilities/` do ticket. Os `const InvalidData` de LiveData/SkinBuilder viraram chaves da própria área (`LiveData.InvalidIdsError`, `SkinBuilder.InvalidIdsError`), sem reaproveitar chaves de outra área.
- Chaves: `CreateMultisell.*` 21, `LiveData.*` 25, `PrimeShop.*` 29, `SearchIcon.*` 16, `SkinBuilder.*` 39, `Tables.*` 2. Novas `Common.CopiedStatus` ("Copiado!") e `Common.UnexpectedError` foram adicionadas no fim do bloco Common. Combinei com o ticket 10, que usa os mesmos nomes e valores e acrescenta também `Common.SelectFilePicker` e `Common.XmlFilesFilter`. Quem fizer o merge depois só precisa manter as duas inserções, sem duplicar. Também reusei `Common.Copy`.
- `NonTranslatable.txt` (D9 e glossário):
  - tipos `Skills`, `Weapons`, `Armor`, `Items`, `Weapon`;
  - categorias do Prime Shop `Equipment`, `Agathions`, `VIP`, `Consumables`, `Reward Coin`;
  - nomes de produto `Live Data`, `Skin Builder`, `Prime Shop Generator`;
  - `Preset`, `Set Item GRP`, `Skins`, `Status`, `Logs`;
  - campos de dado `Icon`, `Icon Panel`;
  - nomes de arquivo `ProductName_Classic-eu.dat`, `prime_shop.xml`.
- `Tag`/`Option.Id` do ticket 03 ficaram intactos. Os `Label` do Prime Shop (`"{id} - {categoria}"`, tipos = chaves de `FileNames`) são dados e continuam literais.

### Saída nunca passa pelo catálogo (prova por ferramenta)

Feito com grep em `Loc.|LiveText` nos 5 `.xaml.cs`. Todos os usos são `ShowNotification`/`SendNotify`/`AddLog`/`throw`/`LiveText.Set(StatusBox)`. Nenhum aparece nos builders abaixo.

- **LiveData**: as saídas são as linhas `.dat` copiadas das tabelas (`ProcessSkill`, `ProcessWeapons`, `ProcessArmors`, `ProcessItems` → `ClientTextBox`/`NameData`/`SetItemGrpData`), o `XElement` (`weapon`/`armor`/`etcitem` com `set`/`for`/`equip`) → `XmlData`, e o remapeamento de `SetItemGrp`. Só usam literais de dado e valores das tabelas. Os `Loc` ficam nas exceções de validação (`FieldsRequiredError`, `SetItemNextIdRequiredError`, `InvalidIdsError`). Elas abortam antes de qualquer saída e vão para o banner.
- **SkinBuilder**: `ConvertCrystal`/`ResetShotCounts`/`ConvertClassName` (`additionalname=[Skin]`, `icon_panel=[None]`), o `XElement` de armas e armaduras (`add_name="Skin"`), `CreateSkinsIds` e `CreateStatus` (`item_begin…item_end`) só usam literais e dados. `Loc` aparece só em `_log.AddLog` (GlobalLogs grava só no TextBox, sem arquivo) e nas exceções de validação.
- **CreateMultisell**: o `XDocument` em `GenerateData_OnClick` (`config`, `XComment("Ingredients")`, `XComment(name)` com fallback `"Production"`, `ingredient`/`production`) continua com os literais originais. Os chips de ingrediente mostram `"{id}-{count}"`, que é dado. `Loc` aparece só em `ShowNotification`/`AddLog`. A exceção de `Parser.ParseId` é lançada antes do XML ser montado.
- **Prime Shop**: `GenerateOutputsAsync` (`product_name_begin…`, `<product …>`, `<!-- nome -->`) usa `ItemOutput.Nome`, que vem de `GetItemNameAsync`. O fallback `$"ID {objectId} sem nome"` vai para `outer_name`/`description`/`name=` do XML e fica num cache estático. Por isso continua literal **de propósito**: se fosse traduzido, a saída em en seria diferente da saída em pt-BR, o que viola o Aceite. Deixei um comentário no código. `Loc` aparece só em `SendNotify`.
- **SearchIcon**: não gera arquivo. `NameOutput`/`IconOutput`/`IconPanelOutput` recebem valores dos `.txt` de assets. O fallback `Não encontrado` (`SearchIcon.NotFoundStatus`) só aparece quando falta o modelo, e esse ramo é inalcançável porque vem depois de `ContainsKey`. Vai apenas para o TextBox e o clipboard, nunca para um arquivo.
- **TableManager/Parser**: só mensagens de exceção. O conteúdo da tabela não é tocado.

### Verificação executada

- Antes (base `feat/i18n-en-ptbr` após 01+03, `dotnet build L2Toolkit.sln -tl:off -v:q`):
  - L2LOC007: 119 (LiveData 30, SkinBuilder 27, CreateMultisell 19, PrimeShopGenerator 25, SearchIcon 18);
  - L2LOC008: 56 (LiveData 3, SkinBuilder 27, CreateMultisell 6, PrimeShopGenerator 11, SearchIcon 8, TableManager 1).
- Depois do commit e também depois de `git merge feat/i18n-en-ptbr` (02, 04, 05, 07 e 08 já integrados): **0 L2LOC007/008** nos arquivos do escopo e 0 erros. Não surgiu nenhum L2LOC009 novo: os restantes são só `Common.Done`/`Common.Save`, que vêm de antes. O build recompilou `L2 Toolkit.dll`.
- Busca manual de texto hardcoded (`Text=`/`Content=`/`Watermark=`/`Tip=`/`PlaceholderText=`/texto de elemento nos 5 AXAML e literais nos `.cs`): o que sobrou é dado de XML/.dat ou está em `NonTranslatable.txt`.

### Deferred: GUI

- Rodar cada ferramenta em pt-BR e em en com a mesma entrada e comparar as saídas:
  - LiveData: Skills, Weapons, Armor, Items;
  - SkinBuilder: Weapons, Armor;
  - CreateMultisell, Prime Shop, SearchIcon.
- Trocar o idioma com logs na tela (SkinBuilder/CreateMultisell): os logs antigos ficam, os rótulos mudam, os logs seguintes saem no idioma novo.
- SearchIcon: o título `RESULTADO`/`RESULT` acompanha a troca depois de uma pesquisa (LiveText).
- Conferir o layout com os textos en (abas, botões).
