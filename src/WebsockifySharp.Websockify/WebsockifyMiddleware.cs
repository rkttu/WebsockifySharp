using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebsockifySharp.Websockify;

/// <summary>
/// Middleware for handling WebSocket connections and proxying data between WebSocket and TCP.
/// </summary>
public sealed class WebsockifyMiddleware
{
    /// <summary>
    /// The recommended buffer size for reading and writing data.
    /// </summary>
    public const int RecommendedBufferSize = 1024 * 64;

    private readonly RequestDelegate _next;
    private readonly string _hostname;
    private readonly int _tcpPort;
    private readonly int _bufferSizeInBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsockifyMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="hostname">The hostname of the TCP server.</param>
    /// <param name="tcpPort">The TCP port number.</param>
    /// <param name="bufferSizeInBytes">The buffer size in bytes.</param>
    public WebsockifyMiddleware(
        RequestDelegate next,
        string hostname,
        int tcpPort,
        int bufferSizeInBytes = RecommendedBufferSize)
    {
        if (next == null)
            throw new ArgumentNullException(nameof(next));
        if (hostname == null)
            throw new ArgumentNullException(nameof(hostname));
        if (tcpPort < IPEndPoint.MinPort || tcpPort > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(tcpPort), tcpPort, "The TCP port number is invalid.");
        if (bufferSizeInBytes < 1024)
            bufferSizeInBytes = RecommendedBufferSize;

        _next = next;
        _hostname = hostname;
        _tcpPort = tcpPort;
        _bufferSizeInBytes = bufferSizeInBytes;
    }

    private static async Task ReceiveTask(
        NetworkStream networkStream, WebSocket webSocket, int bufferSize,
        ILogger logger, CancellationToken cancellationToken)
    {
        var read = 0;
        var buffer = new byte[bufferSize];

        try
        {
            while (true)
            {
                read = await networkStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, read),
                    WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception thrownException)
        {
            logger.LogError(thrownException, $"{nameof(ReceiveTask)} method interrupted due to exception.");
        }
    }

    private static async Task SendTask(
        WebSocket webSocket, NetworkStream networkStream, TcpClient tcpClient,
        int bufferSize, ILogger logger, CancellationToken cancellationToken)
    {
        var read = default(WebSocketReceiveResult);
        var buffer = new byte[bufferSize];

        try
        {
            while (true)
            {
                read = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken).ConfigureAwait(false);

                if (read.CloseStatus.HasValue)
                    break;

                await networkStream.WriteAsync(buffer, 0, read.Count, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception thrownException)
        {
            logger.LogError(thrownException, $"{nameof(SendTask)} method interrupted due to exception.");
        }

        var lastRead = read;
        await networkStream.DisposeAsync().ConfigureAwait(false);
        tcpClient.Close();

        await webSocket.CloseAsync(
            lastRead?.CloseStatus ?? default,
            lastRead?.CloseStatusDescription ?? string.Empty,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var logger = context.RequestServices.GetRequiredService<ILogger<WebsockifyMiddleware>>();

        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(_hostname, _tcpPort, cancellationToken).ConfigureAwait(false);

            var networkStream = tcpClient.GetStream();
            var receiveTask = ReceiveTask(networkStream, webSocket, _bufferSizeInBytes, logger, cancellationToken);
            var sendTask = SendTask(webSocket, networkStream, tcpClient, _bufferSizeInBytes, logger, cancellationToken);
            await Task.WhenAll(receiveTask, sendTask).ConfigureAwait(false);
        }
        else
            context.Response.StatusCode = 400;

        await _next(context);
    }
}
