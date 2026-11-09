using GatesOfHell.Shared.Models;

namespace GatesOfHell.Shared.Services;

/// <summary>
/// Sincroniza jogadores, fichas, itens, monstros e rolagens a cada 5 segundos.
/// O backend é uma planilha Google, então "tempo real" aqui é polling curto.
/// </summary>
public sealed class GameSyncService : IDisposable
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    private readonly GameApiClient _api;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource? _cts;
    private bool _iniciado;

    public GameSyncService(GameApiClient api) => _api = api;

    public GameState? State { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? LastSync { get; private set; }
    public bool IsRunning => _cts is not null;

    public event Action? Changed;
    public event Action? SessionExpired;

    public void Start()
    {
        if (_iniciado || !_api.HasToken)
            return;

        _iniciado = true;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _iniciado = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        State = null;
        Error = null;
        LastSync = null;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!_api.HasToken)
            return;

        if (!await _lock.WaitAsync(0, ct))
            return;

        try
        {
            var estado = await _api.SyncAsync(ct);
            State = estado;
            LastSync = DateTimeOffset.Now;
            Error = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            if (ex.Message.Contains("sess", StringComparison.OrdinalIgnoreCase))
                SessionExpired?.Invoke();
        }
        finally
        {
            _lock.Release();
            Changed?.Invoke();
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RefreshAsync(ct);

            try
            {
                await Task.Delay(Intervalo, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _lock.Dispose();
    }
}
