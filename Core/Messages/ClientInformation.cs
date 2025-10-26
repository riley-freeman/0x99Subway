namespace Crayon.Box.Messages;

public struct ClientInformation(Version clientVersion, string organization)
{
    public string Organization { get; } = organization;
    public OperatingSystem Platform { get; } = Environment.OSVersion;
    private string _version = clientVersion.ToString();

    public Version ClientVersion
    {
        get => new(_version);
        set => _version = value.ToString();
    }
}