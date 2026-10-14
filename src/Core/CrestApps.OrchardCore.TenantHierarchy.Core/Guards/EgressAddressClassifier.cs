using System.Net;
using System.Net.Sockets;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Classifies IP addresses for the egress guard.
/// </summary>
internal static class EgressAddressClassifier
{
    /// <summary>
    /// Returns whether an address is a loopback, link-local, private, shared, multicast or unspecified address that an
    /// outbound request from a tenant must not reach.
    /// </summary>
    /// <param name="address">The address.</param>
    public static bool IsInternal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];

            return first == 0 ||
                first == 10 ||
                first == 127 ||
                (first == 100 && second >= 64 && second <= 127) ||
                (first == 169 && second == 254) ||
                (first == 172 && second >= 16 && second <= 31) ||
                (first == 192 && second == 168) ||
                (first == 192 && second == 0 && bytes[2] == 0) ||
                (first == 198 && (second == 18 || second == 19)) ||
                first >= 224;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal ||
                address.IsIPv6Multicast ||
                address.IsIPv6UniqueLocal)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();

            // 64:ff9b::/96 translates to IPv4, so the embedded address decides.
            if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b)
            {
                var embedded = new IPAddress(new[] { bytes[12], bytes[13], bytes[14], bytes[15] });

                return IsInternal(embedded);
            }

            return false;
        }

        return true;
    }
}
