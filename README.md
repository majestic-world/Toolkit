# L2 Toolkit

Conjunto de ferramentas para gerenciamento do cliente do jogo **Lineage 2 (166p)**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.2-8B44AC?style=flat-square)
![Native AOT](https://img.shields.io/badge/Native_AOT-enabled-2ea44f?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-0078D6?style=flat-square)

---

## Build

### Requisitos

- .NET 10 SDK
- PowerShell 7 (`pwsh`)
- GNU Make
- Inno Setup 6, só para `make dist`
- `create-dmg` (`brew install create-dmg`), só para o `.dmg` do macOS

### Comandos

- `make run`: roda o app em desenvolvimento (`dotnet run --project src`)
- `make build`: publica para Windows (`win-x64`)
- `make macos`: publica para macOS (`osx-arm64` ou `osx-x64`, conforme o Mac) e monta o `L2 Toolkit.app` assinado ad-hoc
- `make linux`: publica para Linux (`linux-x64` ou `linux-arm64`, conforme a máquina)
- `make dist`: publica para Windows e gera o instalador Inno Setup

O Native AOT não compila entre sistemas operacionais: gere cada plataforma no próprio sistema. Para o instalador `.dmg` do macOS, rode `pwsh scripts/build.ps1 -Platform macos -Installer`; para outra arquitetura do mesmo sistema, acrescente `-Architecture x64` ou `-Architecture arm64`.

### Saídas

Tudo fica em `build/`, sem pastas `bin/` ou `obj/`:

- `build/Debug/`: binários de desenvolvimento
- `build/Release/<rid>/publish/`: executável Native AOT
- `build/Release/win-x64/publish/Setup/L2 Toolkit Installer.exe`: instalador do Windows
- `build/logs/<rid>/`: logs das ferramentas de build
- `build/obj/`: arquivos intermediários

### Versão

A versão vem de `APP_VERSION` no `.env`. Ela vira a versão do executável e do badge na titlebar, e o `make dist` a repassa ao instalador.

O app avisa sobre versões novas pelas Releases do GitHub e, se o usuário confirmar, baixa e instala: publique cada versão com a tag igual ao `APP_VERSION` (ex.: `3.9`), o instalador gerado pelo `make dist` anexado e as novidades na descrição da release (aparecem na modal).

### Estrutura

- `src/`: código do app; cada pasta é um namespace `L2Toolkit.*`
- `src/Localization/`: catálogos de textos da interface (`Strings.resx` em pt-BR, `Strings.en.resx` em inglês), seletor de idioma e o gerador/validador do build
- `packaging/`: insumos dos instaladores (Inno Setup e `Info.plist` do macOS)
- `scripts/`: `build.ps1`, usado pelo `Makefile`
- `docs/`: documentação

A interface está em português (Brasil) por padrão; o inglês é opcional e se escolhe em Configurações → APLICATIVO → Idioma, com troca na hora, sem reiniciar.

---

Desenvolvido por **Mk**