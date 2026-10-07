namespace Abadar.Backend.Data;

public static class UserNormalization
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}