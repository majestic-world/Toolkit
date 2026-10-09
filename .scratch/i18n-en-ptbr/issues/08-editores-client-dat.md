# 08 — Editores de client `.dat`

Status: ready-for-agent
Type: task
Blocked by: 01, 03

Spec: `../spec.md`, decisões D3–D8 e D11, [Glossário de tradução].

## Escopo

- `Views/SystemMsgColor.axaml/.xaml.cs`:
  - rótulos, watermark `Nome do preset...`;
  - picker `Selecionar SystemMsg.dat`, filtros `Lineage 2 DAT`/`All`;
  - MessageBox `Erro`/`Erro ao salvar` e `l2.exe não encontrado em:\n{path}`;
  - status (`Preset "{name}" salvo.`…);
  - rótulos de linhas e cards criados em código.
- `Views/EnchantEffect.axaml/.xaml.cs`:
  - rótulos (`Aplicar a todos os graus`, `MIN COLOR`…);
  - pickers `Selecionar WeaponEnchantEffectData.dat` / `FullArmorEnchantEffectData.dat`, filtros;
  - badges e textos de cards montados em código.

## Mudanças

- Chaves em `SystemMsgColor.*`, `EnchantEffect.*` e `Common.*` (Error, filtros).
- Rótulos fixos em controles criados em código usam `AppLanguage.Bind(LocKey…)`, para acompanhar a troca sem reconstruir as listas.
- Os rótulos de tipo `Normal (00000000)`/`Augmented (00000040)` continuam literais (D9), já desacoplados no ticket 03.
- Mensagens técnicas de `ClientDat/` (inglês) continuam sem tradução.

## Aceite

- Zero L2LOC007/008 nos arquivos do escopo.
- Com um `SystemMsg.dat` carregado e linhas selecionadas, trocar o idioma atualiza rótulos e cards sem perder seleção nem edições.
- Salvar o `.dat` em en gera arquivo byte a byte igual ao salvo em pt-BR com as mesmas edições.
- Enchant Effect carrega, edita e salva nos dois idiomas.
