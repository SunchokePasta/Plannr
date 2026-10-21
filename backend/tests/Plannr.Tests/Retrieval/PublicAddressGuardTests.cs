using System.Net;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Tests.Retrieval;

public sealed class PublicAddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]          // loopback
    [InlineData("10.0.0.5")]           // private
    [InlineData("172.16.0.1")]         // private
    [InlineData("172.31.255.255")]     // private (top of the range)
    [InlineData("192.168.1.1")]        // home router
    [InlineData("169.254.169.254")]    // AWS instance metadata
    [InlineData("169.254.170.2")]      // ECS task metadata
    [InlineData("100.64.0.1")]         // carrier-grade NAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]          // multicast
    [InlineData("::1")]                // IPv6 loopback
    [InlineData("::")]
    [InlineData("fe80::1")]            // IPv6 link-local
    [InlineData("fc00::1")]            // IPv6 unique local
    [InlineData("::ffff:10.0.0.5")]    // private IPv4 dressed as IPv6
    public void IsPublic_RefusesNonPublicAddresses(string address)
    {
        Assert.False(PublicAddressGuard.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.15.0.1")]         // just below the private range
    [InlineData("172.32.0.1")]         // just above it
    [InlineData("2606:4700:4700::1111")]
    public void IsPublic_AllowsPublicAddresses(string address)
    {
        Assert.True(PublicAddressGuard.IsPublic(IPAddress.Parse(address)));
    }

    [Fact]
    public async Task ConnectAsync_RefusesLoopbackBeforeConnecting()
    {
        using var handler = new SocketsHttpHandler { ConnectCallback = PublicAddressGuard.ConnectAsync, UseProxy = false };
        using var client = new HttpClient(handler);

        // Port 1 would refuse the connection anyway; the guard must stop it first.
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:1/"));

        Assert.IsType<BlockedAddressException>(exception.InnerException);
    }
}
