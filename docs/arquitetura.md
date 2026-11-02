# Diagrama de classes

O GitHub renderiza Mermaid. No VS Code, abra a visualização Markdown; dependendo
da versão/extensões, use a renderização do GitHub para o diagrama.

```mermaid
classDiagram
 direction TB
 class MainWindow {
  -GameController controller
  -IGameGateway gateway
  +Render()
 }
 class GameController {
  +GameState State
  +PendingIntent Pending
  +Apply(state) bool
  +Prepare(match, action, version) PendingIntent
  +SendPendingAsync() Task
  +RefreshAsync() Task
 }
 class IGameGateway {
  <<interface>>
  +GetStateAsync(match, player, version) Task
  +SendAsync(intent) Task
  +ResultAsync(match, player) Task
 }
 class DominoApiClient {
  +ProfilesAsync() Task
  +ActivateAsync(player, key) Task
  +CreateAsync(player, target) Task
 }
 class DemoGame {
  +View(player) GameState
  +StepOpponentAsync() Task
 }
 class IIntentStore {
  <<interface>>
  +Load() PendingIntent
  +Save(intent)
 }
 class VaultIntentStore
 class MemoryIntentStore
 class WindowsVault {
  +Get(name)
  +Set(name, value)
  +ActivationKey() string
 }
 class IBotStrategy {
  <<interface>>
  +Choose(state, player, map) BotDecision
 }
 class GreedyBot
 class GameState
 class PendingIntent
 class Tile
 class Participant
 MainWindow --> GameController
 MainWindow --> IBotStrategy
 MainWindow --> WindowsVault
 GameController --> IGameGateway
 GameController --> IIntentStore
 GameController o-- GameState
 GameController o-- PendingIntent
 DominoApiClient ..|> IGameGateway
 DemoGame ..|> IGameGateway
 VaultIntentStore ..|> IIntentStore
 MemoryIntentStore ..|> IIntentStore
 VaultIntentStore --> WindowsVault
 GreedyBot ..|> IBotStrategy
 GreedyBot --> GameState
 GameState o-- Tile
 GameState o-- Participant
```

`Tile`, `Participant`, `GameState`, `ActionRequest`, `PendingIntent` e
`Confirmation` são records. Os DTOs mantêm nomes compatíveis com o contrato;
a serialização usa camelCase e enums numéricos. A API não fornece mãos adversárias.
Os arrays dos DTOs não são mutados pelo controlador; ele troca o estado confirmado.

## Uma intenção até sua confirmação

```mermaid
flowchart TD
 A[Humano ou bot escolhe comando] --> B[Salvar UUID, versão e payload]
 B --> C[Enviar à API]
 C --> D{Resposta}
 D -->|Confirmada| E[Aplicar versão mais nova]
 E --> F[Limpar intenção pendente]
 D -->|409| G[Aplicar estado autoritativo]
 G --> H[Exibir rejeição]
 D -->|422| H
 D -->|Rede ou 5xx| I[Manter intenção salva]
 I --> J[Repetir exatamente a mesma intenção]
 J --> C
```

## Decisões e limites

- `IGameGateway` desacopla a lógica do HttpClient e do simulador.
- `IIntentStore` permite verificar persistência com Moq e usar DPAPI na interface.
- `IBotStrategy` permite trocar a heurística sem alterar transporte/controlador.
- Semáforo serializa operações de jogo; a interface também mantém um indicador de ocupação.
- O estado não é atualizado de forma otimista; a API confirma a jogada.
- DPAPI protege segredos com a conta do Windows, fora do diretório do código.
- WPF/XAML define a apresentação; code-behind coordena eventos e serviços. Não é
  alegado um padrão MVVM completo inexistente no código.
- O simulador é separado; seu bônus simplificado de rodada não afeta a API real.
- O mapeamento da próxima ação precisa de confirmação por lacuna no Swagger.
