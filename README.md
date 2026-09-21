# TicTacToe Roguelike — Godot

Migração do jogo da velha roguelike inspirado em Balatro para **Godot 4.7.2 .NET**.

A base de regras veio da branch `marco-5-consolidacao` do projeto Unity, preservando
`Domain` e `Application` como C# puro. A camada de apresentação foi reconstruída
para Godot e a transferência de engine cobre os Marcos 5, 6 e 7.

## Estado atual

- Fase 5: catálogo de ações, providers, handlers e executor desacoplado.
- Fase 6: IA via `IAgentPolicy`, com busca consciente da regra de reação.
- Fase 7: cena `Gameplay.tscn`, tabuleiro Godot, Player e IA integrados ao
  mesmo `EncounterEngine`.
- Marco 8: domínio de runas, inventário de 5 vagas, Intangível e HUD real.
- Marco 9: `EffectEngine` determinístico, handlers ordenados, contexto isolado,
  relatórios de execução e contribuições integradas ao `ScorePipeline`.
- Marco 10: primeira runa funcional (`rune.pilot.independent-multiplier`),
  carregada de um Resource Godot, aplicando MULT ×1,20 com animação genérica da
  pedra de origem e do multiplicador.
- Marco 11: capacidades tipadas além de pontuação: ação de runa para limpar
  casas pelo catálogo comum, modificadores de quantidade de ações/turnos,
  dano direto e modificadores de dano, mutações de inventário com proveniência,
  RNG com seed e proteção contra repetição/ciclos de efeitos.
- A Pedra da Purificação piloto custa uma ação, pode limpar uma casa ocupada e
  possui um uso por instância a cada rodada; Player e Enemy usam o mesmo fluxo.
- Casa central Golden e parâmetros padrão do protótipo Unity preservados.
- Testes puros do projeto antigo migrados para NUnit/.NET.
- CI compila o projeto C# e roda a suíte de regressão; o fechamento do Marco 11
  possui cobertura dedicada em `Tests/Effects/Marco11CapabilityIntegrationTests.cs`.

Consulte [MIGRATION.md](MIGRATION.md) para a descrição técnica da transferência.

## Abrindo no Godot

Abra esta pasta como projeto no Godot 4.7.2 .NET e execute a cena principal.
O arquivo `project.godot` já aponta para `Scenes/Gameplay.tscn`.
