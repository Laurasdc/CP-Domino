namespace Quina.Core;

/// <summary>Serializa comandos e aplica somente versões oficiais mais novas.</summary>
public sealed class GameController(IGameGateway gateway, Guid playerId, IIntentStore store)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public Guid PlayerId { get; } = playerId;
    public GameState? State { get; private set; }
    public PendingIntent? Pending { get; private set; } = store.Load();

    public bool Apply(GameState candidate)
    {
        if (candidate.PartidaId == Guid.Empty || candidate.VersaoEstado < 0 || !Enum.IsDefined(candidate.Situacao))
            throw new InvalidDataException("Estado retornado pela API é inválido.");
        if (State is not null && State.PartidaId != candidate.PartidaId)
            throw new InvalidOperationException("Recusado estado de outra partida.");
        if (State is not null && candidate.VersaoEstado <= State.VersaoEstado) return false;
        State = candidate;
        return true;
    }

    public void Open(GameState state)
    {
        if (Pending is not null) throw new InvalidOperationException("Resolva a intenção pendente antes de trocar de mesa.");
        var previous = State;
        State = null;
        try { Apply(state); } catch { State = previous; throw; }
    }

    public PendingIntent Prepare(Guid match, string action, long version, Tile? tile = null, Side side = Side.Right)
    {
        if (Pending is not null) throw new InvalidOperationException("Há uma intenção pendente. Repita-a antes de criar outra.");
        if (action is not ("entrar" or "iniciar" or "jogar" or "comprar" or "passar")) throw new ArgumentException("Ação desconhecida.");
        if (match == Guid.Empty || version < 0) throw new ArgumentException("Identificador ou versão inválida.");
        var body = new ActionRequest(PlayerId, version, Guid.NewGuid());
        if (action == "jogar")
        {
            if (tile is null || !Enum.IsDefined(side)) throw new ArgumentException("Escolha pedra e lado.");
            body = body with { ValorA = tile.ValorA, ValorB = tile.ValorB, Lado = side };
        }
        var intent = new PendingIntent(match, action, body);
        store.Save(intent); // Se a gravação falhar, nada é enviado.
        Pending = intent;
        return intent;
    }

    public async Task<GameState> SendPendingAsync(CancellationToken ct = default)
    {
        if (!await gate.WaitAsync(0, ct)) throw new InvalidOperationException("Já existe uma operação em andamento.");
        try
        {
            var intent = Pending ?? throw new InvalidOperationException("Não há intenção pendente.");
            try
            {
                var confirmation = await gateway.SendAsync(intent, ct);
                if (confirmation.ChaveIdempotencia != intent.Body.ChaveIdempotencia || confirmation.Estado is null || confirmation.Estado.PartidaId != intent.MatchId)
                    throw new GameApiException(200, "CONFIRMACAO_INVALIDA", "Confirmação incompatível; repita a mesma intenção.", uncertain: true);
                try { Apply(confirmation.Estado); }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
                { throw new GameApiException(200, "ESTADO_INVALIDO", "Estado inesperado; mantenha a intenção para reconciliação.", uncertain: true); }
                store.Save(null);
                Pending = null;
                return State!;
            }
            catch (GameApiException ex) when (!ex.Uncertain)
            {
                if (ex.Authoritative is not null) Apply(ex.Authoritative);
                store.Save(null);
                Pending = null;
                throw;
            }
        }
        finally { gate.Release(); }
    }

    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        if (!await gate.WaitAsync(0, ct)) return false;
        try
        {
            if (State is null) return false;
            var state = await gateway.GetStateAsync(State.PartidaId, PlayerId, State.VersaoEstado, ct);
            return state is not null && Apply(state);
        }
        finally { gate.Release(); }
    }
}
