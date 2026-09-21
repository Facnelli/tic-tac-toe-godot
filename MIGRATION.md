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

## Marco 10 — primeira runa funcional

A runa piloto usa o ID estável
`rune.pilot.independent-multiplier` e concede um multiplicador independente
`×1,20` a cada resolução de placar. Sua definição é carregada do asset
`Content/Runes/Definitions/PilotIndependentMultiplier.tres`, entra no mesmo
`RuneInventoryState` de qualquer outra runa e é descoberta pelo
`PilotIndependentMultiplierRuneHandler`.

O handler produz uma `ScoreContribution` comum, incluindo
`SourceInstanceId`. Assim, `EncounterEngine` e `ScorePipeline` não possuem
branches para a runa concreta. A apresentação usa somente a proveniência
genérica para pulsar a pedra que ativou e o `ScoreStep` já existente para
animar a alteração do MULT.

O mesmo handler funciona para Player e Enemy. Duas cópias da runa criam duas
contribuições independentes (1,20 × 1,20 = 1,44), mantendo ordenação estável por
`RuneInstanceId`.

## Marco 11 — capacidades de runas e efeitos

O Marco 11 amplia o fluxo sem criar um executor paralelo de gameplay.

- `ClearCellRuneActions` participa do mesmo `ActionCatalog` como provider e
  handler. A Pedra da Purificação piloto consome uma ação, possui um uso por
  instância a cada rodada e só oferece casas ocupadas. Ela continua disponível
  em tabuleiro cheio enquanto existir alvo válido.
- `EncounterEffects` coordena pipelines tipados de turno, dano direto,
  aumento/prevenção de dano e mutações de inventário. O serviço proprietário
  continua sendo responsável pela alteração autoritativa de tabuleiro, vida e
  inventário.
- Quantidade adicional de ações entra na abertura do `TurnContext`. Turno extra
  e pulo passam pelo agendamento do encontro. Um turno pulado é registrado, mas
  não é tratado como tentativa de reação e não apaga a decisão de reação do
  turno que realmente foi jogado.
- `RuneCommandReport` conserva evento, origem, antes/depois e motivo da remoção.
  Descarte, substituição e quebra permanecem motivos distintos.
- `EffectExecutionJournal` fornece identidade de evento, supressão de repetição,
  proteção de ciclo e limite de cadeia. A ordenação usa prioridade e IDs estáveis.
- `SeededRandomSource` e `EffectRandom` permitem rolagens reproduzíveis e
  registradas. `RuneInstanceIdSequence` fornece IDs sequenciais determinísticos
  para conteúdo gerado; a geração de recompensas/runas de uma partida será
  conectada a essa sequência nos marcos de progressão.
- O orçamento total de uma oportunidade possui limite de segurança de 16 ações;
  o limite padrão de cadeia de efeitos é 16. Uma mesma fonte só pode substituir
  o agendamento uma vez por rodada.

A runa de limpeza é conteúdo piloto do Marco 11. Os efeitos sintéticos usados
para provar duas ações, turno extra, pulo, dano e mutações de inventário não são
um catálogo comercial da alpha; esse conteúdo será composto no Marco 15.

## Marco 12 — consolidação do combate no Godot

O conteúdo piloto deixou de esconder parâmetros comerciais no domínio: o MULT
da Runa do Eco vive no Resource Godot, é validado na composição do encontro e
é injetado no handler. Reinícios agora invalidam a geração visual anterior,
interrompem tweens/contagem/apagamento e descartam callbacks pendentes do
`BoardView`, evitando trabalho antigo sobre um encontro novo.

A distribuição .NET foi fechada com `TicTacToeRoguelike.Godot.sln` e
`export_presets.cfg`. A build Windows debug foi exportada e executada fora do
editor. Evidências, comandos e limites de validação estão em `MARCO-12.md`.

## Verificação

A suíte EditMode pura do projeto Unity foi migrada para NUnit/.NET e recebeu
testes adicionais para ActionCatalog e IAgentPolicy. O workflow
`.github/workflows/ci.yml` compila o projeto Godot C# e executa os testes.
