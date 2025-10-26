namespace Crayon.Box;

public struct Authorization(string username, string authenticationToken)
{
    public string Username { get; set; } = username;
    public string AuthenticationToken { get; set; } = authenticationToken;
}