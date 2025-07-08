# L2Toolkit

**Ferramentas profissionais para desenvolvedores de servidores Lineage 2**

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue.svg)](https://www.microsoft.com/windows)
[![WPF](https://img.shields.io/badge/WPF-Modern%20UI-blue.svg)](https://docs.microsoft.com/dotnet/desktop/wpf/)

## 📖 Documentação

**[Documentação Completa](https://majestic-world.github.io/Toolkit/)**

Acesse a documentação completa para guias detalhados, tutoriais e exemplos de uso.

## 🚀 Início Rápido

### Requisitos
- .NET 8.0
- Windows 10/11
- Visual Studio ou Rider (para desenvolvimento)

### Instalação

1. **Clone o repositório:**
   ```bash
   git clone https://github.com/majestic-world/Toolkit.git
   cd Toolkit
   ```

2. **Prepare os arquivos de assets:**
   
   Certifique-se de que os seguintes arquivos estão na pasta `assets/`:
   - `ItemName_Classic-eu.txt`
   - `EtcItemgrp_Classic.txt`
   - `Armorgrp_Classic.txt`
   - `Weapongrp_Classic.txt`
   - `Skillgrp_Classic.txt`
   - `h5_names.txt`

3. **Execute o projeto:**
   ```bash
   dotnet run
   ```

## 🛠️ Funcionalidades

- **Gerar Portas** - Converte dados de portas do UnrealEd para XML
- **Animações (Pawn Data)** - Gerador de dados de animação para NPCs
- **Gerenciador de Spawn** - Cria arquivos de spawn automatizados
- **Prime Shop** - Gerador de itens para Prime Shop
- **Live Data (166)** - Processa arquivos .dat Live para Classic
- **Enchant Skill (GF)** - Cria dados de encantamento para Giran Forge
- **Upgrade Equipment** - Gerencia dados de upgrade de equipamentos
- **Utilitários** - Pesquisa de ícones, análise de logs e correção de descrições

## 📋 Estrutura do Projeto

```
L2Toolkit/
├── pages/           # Interface do usuário
├── database/        # Classes de banco de dados
├── DataMap/         # Modelos de dados
├── Utilities/       # Utilitários e parsers
├── images/          # Recursos de imagem
└── Properties/      # Configurações
```

## 🤝 Contribuindo

Contribuições são bem-vindas! Consulte a [documentação de desenvolvimento](https://majestic-world.github.io/Toolkit/#desenvolvimento) para mais informações.

## 📄 Licença

Este projeto é open source e foi desenvolvido para a comunidade Lineage 2.

---

**Desenvolvido por Majestic World Studio**