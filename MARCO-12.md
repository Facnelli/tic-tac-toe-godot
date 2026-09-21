# Marco 12 — Consolidação do combate no Godot

## Objetivo

Fechar as diferenças restantes entre os contratos dos Marcos 8–11 e a execução real no Godot, tornando o combate configurável, reiniciável com segurança e exportável como build Windows de teste.

## Implementado

- O MULT da Runa do Eco deixou de ser constante no handler e passou para o Resource `PilotIndependentMultiplier.tres`.
- `PilotIndependentMultiplierRuneContent` carrega e valida o conteúdo antes da composição do encontro; ID incorreto, Resource ausente ou multiplicador fora de `(0, 10]` geram diagnóstico explícito.
- O mesmo valor validado é injetado no inventário e no `EffectEngine`; o domínio não depende de Godot nem conhece o valor comercial da runa.
- O reinício invalida a geração anterior, interrompe contagem/apagamento e mata tweens mantidos pelo controller.
- `BoardView.CancelTransientAnimations()` remove callbacks de apagamento e efeitos visuais pendentes.
- O callback de fim do apagamento também verifica a geração do encontro antes de agir.

## Distribuição

- Adicionado `TicTacToeRoguelike.Godot.sln`, exigido pelo exportador .NET do Godot.
- Adicionado preset `Windows Desktop` em `export_presets.cfg`.
- `Build/` foi ignorado pelo Git para não versionar binários.

## Evidências executadas em 21/09/2026

1. `dotnet build TicTacToeRoguelike.Godot.csproj`: sucesso, 0 erros e 0 avisos.
2. `dotnet test Tests/TicTacToeRoguelike.Tests.csproj`: 265/265 testes após o fechamento, incluindo valor configurado e rejeição de multiplicadores inválidos.
3. Godot 4.7.2 .NET `--headless --editor --quit`: importação/registro de classes concluídos com código 0.
4. Cena principal em headless com `--quit-after 120`: código 0.
5. Probe Godot carregando `PilotIndependentMultiplier.tres`: Resource carregado e `IndependentMultiplier = 1.2`.
6. Export debug Windows: sucesso após inclusão da solution .NET.
7. Build exportada `TicTacToeRoguelike-Marco12.console.exe --headless --quit-after 120`: código 0 fora do editor.

## Limites da validação

A validação automatizada cobre domínio/aplicação, carregamento do conteúdo, importação da cena e execução da build. Ela não substitui uma sessão humana completa de UX com mouse observando cada animação, tooltip e legibilidade. Portanto, este documento não afirma que houve inspeção visual manual de todos os estados; o que foi comprovado automaticamente está discriminado acima.

A arquitetura permanece com `EncounterController` como composition root e coordenador da arena. O Marco 12 removeu responsabilidades de configuração e centralizou cancelamento sem transferir regras autoritativas para Presentation. Uma subdivisão maior do controller continua possível quando novos sistemas da run forem adicionados, mas não foi criada abstração sem necessidade funcional neste corte.

## Critério de saída

O combate mantém os fluxos dos Marcos 8–11, conteúdo piloto configurável é validado antes do encontro, reinícios invalidam trabalho visual antigo, testes puros e execução Godot passam, e existe preset/build Windows executável fora do editor.
