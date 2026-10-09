# 01 — Infraestrutura de localização

Status: resolved
Type: task
Blocked by: —

Spec: `../spec.md`, decisões D1–D8, seções [Arquitetura], [Diagnósticos do build] e [Convenção de chaves].

## Objetivo

Entregar os catálogos, o gerador e validador MSBuild, o runtime (`AppLanguage`, `LiveText`) e a aplicação do idioma no startup. Nenhuma tela é migrada aqui. O app continua mostrando o texto atual e abre aplicando `pt-BR`, o idioma padrão (o seletor e a persistência ficam no ticket 02).

## Mudanças

1. `src/Localization/Strings.resx` (pt-BR, base) e `src/Localization/Strings.en.resx`, com o cabeçalho ResX mínimo (`resheader` resmimetype/version) e as chaves semente:
   - `Common.Cancel`, `Common.Close`, `Common.Copy`, `Common.Save`, `Common.Browse`, `Common.Error`, `Common.Done`, `Common.AllFilesFilter`.
   - `Format.DateTime`: pt-BR `dd/MM/yyyy HH:mm`, en `yyyy-MM-dd HH:mm`.
   - Overrides do Fluent: `StringTextFlyoutCutText`, `StringTextFlyoutCopyText`, `StringTextFlyoutPasteText` (pt-BR Recortar/Copiar/Colar, en Cut/Copy/Paste).
2. `src/Localization/NonTranslatable.txt`: um literal por linha, UTF-8, `#` para comentário. Começa com `L2 Toolkit` e `L2 Toolkit (Fafurion) By Mk`.
3. `src/Localization/Localization.targets`, com tasks `RoslynCodeTaskFactory` usando só `System.Xml.Linq` (o spike mostrou que `System.Text.Json` não funciona em task inline):
   - `GenerateLocalization`: Inputs `@(L2LocCatalog)`, Output `$(IntermediateOutputPath)Localization.g.cs`, `BeforeTargets="CoreCompile"`. Adiciona o arquivo a `Compile` e a `FileWrites`. Gera `Loc`, `LocKey` e `LocCatalog` no formato da spec:
     - Placeholders nomeados viram posicionais.
     - Plural `.One`/`.Other` vira método `(int count, …)`.
     - Chaves `String*` vão só para o dicionário.
   - `ValidateLocalization`: roda em todo build e emite L2LOC001–009 com arquivo, linha e coluna (`Log.LogError`/`LogWarning` com subcategoria e código, como no spike). Varre `**/*.axaml` e `**/*.cs` do projeto, exceto `$(BaseIntermediateOutputPath)`. L2LOC007/008/009 são avisos nesta fase.
4. `src/L2Toolkit.csproj`:
   - `<EmbeddedResource Remove="Localization\Strings*.resx" />`.
   - `<L2LocCatalog Include="Localization\Strings.resx" Language="pt-BR" />` e `<L2LocCatalog Include="Localization\Strings.en.resx" Language="en" />`. O gerador trata o primeiro item como catálogo base: a ordem dos parâmetros e o L2LOC001 tomam o pt-BR como referência.
   - `<Import Project="Localization\Localization.targets" />`.
5. `src/Localization/AppLanguage.cs`: `UiLanguage { PtBr, En }`, `Current`, `Changed`, `Apply(UiLanguage)`, `Bind(string key)`, e `Text`, `Format` e `Plural` como `internal`.
   - Apply monta um `ResourceDictionary` (chaves sem placeholder + `String*`), troca um slot fixo de `Application.Current.Resources.MergedDictionaries` (adicionado no primeiro apply), publica o `string[]` com `Volatile.Write` e dispara `Changed`.
   - `Format` e `Plural` usam `CultureInfo.InvariantCulture`.
