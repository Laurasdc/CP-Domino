namespace Quina.Tests;
public sealed class DomainTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(15, 3)]
    [InlineData(7, 0)]
    public void Pontuacao_deve_respeitar_multiplos_de_cinco(int sum, int expected)
    {
        // Arrange: soma e resultado definidos pelos casos.
        // Act
        int actual = MoveRules.Points(sum);
        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void Soma_negativa_deve_ser_rejeitada()
    {
        Action act = () => MoveRules.Points(-5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(6, 2)]
    [InlineData(2, 6)]
    public void Encaixe_esquerdo_deve_orientar_a_pedra(int a, int b)
    {
        Tile[] board = [new(6, 4)];
        var result = MoveRules.Place(board, new(a, b), Side.Left);
        result.Should().Equal(new Tile(2, 6), new Tile(6, 4));
        board.Should().Equal(new Tile(6, 4));
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(2, 4)]
    public void Encaixe_direito_deve_orientar_a_pedra(int a, int b)
    {
        Tile[] board = [new(6, 4)];
        var result = MoveRules.Place(board, new(a, b), Side.Right);
        result.Should().Equal(new Tile(6, 4), new Tile(4, 2));
    }

    [Fact]
    public void Encaixe_invalido_nao_deve_alterar_a_mesa()
    {
        Tile[] board = [new(6, 4)];
        Action act = () => MoveRules.Place(board, new(1, 2), Side.Left);
        act.Should().Throw<InvalidOperationException>();
        board.Should().Equal(new Tile(6, 4));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(7, 0)]
    public void Valores_invalidos_devem_ser_rejeitados(int a, int b)
    {
        Action act = () => MoveRules.Place([], new(a, b), Side.Right);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Mapa_repetido_deve_ser_rejeitado()
    {
        Action act = () => new ActionMap(0, 0, 2, 3, 4, 5).Validate();
        act.Should().Throw<ArgumentException>();
    }
}

public sealed class BotTests
{
    private readonly GreedyBot bot = new();
    [Fact]
    public void Bot_deve_aguardar_fora_do_turno()
    {
        var state = Fixtures.State() with { JogadorDoTurnoId = Fixtures.Opponent };
        var result = bot.Choose(state, Fixtures.Player, new());
        result.Should().BeNull();
    }
    [Fact]
    public void Bot_nao_age_apos_encerramento()
    {
        var state = Fixtures.State() with { Situacao = MatchStatus.Finished };
        bot.Choose(state, Fixtures.Player, new()).Should().BeNull();
    }
    [Fact]
    public void Bot_deve_respeitar_abertura_obrigatoria()
    {
        var state = Fixtures.State() with { Tabuleiro = [], MinhaMao = [new(6, 6), new(5, 5)], PedraAberturaObrigatoria = new(5, 5), ProximaAcao = 1 };
        var result = bot.Choose(state, Fixtures.Player, new());
        result!.Tile.Should().Be(new Tile(5, 5));
    }
    [Theory]
    [InlineData(3, "comprar")]
    [InlineData(4, "passar")]
    public void Compra_e_passagem_devem_seguir_o_estado(int code, string expected)
    {
        var state = Fixtures.State() with { ProximaAcao = code };
        bot.Choose(state, Fixtures.Player, new())!.Action.Should().Be(expected);
    }
    [Fact]
    public void Codigo_desconhecido_nao_deve_gerar_compra_ou_passagem()
    {
        var state = Fixtures.State() with { ProximaAcao = 99 };
        bot.Choose(state, Fixtures.Player, new()).Should().BeNull();
    }
    [Fact]
    public void Bot_deve_priorizar_pontuacao_imediata()
    {
        var result = bot.Choose(Fixtures.State(), Fixtures.Player, new());
        result!.Tile.Should().Be(new Tile(6, 1));
        result.Side.Should().Be(Side.Left);
    }
    [Fact]
    public void Bot_deve_usar_mapeamento_confirmado()
    {
        var state = Fixtures.State() with { ProximaAcao = 2 };
        var map = new ActionMap(5, 4, 3, 2, 1, 0);
        bot.Choose(state, Fixtures.Player, map)!.Action.Should().Be("comprar");
    }
    [Fact]
    public void Sem_encaixe_e_sem_autorizacao_bot_deve_parar()
    {
        var state = Fixtures.State() with { MinhaMao = [new(1, 2)], ProximaAcao = 2 };
        bot.Choose(state, Fixtures.Player, new()).Should().BeNull();
    }
}
