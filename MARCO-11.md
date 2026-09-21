# Marco 11 — Capacidades de runas e efeitos

Status: **concluído no núcleo e integrado ao fluxo Godot** em 21 de setembro de 2026.

Este registro usa como critério o Marco 11 do roteiro até a alpha. Ele não antecipa
o catálogo de conteúdo do Marco 15 nem a consolidação/exportação do Marco 12.

## Entrega

O Marco 11 amplia o motor de efeitos para além de pontuação sem criar caminhos
paralelos de gameplay.

- A Pedra da Purificação usa o `ActionCatalog`, o `ActionExecutor` e uma
  `ClearCellAction` tipada.
- A ação custa uma ação do turno, possui um uso por instância a cada rodada e só
  oferece casas ocupadas. O uso é reiniciado quando uma nova rodada começa.
- A disponibilidade considera a ação especial mesmo com tabuleiro cheio. Quando
  o uso se esgota, ela deixa de manter artificialmente a rodada aberta.
- Limpar uma casa altera o `BoardState` pelo serviço autorizado e a reação é
  recalculada a partir do tabuleiro resultante.
- Player e Enemy recebem a mesma definição de ação, provider, handler, validação e
  execução. A IA escolhe entre as ações legais pelo mesmo catálogo.

## Turnos e reação

`EncounterEffects` produz `TurnPlanModifier` tipado para:

- ações adicionais dentro do mesmo turno;
- turno extra;
- pulo do oponente.

Ações adicionais aumentam o orçamento do `TurnContext`; a reação só é avaliada
quando o turno inteiro termina. Turnos extras e pulos alteram o plano produzido
pelo agendador, sem repetir chamadas no controlador visual.

Um turno pulado é registrado em `LastSkippedTurn`, mas não é tratado como uma
tentativa de reação. Durante os testes de fechamento foi encontrado e corrigido
um defeito em que o registro do pulo apagava `LastReactionDecision`. A correção
mantém a decisão produzida pelo turno real e preserva a oportunidade efetiva de
defesa do reagente.

## Dano e inventário

Existem saídas tipadas para aumento e prevenção de dano e para dano direto/passivo.
A aplicação autoritativa continua pertencendo aos serviços de combate; handlers
não alteram vida diretamente.

Mutações de runa usam `RuneCommand` e `RuneInventoryState.Apply`.
`RuneCommandReport` conserva evento, fonte, comando, estado anterior/posterior
e remoção. Os motivos `Discarded`, `Replaced` e `Broken` são distintos e
testados.

## Determinismo e segurança de encadeamento

`EffectEvent` possui identidade, raiz e profundidade. O
`EffectExecutionJournal` registra execução e bloqueia:

- repetição do mesmo evento/capacidade/fonte;
- ciclos da mesma fonte dentro da mesma raiz;
- cadeias acima do limite configurado.

O limite padrão de cadeia é 16. O orçamento final de um turno é limitado a 16
ações, e cada fonte pode substituir o agendamento uma vez por rodada.

`SeededRandomSource` e `EffectRandom` produzem rolagens reproduzíveis e
registram seed, stream, índice, máximo e resultado. `RuneInstanceIdSequence`
fornece ordem determinística de IDs para instâncias geradas. A geração de
recompensas/runas da partida ainda não existe; quando entrar nos Marcos 13/14,
deverá usar essa sequência em vez de IDs aleatórios não controlados.

## Evidência de testes

Commit de código/testes validado: `16890292e17ff9f2263adb69000784ce3e1dd5dd`.

GitHub Actions:
https://github.com/Facnelli/tic-tac-toe-godot/actions/runs/35627784503

Resultado:

- build Release do projeto Godot C#: sucesso, sem warnings ou erros;
- regressão NUnit: **256 aprovados, 0 falhos, 0 ignorados**.

A suíte dedicada `Tests/Effects/Marco11CapabilityIntegrationTests.cs` comprova:

- limpeza em tabuleiro cheio para Player e Enemy;
- limite de uso da limpeza e encerramento sem loop;
- reavaliação da reação após remover uma sequência;
- duas ações no mesmo turno;
- turno extra durante reação;
- pulo sem consumir a chance de defesa;
- dano passivo com aumento e prevenção tipados;
- mutação de inventário e proveniência;
- motivos distintos de remoção;
- repetição de evento;
- supressão de ciclo;
- limite de cadeia;
- RNG e sequência de IDs reproduzíveis.

## Validação manual e limites deste fechamento

Não foi declarada validação interativa nova no editor ou em build exportada neste
fechamento. O computador remoto autorizado estava offline. O Marco 11 possui como
critério de saída os testes de integração do núcleo; execução da cena, reinícios,
animações, exportação Windows e validação fora do editor são critérios explícitos
do Marco 12 e permanecem abertos para ele.

Os efeitos de duas ações, turno extra, pulo, dano e mutação usados na suíte são
sintéticos de propósito. Eles provam os contratos, mas não são o catálogo de
runas da alpha. A composição de oito a dez runas reais continua no Marco 15.

## Commits relevantes

- `b7ec34d09187193087f034d11314fb6a73ff25be` — implementação inicial das
  capacidades tipadas.
- `f7111389a891a4af42284d51d5dd914a09659301` — suíte inicial de critérios de
  saída.
- `f8fb6f98ad1b594ca587b62d8517d4b90913d2f4` — correção do estado de reação
  ao pular turno.
- `83004e14c270d3c0d6f025262fa474959363ca7a` — cobertura de limpeza, limite
  de uso e reavaliação da reação.
- `16890292e17ff9f2263adb69000784ce3e1dd5dd` — cobertura dos motivos de
  remoção e validação de 256 testes.

Com estas provas, o critério de saída do Marco 11 está atendido. O próximo marco
é o **Marco 12 — Consolidar o combate no Godot**.
