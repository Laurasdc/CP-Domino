using Quina.Core;

namespace Quina.Infrastructure;

public sealed record RoundSummary(int Number, string Reason, Guid? Winner, int Bonus,
    int PlayerPoints, int CpuPoints, int PlayerTotal, int CpuTotal);
public sealed record CachedDemoAction(PendingIntent Intent, Confirmation Result);
public sealed record DemoSnapshot(int Format, Guid Id, Guid PlayerId, Guid OpponentId,
    CpuDifficulty Difficulty, int Target, int? RoundLimit, bool PauseBetweenRounds,
    bool AwaitingNextRound, RoundSummary? LastRound, int[] RoundStartScores,
    Tile[][] Hands, Tile[] Stock, Tile[] Board, int[] Scores, int Turn, int Round,
    int Passes, long Version, Tile? Opening, MatchStatus Status, Guid? Winner,
    HistoryEvent[] Events, CachedDemoAction[] Cache);

public sealed partial class DemoGame
{
    public DemoSnapshot Save() => new(1, Id, PlayerId, OpponentId, Difficulty, target, RoundLimit,
        PauseBetweenRounds, AwaitingNextRound, LastRound, roundStartScores.ToArray(),
        hands.Select(h => h.ToArray()).ToArray(), stock.ToArray(), board.ToArray(), scores.ToArray(),
        turn, round, passes, version, opening, status, winner, events.ToArray(),
        cache.Values.Select(c => new CachedDemoAction(c.Intent, c.Result)).ToArray());

    public static DemoGame Restore(DemoSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Format != 1 || snapshot.Id == Guid.Empty || snapshot.PlayerId == Guid.Empty ||
            snapshot.OpponentId == Guid.Empty || snapshot.PlayerId == snapshot.OpponentId ||
            snapshot.Hands is not { Length: 2 } || snapshot.Hands.Any(h => h is null) ||
            snapshot.Scores is not { Length: 2 } || snapshot.RoundStartScores is not { Length: 2 } ||
            snapshot.Stock is null || snapshot.Board is null || snapshot.Events is null || snapshot.Cache is null ||
            snapshot.Turn is < 0 or > 1 || snapshot.Round < 1 || snapshot.Version < 0 ||
            snapshot.Passes is < 0 or > 2 || snapshot.Scores.Any(n => n < 0) || !Enum.IsDefined(snapshot.Status))
            throw new InvalidDataException("A partida salva é inválida ou pertence a outra versão.");
        var tiles = snapshot.Hands.SelectMany(h => h).Concat(snapshot.Stock).Concat(snapshot.Board).ToArray();
        if (tiles.Length != 28 || tiles.Any(t => t is null || t.ValorA is < 0 or > 6 || t.ValorB is < 0 or > 6) ||
            tiles.Select(t => (Math.Min(t.ValorA, t.ValorB), Math.Max(t.ValorA, t.ValorB))).Distinct().Count() != 28)
            throw new InvalidDataException("As peças da partida salva são inválidas.");
        var game = new DemoGame(target: snapshot.Target, difficulty: snapshot.Difficulty,
            roundLimit: snapshot.RoundLimit, pauseBetweenRounds: snapshot.PauseBetweenRounds)
        {
            Id = snapshot.Id, PlayerId = snapshot.PlayerId, OpponentId = snapshot.OpponentId,
            AwaitingNextRound = snapshot.AwaitingNextRound, LastRound = snapshot.LastRound,
            stock = snapshot.Stock.ToList(), board = snapshot.Board.ToArray(), turn = snapshot.Turn,
            round = snapshot.Round, passes = snapshot.Passes, version = snapshot.Version,
            opening = snapshot.Opening, status = snapshot.Status, winner = snapshot.Winner
        };
        for (int i = 0; i < 2; i++)
        {
            game.hands[i].Clear(); game.hands[i].AddRange(snapshot.Hands[i]);
            game.scores[i] = snapshot.Scores[i]; game.roundStartScores[i] = snapshot.RoundStartScores[i];
        }
        game.events.AddRange(snapshot.Events);
        foreach (var entry in snapshot.Cache)
            game.cache.Add(entry.Intent.Body.ChaveIdempotencia, (entry.Intent, entry.Result));
        return game;
    }
}
