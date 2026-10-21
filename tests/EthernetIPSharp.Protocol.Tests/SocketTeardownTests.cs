using System.Net;
using EthernetIPSharp.Protocol;

namespace EthernetIPSharp.Protocol.Tests;

/// <summary>
/// Regression tests for the socket teardown deadlock: on Unix, disposing a
/// socket while a thread is blocked in a synchronous receive/accept hangs
/// forever inside SafeSocketHandle.CloseAsIs. The loops now use cancellable
/// async operations, so DisposeAsync must complete promptly on every platform.
/// </summary>
public class SocketTeardownTests
{
    // Generous bound for a healthy dispose (normally completes in < 100 ms);
    // a regression fails fast instead of hanging the test host.
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task UdpSocket_DisposeAsync_WhileReceivePending_Completes()
    {
        var socket = new UdpSocket();
        await socket.StartAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

        // Dispose on a pool thread so a synchronous deadlock inside
        // DisposeAsync can't block this test's own watchdog.
        var dispose = Task.Run(() => socket.DisposeAsync().AsTask());
        var completed = await Task.WhenAny(dispose, Task.Delay(DisposeTimeout));

        Assert.Same(dispose, completed);
        await dispose;
    }

    [Fact]
    public async Task TcpSocket_DisposeAsync_WhileAcceptPending_Completes()
    {
        var socket = new TcpSocket();
        await socket.StartAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

        var dispose = Task.Run(() => socket.DisposeAsync().AsTask());
        var completed = await Task.WhenAny(dispose, Task.Delay(DisposeTimeout));

        Assert.Same(dispose, completed);
        await dispose;
    }
}
