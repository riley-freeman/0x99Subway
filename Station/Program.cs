using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Core;

namespace Station;

public class Station : IDisposable
{
    private const short TunnelPort = 9989;
    private const string TunnelAddress = "localhost";

    private const int MaxConnectionRetryTimeout = 60;
    private const int ConnectionRetryTimeoutStep = 5;

    private const string Signature = "_X99_SUBWAY_STATION_";

    private readonly ClientWebSocket? _cws;

    private readonly WebSocket? _socket;


    public Station(WebSocket webSocket)
    {
        _socket = webSocket;
    }

    private Station(Uri uri, string? username, string? password)
    {
        _cws = ConnectSocket(uri, MaxConnectionRetryTimeout, ConnectionRetryTimeoutStep);

        _ = Task.Run(async () =>
        {
            var sBuffer = new List<byte>(Encoding.UTF8.GetBytes(Signature));

            var version = typeof(Station).Assembly.GetName().Version!;
            sBuffer.AddRange(BitConverter.GetBytes(version.Major));
            sBuffer.AddRange(BitConverter.GetBytes(version.Minor));
            sBuffer.AddRange(BitConverter.GetBytes(version.Build));
            sBuffer.AddRange(BitConverter.GetBytes(version.Revision));

            var u = username ?? ReadUsername();
            var p = password ?? ReadPassword();
            var login = $"{u}:{p}";
            var cBuffer = new List<byte>(Encoding.UTF8.GetBytes(login));

            await _cws.SendAsync(
                new ArraySegment<byte>(sBuffer.ToArray()),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None
            );

            await _cws.SendAsync(
                new ArraySegment<byte>(cBuffer.ToArray()),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Station() => Dispose(false);

    private void Dispose(bool disposing)
    {
        _socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
        _cws?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
        
        _socket?.Dispose();
        _cws?.Dispose();
    }
    

    private async Task Run()
    {
        var buffer = new byte[4096];
        if (_cws is null) throw new ArgumentNullException();
        while (_cws.State == WebSocketState.Open)
        {
            var result = await _cws.ReceiveAsync(buffer, CancellationToken.None);
            switch (result.MessageType)
            {
                case WebSocketMessageType.Text:
                    var encodedText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonSerializer.Deserialize<Message>(encodedText);
                    if (message == null) throw new ArgumentException();

                    switch (message.type)
                    {
                        case MessageType.LoginSuccessful:
                            Console.WriteLine("Successfully logged in to the network!");
                            break;

                        case MessageType.LoginFailed:
                            Console.WriteLine(
                                "Authentication has either failed or been revoked! Please restart this process!");
                            break;

                        case MessageType.PageRequest:
                        case MessageType.Invalid:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;

                case WebSocketMessageType.Close:
                    await _cws.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        string.Empty,
                        CancellationToken.None
                    );
                    _cws.Dispose();
                    break;

                case WebSocketMessageType.Binary:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }


    private static void Main(string[] args)
    {
        var uri = new Uri($"ws://{TunnelAddress}:{TunnelPort}/station-clock-in");
        var station = new Station(uri, null, null);
        station.Run().Wait();
    }

    // Retries connecting to a tunnel over and over.
    private static ClientWebSocket ConnectSocket(Uri uri, int maxTimeout, int step)
    {
        var timeout = step;
        var trying = true;
        ClientWebSocket? socket = null;
        while (trying)
            try
            {
                Console.WriteLine($"Connecting to '{uri}'");
                socket = new ClientWebSocket();
                socket.ConnectAsync(uri, CancellationToken.None).Wait();
                trying = false;
            }
            catch
            {
                Console.Error.WriteLine($"Failed connecting to '{uri}', trying again in {timeout} seconds");
                Thread.Sleep(TimeSpan.FromSeconds(timeout));

                // Increment the timeout
                timeout += step;
                if (timeout > maxTimeout) timeout = maxTimeout;
            }

        return socket!;
    }

    private static string ReadUsername()
    {
        while (true)
        {
            Console.Write("Please enter your username: ");
            var username = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(username) && username.Length is >= 1 and <= 64)
                return username;

            Console.WriteLine("Sorry, something went wrong!");
        }
    }

    private static string ReadPassword()
    {
        while (true)
        {
            Console.Write("Please enter your password: ");
            var password = ReadPasswordString();
            if (password.Length is >= 1 and <= 24) return password;
            Console.WriteLine("Sorry, something went wrong!");
        }
    }

    // Reads password from Console with optional masking character.
    // If mask == '\0' then nothing is shown (no masking chars), characters are hidden entirely.
    private static string ReadPasswordString(char mask = '*')
    {
        var pwd = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
                break;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (pwd.Length <= 0) continue;
                // remove last character from the buffer
                pwd.Length--;

                // remove last mask character from console if we're showing masks
                if (mask != '\0')
                    // move cursor back, write space, move back again
                    Console.Write("\b \b");
                // else ignore backspace when nothing to delete
            }
            else if (!char.IsControl(key.KeyChar))
            {
                pwd.Append(key.KeyChar);
                if (mask != '\0') Console.Write(mask);
            }
            // ignore other control keys (arrows, etc.)
        }

        // finish the line so cursor moves to next line if caller wants
        Console.WriteLine();

        return pwd.ToString();
    }
}