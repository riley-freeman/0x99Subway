using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Crayon.Box.Messages;

namespace Crayon.Box;

public class Message
{
    public int Time { get; } = DateTime.Now.Millisecond;
    public MessageType Type { get; set; } = MessageType.Invalid;

    public ServerInformation? ServerInfo { get; set; }
    public ClientInformation? ClientInfo { get; set; }
    public Authorization? AuthorizationInfo { get; set; }
    public CloseInformation? CloseInfo { get; set; }

    public static Message? FromBytes(byte[] buffer, int count)
    {
        var json = Encoding.UTF8.GetString(buffer, 0, count);
        var message = JsonSerializer.Deserialize<Message>(json);
        return message;
    }

    public async Task SendAsync(WebSocket socket)
    {
        // We want to handle the close handshake a little differently
        switch (Type)
        {
            case MessageType.CloseRequest:
                var request = (CloseInformation)CloseInfo!;
                await socket.CloseAsync(request.CloseStatus, request.Description, CancellationToken.None);
                return;

            case MessageType.CloseResponse:
                var output = (CloseInformation)CloseInfo!;
                await socket.CloseOutputAsync(output.CloseStatus, output.Description, CancellationToken.None);
                return;

            default:
                await socket.SendAsync(ToArraySegment(), WebSocketMessageType.Text, true, CancellationToken.None);
                return;
        }
    }

    public static async Task<Message?> ReceiveAsync(WebSocket socket)
    {
        var buffer = new byte[Config.MaxBufferSize];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        switch (result.MessageType)
        {
            case WebSocketMessageType.Text:
                return FromBytes(buffer, result.Count);

            case WebSocketMessageType.Close:
                var desc = Config.StringEncoding.GetString(buffer, 0, result.Count);
                return new Message
                {
                    Type = MessageType.CloseRequest,
                    CloseInfo = new CloseInformation(result.CloseStatusDescription!,
                        (WebSocketCloseStatus)result.CloseStatus!)
                };

            case WebSocketMessageType.Binary:
                throw new ArgumentException("Binary websocket message types are not allowed.");
        }

        return null;
    }


    private ArraySegment<byte> ToArraySegment()
    {
        var json = JsonSerializer.Serialize(this);
        var encoding = Encoding.UTF8.GetBytes(json);

        return new ArraySegment<byte>(encoding);
    }
}