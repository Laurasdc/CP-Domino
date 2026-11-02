using System.Text.Json;

namespace Quina.Tests;

public sealed class LocalMatchTests
{
    private static async Task Move(DemoGame game)
    {
        var player = game.View(game.PlayerId).JogadorDoTurnoId!.Value;
        var state = game.View(player);
        var decision = new GreedyBot().Choose(state, player, new())!;
        await game.SendAsync(new(game.Id, decision.Action, new(player, state.VersaoEstado, Guid.NewGuid())
        { ValorA = decision.Tile?.ValorA, ValorB = decision.Tile?.ValorB, Lado = decision.Tile is null ? null : decision.Side }));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public async Task Limite_de_rodadas_deve_ignorar_meta_e_preservar_intervalos(int limit)
    {
        var game = new DemoGame(42, target: 1, roundLimit: limit, pauseBetweenRounds: true);
        int breaks = 0, steps = 0;
        while (game.View(game.PlayerId).Situacao != MatchStatus.Finished && steps++ < 10000)
        {
            if (game.AwaitingNextRound)
            {
                breaks++;
                var before = game.View(game.PlayerId);
                await game.StepOpponentAsync();
                game.View(game.PlayerId).Should().BeEquivalentTo(before);
                game.LastRound!.Number.Should().Be(before.NumeroRodada);
                game.LastRound.PlayerTotal.Should().Be(before.Players[0].Pontuacao);
                game.ContinueRound();
                game.View(game.PlayerId).NumeroRodada.Should().Be(before.NumeroRodada + 1);
                game.View(game.PlayerId).Hand.Should().HaveCount(7);
            }
            else await Move(game);
        }
        var final = game.View(game.PlayerId);
        final.Situacao.Should().Be(MatchStatus.Finished);
        final.NumeroRodada.Should().Be(limit);
        breaks.Should().Be(limit - 1);
        if (final.Players[0].Pontuacao == final.Players[1].Pontuacao) final.VencedorId.Should().BeNull();
        else final.Players.Single(p => p.JogadorId == final.VencedorId).Pontuacao.Should().Be(final.Players.Max(p => p.Pontuacao));
    }

    [Fact]
    public async Task Salvamento_serializado_deve_restaurar_maos_monte_placar_e_intervalo()
    {
        var game = new DemoGame(7, roundLimit: 3, pauseBetweenRounds: true);
        int steps = 0;
        while (!game.AwaitingNextRound && steps++ < 3000) await Move(game);
        game.AwaitingNextRound.Should().BeTrue();
        var snapshot = game.Save();
        var json = JsonSerializer.Serialize(snapshot, DominoApiClient.Json);
        var restored = DemoGame.Restore(JsonSerializer.Deserialize<DemoSnapshot>(json, DominoApiClient.Json)!);
        restored.Save().Should().BeEquivalentTo(snapshot);
        restored.View(game.PlayerId).Should().BeEquivalentTo(game.View(game.PlayerId));
        restored.ContinueRound();
        restored.View(game.PlayerId).NumeroRodada.Should().Be(2);
        await Move(restored);
        restored.Save().Version.Should().Be(snapshot.Version + 2);
    }

    [Fact]
    public void Salvamento_com_pecas_duplicadas_deve_ser_rejeitado()
    {
        var snapshot = new DemoGame(1).Save();
        snapshot.Hands[0][0] = snapshot.Hands[0][1];
        Action act = () => DemoGame.Restore(snapshot);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Limite_nao_oferecido_deve_ser_rejeitado()
    {
        Action act = () => new DemoGame(roundLimit: 4);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
