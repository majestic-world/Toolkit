# 01 — Infraestrutura de localização

Status: ready-for-agent
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
