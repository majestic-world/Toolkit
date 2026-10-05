# L2 Toolkit

Conjunto de ferramentas para gerenciamento do cliente do jogo **Lineage 2 (166p)**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.2-8B44AC?style=flat-square)
![Native AOT](https://img.shields.io/badge/Native_AOT-enabled-2ea44f?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-0078D6?style=flat-square)

---

## Documentação

Acesse a documentação completa em:
**https://majestic-world.github.io/Toolkit/**

---

## Stack

| Tecnologia   | Versão |
|--------------|--------|
| .NET         | 10.0   |
| Avalonia UI  | 11.2   |
| Native AOT   | —      |

---

## Build

Requer PowerShell 7 (`pwsh`), GNU Make e o .NET 10 SDK. Todos os alvos publicam com Native AOT em `bin/Release/net10.0/<rid>/publish/`; os logs das ferramentas ficam em `obj/BuildLogs/<rid>/`. O Native AOT não compila entre sistemas operacionais: cada plataforma precisa ser gerada no próprio sistema.

| Comando | Resultado |
|---|---|
| `make build` | Windows (`win-x64`) |
| `make macos` | macOS (`osx-arm64` ou `osx-x64`, conforme o Mac), com o bundle `L2 Toolkit.app` assinado ad-hoc |
| `make linux` | Linux (`linux-x64` ou `linux-arm64`, conforme a máquina) |
| `make dist` | Build Windows + instalador Inno Setup em `publish/Setup/L2 Toolkit Installer.exe` (requer Inno Setup 6) |

O instalador `.dmg` do macOS sai de `pwsh scripts/build.ps1 -Platform macos -Installer` (requer `brew install create-dmg`). Use `-Architecture x64|arm64` para outra arquitetura do mesmo sistema.

Desenvolvido por **Mk**