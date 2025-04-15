namespace pengdows.crud;

public class InvalidValueException : Exception
{
    public InvalidValueException(string message) : base(message)
    {
    }
}