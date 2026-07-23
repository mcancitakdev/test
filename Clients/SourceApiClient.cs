using System.Net;
using System.Text.Json;
using ApiPollingWorker.Configuration;
using ApiPollingWorker.Models;
using Microsoft.Extensions.Options;

namespace ApiPollingWorker.Clients;

public sealed class SourceApiClient(
    HttpClient httpClient,
    IOptions<ApiOptions> options,
    ILogger<SourceApiClient> logger) : ISourceApiClient
{
    // Web varsayılanları, JSON alan adlarını büyük/küçük harfe daha toleranslı okur.
    // Tek nesneyi tekrar kullanmak her çağrıda yeniden ayar oluşturulmasını önler.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApiOptions _options = options.Value;

    public async Task<IReadOnlyList<ApiRecordDto>> GetPendingRecordsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // ResponseHeadersRead: Bütün cevap belleğe alınmadan içerik akışını okumaya başlarız.
            using var response = await httpClient.GetAsync(
                _options.Endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Başarısız cevabı hata ayıklamak için loglarız; worker'ın tamamen
                // durmaması için bu sorgulama turuna boş liste ile devam ederiz.
                var body = await ReadSafeBodyAsync(response, cancellationToken);
                logger.LogWarning(
                    "API başarısız cevap verdi. StatusCode: {StatusCode}, ResponseBody: {ResponseBody}",
                    (int)response.StatusCode,
                    body);
                return [];
            }

            // JSON cevabını doğrudan ağ akışından ApiRecordDto listesine dönüştürürüz.
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var records = await JsonSerializer.DeserializeAsync<List<ApiRecordDto>>(stream, JsonOptions, cancellationToken);
            return records ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // İptal servis kapanışından geldiyse üst katmana iletiriz.
            // Böylece uygulama hızlı ve temiz biçimde kapanabilir.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Token iptal edilmediyse bu hata genellikle HttpClient zaman aşımıdır.
            logger.LogWarning(ex, "API isteği {TimeoutSeconds} saniye sonra zaman aşımına uğradı.", _options.TimeoutSeconds);
            return [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "API bağlantı hatası oluştu. HttpRequestError: {HttpRequestError}", ex.HttpRequestError);
            return [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "API cevabı JSON modeline dönüştürülemedi.");
            return [];
        }
    }

    private static async Task<string> ReadSafeBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Çok büyük hata cevaplarının tamamının log dosyasını doldurmasını önler.
        const int maxLength = 2_000;
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return "<empty>";
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return "<empty>";
        }

        return body.Length <= maxLength ? body : string.Concat(body.AsSpan(0, maxLength), "…");
    }
}
