namespace Quina.Tests;
public sealed class SimulationTests
{
    [Theory]
    [InlineData(CpuDifficulty.Easy)]
    [InlineData(CpuDifficulty.Normal)]
    public async Task Cada_dificuldade_deve_respeitar_turnos_e_concluir_partidas(CpuDifficulty difficulty)
    {
        var human = new GreedyBot();
        for (int seed = 0; seed < 20; seed++)
        {
            var game = new DemoGame(seed, target: 5, difficulty: difficulty);
            var initial = game.View(game.PlayerId);
            if (initial.JogadorDoTurnoId == game.PlayerId)
            {
                await game.StepOpponentAsync();
                game.View(game.PlayerId).VersaoEstado.Should().Be(initial.VersaoEstado);
            }
            int steps = 0;
            while (game.View(game.PlayerId).Situacao != MatchStatus.Finished && steps++ < 3000)
            {
                var state = game.View(game.PlayerId);
                if (state.JogadorDoTurnoId == game.OpponentId) await game.StepOpponentAsync();
                else
                {
                    var move = human.Choose(state, game.PlayerId, new())!;
                    await game.SendAsync(new(game.Id, move.Action,
                        new(game.PlayerId, state.VersaoEstado, Guid.NewGuid())
                        { ValorA = move.Tile?.ValorA, ValorB = move.Tile?.ValorB, Lado = move.Tile is null ? null : move.Side }));
                }
                game.View(game.PlayerId).VersaoEstado.Should().Be(state.VersaoEstado + 1);
            }
            var final = game.View(game.PlayerId);
            final.Situacao.Should().Be(MatchStatus.Finished);
            final.Players.Single(p => p.JogadorId == final.VencedorId).Pontuacao.Should().BeGreaterThanOrEqualTo(5);
        }
    }
    [Fact]
    public async Task Cem_partidas_simuladas_devem_terminar_com_bots_e_maos_validas()
    {
        // Arrange: sementes determinísticas e meta curta para teste de ciclo completo.
        var bot = new GreedyBot();
        for (int seed = 0; seed < 100; seed++)
        {
            var demo = new DemoGame(seed, target: 5);
            int steps = 0;
            // Act: ambos os bots recebem somente sua visão privada.
            while (demo.View(demo.PlayerId).Situacao != MatchStatus.Finished && steps++ < 3000)
            {
                var player = demo.View(demo.PlayerId).JogadorDoTurnoId!.Value;
                var state = demo.View(player);
                var decision = bot.Choose(state, player, new());
                decision.Should().NotBeNull($"semente {seed}, versão {state.VersaoEstado}");
                state.Board.Count.Should().BeLessThanOrEqualTo(28);
                (state.Board.Count + state.Players.Sum(p => p.QuantidadePedrasNaMao)).Should().BeLessThanOrEqualTo(28);
                var body = new ActionRequest(player, state.VersaoEstado, Guid.NewGuid())
                { ValorA = decision!.Tile?.ValorA, ValorB = decision.Tile?.ValorB, Lado = decision.Tile is null ? null : decision.Side };
                await demo.SendAsync(new(demo.Id, decision.Action, body));
            }
            // Assert: o jogo termina, o vencedor é conhecido e atingiu a meta.
            var final = demo.View(demo.PlayerId);
            final.Situacao.Should().Be(MatchStatus.Finished, $"semente {seed}");
            final.VencedorId.Should().NotBeNull();
            final.Players.Max(p => p.Pontuacao).Should().BeGreaterThanOrEqualTo(5);
        }
    }
    [Fact]
    public async Task Repetir_intencao_nao_deve_avancar_duas_vezes()
    {
        var demo = new DemoGame(1);
        var player = demo.View(demo.PlayerId).JogadorDoTurnoId!.Value;
        var state = demo.View(player);
        var d = new GreedyBot().Choose(state, player, new())!;
        var intent = new PendingIntent(demo.Id, "jogar", new(player, state.VersaoEstado, Guid.NewGuid()) { ValorA = d.Tile!.ValorA, ValorB = d.Tile.ValorB, Lado = d.Side });
        var first = await demo.SendAsync(intent);
        var second = await demo.SendAsync(intent);
        second.AcaoIdempotente.Should().BeTrue();
        second.Estado.VersaoEstado.Should().Be(first.Estado.VersaoEstado);
        demo.View(player).VersaoEstado.Should().Be(1);
    }
    [Fact]
    public void Visao_publica_do_adversario_nao_deve_conter_mao()
    {
        var demo = new DemoGame(2);
        var state = demo.View(demo.PlayerId);
        state.Hand.Should().HaveCount(7);
        typeof(Participant).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "JogadorId", "Nome", "Pontuacao", "QuantidadePedrasNaMao", "EhVez");
    }
    [Fact]
    public async Task Versao_antiga_deve_ser_rejeitada_com_estado_atual()
    {
        var demo = new DemoGame(3);
        var player = demo.View(demo.PlayerId).JogadorDoTurnoId!.Value;
        var intent = new PendingIntent(demo.Id, "passar", new(player, 999, Guid.NewGuid()));
        Func<Task> act = () => demo.SendAsync(intent);
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.Status.Should().Be(409);
        error.Which.Authoritative!.VersaoEstado.Should().Be(0);
    }
}
