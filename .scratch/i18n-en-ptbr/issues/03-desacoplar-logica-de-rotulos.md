# 03 — Desacoplar lógica dos rótulos exibidos

Status: resolved
Type: task
Blocked by: —

Spec: `../spec.md`, decisão D9.

## Objetivo

Nenhuma decisão de código lê o texto exibido num controle. Assim, traduzir ou renomear um rótulo não muda o comportamento. O texto visível não muda neste ticket.

## Pontos conhecidos

| Arquivo | Linhas | Hoje | Depois |
| --- | --- | --- | --- |
| `Views/LiveData.xaml.cs` | 912-944, 984-1008, 921 | `ComboBoxItem.Content.ToString()` (`Skills`/`Weapons`/`Armor`/`Items`) decide o processamento, a visibilidade dos campos e a categoria do `Presets.dat` | `Tag` com o mesmo id; o código lê `Tag` |
| `Views/LiveData.axaml` | 216-… | `<ComboBoxItem>Skills</ComboBoxItem>` | `<ComboBoxItem Tag="Skills">Skills</ComboBoxItem>` |
| `Views/SkinBuilder.xaml.cs` / `.axaml` | 77-80, 571-597 / 211-… | Mesmo padrão (`Weapons`/`Armor`) | `Tag` |
| `Views/SearchIcon.xaml.cs` / `.axaml` | 133-142 / 201-204 | `switch` sobre `Content` (`Weapon`/`Armor`/`Items`/`Skills`) | `Tag` |
| `Views/PrimeShopGenerator.xaml.cs` | 124-136 | Categoria via `Content.Split('-')[0]`; tipo como chave de `FileNames` | Itens como `record Option(string Id, string Label)` (ou `ComboBoxItem` com `Tag`); o código lê o `Id` |
| `Views/EnchantEffect.xaml.cs` | 470-500 | `TypeLabel` monta `Normal (00000000)`, e a seleção extrai o hex de dentro dos parênteses | Itens com id = hex e rótulo separado; sem parse de texto |

As demais telas usam `SelectedIndex` (Geodata, Splash, Brush e as qualidades em Settings) e ficam fora deste ticket.

## Mudanças

- Aplicar a coluna "Depois". Os ids continuam exatamente iguais aos valores de hoje, porque `Presets.dat`, `FileNames` e os `switch` dependem deles.
- Os rótulos desses tipos continuam literais e iguais nos dois idiomas (D9). Quando o ticket 01 estiver pronto, entram em `NonTranslatable.txt`.
- Procurar outros casos com `grep` de `Content?.ToString()`, `SelectedItem as string` e `.Text ==` em `src/Views`. Registrar os achados em `## Comments`.

## Aceite

- Cada fluxo afetado roda igual a antes, com o mesmo arquivo de entrada e saída idêntica: LiveData com os quatro tipos, SkinBuilder (Weapons, Armor), SearchIcon (quatro tipos), Prime Shop (uma categoria, os três tipos) e Enchant Effect (Normal e Augmented).
- Prova descartável: trocar temporariamente o texto de um `ComboBoxItem` (por exemplo, `Armor` → `Armadura`) não altera o comportamento nem os presets carregados.

## Fora de escopo

Mover os textos para os catálogos (09 e 08).

## Comments

### Feito (branch `i18n/03-tags`)

- `LiveData`, `SkinBuilder`, `SearchIcon`: cada `ComboBoxItem` de tipo ganhou `Tag` igual ao texto atual; o code-behind lê `(X.SelectedItem as ComboBoxItem)?.Tag as string`. O fallback `?? SelectedItem as string` saiu (os itens são sempre `ComboBoxItem` declarados no XAML).
- Novo `Models/Option.cs`: `sealed record Option(string Id, string Label)` com `ToString() => Label` (o ComboBox sem template exibe `ToString()`, então o texto visível é o mesmo de antes).
- `PrimeShopGenerator`: categorias viram `Option("11", "11 - Equipment")`…; tipos viram `Option(chave, chave)` a partir de `FileNames.Keys`. `CheckFiles` e `ValidateInputs` leem `Option.Id`; sem `Split('-')`. Mesma ordem de enumeração dos `FrozenDictionary` de antes.
- `EnchantEffect`: `TypeCombo` recebe `Option(hex, TypeLabel(hex))`; a seleção lê `Id`; o parse de parênteses saiu. `TypeLabel` continua montando o rótulo.

