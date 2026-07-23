using ApiPollingWorker.Configuration;
using ApiPollingWorker.Services;
using Microsoft.Extensions.Options;

namespace ApiPollingWorker.Workers;

public sealed class ApiPollingWorker(
    IRecordProcessor recordProcessor,
    IOptions<WorkerOptions> options,
    ILogger<ApiPollingWorker> logger) : BackgroundService
{
    private readonly WorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ayarlardaki dakika değerini PeriodicTimer'ın kullandığı TimeSpan'e çevirir.
        var interval = TimeSpan.FromMinutes(_options.IntervalMinutes);
        logger.LogInformation("API Polling Worker başladı. Interval: {Interval}, RunImmediately: {RunImmediately}", interval, _options.RunImmediately);

        // true ise ilk zamanlayıcı süresini beklemeden bir tur çalıştırır.
        if (_options.RunImmediately)
        {
            await RunOnceSafelyAsync(stoppingToken);
        }

        // Host kapanma isteği gönderene kadar belirlenen aralıklarla çalışır.
        while (!stoppingToken.IsCancellationRequested)
        {
            // using, bu döngü adımı bittiğinde timer'ın kaynaklarını temizler.
            using var timer = new PeriodicTimer(interval);
            try
            {
                // Servis kapanırsa bekleme hemen iptal olur; tüm süreyi beklemez.
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                await RunOnceSafelyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("API Polling Worker durduruldu.");
    }

    private async Task RunOnceSafelyAsync(CancellationToken cancellationToken)
    {
        // Tur süresini ölçmek, API veya SignalR yavaşladığında bunu loglardan görmemizi sağlar.
        var startedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("API sorgulama turu başladı. StartedAtUtc: {StartedAtUtc}", startedAt);
        try
        {
            await recordProcessor.ProcessAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("API sorgulama turu uygulama kapanışı nedeniyle iptal edildi.");
            return;
        }
        catch (Exception ex)
        {
            // Beklenmeyen hata servisi çökertmez; sonraki periyotta yeniden denenir.
            logger.LogError(ex, "API sorgulama turunda beklenmeyen hata oluştu; sonraki periyotta yeniden denenecek.");
        }
        finally
        {
            logger.LogInformation("API sorgulama turu sona erdi. DurationMs: {DurationMs}", (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
        }
    }
}
