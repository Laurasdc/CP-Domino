using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Quina.Core;

namespace Quina.Infrastructure;

/// <summary>Contrato do Swagger de 22/10/2026. Não registra tokens ou cabeçalhos.</summary>
public sealed class DominoApiClient : IGameGateway, IDisposable
{
    public const string DefaultBaseUrl = "https://domino-quina-api-b1-pf2261.azurewebsites.net";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http;
    public string Token { private get; set; } = "";
    public string ActivationKey { private get; set; } = "";

    public DominoApiClient(HttpMessageHandler? handler = null)
    {
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(DefaultBaseUrl), Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 2_000_000
        };
    }

    private async Task<T?> RequestAsync<T>(HttpMethod method, string path, object? body = null,
        bool privateView = false, bool anonymous = false, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.ParseAdd("application/json");
        if (!anonymous)
        {
            if (string.IsNullOrWhiteSpace(Token)) throw new GameApiException(401, "NAO_AUTENTICADO", "Informe um token válido.");
            request.Headers.Authorization = new("Bearer", Token);
        }
        if (privateView)
        {
            if (string.IsNullOrWhiteSpace(ActivationKey)) throw new GameApiException(403, "SEM_ATIVACAO", "Ative um perfil antes de jogar.");
            request.Headers.Add("X-Jogador-Ativacao", ActivationKey);
        }
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        bool mutation = method != HttpMethod.Get;
        try
        {
            using var response = await http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                ApiError? error = null;
                try { error = JsonSerializer.Deserialize<ApiError>(raw, Json); } catch (JsonException) { }
                var delay = response.Headers.RetryAfter?.Delta;
                if (delay is null && response.Headers.RetryAfter?.Date is { } date) delay = date - DateTimeOffset.UtcNow;
                int status = (int)response.StatusCode;
                string message = status switch
                {
                    400 => "Dados inválidos; revise os campos.",
                    401 => "Token inválido ou revogado. Entre novamente.",
                    403 => "Sem permissão. Confira a conta e a ativação do perfil.",
                    404 => "Partida não encontrada ou indisponível.",
                    409 => "Conflito de versão ou perfil já ativo em outro cliente.",
                    422 => "Ação rejeitada pela regra. Nenhuma jogada foi confirmada.",
                    429 => "Limite de solicitações atingido. Aguarde antes de tentar novamente.",
                    _ => "Resposta inesperada do servidor."
                };
                throw new GameApiException(status, error?.Codigo ?? "HTTP_ERROR", message + (error?.Codigo is { } code ? $" ({code})" : ""),
                    error?.EstadoAutoritativo, mutation && status >= 500, delay);
            }
            if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(raw)) return default;
            try { return JsonSerializer.Deserialize<T>(raw, Json); }
            catch (JsonException)
            { throw new GameApiException((int)response.StatusCode, "JSON_INVALIDO", "Resposta não pôde ser interpretada.", uncertain: mutation); }
        }
        catch (HttpRequestException)
        { throw new GameApiException(0, "FALHA_REDE", "Falha de rede. O resultado de uma ação enviada pode ser desconhecido.", uncertain: mutation); }
        catch (TaskCanceledException)
        { throw new GameApiException(0, "TIMEOUT", "Tempo de resposta excedido. Não presuma sucesso nem repita com outro UUID.", uncertain: mutation); }
    }

    private static T Required<T>(T? value) where T : class => value ?? throw new GameApiException(200, "RESPOSTA_VAZIA", "Era esperado um corpo de resposta.", uncertain: true);

    public async Task RequestTokenAsync(string email, CancellationToken ct = default)
    {
        email = email.Trim();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email || !address.Host.Equals("fiap.com.br", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use seu e-mail @fiap.com.br.");
        await RequestAsync<object>(HttpMethod.Post, "/api/identidade/token", new { email }, anonymous: true, ct: ct);
    }
    public async Task ValidateSessionAsync(CancellationToken ct = default) =>
        _ = Required(await RequestAsync<Session>(HttpMethod.Get, "/api/identidade/sessao", ct: ct));
    public async Task<PlayerProfile[]> ProfilesAsync(CancellationToken ct = default) =>
        Required(await RequestAsync<PlayerProfile[]>(HttpMethod.Get, "/api/jogadores", ct: ct));
    public async Task<PlayerProfile> CreateProfileAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 150) throw new ArgumentException("O nome deve ter entre 1 e 150 caracteres.");
        return Required(await RequestAsync<PlayerProfile>(HttpMethod.Post, "/api/jogadores", new { nome = name }, ct: ct));
    }
    public async Task ActivateAsync(Guid player, string key, CancellationToken ct = default)
    {
        if (key.Length != 64 || !key.All(Uri.IsHexDigit)) throw new ArgumentException("Chave de ativação inválida.");
        _ = Required(await RequestAsync<PlayerProfile>(HttpMethod.Post, $"/api/jogadores/{player}/ativar-cliente", new { chaveAtivacao = key }, ct: ct));
        ActivationKey = key;
    }
    public async Task ReleaseAsync(Guid player, CancellationToken ct = default)
    {
        _ = Required(await RequestAsync<PlayerProfile>(HttpMethod.Delete, $"/api/jogadores/{player}/desativar-cliente", privateView: true, ct: ct));
        ActivationKey = "";
    }
    public async Task<AvailableMatch[]> AvailableAsync(CancellationToken ct = default) =>
        Required(await RequestAsync<AvailableMatch[]>(HttpMethod.Get, "/api/partidas", ct: ct));
    public async Task<GameState> CreateAsync(Guid player, int target, CancellationToken ct = default)
    {
        if (target <= 0) throw new ArgumentException("A meta deve ser positiva.");
        return Required(await RequestAsync<GameState>(HttpMethod.Post, "/api/partidas", new { jogadorId = player, pontuacaoAlvo = target }, true, ct: ct));
    }
    public Task<GameState?> GetStateAsync(Guid match, Guid player, long? knownVersion = null, CancellationToken ct = default) =>
        RequestAsync<GameState>(HttpMethod.Get, $"/api/partidas/{match}" + (knownVersion.HasValue ? $"/atualizacoes?jogadorId={player}&versaoConhecida={knownVersion}" : $"?jogadorId={player}"), privateView: true, ct: ct);
    public async Task<Confirmation> SendAsync(PendingIntent intent, CancellationToken ct = default)
    {
        if (intent.Action is not ("entrar" or "iniciar" or "jogar" or "comprar" or "passar")) throw new ArgumentException("Ação inválida.");
        return Required(await RequestAsync<Confirmation>(HttpMethod.Post, $"/api/partidas/{intent.MatchId}/{intent.Action}", intent.Body, true, ct: ct));
    }
    public async Task<MatchResult> ResultAsync(Guid match, Guid player, CancellationToken ct = default) =>
        Required(await RequestAsync<MatchResult>(HttpMethod.Get, $"/api/partidas/{match}/resultado?jogadorId={player}", privateView: true, ct: ct));
    public void Dispose() => http.Dispose();
    private sealed record Session(string? UsuarioId);
    private sealed record ApiError(string? Codigo, GameState? EstadoAutoritativo);
}