### Equivalência (estática, GUI não aberta)

Programa descartável em `C:/Workspace/Toolkit-wt/_scratch/03` (fora do repo; compila o `Models/Option.cs` real e lê os `.axaml` e o `Presets.dat` do worktree). Saída de `dotnet run` com o estado commitado: `ALL EQUIVALENT`.

| Tela | Valor antigo (texto) → id novo | Usado em |
| --- | --- | --- |
| LiveData | `Skills`→`Skills`, `Weapons`→`Weapons`, `Armor`→`Armor`, `Items`→`Items` | `switch` de processamento, `type == "Armor"`, visibilidade dos campos; `Presets.dat` tem `Weapons` (7) e `Armor` (8), `Skills`/`Items` sem presets (painel oculto, como antes) |
| SkinBuilder | `Weapons`→`Weapons`, `Armor`→`Armor` | `switch` de processamento, `UpdatePresets` |
| SearchIcon | `Weapon`→`Weapon`, `Armor`→`Armor`, `Items`→`Items`, `Skills`→`Skills` | `switch` de busca |
| PrimeShop categoria | `"11 - Equipment".Split('-')[0]`=`11`→`11`; idem `12`, `13`, `14`, `15` | `Category` da saída; rótulo exibido idêntico |
| PrimeShop tipo | `Items`→`Items`, `Armor`→`Armor`, `Weapon`→`Weapon` | chave de `FileNames` / ícones; rótulo idêntico |
| EnchantEffect | `Normal (00000000)`→`00000000`, `Augmented (00000040)`→`00000040`, hex desconhecido (ex.: `00000080`) → o próprio hex | `_entries` por `Type`; rótulo idêntico. `Type` vem do `.dat` como hex de 8 dígitos, sem parênteses, então o parse antigo sempre devolvia o hex |

Prova descartável do Aceite: com o commit aplicado, troquei `Armor` → `Armadura` no conteúdo dos itens de LiveData, SkinBuilder e SearchIcon. `dotnet build L2Toolkit.sln`: 0 avisos, 0 erros. Grep de `Content?.ToString`, `SelectedItem as string`, `Split('-')` e `LastIndexOf('(')` nas cinco telas: nenhuma leitura de rótulo (único acerto: `GradeCombo.SelectedItem as string`, ver abaixo). O programa de prova mostrou que o texto antigo seria `Armadura`, que não é `case` nem categoria do `Presets.dat`. O id novo continua `Armor`, que é `case` e tem presets. Depois revertido com `git checkout -- src/Views`.

### Verificação executada

- `dotnet build L2Toolkit.sln` (worktree): `Compilação com êxito`, 0 avisos, 0 erros.
- `dotnet run` em `_scratch/03`: `ALL EQUIVALENT` (estado commitado). Com o rótulo trocado, 3 diferenças esperadas (texto `Armadura` ≠ id `Armor`).

### Outros achados (grep em `src`)

- `EnchantEffect.GradeCombo.SelectedItem as string`: o grau é um valor de dado do `.dat` exibido como está. Pela D9, ele não é traduzido. Fica assim.
- `hex.Text != raw` em `EnchantEffect` e `SystemMsgColor`: comparam a entrada do usuário (hex), não um rótulo. Fora de escopo.
- `editBtn.Content = MakePencilIcon()` e `MainContent.Content = page` só atribuem; não leem.
- Sem `switch` sobre `Text`/`Content`/`Header`/`SelectedItem`, sem `SelectedValue` e sem comparação de `Text` com literal em `src`.

### Pendente: GUI

Rodar cada fluxo com a mesma entrada e conferir a saída: LiveData (4 tipos, presets de Weapons/Armor, campos de Armor visíveis), SkinBuilder (Weapons, Armor), SearchIcon (4 tipos), Prime Shop (uma categoria, os 3 tipos; rótulos `11 - Equipment` e outros exibidos como antes) e Enchant Effect (Normal e Augmented; rótulos `Normal (00000000)` exibidos como antes).
