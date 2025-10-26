namespace Crayon.Box;

public enum MessageType
{
    ServerInfo,
    ClientInfo,

    CloseRequest,
    CloseResponse,

    PrivilegeElevation,
    AuthSuccessful,
    AuthFailed,

    PageRequest,
    Invalid = int.MaxValue
}