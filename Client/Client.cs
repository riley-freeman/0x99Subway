using System.Net.WebSockets;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using Crayon.Box.Messages;

namespace Crayon.Box;

public class Client : IDisposable
{
    private const int MaxConnectionRetryTimeout = 60;
    private const int ConnectionRetryTimeoutStep = 5;

    private readonly ClientWebSocket? _cws;
    private readonly WebSocket? _socket;


    public Client(WebSocket webSocket)
    {
        _socket = webSocket;
    }

    public Client(Uri uri, string? username, string? password)
    {
        _cws = ConnectSocket(uri, MaxConnectionRetryTimeout, ConnectionRetryTimeoutStep);

        Task.Run(async () =>
        {
            // Validate the server signature 
            var ssBuffer = new byte[Config.ServerSignature.Length];
            await _cws.ReceiveAsync(ssBuffer.AsMemory(), CancellationToken.None);
            var serverSignature = Config.StringEncoding.GetString(ssBuffer);

            // Make sure the signature is as expected
            if (serverSignature != Config.ServerSignature)
                throw new InvalidCredentialException("Server's signature is wrong.");
            
            // Get the server's info
            var serverInfoMsg = (Message)(await Message.ReceiveAsync(_cws))!;
            if (serverInfoMsg.Type != MessageType.ServerInfo)
                throw new InvalidCredentialException("Expected to receive server information.");
            
            // Send over our client's signature
            var signatureBytes = Config.StringEncoding.GetBytes(Config.ClientSignature);
            var signatureSegment = new ArraySegment<byte>(signatureBytes);
            await _cws.SendAsync(signatureSegment, WebSocketMessageType.Text, true, CancellationToken.None);

            // Send over out client's info
            var version = Assembly.GetEntryAssembly()!.GetName().Version!;
            var clientInfo = new ClientInformation(version, Config.Organization);
            await new Message { Type = MessageType.ClientInfo, ClientInfo = clientInfo }.SendAsync(_cws);

            // Get a username and password
            var u = username ?? ReadUsername();
            var p = password ?? ReadPassword();
            var auth = new Authorization(u, p);
            await new Message { Type = MessageType.PrivilegeElevation, AuthorizationInfo = auth }.SendAsync(_cws);
        }).Wait();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Client()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        _socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
        _cws?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();

        _socket?.Dispose();
        _cws?.Dispose();
    }


    public async Task Run()
    {
        var buffer = new byte[4096];
        if (_cws is null) throw new ArgumentNullException();
        while (_cws.State == WebSocketState.Open)
        {
            var message = await Message.ReceiveAsync(_cws);
            switch (message.Type)
            {
                case MessageType.AuthSuccessful:
                    Console.WriteLine("Successfully logged in to the network!");
                    break;

                case MessageType.AuthFailed:
                    Console.WriteLine(
                        "Authentication for has either failed or been revoked! Please restart this process!");
                    break;

                case MessageType.PageRequest:
                case MessageType.Invalid:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
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