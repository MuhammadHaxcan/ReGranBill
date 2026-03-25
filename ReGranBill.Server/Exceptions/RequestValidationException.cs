namespace ReGranBill.Server.Exceptions;

public sealed class RequestValidationException : AppException
{
    public RequestValidationException(string message)
        : base(StatusCodes.Status400BadRequest, message)
    {
    }
}
