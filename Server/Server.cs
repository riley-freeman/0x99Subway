using System.Net.WebSockets;
using System.Reflection;
using Crayon.Box.Messages;

namespace Crayon.Box;

public class Server
{
    public static readonly Dictionary<string, Client> Stations = new();

    private readonly WebApplication _app;

    public Server(WebApplication app)
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

    public void Run()
    {
        _app.Run();
    }

    private async Task StationClockIn(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) return;

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Send the Server's signature
        var utf8Signature = Config.StringEncoding.GetBytes(Config.ServerSignature);
        var segment = new ArraySegment<byte>(utf8Signature);
        await webSocket.SendAsync(segment, WebSocketMessageType.Text, true,
            CancellationToken.None);

        // Send over the Server's info
        var serverVersion = Assembly.GetEntryAssembly()!.GetName().Version;
        var serverInfo = new ServerInformation(serverVersion!, Config.Organization);
        var infoMessage = new Message
        {
            Type = MessageType.ServerInfo,
            ServerInfo = serverInfo
        };
        await infoMessage.SendAsync(webSocket);

        // Get the client signature
        var csBuffer = new byte[Config.ClientSignature.Length];
        await webSocket.ReceiveAsync(csBuffer.AsMemory(), CancellationToken.None);
        var clientSignature = Config.StringEncoding.GetString(csBuffer);

        // Receive the client's info
        var ciMessage = (Message)(await Message.ReceiveAsync(webSocket))!;
        var ci = (ClientInformation)ciMessage.ClientInfo!;
        var cv = ci.ClientVersion;
        Console.WriteLine($"Client Version: {cv}");
        Console.WriteLine($"Client Message: {ciMessage}");

        // Simple Validation
        Console.WriteLine("Signature = " + clientSignature);
        Console.WriteLine("Version = " + cv);
        if (clientSignature != Config.ClientSignature)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.InvalidPayloadData,
                "Bad signature.",
                CancellationToken.None
            );
            return;
        }

        if (!IsAcceptableClient(cv))
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.EndpointUnavailable,
                "Version not supported.",
                CancellationToken.None
            );
            return;
        }

        // Create a station object & add it to the dictionary
        var station = new Client(webSocket);
        string? username = null;

        // Poll the station's messages
        while (webSocket.State == WebSocketState.Open)
            // Try to receive data from the websocket
            try
            {
                var message = await Message.ReceiveAsync(webSocket!);
                if (message == null)
                    throw new ArgumentNullException();

                switch (message.Type)
                {
                    case MessageType.PrivilegeElevation:
                        var authorization = (Authorization)message.AuthorizationInfo!;
                        username = authorization.Username;
                        Stations.Add(username, station);
                        
                        // Send back a successful login message
                        var successMessage = new Message { Type = MessageType.AuthSuccessful };
                        await successMessage.SendAsync(webSocket);
                        break;
                    
                    case MessageType.CloseRequest:
                        var closeInfo = (CloseInformation)message.CloseInfo!;
                        closeInfo.Description = Config.ClosingSignature;
                        if (username != null)
                            Stations.Remove(username);

                        var crm = new Message
                        {
                            Type = MessageType.CloseResponse,
                            CloseInfo = closeInfo,
                        };
                        await crm.SendAsync(webSocket);
                        break;
                    
                    case MessageType.CloseResponse:
                        if (username != null)
                            Stations.Remove(username);
                        break;

                    case MessageType.ServerInfo:
                    case MessageType.ClientInfo:
                    case MessageType.AuthSuccessful:
                    case MessageType.AuthFailed:
                    case MessageType.PageRequest:
                    case MessageType.Invalid:
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (WebSocketException e)
            {
                var ipAddress = context.Connection.RemoteIpAddress;
                if (ipAddress != null)
                    Console.WriteLine(
                        $"An error occured attempting to receive a message from '{ipAddress.ToString()}': {e}");

                if (username != null)
                    Stations.Remove(username);
                break;
            }
    }

    private static bool IsAcceptableClient(Version version)
    {
        if (version.Major >= 25) return true;

        return false;
    }
}