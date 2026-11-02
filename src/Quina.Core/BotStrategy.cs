namespace Quina.Core;

public sealed record Move(Tile Tile, Side Side, IReadOnlyList<Tile> Board);
public sealed record BotDecision(string Action, Tile? Tile = null, Side Side = Side.Right, string Reason = "");

/// <summary>Regras auxiliares para sugestões. A API continua decidindo validade e placar.</summary>
public static class MoveRules
{
    public static int Points(int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        return total % 5 == 0 ? total / 5 : 0;
    }

    public static IReadOnlyList<Tile> Place(IReadOnlyList<Tile> board, Tile tile, Side side)
    {
        if (tile.ValorA is < 0 or > 6 || tile.ValorB is < 0 or > 6 || !Enum.IsDefined(side))
            throw new ArgumentException("Pedra ou lado inválido.");
        if (board.Count == 0) return [tile];
        int end = side == Side.Left ? board[0].ValorA : board[^1].ValorB;
        if (!tile.Contains(end)) throw new InvalidOperationException("A pedra não encaixa nessa ponta.");
        var oriented = side == Side.Left
            ? (tile.ValorB == end ? tile : new Tile(tile.ValorB, tile.ValorA))
            : (tile.ValorA == end ? tile : new Tile(tile.ValorB, tile.ValorA));
        return side == Side.Left ? new[] { oriented }.Concat(board).ToArray() : board.Concat([oriented]).ToArray();
    }

    public static IEnumerable<Move> LegalMoves(GameState state)
    {
        foreach (var tile in state.Hand)
        {
            if (state.Board.Count == 0 && state.PedraAberturaObrigatoria is { } required && !tile.Same(required)) continue;
            foreach (var side in state.Board.Count == 0 ? new[] { Side.Right } : new[] { Side.Left, Side.Right })
            {
                IReadOnlyList<Tile>? board = null;
                try { board = Place(state.Board, tile, side); }
                catch (InvalidOperationException) { /* Encaixe incompatível: não é candidato. */ }
                if (board is not null) yield return new Move(tile, side, board);
            }
        }
    }
}

public interface IBotStrategy { BotDecision? Choose(GameState state, Guid playerId, ActionMap map); }

/// <summary>Fechar mão, pontuar, descarregar peso e preservar mobilidade; sem informações ocultas.</summary>
public sealed class GreedyBot : IBotStrategy
{
    public BotDecision? Choose(GameState state, Guid playerId, ActionMap map)
    {
        if (state.Situacao != MatchStatus.Playing || state.JogadorDoTurnoId != playerId) return null;
        if (state.ProximaAcao == map.Buy) return new("comprar", Reason: "A API indica compra de uma pedra.");
        if (state.ProximaAcao == map.Pass) return new("passar", Reason: "A API indica passagem permitida.");
        if (state.ProximaAcao != map.Play && state.ProximaAcao != map.Open) return null;
        var best = MoveRules.LegalMoves(state).Select(move =>
        {
            var rest = state.Hand.Where(tile => !tile.Same(move.Tile)).ToArray();
            return new
            {
                Move = move, Finish = rest.Length == 0,
                Score = MoveRules.Points(move.Board[0].ValorA + move.Board[^1].ValorB),
                Mobility = rest.Count(t => t.Contains(move.Board[0].ValorA) || t.Contains(move.Board[^1].ValorB))
            };
        }).OrderByDescending(x => x.Finish).ThenByDescending(x => x.Score)
          .ThenByDescending(x => x.Move.Tile.Weight).ThenByDescending(x => x.Mobility)
          .ThenByDescending(x => x.Move.Tile.ValorA == x.Move.Tile.ValorB).ThenBy(x => x.Move.Side).FirstOrDefault();
        return best is null ? null : new("jogar", best.Move.Tile, best.Move.Side,
            $"Estimativa: {best.Score} ponto(s); descarte: {best.Move.Tile.Weight}. O placar oficial é da API.");
    }
}
