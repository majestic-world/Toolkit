# L2Toolkit

L2Toolkit é uma suíte desktop para desenvolvedores de servidores Lineage 2. Ela transforma dados do cliente em artefatos consumidos por servidores e ferramentas de desenvolvimento.

## Dados de jogo

**Client `.dat`**:
Arquivo binário do cliente Lineage 2. No contexto Fafurion, é uma fonte de dados criptografada e compactada que pode ser lida, editada e serializada novamente.
_Avoid_: arquivo de texto do jogo.

**Classic asset**:
Arquivo de dados Classic em texto usado pelas ferramentas que processam o cliente Classic.
_Avoid_: tabela Live, recurso embutido.

**Live Data (166)**:
Conjunto de dados do cliente Live, protocolo 166, convertido para uso no Classic.
_Avoid_: asset Classic.

**`.l2dat` table**:
Representação compactada de dados tabulares já extraídos do cliente, distribuída pelo L2Toolkit como recurso embutido e substituível pelo usuário em disco.
_Avoid_: client `.dat`.

**Name table (`L2GameDataName`)**:
Tabela que resolve índices numéricos do cliente em strings de jogo, como nomes de ícones e caminhos de mesh.
_Avoid_: tabela de itens.

**MAP_INT**:
Índice de 32 bits para a Name table. Para preservar um arquivo em round trip, seu índice bruto é a fonte de verdade, mesmo quando há uma string resolvida para exibição.
_Avoid_: nome resolvido.

## Artefatos e integridade

**Server artifact**:
Saída gerada pelo L2Toolkit para ser consumida por um servidor, como XML, GRP, texto tabulado ou geodata convertida.
_Avoid_: dado bruto do cliente.

**Round trip**:
Leitura, edição e nova gravação de um client `.dat` sem quebrar sua estrutura binária ou sua compatibilidade com a ferramenta de referência e o cliente.
_Avoid_: exportação de texto.

**SafePackage footer**:
Rodapé binário obrigatório em famílias de arquivos marcadas com `isSafePackage="true"`. Sua ausência torna o client `.dat` inválido para o cliente.
_Avoid_: padding opcional.

**`v413_original`**:
Par de chaves `Lineage2Ver413` usado por clientes oficiais NCSoft; pode ser usado na leitura, mas não permite criar arquivos aceitos pelo cliente oficial porque a chave privada não está disponível.
_Avoid_: chave de recriptografia privada.

**`v413_encdec`**:
Par de chaves `Lineage2Ver413` usado para recriptografar arquivos destinados a clientes privados compatíveis.
_Avoid_: chave oficial NCSoft.
