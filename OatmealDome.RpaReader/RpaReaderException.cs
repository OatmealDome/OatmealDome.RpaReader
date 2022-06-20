namespace OatmealDome.RpaReader;

public class RpaReaderException : Exception
{
    public RpaReaderException(string message) : base(message)
    {
    }

    public RpaReaderException(string message, Exception inner) : base(message, inner)
    {
    }
}
