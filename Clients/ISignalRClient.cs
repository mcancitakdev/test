using ApiPollingWorker.Models;

namespace ApiPollingWorker.Clients;

// SignalR'a mesaj gönderme işleminin sözleşmesi.
// IAsyncDisposable, bağlantının uygulama kapanırken düzgün kapatılmasını sağlar.
public interface ISignalRClient : IAsyncDisposable
{
    Task SendAsync(SignalRMessageDto message, CancellationToken cancellationToken);
}
