namespace ShowVault.Api.Contracts;

public sealed record ApiResponse<T>(
    string Status,
    string Message,
    string CorrelationId,
    string Version,
    DateTimeOffset Timestamp,
    T Payload)
{
    public static ApiResponse<T> Success(T payload, string correlationId, string message = "Request completed") =>
        new("success", message, correlationId, "1.0", DateTimeOffset.UtcNow, payload);
}

public sealed record PlatformStatus(string Name, string Stage);
