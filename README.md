# L2Toolkit

**Ferramentas profissionais para desenvolvedores de servidores Lineage 2**

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue.svg)](https://www.microsoft.com/windows)
[![WPF](https://img.shields.io/badge/WPF-Modern%20UI-blue.svg)](https://docs.microsoft.com/dotnet/desktop/wpf/)

## 📖 Documentação

**[Documentação Completa](https://majestic-world.github.io/Toolkit/)**

Acesse a documentação completa para guias detalhados, tutoriais e exemplos de uso.

## ⚡ Destaques da Atualização Live Data

- 🔄 **Conversão automática de grades**: S80, R95, R99, R110 → S
- ✨ **Brilho do enchant**: Ativa efeito visual GPR nas armaduras
- 📄 **Geração de XML**: Cria arquivos XML completos com todos os stats
- 📊 **Novo arquivo**: `ItemStatData.txt` para processamento de status

## 🚀 Início Rápido

### Requisitos
- .NET 10
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
   - `ItemStatData.txt` - **Necessário para geração de XML**
   - `h5_names.txt`

3. **Execute o projeto:**
   ```bash
   dotnet run
   ```

> **💡 Dica**: Para usar a geração de XML no Live Data, certifique-se de ter o arquivo `ItemStatData.txt` na pasta dos arquivos .dat. As opções de conversão S+ e brilho do enchant estão disponíveis através dos checkboxes na interface.

## 🛠️ Funcionalidades

- **Gerar Portas** - Converte dados de portas do UnrealEd para XML
- **Animações (Pawn Data)** - Gerador de dados de animação para NPCs
- **Gerenciador de Spawn** - Cria arquivos de spawn automatizados
- **Prime Shop** - Gerador de itens para Prime Shop
- **Live Data (166)** - Processa arquivos .dat Live para Classic com geração de XML
  - ✨ **Conversão S+ → S** - Converte automaticamente S80, R95, R99, R110 para grade S
  - ✨ **Brilho do Enchant** - Habilita efeito visual de brilho no enchant (GPR)
  - ✨ **Geração de XML** - Cria arquivos XML completos para armas e armaduras
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

## 📊 Sumário das Melhorias

| Funcionalidade | Antes | Depois |
|---|---|---|
| **Arquivos necessários** | 6 arquivos | 7 arquivos (+ItemStatData.txt) |
| **Saídas geradas** | 2 campos | 3 campos (+XML) |
| **Compatibilidade** | Manual | Automática (S+ → S) |
| **Enchant visual** | Não configurável | Configurável (GPR) |
| **Formato XML** | Não disponível | Completo com stats |

## 🆕 Novidades

### Live Data - Versão Atualizada

#### Principais Melhorias
- **Novo arquivo necessário**: `ItemStatData.txt` para geração de XML
- **Conversão automática de grades**: S80, R95, R99, R110 → S (configurável)
- **Brilho do enchant**: Habilita efeito visual de brilho no enchant para armaduras
- **Geração de XML**: Cria arquivos XML completos para armas e armaduras com todos os stats

#### Opções de Configuração
- ☑️ **Converter S+ em S?** - Converte grades avançadas para compatibilidade com Classic
- ☑️ **Habilitar brilho no enchant?** - Ativa efeito GPR (`full_armor_enchant_effect_type=1`)

#### Saídas Geradas
1. **Dados GRP** - Arquivos cliente formatados
2. **Nomes** - Mapeamento de nomes dos itens
3. **Dados XML** - Arquivos XML com stats completos (novo!)

---

**Desenvolvido por Majestic World Studio**

> 🚀 **Última Atualização**: Live Data agora suporta geração automática de XML para equipamentos com conversão de grades e configuração de enchant!