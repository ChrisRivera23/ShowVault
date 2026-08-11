using System.Text;
using System.Text.Json;

namespace ShowVault.AgentContracts;

public static class AgentEventValidation
{
    public const int MaxCorrelationIdLength = 100;
    public const int MaxPayloadUtf8Bytes = 64 * 1024;

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32
    };

    public static bool TryValidate(AgentEventEnvelope envelope, out string error)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.EventId == Guid.Empty)
        {
            error = "Event ID is required.";
            return false;
        }

        if (envelope.AgentId == Guid.Empty)
        {
            error = "Agent ID is required.";
            return false;
        }

        if (!Enum.IsDefined(envelope.Type))
        {
            error = "Event type is unsupported.";
            return false;
        }

        if (!string.Equals(
                envelope.ProtocolVersion,
                AgentProtocol.Version,
                StringComparison.Ordinal))
        {
            error = "Protocol version is unsupported.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId) ||
            envelope.CorrelationId.Length > MaxCorrelationIdLength)
        {
            error = $"Correlation ID must contain 1 to {MaxCorrelationIdLength} characters.";
            return false;
        }

        if (envelope.Payload is null ||
            Encoding.UTF8.GetByteCount(envelope.Payload) > MaxPayloadUtf8Bytes)
        {
            error = $"Payload must not exceed {MaxPayloadUtf8Bytes} UTF-8 bytes.";
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(envelope.Payload, JsonOptions);
        }
        catch (JsonException)
        {
            error = "Payload must be valid JSON with a maximum depth of 32.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
