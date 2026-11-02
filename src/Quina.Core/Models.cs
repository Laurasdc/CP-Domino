using System.Text.Json.Serialization;

namespace Quina.Core;

/// <summary>Valores de uma pedra; não atribui pontuação oficial.</summary>
public sealed record Tile(int ValorA, int ValorB)
{
    [JsonIgnore] public int Weight => ValorA + ValorB;
    public bool Same(Tile other) => (ValorA == other.ValorA && ValorB == other.ValorB) || (ValorA == other.ValorB && ValorB == other.ValorA);
    public bool Contains(int value) => ValorA == value || ValorB == value;
    public override string ToString() => $"{ValorA} | {ValorB}";
}

public sealed record PlayerProfile(Guid JogadorId, string? Nome, bool Ativo, DateTimeOffset? AtivadoEmUtc);
public sealed record Participant(Guid JogadorId, string? Nome, int Pontuacao, int QuantidadePedrasNaMao, bool EhVez);
public sealed record AvailableMatch(Guid PartidaId, int PontuacaoAlvo, long VersaoEstado);
public enum MatchStatus { Waiting = 0, Playing = 1, Finished = 2 }
public enum Side { Left = 0, Right = 1 }

/// <summary>Visão privada oficial. Não existe propriedade de mão adversária.</summary>
public sealed record GameState
{
    public Guid PartidaId { get; init; }
    public long VersaoEstado { get; init; }
    public MatchStatus Situacao { get; init; }
    public int NumeroRodada { get; init; }
    public Guid? JogadorDoTurnoId { get; init; }
    public Tile[]? MinhaMao { get; init; }
    public Tile[]? Tabuleiro { get; init; }
    public Participant[]? Participantes { get; init; }
    public Guid? VencedorId { get; init; }
    public int ProximaAcao { get; init; }
    public Tile? PedraAberturaObrigatoria { get; init; }
    public Guid ParticipacaoId { get; init; }
    [JsonIgnore] public IReadOnlyList<Tile> Hand => MinhaMao ?? [];
    [JsonIgnore] public IReadOnlyList<Tile> Board => Tabuleiro ?? [];
    [JsonIgnore] public IReadOnlyList<Participant> Players => Participantes ?? [];
}

public sealed record ActionRequest(Guid JogadorId, long VersaoEsperada, Guid ChaveIdempotencia)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? ValorA { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? ValorB { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Side? Lado { get; init; }
}
public sealed record Confirmation(Guid ChaveIdempotencia, bool AcaoIdempotente, GameState Estado);
public sealed record PendingIntent(Guid MatchId, string Action, ActionRequest Body);
public sealed record HistoryEvent(Guid EventoId, Guid JogadorId, Tile? PecaJogada, Side Lado, bool PassouVez, int PontosConcedidos, DateTimeOffset RecebidoEmUtc);
public sealed record MatchResult(Guid PartidaId, MatchStatus Situacao, long VersaoEstado, Guid? VencedorId, Participant[]? Participantes, HistoryEvent[]? Eventos);

/// <summary>Swagger enumera 0..5 sem nomes. A hipótese inicial exige confirmação pelo usuário.</summary>
public sealed record ActionMap(int Wait = 0, int Open = 1, int Play = 2, int Buy = 3, int Pass = 4, int End = 5)
{
    public void Validate()
    {
        if (!new[] { Wait, Open, Play, Buy, Pass, End }.Order().SequenceEqual(Enumerable.Range(0, 6)))
            throw new ArgumentException("Use todos os números de 0 a 5, sem repetição.");
    }
    public string Label(int value) => value == Wait ? "Aguarde o adversário" : value == Open ? "Jogue a abertura obrigatória" :
        value == Play ? "Escolha uma pedra e uma ponta" : value == Buy ? "Compre uma pedra" : value == Pass ? "Passe a vez" : value == End ? "Partida encerrada" : "Código de ação desconhecido";
}
