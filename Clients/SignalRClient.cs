using ApiPollingWorker.Configuration;
using ApiPollingWorker.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace ApiPollingWorker.Clients;

public sealed class SignalRClient : ISignalRClient
{
    // Tek bağlantı nesnesini uygulamanın ömrü boyunca yeniden kullanırız.
    private readonly HubConnection _connection;
    private readonly SignalROptions _options;
    private readonly ILogger<SignalRClient> _logger;
    // Birden fazla gönderim aynı anda bağlantı kurmaya çalışırsa sadece biri StartAsync çalıştırır.
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    public SignalRClient(IOptions<SignalROptions> options, ILogger<SignalRClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Bağlantı nesnesini hazırlarız. Gerçek bağlantı ilk mesaj gönderilirken kurulur.
        _connection = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl, connectionOptions =>
            {
                if (!string.IsNullOrWhiteSpace(_options.AccessToken))
                {
                    connectionOptions.AccessTokenProvider = () => Task.FromResult<string?>(_options.AccessToken);
                }
            })
            // Geçici ağ kopmalarında SignalR'ın otomatik yeniden bağlanmasını açar.
            .WithAutomaticReconnect()
            .Build();

        // Bu olaylar bağlantı durumunu loglardan takip etmemizi sağlar.
        _connection.Reconnecting += exception =>
        {
            _logger.LogWarning(exception, "SignalR bağlantısı koptu; yeniden bağlanılıyor.");
            return Task.CompletedTask;
        };
        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR bağlantısı yenilendi. ConnectionId: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };
        _connection.Closed += exception =>
        {
            _logger.LogWarning(exception, "SignalR bağlantısı kapandı.");
            return Task.CompletedTask;
        };
    }

    public async Task SendAsync(SignalRMessageDto message, CancellationToken cancellationToken)
    {
        // Kapatılmış istemcinin yanlışlıkla tekrar kullanılmasını engeller.
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Mesaj göndermeden önce bağlantının hazır olduğundan emin olur.
        await EnsureConnectedAsync(cancellationToken);
        try
        {
            // Ayarlardaki MethodName metodunu çağırıp message nesnesini parametre yollar.
            await _connection.InvokeAsync(_options.MethodName, message, cancellationToken);
            _logger.LogInformation("Kayıt SignalR'a gönderildi. RecordId: {RecordId}, MethodName: {MethodName}", message.Id, _options.MethodName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR veri gönderme hatası. RecordId: {RecordId}, MethodName: {MethodName}", message.Id, _options.MethodName);
            throw;
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        // Zaten bağlıysak kilit almadan hızlıca devam ederiz.
        if (_connection.State == HubConnectionState.Connected)
        {
            return;
        }

        // Bağlantı kurma bölümüne aynı anda yalnızca bir çağrı girebilir.
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Kilidi beklerken başka çağrı bağlantıyı kurmuş olabilir; tekrar kontrol ederiz.
            if (_connection.State == HubConnectionState.Connected)
            {
                return;
            }

            if (_connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            {
                // SignalR zaten uğraşıyorsa ikinci bir StartAsync başlatmayız.
                throw new InvalidOperationException($"SignalR bağlantısı henüz hazır değil. State: {_connection.State}");
            }

            _logger.LogInformation("SignalR bağlantısı başlatılıyor. HubUrl: {HubUrl}", _options.HubUrl);
            await _connection.StartAsync(cancellationToken);
            _logger.LogInformation("SignalR bağlantısı kuruldu. ConnectionId: {ConnectionId}", _connection.ConnectionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR bağlantısı kurulamadı. HubUrl: {HubUrl}", _options.HubUrl);
            throw;
        }
        finally
        {
            // Hata olsa bile kilit bırakılır; yoksa sonraki çağrılar sonsuza kadar bekler.
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose birden fazla çağrılsa da temizliği yalnızca bir kere yapar.
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _connectionLock.WaitAsync();
        try
        {
            // Aktif SignalR bağlantısını ve kullandığı kaynakları kapatır.
            await _connection.DisposeAsync();
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }
}
