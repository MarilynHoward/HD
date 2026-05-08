using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RestaurantPosWpf;

/// <summary>
/// Resolves the terminal's primary IPv4 address for stamping into
/// <c>public.audit_trail.ip_address</c>. The value is cached with a short TTL so roaming laptops /
/// Wi-Fi reconnects are eventually reflected without hitting the NIC table on every audit write.
/// Never throws — returns an empty string when no suitable NIC is found.
/// <para>
/// Selection rules (first match wins):
/// </para>
/// <list type="number">
///   <item><description>Interface is <see cref="OperationalStatus.Up"/> and not loopback / tunnel.</description></item>
///   <item><description>Ethernet and Wi-Fi are preferred over everything else.</description></item>
///   <item><description>IPv4 unicast address must not be <c>127.*</c> (loopback) or <c>169.254.*</c> (APIPA link-local).</description></item>
/// </list>
/// </summary>
public static class NetworkIdentity
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private static readonly object Gate = new();
    private static string _cached = "";
    private static DateTime _cachedAtUtc = DateTime.MinValue;

    /// <summary>
    /// Returns the terminal's primary IPv4 address in dotted-quad form (e.g. <c>"10.1.2.17"</c>),
    /// or an empty string when no usable adapter can be determined.
    /// </summary>
    public static string GetLocalIpv4()
    {
        lock (Gate)
        {
            if (_cached.Length > 0 && DateTime.UtcNow - _cachedAtUtc < CacheDuration)
                return _cached;

            _cached = ResolvePrimaryIpv4();
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
    }

    private static string ResolvePrimaryIpv4()
    {
        try
        {
            NetworkInterface[] all;
            try { all = NetworkInterface.GetAllNetworkInterfaces(); }
            catch (NetworkInformationException) { return ""; }

            // Pass 1: Ethernet / Wi-Fi, fully operational, non-link-local.
            foreach (var nic in all)
            {
                if (!IsCandidate(nic))
                    continue;
                if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.GigabitEthernet)
                    continue;
                var ip = FirstUsableIpv4(nic);
                if (ip.Length > 0)
                    return ip;
            }

            // Pass 2: any other Up non-loopback/tunnel NIC (VPN, mobile broadband, etc.).
            foreach (var nic in all)
            {
                if (!IsCandidate(nic))
                    continue;
                var ip = FirstUsableIpv4(nic);
                if (ip.Length > 0)
                    return ip;
            }
        }
        catch
        {
            // Deliberately swallowed: ip_address is a best-effort diagnostic field.
        }

        return "";
    }

    private static bool IsCandidate(NetworkInterface nic)
    {
        if (nic.OperationalStatus != OperationalStatus.Up)
            return false;
        return nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Loopback => false,
            NetworkInterfaceType.Tunnel => false,
            _ => true
        };
    }

    private static string FirstUsableIpv4(NetworkInterface nic)
    {
        IPInterfaceProperties props;
        try { props = nic.GetIPProperties(); }
        catch (NetworkInformationException) { return ""; }

        foreach (var unicast in props.UnicastAddresses)
        {
            if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                continue;
            var bytes = unicast.Address.GetAddressBytes();
            if (bytes.Length != 4)
                continue;
            // Skip loopback (127.0.0.0/8) and APIPA link-local (169.254.0.0/16).
            if (bytes[0] == 127)
                continue;
            if (bytes[0] == 169 && bytes[1] == 254)
                continue;
            if (IPAddress.IsLoopback(unicast.Address))
                continue;
            return unicast.Address.ToString();
        }
        return "";
    }
}
