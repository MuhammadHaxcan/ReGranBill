namespace ReGranBill.Server.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string ClientMessage { get; }

    public AppException(int statusCode, string clientMessage, Exception? innerException = null)
        : base(clientMessage, innerException)
    {
        StatusCode = statusCode;
        ClientMessage = clientMessage;
    }
}
