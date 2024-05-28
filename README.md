# WebsockifySharp

[![NuGet Version](https://img.shields.io/nuget/v/WebsockifySharp.Websockify?label=Websockify%20NuGet)](https://www.nuget.org/packages/WebsockifySharp.Websockify/) [![NuGet Version](https://img.shields.io/nuget/v/WebsockifySharp.Unwebsockify?label=Unwebsockify%20NuGet)](https://www.nuget.org/packages/WebsockifySharp.Unwebsockify/) ![Build Status](https://github.com/rkttu/WebsockifySharp/actions/workflows/dotnet.yml/badge.svg) [![GitHub Sponsors](https://img.shields.io/github/sponsors/rkttu)](https://github.com/sponsors/rkttu/)

A library that provides the ability to tunnel TCP socket connections to ASP.NET Core WebSockets (Websockify) and add a TCP socket bridge (Unwebsockify) to your local environment.

## Minimum Requirements

- Websockify: Requires a supported LTS version of .NET 6.0, .NET 8.0 as of May 2024. Also, It's ASP.NET middleware, so the Web SDK is a must-have.
  - Supported .NET Version: .NET 6.0, .NET 8.0
- Unwebsockify: Requires a platform with .NET Standard 1.3 or later that can handle receiving and sending connections over TCP sockets.
  - Supported .NET Version: .NET Core 1.0+, .NET 5+, .NET Framework 4.6+, Mono 4.6+, UWP 10.0+, Unity 2018.1+

## Usage

### Websockify

```csharp
// Specify the address and port number of the remote server with an open TCP listening socket that you want to relay the connection to.
var targetAddress = "127.0.0.1";
var targetPort = 8900;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:5000");

var app = builder.Build();

// Add the Websockify middleware to the application pipeline.
app.UseWebsockify("/websockify", targetAddress, targetPort);

await app.RunAsync(cancellationToken).ConfigureAwait(false);
```

### Unwebsockify

```csharp
var remoteWebSocketUrl = new Uri("ws://192.168.1.3:8900/websockify");
var localListenEndPoint = new IPEndPoint(IPAddress.Loopback, 13232);

using (var unwebsockify = new Unwebsockify(remoteWebSocketUrl, localListenEndPoint))
{
	await unwebsockify.StartAsync().ConfigureAwait(false);
}
```

## Source code original author notice

- Websockify for ASP.NET Core: [https://github.com/ProjectMile/Mile.Websockify](https://github.com/ProjectMile/Mile.Websockify)
- Unwebsockify (Python): [https://github.com/jimparis/unwebsockify](https://github.com/jimparis/unwebsockify)


## License

This library follows Apache-2.0 license. See [LICENSE](./LICENSE) file for more information.
