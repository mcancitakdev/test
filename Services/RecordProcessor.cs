using ApiPollingWorker.Clients;
using ApiPollingWorker.Models;

namespace ApiPollingWorker.Services;

public sealed class RecordProcessor(
    ISourceApiClient sourceApiClient,
    ISignalRClient signalRClient,
    ILogger<RecordProcessor> logger) : IRecordProcessor
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        // Önce kaynak API'den o anda işlenmeyi bekleyen bütün kayıtları alırız.
        var records = await sourceApiClient.GetPendingRecordsAsync(cancellationToken);
        if (records.Count == 0)
        {
            logger.LogInformation("API bekleyen kayıt döndürmedi; SignalR'a veri gönderilmeyecek.");
            return;
        }

        logger.LogInformation("API'den {RecordCount} kayıt alındı.", records.Count);
        var sentCount = 0;

        // Kayıtlar sırayla gönderilir. Bir kaydın hatası diğerlerini engellemez.
        foreach (var record in records)
        {
            // Servis kapanıyorsa yeni kayda başlamadan döngüyü hemen keser.
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // API ve SignalR modellerini ayrı tutmak, taraflardan birinin veri şekli
                // ileride değiştiğinde diğer tarafın doğrudan etkilenmesini önler.
                var message = new SignalRMessageDto(record.Id, record.Message, record.CreatedDate);
                await signalRClient.SendAsync(message, cancellationToken);
                sentCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Hatalı kaydı loglayıp sonraki kaydı işlemeye devam ederiz.
                logger.LogWarning(ex, "Kayıt işlenemedi; sonraki kayda geçiliyor. RecordId: {RecordId}", record.Id);
            }
        }

        logger.LogInformation("Tur tamamlandı. TotalCount: {TotalCount}, SentCount: {SentCount}, FailedCount: {FailedCount}", records.Count, sentCount, records.Count - sentCount);
    }
}
