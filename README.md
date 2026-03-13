# L2 Toolkit

Conjunto de ferramentas para gerenciamento do cliente do jogo **Lineage 2 (166p)**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.2-8B44AC?style=flat-square)
![Native AOT](https://img.shields.io/badge/Native_AOT-enabled-2ea44f?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=flat-square&logo=windows)

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

```csharp
dotnet publish -r win-x64 -c Release -p:PublishAot=true -p:OptimizationPreference=Size -p:StackTraceSupport=false
```

Desenvolvido por **Mk**
