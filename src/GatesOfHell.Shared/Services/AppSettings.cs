using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace GatesOfHell.Shared.Services;

/// <summary>
/// URL do Web App do Google Apps Script. Pode vir do appsettings.json ou ser
/// configurada na tela de login (guardada no localStorage).
/// </summary>
public sealed class AppSettings
{
    public const string UrlStorageKey = "goh.apiUrl";

    private readonly IJSRuntime _js;
    private readonly IConfiguration _configuration;
    private bool _loaded;

    public AppSettings(IJSRuntime js, IConfiguration configuration)
    {
        _js = js;
        _configuration = configuration;
    }

    public string ApiUrl { get; private set; } = "";
    public bool IsConfigured => TryNormalize(ApiUrl, out _);
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_loaded)
            return;

        string? salva = null;
        try
        {
            salva = await _js.InvokeAsync<string?>("localStorage.getItem", UrlStorageKey);
        }
        catch (JSDisconnectedException)
        {
            // Sem JS ainda; usa apenas a configuração.
        }

        ApiUrl = !string.IsNullOrWhiteSpace(salva)
            ? salva!.Trim()
            : (_configuration["ApiBaseUrl"] ?? "").Trim();

        _loaded = true;
    }

    public async Task SetApiUrlAsync(string url)
    {
        if (!TryNormalize(url, out var normalizada))
            throw new ArgumentException("Informe uma URL http(s) válida terminando em /exec.");

        ApiUrl = normalizada;
        await _js.InvokeVoidAsync("localStorage.setItem", UrlStorageKey, normalizada);

        if (!_loaded)
            _loaded = true;

        Changed?.Invoke();
    }

    public static bool TryNormalize(string? url, out string normalizada)
    {
        normalizada = "";
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var texto = url.Trim();
        if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return false;

        normalizada = texto;
        return true;
    }
}
