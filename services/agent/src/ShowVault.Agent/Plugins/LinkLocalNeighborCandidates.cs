using System.ComponentModel;
using System.Diagnostics;
using System.Net;

namespace ShowVault.Agent.Plugins;

public interface IArpTableReader
{
    Task<string> ReadAsync(CancellationToken cancellationToken);
}

public sealed class SystemArpTableReader : IArpTableReader
{
    private const int MaximumOutputCharacters = 262_144;

    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(2));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("arp")
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "-a" : "-an");
        var started = false;
        try
        {
            if (!process.Start()) return string.Empty;
            started = true;
            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
            await process.WaitForExitAsync(timeoutSource.Token);
            var output = await outputTask;
            _ = await errorTask;
            return output.Length <= MaximumOutputCharacters
                ? output
                : output[..MaximumOutputCharacters];
        }
        catch (Win32Exception) { return string.Empty; }
        catch (InvalidOperationException) { return string.Empty; }
        catch (IOException) { return string.Empty; }
        catch (UnauthorizedAccessException) { return string.Empty; }
        catch (PlatformNotSupportedException) { return string.Empty; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return string.Empty; }
        finally
        {
            if (started)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
            }
        }
    }
}

public interface ILinkLocalNeighborProvider
{
    Task<IReadOnlyList<IPAddress>> GetCandidatesAsync(
        ApprovedSubnet subnet,
        CancellationToken cancellationToken);
}

public sealed class ArpLinkLocalNeighborProvider(
    ILocalInterfaceProvider interfaceProvider,
    IArpTableReader arpTableReader) : ILinkLocalNeighborProvider
{
    private const int MaximumCandidateCount = 64;

    public async Task<IReadOnlyList<IPAddress>> GetCandidatesAsync(
        ApprovedSubnet subnet,
        CancellationToken cancellationToken)
    {
        if (!IsLinkLocal(subnet.Network) || subnet.PrefixLength != 16 ||
            !LocalSubnetProposalDiscovery.TryGetSingleLinkLocalInterface(
                interfaceProvider.GetAddresses(), out var networkInterface) ||
            !IsInSubnet(networkInterface.Address, subnet.Network, subnet.PrefixLength))
            return [];

        var output = await arpTableReader.ReadAsync(cancellationToken);
        return Parse(output, networkInterface, subnet)
            .Distinct()
            .Take(MaximumCandidateCount)
            .ToArray();
    }

    private static IEnumerable<IPAddress> Parse(
        string output,
        LocalInterfaceAddress networkInterface,
        ApprovedSubnet subnet)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var hasWindowsSections = lines.Any(line =>
            line.TrimStart().StartsWith("Interface:", StringComparison.OrdinalIgnoreCase));
        var matchingWindowsSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (hasWindowsSections && trimmed.StartsWith("Interface:", StringComparison.OrdinalIgnoreCase))
            {
                matchingWindowsSection = TryReadFirstAddress(trimmed, out var sectionAddress) &&
                    sectionAddress.Equals(networkInterface.Address);
                continue;
            }

            if (trimmed.Contains("incomplete", StringComparison.OrdinalIgnoreCase) ||
                (hasWindowsSections && !matchingWindowsSection) ||
                (!hasWindowsSections && !ContainsUnixInterface(trimmed, networkInterface.InterfaceName)) ||
                !TryReadFirstAddress(trimmed, out var candidate) ||
                candidate.Equals(networkInterface.Address) ||
                !IsInSubnet(candidate, subnet.Network, subnet.PrefixLength) ||
                !IsUsableHost(candidate, subnet.PrefixLength))
                continue;
            yield return candidate;
        }
    }

    private static bool ContainsUnixInterface(string line, string interfaceName) =>
        line.Contains($" on {interfaceName} ", StringComparison.Ordinal) ||
        line.EndsWith($" on {interfaceName}", StringComparison.Ordinal);

    private static bool TryReadFirstAddress(string line, out IPAddress address)
    {
        foreach (var token in line.Split(
                     [' ', '\t', '(', ')', ',', ':'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (IPAddress.TryParse(token, out var parsed) && parsed.AddressFamily ==
                System.Net.Sockets.AddressFamily.InterNetwork)
            {
                address = parsed;
                return true;
            }
        }
        address = IPAddress.None;
        return false;
    }

    private static bool IsLinkLocal(string address) =>
        IPAddress.TryParse(address, out var parsed) &&
        parsed.GetAddressBytes() is [169, 254, _, _];

    private static bool IsInSubnet(IPAddress address, string network, int prefixLength)
    {
        var addressValue = ReadUInt32(address);
        var networkValue = ReadUInt32(IPAddress.Parse(network));
        var mask = uint.MaxValue << (32 - prefixLength);
        return (addressValue & mask) == (networkValue & mask);
    }

    private static bool IsUsableHost(IPAddress address, int prefixLength)
    {
        var hostMask = uint.MaxValue >> prefixLength;
        var host = ReadUInt32(address) & hostMask;
        return host != 0 && host != hostMask;
    }

    private static uint ReadUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }
}
