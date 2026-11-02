# Integrantes

## Laura Souza - 556320

## Vinicius Henrique - 556908

## Gustavo Pierre - 558928

## Enzo Dias - 558225

## Gabriel Belo - 551669

# QUINA — Dominó Ponta de Quina

Jogo de dominó desenvolvido em C# com interface desktop em WPF. Permite jogar contra a CPU ou acessar partidas online pela API.

## Funcionalidades

### Partidas contra a CPU

- Jogo local, sem necessidade de login ou internet.
- Dificuldades Fácil e Normal.
- Partidas com duração de 3, 5 ou 7 rodadas.
- Partidas com meta de 15, 30 ou 50 pontos.
- Indicação das peças disponíveis e dos encaixes possíveis.
- Compra de peças, passagem de turno e dicas de jogada.
- Opção de jogar automaticamente.
- Placar, indicação de turno e acompanhamento da rodada.
- Resumo de pontuação entre rodadas.
- Tela de resultado com opção de jogar novamente.
- Salvamento automático e retomada após fechar o aplicativo.
- Reinício e encerramento da partida.

### Partidas online

- Solicitação de chave de acesso por e-mail FIAP.
- Autenticação e recuperação do acesso salvo.
- Criação, seleção e ativação de perfis de jogador.
- Criação de partidas com pontuação alvo.
- Listagem e ingresso em mesas disponíveis.
- Cópia do ID da mesa e retomada de partidas por ID.
- Atualização do estado da partida pela API.
- Exibição da mão própria e da quantidade de peças do adversário.
- Envio de jogadas, compras e passagens de turno.
- Jogador autônomo.
- Consulta de resultado e histórico.
- Saída da sessão e liberação do perfil.

### Interface

- Tema escuro com destaques em violeta.
- Peças de dominó desenhadas na interface.
- Mesa que se ajusta à sequência de peças.
- Animações e avisos de pontuação.
- Ajuda com instruções de como jogar.
- Janela redimensionável e modo de tela cheia com F11.

## Tecnologias

- C# e .NET 8
- WPF e XAML
- API REST com HttpClient
- xUnit
- FluentAssertions
- Moq
- Proteção de dados locais com Windows DPAPI

## Como executar

### Pré-requisitos

- Windows
- .NET 8 SDK ou uma versão compatível
- Visual Studio ou Visual Studio Code com suporte a C#

### Execução

Na pasta que contém o arquivo `Quina.sln`:

```bash
dotnet restore Quina.sln
dotnet build Quina.sln
dotnet run --project src/Quina.Desktop/Quina.Desktop.csproj
```

Também é possível iniciar pelo arquivo `iniciar.bat`.

## Testes

```bash
dotnet test Quina.sln
```

Ou execute o arquivo `testar.bat`.

## Estrutura do projeto

```text
src/
├── Quina.Core/             Modelos, controlador e estratégia do jogador autônomo
├── Quina.Infrastructure/   Comunicação com a API e jogo local
└── Quina.Desktop/          Interface WPF e armazenamento protegido

tests/
└── Quina.Tests/            Testes automatizados

docs/                      Documentação técnica
```

## API

O modo online utiliza a API Dominó Ponta de Quina.

[Documentação Swagger](https://domino-quina-api-b1-pf2261.azurewebsites.net/swagger/index.html)

## Regras do modo local

As jogadas pontuam quando a soma das duas pontas externas é múltipla de 5, concedendo soma ÷ 5 pontos.

Ao finalizar uma rodada, o jogador com a menor soma nas peças restantes recebe 1 ponto de bônus. Empates nessa soma não concedem bônus.

Nas partidas por rodadas, vence quem tiver o maior placar ao término da última rodada. Placares iguais resultam em empate. Nas partidas por pontos, vence quem alcançar ou ultrapassar a meta.

O modo online utiliza as regras e a pontuação determinadas pela API.
