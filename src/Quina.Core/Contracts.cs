namespace Quina.Core;

public interface IGameGateway
{
    Task<GameState?> GetStateAsync(Guid match, Guid player, long? knownVersion = null, CancellationToken ct = default);
    Task<Confirmation> SendAsync(PendingIntent intent, CancellationToken ct = default);
    Task<MatchResult> ResultAsync(Guid match, Guid player, CancellationToken ct = default);
}

/// <summary>Persistência anterior ao envio permite retomar a mesma intenção após reinício.</summary>
public interface IIntentStore
{
    PendingIntent? Load();
    void Save(PendingIntent? intent);
}

public sealed class MemoryIntentStore : IIntentStore
{
    private PendingIntent? value;
    public PendingIntent? Load() => value;
    public void Save(PendingIntent? intent) => value = intent;
}

public sealed class GameApiException(int status, string code, string message, GameState? authoritative = null,
    bool uncertain = false, TimeSpan? retryAfter = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public GameState? Authoritative { get; } = authoritative;
    public bool Uncertain { get; } = uncertain;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
