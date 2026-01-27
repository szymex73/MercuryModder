namespace MercuryModder.Helpers;

public class ExceptionList : Exception
{
    public List<Exception> InnerExceptions;
    public ExceptionList(string message, List<Exception> inner) : base(message)
    {
        InnerExceptions = inner;
    }
}
