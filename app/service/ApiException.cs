namespace service;

public class ApiException : Exception
{
    public ErrorModel Error { get; }

    public ApiException(ErrorCode error, string? message = null)
        : base(message ?? error.ToString())
    {
        Error = new ErrorModel
        {
            Code = (int)error,
            Message = message ?? error.ToString(),
            CodeStr = error.ToString()
        };
    }
}
