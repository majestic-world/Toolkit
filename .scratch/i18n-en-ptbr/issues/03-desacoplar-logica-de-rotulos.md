# 03 — Desacoplar lógica dos rótulos exibidos

Status: ready-for-agent
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
