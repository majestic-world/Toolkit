# L2Toolkit

Um conjunto de ferramentas para desenvolvedores de servidores Lineage 2, desenvolvido em C# com WPF.

## 📁 Estrutura do Projeto

```
L2Toolkit/
├── pages/                     # Páginas/UserControls da aplicação
│   ├── MainWindow.xaml/.cs    # Janela principal
│   ├── DescriptionFix.xaml/.cs    # Ferramenta de correção de descrições
│   ├── DoorGenerateControl.xaml/.cs # Gerador de portas XML
│   ├── PawnDataControl.xaml/.cs     # Gerador de dados de Pawn
│   ├── PrimeShopGenerator.xaml/.cs  # Gerador de Prime Shop
│   └── SpawnManager.xaml/.cs        # Gerenciador de Spawns
├── Properties/                # Arquivos de propriedades do projeto
├── images/                    # Ícones e imagens
├── bin/                       # Executáveis e assets
│   └── Debug/
│       └── assets/            # Arquivos de dados do jogo
├── App.xaml/.cs              # Configuração da aplicação
├── L2Toolkit.csproj          # Arquivo de projeto
└── App.config               # Configuração da aplicação
```

## 📋 Requisitos

- .NET Framework 4.8.1
- Windows 10/11
- Visual Studio ou Rider (para desenvolvimento)

## 🔧 Configuração

1. Certifique-se de que os arquivos de assets estão na pasta `assets/`:
   - `ItemName_Classic-eu.txt`
   - `EtcItemgrp_Classic.txt`
   - `Armorgrp_Classic.txt`
   - `Weapongrp_Classic.txt`
   - `h5_names.txt`

2. Execute o projeto através da IDEA ou use o executável compilado.

## 📝 Como Usar

### Door Forge
1. Cole os dados da porta do UnrealEd no campo de entrada
2. Clique em "🔄 Converter para XML"
3. Copie o resultado gerado

### Pawn Data
1. Insira o nome do Pawn
2. Preencha as animações para cada tipo (separadas por ';' para múltiplas)
3. Clique em "✨ Gerar PawnAnimData"

### Prime Shop
1. Insira os IDs dos itens (separados por vírgula)
2. Selecione categoria e tipo
3. Defina preço e quantidade
4. Clique em "✨ GERAR"

### Spawn Manager
1. Insira os IDs dos NPCs (separados por ';')
2. Cole os dados originais de spawn
3. Clique em "✨ Criar Spawns"

### Item Description
1. Selecione o arquivo a ser processado
2. Clique em "✨ PROCESSAR ARQUIVO"
3. Escolha onde salvar o arquivo modificado

## 📄 Licença

Este projeto foi desenvolvido para a comunidade de desenvolvedores Lineage 2.

---

**Desenvolvido por MK Dev** - v1.3 