namespace Crayon.Box.Messages;

public struct ServerInformation(Version serverVersion, string organization)
{
    private string _version = serverVersion.ToString();
    public string Organization { get; } = organization;

    public Version ServerVersion
    {
        get => new(_version);
        set => _version = value.ToString();
    }
}