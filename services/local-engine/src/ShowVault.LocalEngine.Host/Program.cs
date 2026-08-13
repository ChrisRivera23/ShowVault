using System.Text.Json;
using ShowVault.LocalEngine;

return await LocalEngineHost.RunAsync();

internal static class LocalEngineHost
{
    private const int MaximumRequestCharacters = 32_768;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync()
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            var line = await Console.In.ReadLineAsync(cancellation.Token);
            if (line is null || line.Length is < 2 or > MaximumRequestCharacters)
            {
                return await ErrorAsync("invalid_request");
            }

            HostRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<HostRequest>(line, JsonOptions);
            }
            catch (JsonException)
            {
                return await ErrorAsync("invalid_request");
            }

            if (request is null)
            {
                return await ErrorAsync("invalid_request");
            }

            var engine = new LocalRecoveryEngine();
            switch (request.Operation)
            {
                case "save" when Valid(request.CandidateKey) &&
                                      ValidPath(request.SelectedSource) &&
                                      ValidPath(request.SelectedVault):
                    {
                        using var saveCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellation.Token);
                        var cancelMonitor = MonitorCancelAsync(saveCancellation);
                        var progress = new SynchronousProgress<LocalSaveProgress>(value =>
                            Write(new HostEnvelope("progress", value, null)));
                        try
                        {
                            var result = await engine.SaveAsync(
                                new(request.CandidateKey!, request.SelectedSource!, request.SelectedVault!),
                                progress,
                                saveCancellation.Token);
                            Write(new HostEnvelope("result", result, null));
                            return 0;
                        }
                        finally
                        {
                            saveCancellation.Cancel();
                            try { await cancelMonitor; } catch (OperationCanceledException) { }
                        }
                    }
                case "inspect" when ValidPath(request.SelectedVault):
                    var summaries = await engine.InspectVaultStateAsync(
                        request.SelectedVault!, cancellation.Token);
                    Write(new HostEnvelope("result", summaries, null));
                    return 0;
                default:
                    return await ErrorAsync("unsupported_operation");
            }
        }
        catch (OperationCanceledException)
        {
            return await ErrorAsync("cancelled");
        }
        catch (LocalEngineException)
        {
            return await ErrorAsync("save_rejected");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return await ErrorAsync("local_io_failed");
        }
    }

    private static bool Valid(string? value) =>
        value is { Length: > 0 and <= 256 } && !string.IsNullOrWhiteSpace(value);

    private static bool ValidPath(string? value) =>
        value is { Length: > 0 and <= 8_192 } && !string.IsNullOrWhiteSpace(value) &&
        Path.IsPathFullyQualified(value) && value.IndexOf('\0') < 0;

    private static async Task<int> ErrorAsync(string code)
    {
        Write(new HostEnvelope("error", null, code));
        await Console.Out.FlushAsync();
        return 2;
    }

    private static async Task MonitorCancelAsync(CancellationTokenSource cancellation)
    {
        var line = await Console.In.ReadLineAsync(cancellation.Token);
        if (line is null || line.Length > MaximumRequestCharacters)
        {
            return;
        }
        try
        {
            var request = JsonSerializer.Deserialize<HostRequest>(line, JsonOptions);
            if (request?.Operation == "cancel" && request.CandidateKey is null &&
                request.SelectedSource is null && request.SelectedVault is null)
            {
                cancellation.Cancel();
            }
        }
        catch (JsonException)
        {
            // A malformed follow-up is ignored; the closed Save continues safely.
        }
    }

    private static void Write(HostEnvelope envelope)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(envelope, JsonOptions));
        Console.Out.Flush();
    }

    private sealed record HostRequest(
        string? Operation,
        string? CandidateKey,
        string? SelectedSource,
        string? SelectedVault);

    private sealed record HostEnvelope(string Type, object? Payload, string? Code);

    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
