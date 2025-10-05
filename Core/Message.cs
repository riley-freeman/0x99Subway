using System.Text;
using System.Text.Json;

namespace Core;

public class Message
{
    private int id;


    public Message()
    {
        id = 0;
        type = MessageType.LoginSuccessful;
    }

    public MessageType type { get; set; }

    public static Message? FromBytes(byte[] buffer)
    {
        var json = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
        var message = JsonSerializer.Deserialize<Message>(json);
        return message;
    }

    public ArraySegment<byte> ToArraySegment()
    {
        var json = JsonSerializer.Serialize(this);
        var encoding = Encoding.UTF8.GetBytes(json);

        return new ArraySegment<byte>(encoding);
    }
}