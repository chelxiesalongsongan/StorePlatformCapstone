namespace OnlineStore.Shared;

public static class Correlation
{
    private static readonly AsyncLocal<string?> CurrentId = new();

    public static string Current =>
        CurrentId.Value ??= Guid.NewGuid().ToString();

    public static void Set(string correlationId)
    {
        CurrentId.Value = correlationId;
    }
}
