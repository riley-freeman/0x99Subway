namespace Crayon.Box.Messages;

public readonly struct RequestAuthenticationMessage(string username, string authenticationToken)
{
    public string Username { get; } = username;
    public string AuthenticationToken { get; } = authenticationToken;
}