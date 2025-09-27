using System.Net.WebSockets;
using System.Text;

namespace Tunnel;
public class Tunnel
{
    private const string ExpectedSignature = "_X99_SUBWAY_STATION_";
    
    private readonly WebApplication _app;
    private Tunnel(WebApplication app)
    {
        _app = app;
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }
        
        app.MapGet("/", () => "Hello World!")
            .WithName("Landing Page");
        
        // Maps a user's request to a station
        app.MapGet("/@{id}", (string id) => $"Station ID: {id}");
        
        // Begins a station connection
        app.Map("/station_clock_in", StationClockIn);

        app.UseHttpsRedirection();
        app.UseWebSockets();
    }

    private void Run()
    {
        _app.Run();
    }
    
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddOpenApi();
        
        var app = builder.Build();
        var tunnel = new Tunnel(app);
        tunnel.Run();
    }

    private static async Task StationClockIn(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) return;

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Get the client info
        var fBuffer = new byte[512];
        await webSocket.ReceiveAsync(fBuffer.AsMemory(), CancellationToken.None);

        var signature = Encoding.UTF8.GetString(fBuffer[0..ExpectedSignature.Length]);

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
        var message = Encoding.UTF8.GetBytes("LOGIN SUCCESSFUL");
        await webSocket.SendAsync(message, WebSocketMessageType.Text, true, CancellationToken.None);

        // Close the websocket
        await webSocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "We are closing due to nothing being implemented yet.",
            CancellationToken.None
        );
    }
}





