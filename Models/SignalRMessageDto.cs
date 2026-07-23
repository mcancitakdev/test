namespace ApiPollingWorker.Models;

// Kaynak API kaydından üretilip SignalR Hub metoduna gönderilen mesaj modeli.
public sealed record SignalRMessageDto(long Id, string Message, DateTimeOffset CreatedDate);
