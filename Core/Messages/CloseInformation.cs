using System.Net.WebSockets;

namespace Crayon.Box.Messages;

public struct CloseInformation(string desc, WebSocketCloseStatus status)
{
    public string Description { get; set; } = desc;
    public WebSocketCloseStatus CloseStatus { get; set; } = status;
}