6. `src/Localization/LiveText.cs`: `Set(TextBlock, Func<string>)`, `Set(AvaloniaObject, AvaloniaProperty<string?>, Func<string>)` e `Clear(AvaloniaObject)`. Usa `ConditionalWeakTable` e assina `AppLanguage.Changed` uma única vez, com prune de referências mortas.
7. `src/App.axaml.cs`: `AppLanguage.Apply(UiLanguage.PtBr)` logo depois de `AppTheme.ApplySaved()`. O ticket 02 troca por `ApplySaved()`.
8. Apagar `src/Properties/Resources.resx` e `Resources.Designer.cs`, que estão vazios e sem uso.

## Aceite

- `dotnet build L2Toolkit.sln` passa sem erro. Os avisos L2LOC007/008 listam os literais atuais com arquivo e linha (ordem de grandeza: 463 em AXAML, mais de 171 em C#).
- Script descartável (não commitado) provoca cada erro e confere código, arquivo e linha:
  - remover uma chave do en → L2LOC001;
  - `{count}` vs `{total}` → L2LOC002;
  - chave `settings.theme` → L2LOC003;
  - valor vazio → L2LOC004;
  - `{DynamicResource Nao.Existe}` → L2LOC005;
  - `.One` sem `.Other` → L2LOC006.
- `Localization.g.cs` aparece em `build/obj/`, não é recompilado se os `.resx` não mudam, e `Loc.Common.Cancel` compila.
- Com `make run`, o menu de contexto de um `TextBox` mostra Recortar/Copiar/Colar, o que corrige o inglês que aparece hoje. Uma chamada temporária a `AppLanguage.Apply(UiLanguage.En)` (descartável) passa a mostrar Cut/Copy/Paste sem reiniciar.
- `make build` (Native AOT, Windows) termina sem avisos novos, além dos L2LOC, e o executável publicado abre.
- Registrar no `## Comments` se o IDE (Rider ou VS) enxerga uma chave nova em `Loc.` só depois do build ou já no design-time build.

## Fora de escopo

Seletor de idioma e persistência (02). Migração de telas (04–10).

## Comments

### Feito

- `src/Localization/Strings.resx` (pt-BR, base) e `Strings.en.resx`: cabeçalho `resheader` resmimetype/version, chaves semente `Common.*` (8), `Format.DateTime` e os três overrides `StringTextFlyout*`. Cada catálogo tem um comentário-âncora por área da [Convenção de chaves](../spec.md#convenção-de-chaves), em ordem alfabética; os tickets 04–10 inserem as chaves sob a âncora da própria área, o que mantém os merges paralelos sem conflito. `NonTranslatable.txt` segue o mesmo esquema de âncoras.
- `src/Localization/Localization.targets`: uma task inline (`RoslynCodeTaskFactory`, só `System.Xml.Linq`, C# 7.3) com `Mode=Generate|Validate`, chamada por dois targets:
  - `GenerateLocalization`: `BeforeTargets="CoreCompile"`, Inputs `@(L2LocCatalog)` mais o próprio `.targets` (mudança no gerador também regenera), Output `$(IntermediateOutputPath)Localization.g.cs` (`build/obj/Debug/`, `build/obj/Release/win-x64/`). Inclui o arquivo em `Compile` e `FileWrites`; quando o target é pulado, os itens valem por output inference. Gera `Loc` (propriedade, método com `object?` por placeholder na ordem do pt-BR, plural `(int count, …)`), `LocKey` (só chaves sem placeholder e sem plural) e `LocCatalog` (`Keys`, `ResourceIndexes`, um `string[]` por idioma). Valores sem placeholder ficam sem escape (`{{` → `{`); com placeholder, viram formato posicional remapeado por nome (o en pode mudar a ordem). `String*` vai só para o dicionário. Uma tradução inválida cai no valor base, então o gerador nunca produz C# que não compila.
  - `ValidateLocalization`: roda em todo build (pulado só quando `DesignTimeBuild=true`), varre `**/*.axaml` e `**/*.cs` (exclui `$(DefaultItemExcludes)`, `$(BaseIntermediateOutputPath)` e `$(BaseOutputPath)`) e emite L2LOC001–009 via `Log.LogError/LogWarning(subcategory: null, code, …, file, line, column, …)`. O formato da linha fica `arquivo(l,c): warning L2LOC00x: …`, o mesmo que `scripts/build.ps1` conta. Uma subcategoria não nula quebraria o filtro `': warning '` do script. `GenerateLocalization` depende de `ValidateLocalization`, então um erro de catálogo para o build antes da geração.
- `src/L2Toolkit.csproj`: `EmbeddedResource Remove`, os dois `L2LocCatalog` (pt-BR primeiro) e o `Import`.
- `src/Localization/AppLanguage.cs` e `LiveText.cs` conforme a spec (API de runtime, sem `Saved`/`ApplySaved`/`Set`, que ficam para o ticket 02). Nenhum `ResourceManager` nem reflexão: os textos são arrays estáticos gerados, seguros para Native AOT e trimming.
- `App.axaml.cs`: `AppLanguage.Apply(UiLanguage.PtBr)` logo depois de `AppTheme.ApplySaved()`.
- `src/Properties/Resources.resx` e `Resources.Designer.cs` apagados.

### Decisões de interpretação

- L2LOC008: o gatilho de atribuição cobre `Text|Title|Content|Watermark =` (e `+=`) tanto em `x.Prop =` quanto em inicializador (`new TextBlock { Text = "…" }`). A spec cita só `Title =` em inicializador. Sem a extensão ficariam de fora 11 rótulos fixos de controles criados em código (EnchantEffect, CreateMultisell, SystemMsgColor, BrushVariantsWindow).
- L2LOC008 ignora os literais de `Patterns`/`MimeTypes`/`AppleUniformTypeIdentifiers =` (`"*.png"`), que não são texto de UI. `// loc-ok` vale no fim da linha do literal ou da linha onde começa o gatilho.
- L2LOC007: texto direto de elemento é checado em controles de texto (`TextBlock`, `Run`, `ComboBoxItem`, `Button`, `CheckBox`, `x:String`…) e em property elements `*.Text/Content/Header/Watermark/Title/Tip`. Fora disso, `StreamGeometry` e `Color`, por exemplo, gerariam falso positivo.
- L2LOC005 checa `{DynamicResource X.Y}` (só chaves com ponto, então `Theme*`/`String*` ficam de fora). Uma chave existente com placeholder ou plural também é erro.
- L2LOC003 também cobre `X.One`/`X.Other` cujo `X` é prefixo de outra chave, e segmentos repetidos (`A.A`, `Loc.*`), porque nos dois casos o C# gerado não compilaria (CS0542/CS0102).
- L2LOC001 aponta a linha da chave no catálogo onde ela existe e diz em qual catálogo ela falta. Placeholders são comparados por nome; o formato (`:N0`) pode diferir entre idiomas.
- Diagnósticos apontam a coluna do nome do elemento `<data` (col 4 com a indentação de 2 espaços) e, em AXAML, a do atributo ou do texto.

### Verificação executada

- `dotnet build L2Toolkit.sln` (worktree limpo, sem chave em uso): exit 0, **L2LOC007 = 463** (22 arquivos AXAML), **L2LOC008 = 224** (29 arquivos C#), **L2LOC009 = 9** (`Common.*` sem uso e `Format.DateTime`; o build sem ticket 01 tinha 0 avisos). Contagem por linha única: o console do MSBuild repete cada aviso no resumo final.
- Script descartável `C:/Workspace/Toolkit-wt/_scratch/01/provoke.py` (fora do repo; altera, compila e restaura byte a byte). Todos os casos falham o build com o código esperado:
  - remover `Common.Copy` do en → `Strings.resx(37,4): error L2LOC001` (falta em Strings.en.resx);
  - `{count}` vs `{total}` → `Strings.en.resx(23,4): error L2LOC002`;
  - `{0}` → `Strings.resx(23,4)` e `Strings.en.resx(23,4): error L2LOC002` (placeholder posicional);
  - `settings.theme` → `Strings.resx(23,4)` e `Strings.en.resx(23,4): error L2LOC003`;
  - `Common.Cancel.Tip` ao lado de `Common.Cancel` → `Strings.resx(34,4): error L2LOC003` (folha também é prefixo);
  - valor vazio no en → `Strings.en.resx(35,4): error L2LOC004`;
  - `Tag="{DynamicResource Nao.Existe}"` → `DescriptionFix.axaml(70,50): error L2LOC005` (coluna da chave);
  - `Common.ProbeFiles.One` sem `.Other` → `Strings.resx(23,4): error L2LOC006`;
  - árvore restaurada → exit 0.
- `probe_case.py` (descartável): adiciona `Common.ProbeExported` (pt `{count} brushes exportados em {folder} ({{ok}}).`, en `Exported to {folder}: {count:N0} brushes ({{ok}}).`) e o plural `Common.ProbeFiles`, mais um `.cs` temporário que usa `Loc.Common.Cancel`, `Loc.Common.ProbeExported(1234, …)`, `Loc.Common.ProbeFiles(1|2, …)` e `LocKey.Common.Cancel`. O build compila. Um console por reflexão leu `pt-BR: 1234 brushes exportados em C:\x ({ok}).`, `1 arquivo em A`, `2 arquivos em B` e, com o array en, `Exported to C:\x: 1,234 brushes ({ok}).`, `1 file in A`, `2 files in B`. Remapeamento por nome, `{{`, plural e formato invariante conferem. O L2LOC009 de `Common.Cancel` some quando a chave é usada. `{DynamicResource Common.ProbeExported}` em AXAML → `L2LOC005 … tem placeholders`.
- Incremental: no segundo build, `Ignorando o destino "GenerateLocalization"…` e `Ignorando o destino "CoreCompile"…`, e o mtime de `build/obj/Debug/Localization.g.cs` não muda. Os avisos de validação aparecem de novo, porque a validação roda em todo build.
- `make build` (Native AOT, win-x64): `L2 Toolkit.exe 35.7 MB, 696 warnings`, todos L2LOC (463 + 224 + 9). Nenhum aviso de outro código (IL/AOT/CS). O executável existe em `build/Release/win-x64/publish/L2 Toolkit.exe`.

### IDE / design-time build (não verificado em IDE)

Não havia Rider nem VS para testar. Simulação por CLI (`dotnet msbuild src/L2Toolkit.csproj -t:ResolveReferences;Compile -p:DesignTimeBuild=true -p:SkipCompilerExecution=true -p:ProvideCommandLineArgs=true`, com o `.g.cs` apagado antes): `GenerateLocalization` rodou, recriou o arquivo, e a linha do `csc` passou a incluir `Localization.g.cs`. `ValidateLocalization` foi pulado e não houve avisos L2LOC. VS (`CompileDesignTime`) e Rider chamam `CoreCompile` com `SkipCompilerExecution` no design-time build, e o hook `BeforeTargets="CoreCompile"` entra nesse caminho. [INFERENCE] Uma chave nova aparece em `Loc.` no próximo design-time build. Editar o `.resx` provavelmente não dispara um design-time build sozinho (o IDE observa o projeto, não o conteúdo dos itens `L2LocCatalog`), então na prática a chave aparece depois de um build ou de um reload do projeto. Isso precisa ser confirmado num IDE.

### Deferred: GUI

- `make run`: o menu de contexto de um `TextBox` mostra Recortar/Copiar/Colar.
- Chamada temporária a `AppLanguage.Apply(UiLanguage.En)` → Cut/Copy/Paste sem reiniciar.
- O executável publicado (`build/Release/win-x64/publish/L2 Toolkit.exe`) abre.
