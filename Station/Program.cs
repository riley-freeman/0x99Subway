using System.Net.WebSockets;
using System.Reflection;
using System.Text;

[assembly:AssemblyVersion("25.0.0.0")]
class Station
{
    private const short TUNNEL_PORT = 9989;
    private const string TUNNEL_ADDRESS = "localhost";

    private const string SIGNATURE = "_X99_SUBWAY_STATION_";

    private ClientWebSocket webSocket;
    
    private Station(Uri uri, string username, string password)
    {
        webSocket = new ClientWebSocket();
        webSocket.ConnectAsync(uri, CancellationToken.None).Wait();
        
        _ = Task.Run(async () =>
        {
            var sBuffer = new List<byte>(Encoding.UTF8.GetBytes(SIGNATURE));

            var version = typeof(Station).Assembly.GetName().Version!;
            sBuffer.AddRange(BitConverter.GetBytes(version.Major));
            sBuffer.AddRange(BitConverter.GetBytes(version.Minor));
            sBuffer.AddRange(BitConverter.GetBytes(version.Build));
            sBuffer.AddRange(BitConverter.GetBytes(version.Revision));

            var login = $"{username}:{password}";
            var cBuffer = new List<byte>(Encoding.UTF8.GetBytes(login));

            await webSocket.SendAsync(
                new ArraySegment<byte>(sBuffer.ToArray()),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None
            );

            await webSocket.SendAsync(
                new ArraySegment<byte>(cBuffer.ToArray()),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        });
    }

    private async Task Run()
    {
        var buffer = new byte[4096];
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                
            }
        }
    }
    
    
    static void Main(string[] args)
    {
        // Create a Uri
        var uri = new Uri($"ws://{TUNNEL_ADDRESS}:{TUNNEL_PORT}");
        
        // Get the user's credentials
        Console.Write("Please enter your Box username: ");
        var username = Console.ReadLine()!;
        while (username.Length is < 1 or > 32)
        {
            username = Console.ReadLine()!;
        }
        
        Console.Write("Please enter your Box password: ");
        var password = ReadPassword();

        var station = new Station(uri, username, password);
        station.Run();
    }
    
    // Reads password from Console with optional masking character.
    // If mask == '\0' then nothing is shown (no masking chars), characters are hidden entirely.
    static string ReadPassword(char mask = '*')
    {
        var pwd = new StringBuilder();
        ConsoleKeyInfo key;

        while (true)
        {
            key = Console.ReadKey(intercept: true); // intercept = true => keystroke not displayed automatically

            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (pwd.Length > 0)
                {
                    // remove last character from the buffer
                    pwd.Length--;

                    // remove last mask character from console if we're showing masks
                    if (mask != '\0')
                    {
                        // move cursor back, write space, move back again
                        Console.Write("\b \b");
                    }
                }
                // else ignore backspace when nothing to delete
            }
            else if (!char.IsControl(key.KeyChar))
            {
                pwd.Append(key.KeyChar);
                if (mask != '\0')
                {
                    Console.Write(mask);
                }
            }
            // ignore other control keys (arrows, etc.)
        }

        // finish the line so cursor moves to next line if caller wants
        Console.WriteLine();

        return pwd.ToString();
    }
}
