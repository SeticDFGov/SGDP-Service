namespace service;
using EnumsNET;
public class ApiException : Exception
{
    public ErrorModel Error { get; }
    public ApiException(ErrorCode error, string? message = null) : base(message ?? error.AsString(EnumFormat.Description))
    {
        Error = new ErrorModel
        {
            Code = (int)error,
            Message = message,
            CodeStr = error.ToString()
        };
    }
}