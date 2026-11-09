using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GatesOfHell.Shared.Dice;
using GatesOfHell.Shared.Models;

namespace GatesOfHell.Shared.Services;

public sealed class GameApiException : Exception
{
    public GameApiException(string message) : base(message) { }
}

public sealed class GameApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly AppSettings _settings;

    public GameApiClient(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public string? Token { get; set; }
    public bool HasToken => !string.IsNullOrWhiteSpace(Token);

    public async Task<JsonElement> PingAsync(CancellationToken ct = default) =>
        await SendAsync<JsonElement>(_settings.ApiUrl, "ping", null, false, ct);

    public async Task<JsonElement> PingUrlAsync(string url, CancellationToken ct = default) =>
        await SendAsync<JsonElement>(url, "ping", null, false, ct);

    public Task<AuthResult> LoginAsync(string usuario, string senha, CancellationToken ct = default) =>
        SendAsync<AuthResult>(_settings.ApiUrl, "login", new { usuario, senha }, false, ct);

    public Task<AuthResult> CadastrarAsync(string nome, string usuario, string senha,
        string? codigoMestre, CancellationToken ct = default) =>
        SendAsync<AuthResult>(_settings.ApiUrl, "register",
            new { nome, usuario, senha, codigoMestre }, false, ct);

    public Task<Usuario> MeAsync(CancellationToken ct = default) =>
        SendAsync<Usuario>(_settings.ApiUrl, "me", null, true, ct);

    public Task<JsonElement> LogoutAsync(CancellationToken ct = default) =>
        SendAsync<JsonElement>(_settings.ApiUrl, "logout", null, true, ct);

    public Task<GameState> SyncAsync(CancellationToken ct = default) =>
        SendAsync<GameState>(_settings.ApiUrl, "sync", null, true, ct);

    public Task<Personagem> CriarPersonagemAsync(string nome, string classe, string raca,
        string? usuarioId = null, CancellationToken ct = default) =>
        SendAsync<Personagem>(_settings.ApiUrl, "createCharacter",
            new { nome, classe, raca, usuarioId }, true, ct);

    public Task<Personagem> SalvarPersonagemAsync(Personagem personagem, CancellationToken ct = default) =>
        SendAsync<Personagem>(_settings.ApiUrl, "saveCharacter", new { personagem }, true, ct);

    public Task<JsonElement> ExcluirPersonagemAsync(string id, CancellationToken ct = default) =>
        SendAsync<JsonElement>(_settings.ApiUrl, "deleteCharacter", new { id }, true, ct);

    public Task<ItemInventario> SalvarItemAsync(ItemInventario item, CancellationToken ct = default) =>
        SendAsync<ItemInventario>(_settings.ApiUrl, "saveItem", new { item }, true, ct);

    public Task<JsonElement> ExcluirItemAsync(string id, CancellationToken ct = default) =>
        SendAsync<JsonElement>(_settings.ApiUrl, "deleteItem", new { id }, true, ct);

    public Task<Monstro> SalvarMonstroAsync(Monstro monstro, CancellationToken ct = default) =>
        SendAsync<Monstro>(_settings.ApiUrl, "saveMonster", new { monstro }, true, ct);

    public Task<JsonElement> ExcluirMonstroAsync(string id, CancellationToken ct = default) =>
        SendAsync<JsonElement>(_settings.ApiUrl, "deleteMonster", new { id }, true, ct);

    public Task<List<RollEntry>> SalvarRolagemAsync(string contexto, string? personagemId,
        IReadOnlyList<RollResult> resultados, CancellationToken ct = default) =>
        SendAsync<List<RollEntry>>(_settings.ApiUrl, "saveRoll",
            new { contexto, personagemId, resultados }, true, ct);

    public Task<JsonElement> ExcluirRolagemAsync(string id, CancellationToken ct = default) =>
        SendAsync<JsonElement>(_settings.ApiUrl, "deleteRoll", new { id }, true, ct);

    public Task<JsonElement> LimparRolagensAsync(CancellationToken ct = default) =>
        SendAsync<JsonElement>(_settings.ApiUrl, "clearRolls", null, true, ct);

    public Task<Config> SalvarConfigAsync(Config config, CancellationToken ct = default) =>
        SendAsync<Config>(_settings.ApiUrl, "saveConfig", new { config }, true, ct);

    private async Task<T> SendAsync<T>(string url, string action, object? payload,
        bool authorized, CancellationToken ct)
    {
        if (!AppSettings.TryNormalize(url, out var alvo))
            throw new GameApiException("Configure a URL do Web App do Apps Script antes de continuar.");

        if (authorized && !HasToken)
            throw new GameApiException("Sua sessão expirou. Entre novamente.");

        JsonObject corpo;
        if (payload is null)
        {
            corpo = new JsonObject();
        }
        else
        {
            corpo = JsonSerializer.SerializeToNode(payload, JsonOptions) as JsonObject
                    ?? throw new GameApiException("Não foi possível preparar a requisição.");
        }

        corpo["action"] = action;
        if (authorized && HasToken)
            corpo["token"] = Token;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(35));

        HttpResponseMessage resposta;
        try
        {
            using var conteudo = new StringContent(corpo.ToJsonString(JsonOptions), Encoding.UTF8, "text/plain");
            resposta = await _http.PostAsync(alvo, conteudo, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GameApiException("O Google Apps Script demorou demais para responder. Tente novamente.");
        }
        catch (HttpRequestException ex)
        {
            throw new GameApiException(
                $"Não foi possível falar com o Web App ({ex.Message}). Confira a URL, a publicação e a conexão.");
        }

        using (resposta)
        {
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            if (!resposta.IsSuccessStatusCode)
            {
                throw new GameApiException(resposta.StatusCode == HttpStatusCode.Unauthorized
                    ? "Acesso negado pelo Web App. Verifique se ele foi publicado para \"Qualquer pessoa\"."
                    : $"O Web App respondeu HTTP {(int)resposta.StatusCode}. Confira a implantação.");
            }

            if (string.IsNullOrWhiteSpace(texto))
                throw new GameApiException("O Web App respondeu vazio. Confira se a implantação está correta.");

            ApiEnvelope<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(texto, JsonOptions);
            }
            catch (JsonException)
            {
                throw new GameApiException(
                    "Resposta inválida do Web App. Confira a URL /exec e se o acesso está liberado para qualquer pessoa.");
            }

            if (envelope is null)
                throw new GameApiException("Resposta inesperada do Web App.");
            if (!envelope.Ok)
                throw new GameApiException(envelope.Error ?? "O Web App retornou um erro.");
            if (envelope.Data is null)
                throw new GameApiException("O Web App não retornou dados.");

            return envelope.Data;
        }
    }
}
