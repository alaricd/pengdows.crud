namespace pengdows.crud;

public class Utils
{
    public static bool IsNullOrDbNull(object? value)
    {
        return (value == null || value is DBNull);
    }
}