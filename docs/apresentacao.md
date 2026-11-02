# Roteiro de apresentação — 7 a 9 minutos

## Objetivo e arquitetura — 1 minuto

“Nosso cliente em C# permite jogar dominó pela API do professor. A API controla
regras, distribuição, pontuação e vencedor; o cliente apresenta o estado,
envia intenções e escolhe ações quando o jogador autônomo está ativado.”

No VS Code, mostre `Quina.sln`, os quatro projetos e o diagrama de classes.
Explique `IGameGateway`, `IIntentStore` e `IBotStrategy` como pontos de isolamento.

## Acesso e sala — 1 minuto

Receba os tokens antes da aula para não depender de e-mail durante a defesa.
Não projete o token nem a chave. Mostre perfil, ativação, criação da mesa,
ingresso do outro grupo e início explícito.

## Mesa manual — 2 minutos

Mostre mão própria, contagem do adversário, abertura, pontas, rodada, turno,
placar e versão. Faça uma jogada e observe a confirmação nos dois clientes.
Explique por que uma jogada rejeitada não remove a pedra da interface.

## Jogador autônomo — 2 minutos

Use “Sugerir jogada” para explicar a decisão. Ative o bot e conclua uma partida
com meta curta acordada com o outro grupo. Mostre resultado e histórico oficial.
O bot só recebe dados do participante e informações públicas.

## Testes e resiliência — 1 a 2 minutos

Execute no terminal:

```powershell
dotnet test tests/Quina.Tests/Quina.Tests.csproj
```

Mostre os cenários de soma 10/15, um teste de exceção com FluentAssertions e o
uso de Moq para verificar consulta/envio/persistência. Explique:

- Persistir antes de enviar permite retomar a intenção após reinício.
- Timeout não significa rejeição; o mesmo UUID evita duplicação.
- 409 usa estado autoritativo; 422 mantém a visão confirmada.

Só apresente testes como aprovados depois da execução real. O treino local não
comprova integração com a API. Use as evidências anotadas na homologação.

## Perguntas frequentes

**Por que WPF se usamos VS Code?** O VS Code edita C# e XAML; o .NET SDK compila.
Não dependemos de um designer visual. O aplicativo executa no Windows.

**Por que existe simulador?** Para testar o fluxo e apresentar a interface sem
credenciais. Seu cálculo de fim de rodada é simplificado e não substitui a API.

**A estratégia é ótima?** Não. É determinística e explicável: fechar mão,
pontuar, descartar peso e preservar encaixes. Não usa informações ocultas.

**Onde ficam segredos?** Fora do projeto, cifrados por DPAPI com o usuário Windows.

**Como resolver perfil ativo em outro cliente?** Liberar explicitamente no cliente
original. Não há tomada automática ou expiração por fechamento.

**Por que os códigos de próxima ação são configuráveis?** O contrato enumera
valores sem nomes. A associação deve ser confirmada com o professor.

## Evidência por critério do PDF

| Critério | Peso | Material/execução |
|---|---:|---|
| Documentação | 5% | README, docs e comentários de responsabilidades |
| Convenções | 5% | Projetos, interfaces, records e métodos assíncronos |
| Apresentação | 10% | Este roteiro e demonstração real |
| Diagrama de classes | 10% | `arquitetura.md` |
| Conexão e registro | 15% | Acesso, criação e ativação de perfil |
| Partidas/estado correto | 20% | Mesa manual completa na API |
| Jogador autônomo | 20% | Partida completa com bot na API |
| Testes automatizados | 15% | Execução xUnit/FluentAssertions/Moq e relatório |

A nota depende da homologação, execução em sala e compreensão do grupo.
