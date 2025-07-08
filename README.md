# L2Toolkit

Um conjunto de ferramentas para desenvolvedores de servidores Lineage 2, desenvolvido em C# com WPF.

## 📋 Requisitos

- .NET 8.0
- Windows 10/11
- Visual Studio ou Rider (para desenvolvimento)

## 🔧 Configuração

1. Certifique-se de que os arquivos de assets estão na pasta `assets/`:
   - `ItemName_Classic-eu.txt`
   - `EtcItemgrp_Classic.txt`
   - `Armorgrp_Classic.txt`
   - `Weapongrp_Classic.txt`
   - `assets/Skillgrp_Classic.txt`
   - `assets/h5_names.txt`

2. Execute o projeto através da IDE ou use o executável compilado.

## 📝 Funcionalidades

### 🛠️ Ferramentas Principais

#### Gerar Portas
Converte dados de portas do UnrealEd para formato XML.

**Como usar:**
1. Cole os dados da porta do UnrealEd no campo de entrada
2. Clique em "Converter para XML"
3. Copie o resultado gerado

#### Animações (Pawn Data)
Gerador de dados de animação para NPCs.

**Como usar:**
1. Insira o nome do Pawn
2. Preencha as animações para cada tipo (separadas por `;` para múltiplas)
3. Clique em "Gerar PawnAnimData"

#### Gerenciador de Spawn
Cria arquivos de spawn para NPCs de forma automatizada.

**Como usar:**
1. Insira os IDs dos NPCs (separados por `;`)
2. Cole os dados originais de spawn
3. Clique em "Criar Spawns"

#### Prime Shop
Gerador de itens para a Prime Shop do cliente.

**Como usar:**
1. Insira os IDs dos itens (separados por `;`)
2. Selecione categoria e tipo
3. Defina preço e quantidade
4. Clique em "GERAR"

#### Corrigir Descrição
Processa arquivos de descrição de itens para correção.

**Como usar:**
1. Selecione o arquivo a ser processado
2. Clique em "PROCESSAR ARQUIVO"
3. Escolha onde salvar o arquivo modificado

#### Missões Diárias
Gera dados de missões diárias para o cliente.

**Como usar:**
1. Selecione o arquivo `OneDayReward.xml` do servidor
2. Clique em "Gerar Dados"
3. Copie o resultado gerado para os arquivos do client

#### Upgrade Equipment
Processa dados de upgrade de equipamentos.

**Como usar:**
1. Selecione o arquivo `equipment_upgrade.xml` do servidor
2. Clique em "Gerar Dados"
3. Copie o resultado gerado para os arquivos do client

#### Modo Live Data (166)
Obtém arquivos .dat Live para uso no Classic, ideal para skills, armas, armaduras e itens.

**Arquivos necessários na pasta:**
A pasta selecionada deve conter os seguintes arquivos:
- `Skillgrp.txt` - Dados de skills
- `SkillName-eu.txt` - Nomes de skills
- `ItemName-eu.txt` - Nomes de itens
- `Weapongrp.txt` - Dados de armas
- `Armorgrp.txt` - Dados de armaduras
- `EtcItemgrp.txt` - Dados de itens diversos

**Como usar:**
1. Selecione a pasta com os arquivos .dat em formato txt
2. Escolha o tipo de processamento:
   - **Skills** - Processa skills do jogo
   - **Weapons** - Processa armas
   - **Armor** - Processa armaduras
   - **Items** - Processa itens diversos
3. Insira os IDs dos itens desejados:
   - ID único: `1000`
   - Múltiplos IDs: `1000;1001;1002`
   - Range de IDs: `1000...1010`
4. Clique em "Gerar"

**Resultados:**
- Dados GRP
- Nomes nomes do items/skills

#### Enchant Skill (GF)
Cria arquivo `skill_enchant_data.xml` para Giran Forge.

**Como usar:**
1. Selecione a pasta de skills
2. Configure o item necessário e quantidade
3. Clique em "Gerar Dados"
4. Copie os dados gerados

### 🔧 Utilitários

#### Pesquisar Ícone
Busca ícones de itens e skills por ID.

**Como usar:**
1. Selecione o tipo de item
2. Insira o ID do item/skill
3. Clique em "Pesquisar"

#### Separar Logs
Ferramenta para análise de dados de logs do servidor.

**Como usar:**
1. Selecione o arquivo de log
2. Escolha a pasta de saída
3. Insira a key de busca
4. Opcional: adicione o evento de pesquisa
5. Clique em "Gerar Dados"

## 📋 Estrutura do Projeto

```
L2Toolkit/
├── pages/           # Páginas/Controles da interface
├── database/        # Classes de banco de dados
├── DataMap/         # Modelos de dados
├── Utilities/       # Utilitários e parsers
├── images/          # Recursos de imagem
└── Properties/      # Configurações do projeto
```

## 🚀 Desenvolvimento

### Tecnologias Utilizadas

- **C# .NET 8.0** - Framework principal
- **WPF** - Interface gráfica
- **XAML** - Markup da interface

### Estrutura de Dados

- **EnchantData** - Dados de encantamento
- **IconModel** - Modelos de ícones
- **ItemsNameModel** - Nomes de itens
- **Skills** - Dados de skills
- **UpgradeData** - Dados de upgrade
- **UpgradeLucera** - Dados específicos de Lucera

## 📄 Licença

Este projeto foi desenvolvido para a comunidade de desenvolvedores Lineage 2.

---

**Desenvolvido por MK Dev**