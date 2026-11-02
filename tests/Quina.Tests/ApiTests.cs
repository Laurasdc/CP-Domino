using System.Net;
using System.Text;
using System.Text.Json;

namespace Quina.Tests;
public sealed class ApiTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private static HttpResponseMessage Response(HttpStatusCode code, string body = "") => new(code)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static DominoApiClient Client(HttpMessageHandler handler) => new(handler) { Token = "test-token", ActivationKey = new string('a', 64) };

    [Fact]
    public async Task Consulta_privada_deve_enviar_cabecalhos_e_versao()
    {
        string? authorization = null, activation = null, url = null;
        using var client = Client(new Handler((request, _) =>
        {
            authorization = request.Headers.Authorization?.ToString();
            activation = request.Headers.GetValues("X-Jogador-Ativacao").Single();
            url = request.RequestUri!.ToString();
            return Task.FromResult(Response(HttpStatusCode.NoContent));
        }));
        var state = await client.GetStateAsync(Fixtures.Match, Fixtures.Player, 8);
        state.Should().BeNull();
        authorization.Should().Be("Bearer test-token");
        activation.Should().Be(new string('a', 64));
        url.Should().Contain("/atualizacoes?").And.Contain("versaoConhecida=8").And.StartWith("https://");
    }
    [Fact]
    public async Task Token_202_nao_exige_corpo_nem_envia_bearer()
    {
        string? sent = null; bool bearer = true;
        using var client = Client(new Handler(async (request, ct) =>
        {
            bearer = request.Headers.Authorization is not null;
            sent = await request.Content!.ReadAsStringAsync(ct);
            return Response(HttpStatusCode.Accepted);
        }));
        await client.RequestTokenAsync("aluno@fiap.com.br");
        bearer.Should().BeFalse();
        JsonDocument.Parse(sent!).RootElement.GetProperty("email").GetString().Should().Be("aluno@fiap.com.br");
    }
    [Theory]
    [InlineData("aluno@example.com")]
    [InlineData("x@fiap.com.br.evil.test")]
    [InlineData("sem-arroba")]
    public async Task Email_invalido_deve_ser_rejeitado_sem_rede(string email)
    {
        int calls = 0;
        using var client = Client(new Handler((_, _) => { calls++; return Task.FromResult(Response(HttpStatusCode.Accepted)); }));
        Func<Task> act = () => client.RequestTokenAsync(email);
        await act.Should().ThrowAsync<ArgumentException>();
        calls.Should().Be(0);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(429)]
    public async Task Erros_de_regra_devem_ser_explicitos_e_sem_segredos(int status)
    {
        using var client = Client(new Handler((_, _) => Task.FromResult(Response((HttpStatusCode)status, "{\"codigo\":\"TESTE\"}"))));
        Func<Task> act = () => client.ProfilesAsync();
        var result = await act.Should().ThrowAsync<GameApiException>();
        result.Which.Status.Should().Be(status);
        result.Which.Code.Should().Be("TESTE");
        result.Which.Uncertain.Should().BeFalse();
        result.Which.Message.Should().NotContain("test-token").And.NotContain(new string('a', 64));
    }
    [Fact]
    public async Task Erro_booleano_do_token_deve_ser_tratado()
    {
        using var client = Client(new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.BadRequest, "false"))));
        Func<Task> act = () => client.RequestTokenAsync("aluno@fiap.com.br");
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.Status.Should().Be(400);
    }
    [Fact]
    public async Task Limite_deve_preservar_retry_after()
    {
        using var client = Client(new Handler((_, _) =>
        {
            var response = Response(HttpStatusCode.TooManyRequests, "{\"codigo\":\"LIMITE_EXCEDIDO\"}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
            return Task.FromResult(response);
        }));
        Func<Task> act = () => client.RequestTokenAsync("aluno@fiap.com.br");
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
    }
    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Erro_servidor_em_comando_deve_ser_incerto(int code)
    {
        using var client = Client(new Handler((_, _) => Task.FromResult(Response((HttpStatusCode)code, "indisponível"))));
        var intent = new PendingIntent(Fixtures.Match, "comprar", new(Fixtures.Player, 5, Guid.NewGuid()));
        Func<Task> act = () => client.SendAsync(intent);
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.Uncertain.Should().BeTrue();
    }
    [Fact]
    public async Task Falha_de_rede_em_comando_deve_ser_incerta()
    {
        using var client = Client(new Handler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))));
        var intent = new PendingIntent(Fixtures.Match, "comprar", new(Fixtures.Player, 5, Guid.NewGuid()));
        Func<Task> act = () => client.SendAsync(intent);
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.Uncertain.Should().BeTrue();
    }
    [Fact]
    public async Task Json_invalido_em_comando_deve_manter_incerteza()
    {
        using var client = Client(new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, "<html>erro</html>"))));
        var intent = new PendingIntent(Fixtures.Match, "comprar", new(Fixtures.Player, 5, Guid.NewGuid()));
        Func<Task> act = () => client.SendAsync(intent);
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.Code.Should().Be("JSON_INVALIDO");
        error.Which.Uncertain.Should().BeTrue();
    }
    [Fact]
    public async Task Conflito_deve_deserializar_estado_autoritativo()
    {
        string json = JsonSerializer.Serialize(new { codigo = "CONFLITO_DE_ESTADO", estadoAutoritativo = Fixtures.State(8) }, DominoApiClient.Json);
        using var client = Client(new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.Conflict, json))));
        var intent = new PendingIntent(Fixtures.Match, "comprar", new(Fixtures.Player, 5, Guid.NewGuid()));
        Func<Task> act = () => client.SendAsync(intent);
        var error = await act.Should().ThrowAsync<GameApiException>();
        error.Which.Authoritative!.VersaoEstado.Should().Be(8);
    }
    [Fact]
    public async Task Jogada_deve_serializar_lado_numerico_e_campos_camel_case()
    {
        string? body = null;
        var intent = new PendingIntent(Fixtures.Match, "jogar", new(Fixtures.Player, 5, Guid.NewGuid()) { ValorA = 6, ValorB = 1, Lado = Side.Left });
        using var client = Client(new Handler(async (request, ct) =>
        {
            body = await request.Content!.ReadAsStringAsync(ct);
            return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new Confirmation(intent.Body.ChaveIdempotencia, false, Fixtures.State(6)), DominoApiClient.Json));
        }));
        var confirmation = await client.SendAsync(intent);
        using var document = JsonDocument.Parse(body!);
        var json = document.RootElement;
        json.GetProperty("lado").GetInt32().Should().Be(0);
        json.GetProperty("versaoEsperada").GetInt64().Should().Be(5);
        json.GetProperty("chaveIdempotencia").GetGuid().Should().Be(intent.Body.ChaveIdempotencia);
        confirmation.Estado.VersaoEstado.Should().Be(6);
    }
    [Fact]
    public void Compra_nao_deve_serializar_campos_de_jogada()
    {
        var request = new ActionRequest(Fixtures.Player, 5, Guid.NewGuid());
        string json = JsonSerializer.Serialize(request, DominoApiClient.Json);
        json.Should().NotContain("valorA").And.NotContain("valorB").And.NotContain("lado");
    }
}
