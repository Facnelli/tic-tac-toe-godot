# Migração Unity → Godot

## Base

A migração usa como referência a branch `marco-5-consolidacao` do repositório Unity.
`Domain` e `Application` foram transportados como C# puro. Nenhum arquivo `.meta`,
prefab, cena Unity ou `MonoBehaviour` foi copiado.

## Fase 5 — consolidação de ações

- `ActionCatalog` reúne providers e handlers.
- `IGameActionProvider` enumera intenções concretas.
- `IGameActionHandler` aplica uma categoria.
- `PlaceMarkActionHandler` contém a regra de execução da colocação.
- `ActionExecutor` conserva somente validações comuns e despacho.
- Adicionar uma categoria futura não exige editar o executor central.

## Fase 6 — IA

`BasicTicTacToePolicy` implementa `IAgentPolicy` e devolve uma instância recebida
em `AgentDecisionContext.LegalActions`. A busca usa `ReactionStateFactory` e
`ReactionRule`: uma primeira sequência inicia reação, mas não é tratada como
vitória terminal. O tabuleiro autoritativo nunca é alterado pela busca.

## Fase 7 — Godot

A apresentação foi reconstruída com nós Godot:

- `BoardCellView` — botão/casa visual;
- `BoardView` — grade gerada pela `BoardDefinition`;
- `CombatReportAnimator` — consome o relatório já resolvido, sem reaplicar dano;
- `EncounterController` — composition root fino;
- `Scenes/Gameplay.tscn` — cena principal.

Player e IA usam o mesmo caminho:
`ActionCatalog -> ActionExecutor -> EncounterEngine`.

O encontro padrão preserva 3x3, centro Golden, 100 HP por lado, sequência de
vitória 3, pontuação 2–3 e multiplicador 1,5x.

## Marco 8 — Runas

- `RuneDefinition` separa o tipo de runa de `RuneInstance`.
- Player e Enemy usam o mesmo `RuneInventoryState`.
- O limite padrão é de cinco vagas; runas Intangíveis não consomem vaga.
- O inventário pertence ao Confronto e persiste quando uma nova Rodada começa.
- A apresentação Godot usa `RuneDefinitionResource` e `RuneInventoryView`.

## Marco 9 — motor de efeitos

`EffectEngine` executa `IGameEffectHandler` por evento em ordem determinística:
evento, prioridade e `HandlerId` estável. Cada handler recebe um
`EffectContext` isolado, com cópia do tabuleiro e fotografias das runas, para
que efeitos não alterem silenciosamente a fonte autoritativa nem dependam da
ordem de registro.

No evento `ScoreRequested`, as saídas são `ScoreContribution`. O
`EncounterEngine` pede os efeitos de Player e Enemy antes do cálculo e entrega
essas contribuições ao `ScorePipeline`. `EffectExecutionReport` conserva
handlers consultados, ordem, aplicabilidade e fontes produzidas para UI, debug,
testes e futuros replays.

Neste marco não existe regra específica de runa dentro do motor. O próximo corte
pode registrar handlers de runas concretas sem editar `EncounterEngine` ou
`ScoreBreakdown`.

## Verificação

A suíte EditMode pura do projeto Unity foi migrada para NUnit/.NET e recebeu
testes adicionais para ActionCatalog e IAgentPolicy. O workflow
`.github/workflows/ci.yml` compila o projeto Godot C# e executa os testes.
