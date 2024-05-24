using WebsockifySharp.Websockify;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Contains extension methods to be added to ASP.NET Core.
/// </summary>
public static class WebsockifyMiddlewareExtensions
{
    /// <summary>
    /// Adds the Websockify middleware to the request pipeline.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
    /// <param name="pathMatch">The request path to match.</param>
    /// <param name="hostName">The hostname of the target server.</param>
    /// <param name="tcpPort">The TCP port of the target server.</param>
    /// <param name="bufferSizeBytes">The buffer size in bytes (optional).</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseWebsockify(
        this IApplicationBuilder app,
        PathString pathMatch,
        string hostName,
        int tcpPort,
        int bufferSizeBytes = WebsockifyMiddleware.RecommendedBufferSize)
        => app.UseWebSockets().Map(pathMatch, a => a.UseMiddleware<WebsockifyMiddleware>(hostName, tcpPort, bufferSizeBytes));
}
