using System.Text.Json.Serialization;

namespace ApiPollingWorker.Models;

// Kaynak REST API'den gelen bir JSON kaydını temsil eder.
// JsonPropertyName, JSON alan adlarını C# özellikleriyle eşleştirir.
public sealed record ApiRecordDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("createdDate")] DateTimeOffset CreatedDate);
