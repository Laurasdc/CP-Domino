namespace Quina.Tests;
internal static class Fixtures
{
    public static readonly Guid Player = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Opponent = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Match = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static GameState State(long version = 5) => new()
    {
        PartidaId = Match, VersaoEstado = version, Situacao = MatchStatus.Playing,
        NumeroRodada = 1, JogadorDoTurnoId = Player,
        MinhaMao = [new(6, 1), new(6, 3)], Tabuleiro = [new(6, 4)], ProximaAcao = 2,
        Participantes = [new(Player, "A", 0, 2, true), new(Opponent, "B", 0, 7, false)],
        ParticipacaoId = Player
    };
}
