using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// Stops Server-Side Request Forgery (SSRF). Plannr fetches whatever URL a
/// user shares, from inside our own network, and shows them what came back.
/// Without this, sharing http://169.254.169.254/... would have the server
/// read out its own AWS credentials, and http://10.0.1.5:3306 would probe
/// the database.
///
/// The check runs at connection time, on the address actually being
/// connected to -- not on the URL. A hostname can resolve to a public
/// address when looked at and a private one a moment later (DNS
/// rebinding); checking the address we then connect to leaves no gap. It
/// also covers every redirect hop, since each hop is a new connection.
/// </summary>
public static class PublicAddressGuard
{
    /// <summary>SocketsHttpHandler.ConnectCallback: resolve, refuse anything private, connect to what was checked.</summary>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var endPoint = context.DnsEndPoint;
        var addresses = await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);

        // If any address is private, refuse the host outright rather than
        // picking a public one: a name with both is not an honest website.
        var blocked = addresses.FirstOrDefault(address => !IsPublic(address));
        if (blocked is not null)
        {
            throw new BlockedAddressException(
                $"{endPoint.Host} resolves to {blocked}, which is not a public internet address.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, endPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// False for loopback, private, carrier-grade NAT, link-local (where the
    /// AWS and ECS metadata services live), benchmarking, multicast and
    /// reserved ranges, and their IPv6 equivalents.
    /// </summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            var isPrivate =
                b[0] is 0 or 10 or 127                     // "this network", private, loopback
                || (b[0] == 100 && b[1] is >= 64 and <= 127) // carrier-grade NAT
                || (b[0] == 169 && b[1] == 254)              // link-local: AWS/ECS metadata
                || (b[0] == 172 && b[1] is >= 16 and <= 31)  // private
                || (b[0] == 192 && b[1] == 168)              // private
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)   // IETF protocol assignments
                || (b[0] == 198 && b[1] is 18 or 19)         // benchmarking
                || b[0] >= 224;                              // multicast, reserved, broadcast

            return !isPrivate;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.Equals(IPAddress.IPv6Any)
                || address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal
                || address.IsIPv6Multicast);
        }

        return false;
    }
}
