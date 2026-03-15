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

Build:

**Windows**
```bash
dotnet publish -r win-x64 -c Release -p:PublishAot=true -p:OptimizationPreference=Speed -p:StackTraceSupport=false -p:InvariantGlobalization=true
```

**Linux**
```bash
dotnet publish -r linux-x64 -c Release -p:PublishAot=true -p:OptimizationPreference=Speed -p:StackTraceSupport=false -p:InvariantGlobalization=true
```

**macOS (Intel)**
```bash
dotnet publish -r osx-x64 -c Release -p:PublishAot=true -p:OptimizationPreference=Speed -p:StackTraceSupport=false -p:InvariantGlobalization=true
```

**macOS (Apple Silicon)**
```bash
dotnet publish -r osx-arm64 -c Release -p:PublishAot=true -p:OptimizationPreference=Speed -p:StackTraceSupport=false -p:InvariantGlobalization=true
```

---

## Deploy

**Windows** — compila e gera o instalador via Inno Setup:
```powershell
.\Deploy.ps1
```

**macOS (Apple Silicon)** — compila, monta o `.app` bundle e gera o instalador `.dmg`:
```bash
chmod +x Deploy-macOS.sh
./Deploy-macOS.sh
```
Requer `create-dmg` instalado (`brew install create-dmg`). Os artefatos são gerados em `bin/Release/net10.0/osx-arm64/publish/`.

Desenvolvido por **Mk**