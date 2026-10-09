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
