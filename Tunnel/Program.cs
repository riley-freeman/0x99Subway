using System.Net.WebSockets;
using System.Text;
using Core;

namespace Tunnel;

public class Tunnel
{
    private const string Signature = "_X99_SUBWAY_TUNNEL_";
    private const string ExpectedSignature = "_X99_SUBWAY_STATION_";

    public static readonly Dictionary<string, Station.Station> Stations = new();

    private readonly WebApplication _app;

    private Tunnel(WebApplication app)
    {
        _app = app;
        if (app.Environment.IsDevelopment()) app.MapOpenApi();


        // Maps a user's request to a station
        // app.MapGet("/@{id}/{*rest}",
        //     (string id, string rest) => $"Attempting to get '/{rest}' from @{id}" +
        //                                 (Stations.ContainsKey(id) ? 
        //                                     "Miraculously, the station is connected!" : 
        //                                     "Tragically, it was not around"));

        // Begins a station connection
        app.Map("/station-clock-in", StationClockIn);

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseWebSockets();
        // app.UseAuthentication();

        app.MapControllers();
        app.MapRazorPages();
        app.MapBlazorHub();
    }

    private void Run()
    {
        _app.Run();
    }

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddOpenApi();
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        var app = builder.Build();
        var tunnel = new Tunnel(app);
        tunnel.Run();
    }

    private async Task StationClockIn(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) return;

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Send the Tunnel's signature
        var utf8Signature = Encoding.UTF8.GetBytes(Signature);
        var segment = new ArraySegment<byte>(utf8Signature);
        await webSocket.SendAsync(segment, WebSocketMessageType.Text, true,
            CancellationToken.None);
        
        // Get the client info
        var fBuffer = new byte[512];
        await webSocket.ReceiveAsync(fBuffer.AsMemory(), CancellationToken.None);

        var signature = Encoding.UTF8.GetString(fBuffer[..ExpectedSignature.Length]);

        // Receive the client's version
        var versionBytes = fBuffer[ExpectedSignature.Length..];
        var major = BitConverter.ToInt32(versionBytes, 0);
        var minor = BitConverter.ToInt32(versionBytes, 4);
        var build = BitConverter.ToInt32(versionBytes, 8);
        var patch = BitConverter.ToInt32(versionBytes, 12);
        var cv = new Version(major, minor, build, patch);

        // Simple Validation
        Console.WriteLine("Signature = " + signature);
        Console.WriteLine("Version = " + cv);
        if (signature != ExpectedSignature)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.InvalidPayloadData,
                "Bad signature.",
                CancellationToken.None
            );
            return;
        }

        if (cv != new Version(25, 0, 0, 0))
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.EndpointUnavailable,
                "Version not supported.",
                CancellationToken.None
            );
            return;
        }

        // Receive the first message from the client, which should be authentication information
        // Authentication should look like "username:password" with
        // username 24 alphanumeric characters & password 24 'typable' ones
        var authentication = new byte[49]; // 49 to account for worst case & ':'
        await webSocket.ReceiveAsync(authentication.AsMemory(), CancellationToken.None);

        var authString = Encoding.UTF8.GetString(authentication);
        var username = authString.Substring(0, authString.IndexOf(':')).ToLower();
        var password = authString.Substring(authString.IndexOf(':') + 1);
        Console.WriteLine("Username = " + username);
        Console.WriteLine("Password = " + password);

        // TODO actually authenticate

        // Write to the station they logged in successfully
        var loginMessage = new Message
        {
            type = MessageType.LoginSuccessful
        };
        await webSocket.SendAsync(loginMessage.ToArraySegment(), WebSocketMessageType.Text, true,
            CancellationToken.None);

        // Create a station object & add it to the dictionary
        var station = new Station.Station(webSocket);
        Stations.Add(username, station);

        // Poll the station's messages
        while (webSocket.State == WebSocketState.Open)
        {
            var buffer = new byte[4096];

            // Try to receive data from the websocket
            ValueWebSocketReceiveResult result;
            try
            {
                result = await webSocket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);
            }
            catch (WebSocketException)
            {
                Stations.Remove(username);
                break;
            }

            switch (result.MessageType)
            {
                case WebSocketMessageType.Text:
                    var message = Message.FromBytes(buffer);
                    if (message == null) throw new ArgumentException();

                    switch (message.type)
                    {
                        case MessageType.LoginSuccessful:
                        case MessageType.LoginFailed:
                        case MessageType.PageRequest:
                        case MessageType.Invalid:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;

                case WebSocketMessageType.Close:
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty,
                        CancellationToken.None);
                    break;

                case WebSocketMessageType.Binary:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}