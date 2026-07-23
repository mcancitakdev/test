using ApiPollingWorker.Models;

namespace ApiPollingWorker.Clients;

// Kaynak REST API ile konuşan sınıfın sözleşmesi.
// Arayüz kullanmak, gerçek istemciyi testte sahte bir istemciyle değiştirmeyi kolaylaştırır.
public interface ISourceApiClient
{
    // Bekleyen kayıtları getirir; kayıt yoksa boş liste döner.
    Task<IReadOnlyList<ApiRecordDto>> GetPendingRecordsAsync(CancellationToken cancellationToken);
}
