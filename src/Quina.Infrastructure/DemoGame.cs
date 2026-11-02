using Quina.Core;

namespace Quina.Infrastructure;

/// <summary>Simulador separado do servidor real. Bônus de fim de rodada é simplificado.</summary>
public enum CpuDifficulty { Easy, Normal }

public sealed partial class DemoGame : IGameGateway
{
    private readonly Random random;
    public CpuDifficulty Difficulty { get; }
    private readonly List<Tile>[] hands = [[], []];
    private List<Tile> stock = [];
    private IReadOnlyList<Tile> board = [];
    private readonly int[] scores = [0, 0];
    private readonly int target;
    public int Target => target;
    public int? RoundLimit { get; }
    public bool PauseBetweenRounds { get; }
    public bool AwaitingNextRound { get; private set; }
    public RoundSummary? LastRound { get; private set; }
    private readonly int[] roundStartScores = [0, 0];
    private int turn, round, passes;
    private long version;
    private Tile? opening;
    private MatchStatus status = MatchStatus.Playing;
    private Guid? winner;
    private readonly Dictionary<Guid, (PendingIntent Intent, Confirmation Result)> cache = [];
    private readonly List<HistoryEvent> events = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PlayerId { get; private set; } = Guid.NewGuid();
    public Guid OpponentId { get; private set; } = Guid.NewGuid();
    public DemoGame(int? seed = null, int target = 15, CpuDifficulty difficulty = CpuDifficulty.Normal, int? roundLimit = null, bool pauseBetweenRounds = false)
    {
        if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
        random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.target = target;
        if (!Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
        Difficulty = difficulty;
        if (roundLimit is not null && roundLimit is not (3 or 5 or 7)) throw new ArgumentOutOfRangeException(nameof(roundLimit));
        RoundLimit = roundLimit; PauseBetweenRounds = pauseBetweenRounds;
        NewRound();
    }

    private void NewRound()
    {
        round++; AwaitingNextRound = false;
        Array.Copy(scores, roundStartScores, 2);
        var deck = (from a in Enumerable.Range(0, 7) from b in Enumerable.Range(a, 7 - a) select new Tile(a, b)).ToList();
        for (int i = deck.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (deck[i], deck[j]) = (deck[j], deck[i]); }
        hands[0].Clear(); hands[0].AddRange(deck.Take(7));
        hands[1].Clear(); hands[1].AddRange(deck.Skip(7).Take(7));
        stock = deck.Skip(14).ToList(); board = []; passes = 0;
        var best = hands.SelectMany((hand, index) => hand.Select(t => new { Tile = t, Index = index }))
            .OrderByDescending(x => x.Tile.ValorA == x.Tile.ValorB).ThenByDescending(x => x.Tile.Weight)
            .ThenByDescending(x => Math.Max(x.Tile.ValorA, x.Tile.ValorB)).First();
        turn = best.Index; opening = best.Tile;
    }
    private int NextAction(int index)
    {
        if (status == MatchStatus.Finished) return 5;
        if (AwaitingNextRound || index != turn) return 0;
        if (board.Count == 0) return 1;
        if (hands[index].Any(t => t.Contains(board[0].ValorA) || t.Contains(board[^1].ValorB))) return 2;
        return stock.Count > 0 ? 3 : 4;
    }
    public GameState View(Guid player)
    {
        if (player != PlayerId && player != OpponentId) throw new GameApiException(403, "SEM_PERMISSAO", "Perfil não participa.");
        var ids = new[] { PlayerId, OpponentId };
        int index = player == PlayerId ? 0 : 1;
        return new GameState
        {
            PartidaId = Id, VersaoEstado = version, Situacao = status, NumeroRodada = round,
            JogadorDoTurnoId = ids[turn], MinhaMao = hands[index].ToArray(), Tabuleiro = board.ToArray(),
            Participantes = ids.Select((id, i) => new Participant(id, i == 0 ? "Você" : "CPU", scores[i], hands[i].Count, turn == i)).ToArray(),
            VencedorId = winner, ProximaAcao = NextAction(index), PedraAberturaObrigatoria = board.Count == 0 ? opening : null,
            ParticipacaoId = player
        };
    }
    public Task<GameState?> GetStateAsync(Guid match, Guid player, long? knownVersion = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (match != Id) throw new GameApiException(404, "PARTIDA_INDISPONIVEL", "Partida não encontrada.");
        return Task.FromResult(knownVersion == version ? null : View(player));
    }
    public Task<Confirmation> SendAsync(PendingIntent intent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (intent.MatchId != Id) throw new GameApiException(404, "PARTIDA_INDISPONIVEL", "Partida não encontrada.");
        if (cache.TryGetValue(intent.Body.ChaveIdempotencia, out var old))
        {
            if (old.Intent != intent) throw new GameApiException(409, "CHAVE_REUTILIZADA", "Intenção diferente com o mesmo UUID.");
            return Task.FromResult(old.Result with { AcaoIdempotente = true });
        }
        var body = intent.Body;
        _ = View(body.JogadorId);
        if (body.VersaoEsperada != version) throw new GameApiException(409, "CONFLITO_DE_ESTADO", "Versão desatualizada.", View(body.JogadorId));
        int index = body.JogadorId == PlayerId ? 0 : 1;
        if (AwaitingNextRound || turn != index || status != MatchStatus.Playing) throw new GameApiException(422, "FORA_DO_TURNO", "Aguarde sua vez.");
        int action = NextAction(index), gained = 0;
        Tile? played = null;
        if (intent.Action == "jogar" && action is 1 or 2)
        {
            var tile = new Tile(body.ValorA ?? -1, body.ValorB ?? -1);
            var actual = hands[index].FirstOrDefault(t => t.Same(tile));
            if (actual is null) throw new GameApiException(422, "PECA_AUSENTE", "Pedra ausente.");
            if (board.Count == 0 && !tile.Same(opening!)) throw new GameApiException(422, "ABERTURA_INVALIDA", "Use a abertura indicada.");
            try { board = MoveRules.Place(board, tile, body.Lado ?? Side.Right); }
            catch (InvalidOperationException) { throw new GameApiException(422, "ENCAIXE_INVALIDO", "Pedra não encaixa."); }
            hands[index].Remove(actual); played = actual; passes = 0;
            gained = MoveRules.Points(board[0].ValorA + board[^1].ValorB); scores[index] += gained;
            turn = 1 - index;
        }
        else if (intent.Action == "comprar" && action == 3)
        {
            var tile = stock[^1]; stock.RemoveAt(stock.Count - 1); hands[index].Add(tile);
            if (!tile.Contains(board[0].ValorA) && !tile.Contains(board[^1].ValorB)) turn = 1 - index;
        }
        else if (intent.Action == "passar" && action == 4) { passes++; turn = 1 - index; }
        else throw new GameApiException(422, "ACAO_NAO_PERMITIDA", "Ação não permitida.");
        events.Add(new(Guid.NewGuid(), body.JogadorId, played, body.Lado ?? Side.Right, intent.Action == "passar", gained, DateTimeOffset.UtcNow));
        if (RoundLimit is null && scores[index] >= target)
        {
            status = MatchStatus.Finished; winner = body.JogadorId;
            LastRound = new(round, "Meta de pontos atingida", body.JogadorId, 0,
                scores[0] - roundStartScores[0], scores[1] - roundStartScores[1], scores[0], scores[1]);
        }
        else if (hands[index].Count == 0 || passes >= 2)
        {
            int left = hands[0].Sum(t => t.Weight), right = hands[1].Sum(t => t.Weight);
            int? won = left == right ? null : left < right ? 0 : 1;
            if (won.HasValue) scores[won.Value]++; // Bônus local simplificado: não é uma regra confirmada da API.
            Guid? roundWinner = won is null ? null : won == 0 ? PlayerId : OpponentId;
            LastRound = new(round, hands[index].Count == 0 ? "Mão concluída" : "Mesa fechada: dois passes",
                roundWinner, won.HasValue ? 1 : 0, scores[0] - roundStartScores[0], scores[1] - roundStartScores[1], scores[0], scores[1]);
            if (RoundLimit.HasValue ? round >= RoundLimit.Value : scores.Max() >= target)
            {
                status = MatchStatus.Finished;
                winner = scores[0] == scores[1] ? null : scores[0] > scores[1] ? PlayerId : OpponentId;
            }
            else if (PauseBetweenRounds) AwaitingNextRound = true;
            else NewRound();
        }
        version++;
        var result = new Confirmation(body.ChaveIdempotencia, false, View(body.JogadorId));
        cache.Add(body.ChaveIdempotencia, (intent, result));
        return Task.FromResult(result);
    }
    public void ContinueRound()
    {
        if (!AwaitingNextRound || status == MatchStatus.Finished) throw new InvalidOperationException("Não há rodada para continuar.");
        NewRound(); version++;
    }
    public async Task StepOpponentAsync()
    {
        var state = View(OpponentId);
        var decision = new GreedyBot().Choose(state, OpponentId, new());
        if (decision is null) return;
        if (Difficulty == CpuDifficulty.Easy && decision.Action == "jogar")
        {
            var moves = MoveRules.LegalMoves(state).ToArray();
            var move = moves[random.Next(moves.Length)];
            decision = new BotDecision("jogar", move.Tile, move.Side);
        }
        var body = new ActionRequest(OpponentId, version, Guid.NewGuid())
        { ValorA = decision.Tile?.ValorA, ValorB = decision.Tile?.ValorB, Lado = decision.Tile is null ? null : decision.Side };
        await SendAsync(new(Id, decision.Action, body));
    }
    public Task<MatchResult> ResultAsync(Guid match, Guid player, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (match != Id) throw new GameApiException(404, "PARTIDA_INDISPONIVEL", "Partida não encontrada.");
        return Task.FromResult(new MatchResult(Id, status, version, winner, View(player).Participantes, events.ToArray()));
    }
}
