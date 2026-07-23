namespace ApiPollingWorker.Services;

// Bir sorgulama turunun işini tanımlar: kayıtları API'den alıp SignalR'a iletmek.
public interface IRecordProcessor
{
    Task ProcessAsync(CancellationToken cancellationToken);
}
