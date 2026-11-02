namespace Quina.Tests;

public sealed class ControllerTests
{
    private readonly Mock<IGameGateway> gateway = new(MockBehavior.Strict);
    private readonly Mock<IIntentStore> store = new();
    private GameController NewController()
    {
        var c = new GameController(gateway.Object, Fixtures.Player, store.Object);
        c.Apply(Fixtures.State());
        return c;
    }
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Versao_antiga_ou_igual_nao_substitui_estado(long version)
    {
        var c = NewController();
        bool changed = c.Apply(Fixtures.State(version));
        changed.Should().BeFalse();
        c.State!.VersaoEstado.Should().Be(5);
    }
    [Fact]
    public void Versao_nova_deve_ser_aplicada()
    {
        var c = NewController();
        c.Apply(Fixtures.State(6)).Should().BeTrue();
        c.State!.VersaoEstado.Should().Be(6);
    }
    [Fact]
    public void Estado_de_outra_partida_deve_ser_rejeitado()
    {
        var c = NewController();
        Action act = () => c.Apply(Fixtures.State(7) with { PartidaId = Guid.NewGuid() });
        act.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void Preparar_nao_deve_mudar_estado_nem_enviar_rede()
    {
        var c = NewController();
        var before = c.State;
        var intent = c.Prepare(Fixtures.Match, "comprar", 5);
        intent.Body.ChaveIdempotencia.Should().NotBeEmpty();
        c.State.Should().BeSameAs(before);
        store.Verify(s => s.Save(intent), Times.Once);
        gateway.VerifyNoOtherCalls();
    }
    [Fact]
    public void Falha_no_armazenamento_impede_nova_intencao()
    {
        var c = NewController();
        store.Setup(s => s.Save(It.IsAny<PendingIntent>())).Throws(new IOException("cofre indisponível"));
        Action act = () => c.Prepare(Fixtures.Match, "comprar", 5);
        act.Should().Throw<IOException>();
        c.Pending.Should().BeNull();
        gateway.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task Timeout_deve_preservar_mesma_intencao_na_repeticao()
    {
        var c = NewController();
        var intent = c.Prepare(Fixtures.Match, "comprar", 5);
        gateway.SetupSequence(g => g.SendAsync(intent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GameApiException(0, "TIMEOUT", "timeout", uncertain: true))
            .ReturnsAsync(new Confirmation(intent.Body.ChaveIdempotencia, true, Fixtures.State(6)));
        Func<Task> first = () => c.SendPendingAsync();
        await first.Should().ThrowAsync<GameApiException>();
        c.Pending.Should().BeSameAs(intent);
        await c.SendPendingAsync();
        gateway.Verify(g => g.SendAsync(intent, It.IsAny<CancellationToken>()), Times.Exactly(2));
        c.Pending.Should().BeNull();
        c.State!.VersaoEstado.Should().Be(6);
    }
    [Fact]
    public void Intencao_pendente_bloqueia_outra_intencao()
    {
        var c = NewController();
        c.Prepare(Fixtures.Match, "comprar", 5);
        Action act = () => c.Prepare(Fixtures.Match, "passar", 5);
        act.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public async Task Conflito_deve_aplicar_estado_autoritativo()
    {
        var c = NewController();
        var intent = c.Prepare(Fixtures.Match, "comprar", 5);
        gateway.Setup(g => g.SendAsync(intent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GameApiException(409, "CONFLITO_DE_ESTADO", "conflito", Fixtures.State(7)));
        Func<Task> act = () => c.SendPendingAsync();
        await act.Should().ThrowAsync<GameApiException>();
        c.State!.VersaoEstado.Should().Be(7);
        c.Pending.Should().BeNull();
        store.Verify(s => s.Save(null), Times.Once);
    }
    [Fact]
    public async Task Rejeicao_nao_deve_remover_pedra_nem_conceder_ponto()
    {
        var c = NewController();
        var original = c.State;
        var intent = c.Prepare(Fixtures.Match, "jogar", 5, new Tile(6, 1), Side.Left);
        gateway.Setup(g => g.SendAsync(intent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GameApiException(422, "FORA_DO_TURNO", "rejeitada"));
        Func<Task> act = () => c.SendPendingAsync();
        await act.Should().ThrowAsync<GameApiException>();
        c.State.Should().BeSameAs(original);
        c.Pending.Should().BeNull();
    }
    [Fact]
    public async Task Confirmacao_de_outra_intencao_nao_limpa_pendente()
    {
        var c = NewController();
        var intent = c.Prepare(Fixtures.Match, "comprar", 5);
        gateway.Setup(g => g.SendAsync(intent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Confirmation(Guid.NewGuid(), false, Fixtures.State(6)));
        Func<Task> act = () => c.SendPendingAsync();
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.Uncertain.Should().BeTrue();
        c.Pending.Should().BeSameAs(intent);
    }
    [Fact]
    public async Task Atualizacao_204_preserva_estado()
    {
        var c = NewController();
        gateway.Setup(g => g.GetStateAsync(Fixtures.Match, Fixtures.Player, 5, It.IsAny<CancellationToken>())).ReturnsAsync((GameState?)null);
        (await c.RefreshAsync()).Should().BeFalse();
        c.State!.VersaoEstado.Should().Be(5);
        store.Verify(s => s.Save(It.IsAny<PendingIntent>()), Times.Never);
    }
    [Fact]
    public void Reinicio_deve_recuperar_intencao_persistida()
    {
        var pending = new PendingIntent(Fixtures.Match, "passar", new(Fixtures.Player, 5, Guid.NewGuid()));
        store.Setup(s => s.Load()).Returns(pending);
        var c = new GameController(gateway.Object, Fixtures.Player, store.Object);
        c.Pending.Should().BeSameAs(pending);
        store.Verify(s => s.Load(), Times.Once);
    }
    [Fact]
    public async Task Duplo_envio_concorrente_deve_ser_bloqueado()
    {
        var c = NewController();
        var intent = c.Prepare(Fixtures.Match, "comprar", 5);
        var response = new TaskCompletionSource<Confirmation>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.Setup(g => g.SendAsync(intent, It.IsAny<CancellationToken>())).Returns(response.Task);
        var first = c.SendPendingAsync();
        Func<Task> duplicate = () => c.SendPendingAsync();
        await duplicate.Should().ThrowAsync<InvalidOperationException>();
        response.SetResult(new(intent.Body.ChaveIdempotencia, false, Fixtures.State(6)));
        await first;
        gateway.Verify(g => g.SendAsync(intent, It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task Falha_ao_limpar_journal_deve_manter_intencao_para_repeticao()
    {
        var c = NewController();
        var intent = c.Prepare(Fixtures.Match, "comprar", 5);
        gateway.Setup(g => g.SendAsync(intent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Confirmation(intent.Body.ChaveIdempotencia, false, Fixtures.State(6)));
        store.Setup(s => s.Save(null)).Throws(new IOException("cofre indisponível"));
        Func<Task> act = () => c.SendPendingAsync();
        await act.Should().ThrowAsync<IOException>();
        c.Pending.Should().BeSameAs(intent);
        c.State!.VersaoEstado.Should().Be(6);
    }
}
