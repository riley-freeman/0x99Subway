using System.Text;

namespace Crayon.Box;

public static class Config
{
    public const string ServerAddress = "localhost";
    public const short ServerPort = 9989;

    public const string ServerSignature = "__OPENX99__BOX_SERVER__";
    public const string ClientSignature = "__OPENX99__BOX_CLIENT__";
    public const string ClosingSignature = "goodbye.";

    public const string Organization = "com.crayon.box.open-distribution";
    public static readonly string? Features = null;

    public static readonly byte MaxUsernameLength = 8;
    public static readonly byte MaxPasswordLength = 24;


    public static readonly Encoding StringEncoding = Encoding.UTF8;
    public static readonly int MaxBufferSize = 4096;
}