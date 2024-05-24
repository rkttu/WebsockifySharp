using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebsockifySharp.Unwebsockfiy
{
    using AcceptExceptionHandler = Func<Exception, CancellationToken, Task>;
    using PassthroughExceptionHandler = Func<Exception, CancellationToken, Task>;

    /// <summary>
    /// Represents a class for unwebsockifying WebSocket connections and forwarding data to a local TCP endpoint.
    /// </summary>
    public partial class Unwebsockify : IDisposable
    {
        /// <summary>
        /// The recommended local listen backlog.
        /// </summary>
        public const int RecommendedLocalListenBacklog = 128;

        /// <summary>
        /// The recommended receive buffer size.
        /// </summary>
        public const int RecommendedReceiveBufferSize = 1024 * 1024;

        /// <summary>
        /// The recommended send buffer size.
        /// </summary>
        public const int RecommendedSendBufferSize = 1024 * 64;

        /// <summary>
        /// Initializes a new instance of the <see cref="Unwebsockify"/> class.
        /// </summary>
        /// <param name="remoteWebSocketUrl">The remote WebSocket URL.</param>
        /// <param name="localListenEndPoint">The local listen endpoint.</param>
        public Unwebsockify(Uri remoteWebSocketUrl, IPEndPoint localListenEndPoint)
        {
            _remoteWebSocketUrl = remoteWebSocketUrl;
            _localListenEndPoint = localListenEndPoint;
            _localListenerSocket = new Socket(_localListenEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _localListenBacklog = RecommendedLocalListenBacklog;
            _receiveBufferSize = RecommendedReceiveBufferSize;
            _sendBufferSize = RecommendedSendBufferSize;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Unwebsockify"/> class.
        /// </summary>
        ~Unwebsockify()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="Unwebsockify"/> class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="Unwebsockify"/> class.
        /// </summary>
        /// <param name="disposing">A boolean value indicating whether the method is called from the <see cref="System.IDisposable.Dispose"/> method.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed)
                    return;

                if (disposing)
                {
                    // Add the managed resource disposal code here.
                    _cancellationTokenSource?.Cancel();
                    _localListenerTask?.Wait();
                    _localListenerSocket?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }

                // Add the unmanaged resource disposal code here.

                _disposed = true;
            }
        }

        private Uri _remoteWebSocketUrl;
        private IPEndPoint _localListenEndPoint;
        private int _localListenBacklog;
        private int _receiveBufferSize;
        private int _sendBufferSize;
        private AcceptExceptionHandler _acceptExceptionHandler;
        private PassthroughExceptionHandler _passthroughExceptionHandler;

        /// <summary>
        /// Gets or sets the remote WebSocket URL.
        /// </summary>
        public Uri RemoteWebSocketUrl
        {
            get => _remoteWebSocketUrl;
            set => _remoteWebSocketUrl = value;
        }

        /// <summary>
        /// Gets or sets the local listen endpoint.
        /// </summary>
        public IPEndPoint LocalListenEndPoint
        {
            get => _localListenEndPoint;
            set => _localListenEndPoint = value;
        }

        /// <summary>
        /// Gets or sets the local listen backlog.
        /// </summary>
        public int LocalListenBacklog
        {
            get => _localListenBacklog;
            set => _localListenBacklog = Math.Max(value, RecommendedLocalListenBacklog);
        }

        /// <summary>
        /// Gets or sets the receive buffer size.
        /// </summary>
        public int ReceiveBufferSize
        {
            get => _receiveBufferSize;
            set => _receiveBufferSize = Math.Max(value, RecommendedReceiveBufferSize);
        }

        /// <summary>
        /// Gets or sets the send buffer size.
        /// </summary>
        public int SendBufferSize
        {
            get => _sendBufferSize;
            set => _sendBufferSize = Math.Max(value, RecommendedSendBufferSize);
        }

        /// <summary>
        /// Gets or sets the accept exception handler.
        /// </summary>
        public AcceptExceptionHandler AcceptExceptionHandler
        {
            get => _acceptExceptionHandler;
            set => _acceptExceptionHandler = value;
        }

        /// <summary>
        /// Gets or sets the passthrough exception handler.
        /// </summary>
        public PassthroughExceptionHandler PassthroughExceptionHandler
        {
            get => _passthroughExceptionHandler;
            set => _passthroughExceptionHandler = value;
        }

        private bool _disposed;
        private readonly object _disposeLock = new object();

        /// <summary>
        /// Gets a value indicating whether the current instance is disposed.
        /// </summary>
        public bool Disposed => _disposed;

        private Socket _localListenerSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _localListenerTask;

        /// <summary>
        /// Starts the unwebsockify process asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _localListenerSocket.Bind(_localListenEndPoint);
            _localListenerSocket.Listen(_localListenBacklog);

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _localListenerTask = Task.Run(() => ListenAsync(
                _localListenerSocket, _receiveBufferSize, _sendBufferSize, _remoteWebSocketUrl,
                _acceptExceptionHandler, _passthroughExceptionHandler, _cancellationTokenSource.Token));
            await _localListenerTask.ConfigureAwait(false);
        }

        private static async Task ListenAsync(
            Socket localListenerSocket, int receiveBufferSize, int sendBufferSize, Uri remoteWebSocketUrl,
            AcceptExceptionHandler acceptExceptionHandler,
            PassthroughExceptionHandler passthroughExceptionHandler,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var clientSocket = await localListenerSocket.AcceptAsync().ConfigureAwait(false);
                    _ = PassthroughTransmissionAsync(clientSocket, receiveBufferSize, sendBufferSize, remoteWebSocketUrl, passthroughExceptionHandler, cancellationToken);
                }
            }
            catch (OperationCanceledException) { /* Simply ignore; Expected exception when cancellation is requested */ }
            catch (Exception ex)
            {
                if (acceptExceptionHandler != null)
                    await acceptExceptionHandler.Invoke(ex, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task PassthroughTransmissionAsync(
            Socket clientSocket, int receiveBufferSize, int sendBufferSize, Uri remoteWebSocketUrl,
            PassthroughExceptionHandler passthroughExceptionHandler, CancellationToken cancellationToken)
        {
            using (clientSocket)
            {
                var sendBuffer = new byte[sendBufferSize];

                using (var remoteWebSocket = new ClientWebSocket())
                {
                    try
                    {
                        await remoteWebSocket.ConnectAsync(remoteWebSocketUrl, cancellationToken).ConfigureAwait(false);
                        var wsToTcpTask = PassthroughWebSocketToTcpAsync(remoteWebSocket, receiveBufferSize, clientSocket, cancellationToken);
                        var tcpToWsTask = PassthroughTcpToWebSocketAsync(remoteWebSocket, clientSocket, sendBuffer, cancellationToken);
                        await Task.WhenAny(wsToTcpTask, tcpToWsTask).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (passthroughExceptionHandler != null)
                            await passthroughExceptionHandler.Invoke(ex, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task PassthroughWebSocketToTcpAsync(WebSocket remoteWebSocket, int receiveBufferSize, Socket client, CancellationToken cancellationToken)
        {
            var buffer = new byte[receiveBufferSize];
            while (remoteWebSocket.State == WebSocketState.Open)
            {
                var result = await remoteWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                await client.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), SocketFlags.None).ConfigureAwait(false);
            }
        }

        private static async Task PassthroughTcpToWebSocketAsync(WebSocket remoteWebSocket, Socket clientSocket, byte[] buffer, CancellationToken cancellationToken)
        {
            int read;
            while ((read = await clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None).ConfigureAwait(false)) > 0)
                await remoteWebSocket.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            await remoteWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
        }
    }

#if NETSTANDARD2_0_OR_GREATER
    partial class Unwebsockify : IAsyncDisposable
    {
        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="Unwebsockify"/> class asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="Unwebsockify"/> class and optionally releases the managed resources.
        /// </summary>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            bool alreadyDisposed;

            lock (_disposeLock)
            {
                alreadyDisposed = _disposed;
                _disposed = true;
            }

            if (alreadyDisposed)
                return;

            // Add the managed resource disposal (async) code here.
            _cancellationTokenSource?.Cancel();

            if (_localListenerTask != null)
            {
                try { await _localListenerTask.ConfigureAwait(false); }
                catch { }
            }

            _localListenerSocket?.Dispose();
            _cancellationTokenSource?.Dispose();

            await Task.CompletedTask;

            // Add the unmanaged resource disposal code here.
        }
    }
#endif // NETSTANDARD2_0_OR_GREATER
}
