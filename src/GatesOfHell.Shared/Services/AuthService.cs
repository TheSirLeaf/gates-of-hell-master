using GatesOfHell.Shared.Models;
using Microsoft.JSInterop;

namespace GatesOfHell.Shared.Services;

public sealed class AuthService
{
    private const string TokenKey = "goh.token";

    private readonly GameApiClient _api;
    private readonly AppSettings _settings;
    private readonly IJSRuntime _js;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public AuthService(GameApiClient api, AppSettings settings, IJSRuntime js)
    {
        _api = api;
        _settings = settings;
        _js = js;
    }

    public Usuario? User { get; private set; }
    public bool IsAuthenticated => User is not null && _api.HasToken;
    public bool IsMaster => User?.EhMestre == true;
    public string Token => _api.Token ?? "";
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            await _settings.InitializeAsync();

            string? token = null;
            try
            {
                token = await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            }
            catch (JSDisconnectedException)
            {
            }

            if (!string.IsNullOrWhiteSpace(token) && _settings.IsConfigured)
            {
                _api.Token = token;
                try
                {
                    User = await _api.MeAsync();
                }
                catch
                {
                    await ClearLocalAsync();
                }
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }

        Changed?.Invoke();
    }

    public async Task LoginAsync(string usuario, string senha)
    {
        var resultado = await _api.LoginAsync(usuario.Trim(), senha);
        await AplicarAsync(resultado);
    }

    public async Task RegisterAsync(string nome, string usuario, string senha, string? codigoMestre)
    {
        var resultado = await _api.CadastrarAsync(nome.Trim(), usuario.Trim(), senha, codigoMestre);
        await AplicarAsync(resultado);
    }

    public async Task LogoutAsync()
    {
        if (_api.HasToken)
        {
            try
            {
                await _api.LogoutAsync();
            }
            catch
            {
                // Sessão local é limpa mesmo se o servidor estiver indisponível.
            }
        }

        await ClearLocalAsync();
    }

    public async Task ClearLocalAsync()
    {
        _api.Token = null;
        User = null;

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        }
        catch (JSDisconnectedException)
        {
        }

        Changed?.Invoke();
    }

    private async Task AplicarAsync(AuthResult resultado)
    {
        if (string.IsNullOrWhiteSpace(resultado.Token) || resultado.Usuario is null)
            throw new GameApiException("O servidor não devolveu uma sessão válida.");

        _api.Token = resultado.Token;
        User = resultado.Usuario;
        _initialized = true;

        await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, resultado.Token);
        Changed?.Invoke();
    }
}
