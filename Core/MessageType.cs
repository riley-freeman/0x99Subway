namespace Core;

public enum MessageType
{
    LoginSuccessful,
    LoginFailed,

    PageRequest,
    Invalid = int.MaxValue
}