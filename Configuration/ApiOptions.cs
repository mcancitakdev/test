using System.ComponentModel.DataAnnotations;

namespace ApiPollingWorker.Configuration;

public sealed class ApiOptions
{
    // Bu sınıf appsettings.json içindeki "Api" bölümünü temsil eder.
    public const string SectionName = "Api";

    // Örnek: https://localhost:5001
    [Required, Url]
    public string BaseUrl { get; init; } = string.Empty;

    // BaseUrl'in sonuna eklenecek yol. Örnek: /api/records/pending
    [Required]
    public string Endpoint { get; init; } = string.Empty;

    // API bu sürede yanıt vermezse istek zaman aşımına uğrar.
    [Range(1, 600)]
    public int TimeoutSeconds { get; init; } = 30;

    // API'nin kullandığı kimlik doğrulama yöntemine göre ilgili alan doldurulur.
    // Gizli değerleri appsettings.json yerine User Secrets/ortam değişkeninde tutun.
    public string BearerToken { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;

    // API anahtarının gönderileceği HTTP başlığının adı.
    [Required]
    public string ApiKeyHeaderName { get; init; } = "X-API-Key";
}
