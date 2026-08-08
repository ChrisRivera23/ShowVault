using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ShowVault.Agent.Plugins;

public sealed record LocalInterfaceAddress(
    string InterfaceName,
    string InterfaceDescription,
    NetworkInterfaceType InterfaceType,
    OperationalStatus OperationalStatus,
    IPAddress Address,
    IPAddress SubnetMask);

public sealed record LocalSubnetProposal(
    string Network,
    int PrefixLength,
    string InterfaceType,
    string Evidence,
    bool RequiresOperatorApproval);

public interface ILocalInterfaceProvider
{
    IReadOnlyList<LocalInterfaceAddress> GetAddresses();
}

public sealed class LocalInterfaceProvider : ILocalInterfaceProvider
{
    private const int MaximumInterfaceCount = 128;
    private const int MaximumAddressCountPerInterface = 32;

    public IReadOnlyList<LocalInterfaceAddress> GetAddresses()
    {
        var results = new List<LocalInterfaceAddress>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces()
                     .Take(MaximumInterfaceCount))
        {
            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            foreach (var address in properties.UnicastAddresses.Take(MaximumAddressCountPerInterface))
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork ||
                    address.IPv4Mask is null)
                {
                    continue;
                }

                results.Add(new LocalInterfaceAddress(
                    networkInterface.Name,
                    networkInterface.Description,
                    networkInterface.NetworkInterfaceType,
                    networkInterface.OperationalStatus,
                    address.Address,
                    address.IPv4Mask));
            }
        }

        return results;
    }
}

public sealed class LocalSubnetProposalDiscovery(ILocalInterfaceProvider interfaceProvider)
{
    public const int MaximumProposalCount = 8;
    public const int MinimumPrefixLength = 24;
    public const int MaximumPrefixLength = 30;

    private static readonly string[] ExcludedInterfaceMarkers =
    [
        "bridge", "docker", "hyper-v", "loopback", "parallels", "tailscale", "tunnel",
        "utun", "virtual", "vmnet", "vmware", "vpn", "vbox", "awdl", "llw"
    ];

    public IReadOnlyList<LocalSubnetProposal> Discover()
    {
        var proposals = new List<LocalSubnetProposal>();
        var seenNetworks = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in interfaceProvider.GetAddresses())
        {
            if (proposals.Count == MaximumProposalCount)
            {
                break;
            }

            if (!IsEligibleInterface(item) || !IsPrivateUnicast(item.Address) ||
                !TryGetPrefixLength(item.SubnetMask, out var assignedPrefix) ||
                assignedPrefix > MaximumPrefixLength ||
                !IsUsableHostAddress(item.Address, assignedPrefix))
            {
                continue;
            }

            var proposedPrefix = Math.Max(assignedPrefix, MinimumPrefixLength);
            var network = ApplyPrefix(item.Address, proposedPrefix);
            var cidr = $"{network}/{proposedPrefix}";
            if (!seenNetworks.Add(cidr))
            {
                continue;
            }

            var boundedReason = assignedPrefix < proposedPrefix
                ? $"; narrowed from the directly assigned /{assignedPrefix} network to /{proposedPrefix}"
                : string.Empty;
            proposals.Add(new LocalSubnetProposal(
                network.ToString(),
                proposedPrefix,
                item.InterfaceType.ToString(),
                $"Active {DescribeInterface(item.InterfaceType)} interface has a private IPv4 address with a usable subnet mask{boundedReason}; no hosts were contacted",
                true));
        }

        return proposals;
    }

    private static bool IsEligibleInterface(LocalInterfaceAddress item)
    {
        if (item.OperationalStatus != OperationalStatus.Up ||
            item.InterfaceType is NetworkInterfaceType.Loopback or
                NetworkInterfaceType.Tunnel or
                NetworkInterfaceType.Ppp)
        {
            return false;
        }

        if (item.InterfaceType is not (NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.Ethernet3Megabit or
            NetworkInterfaceType.FastEthernetFx or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.Wireless80211))
        {
            return false;
        }

        var identity = $"{item.InterfaceName} {item.InterfaceDescription}";
        return !ExcludedInterfaceMarkers.Any(marker =>
            identity.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivateUnicast(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 &&
            (bytes[0] == 10 ||
             (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
             (bytes[0] == 192 && bytes[1] == 168));
    }

    private static bool TryGetPrefixLength(IPAddress mask, out int prefixLength)
    {
        prefixLength = 0;
        var sawZero = false;
        foreach (var value in mask.GetAddressBytes())
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                var isSet = (value & (1 << bit)) != 0;
                if (sawZero && isSet)
                {
                    return false;
                }

                if (isSet)
                {
                    prefixLength++;
                }
                else
                {
                    sawZero = true;
                }
            }
        }

        return prefixLength is >= 1 and <= 32;
    }

    private static IPAddress ApplyPrefix(IPAddress address, int prefixLength)
    {
        var bytes = address.GetAddressBytes();
        for (var index = 0; index < bytes.Length; index++)
        {
            var remainingBits = prefixLength - (index * 8);
            var mask = remainingBits >= 8 ? 0xff : remainingBits <= 0 ? 0 : 0xff << (8 - remainingBits);
            bytes[index] = (byte)(bytes[index] & mask);
        }

        return new IPAddress(bytes);
    }

    private static bool IsUsableHostAddress(IPAddress address, int prefixLength)
    {
        var addressValue = ReadUInt32(address);
        var hostMask = uint.MaxValue >> prefixLength;
        var hostValue = addressValue & hostMask;
        return hostValue != 0 && hostValue != hostMask;
    }

    private static uint ReadUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) |
               ((uint)bytes[1] << 16) |
               ((uint)bytes[2] << 8) |
               bytes[3];
    }

    private static string DescribeInterface(NetworkInterfaceType type) =>
        type == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Ethernet";
}
