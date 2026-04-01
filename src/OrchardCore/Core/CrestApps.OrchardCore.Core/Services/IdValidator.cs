namespace CrestApps.OrchardCore.Core.Services;

public static class IdValidator
{
    private const int _idLength = 26;

    // Same alphabet used by GenerateId
    private static readonly string _encode32Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    private static readonly HashSet<char> _allowedChars =
        _encode32Alphabet.ToHashSet();

    public static bool IsValid(string id)
    {
        if (id is null)
        {
            return false;
        }

        if (id.Length != _idLength)
        {
            return false;
        }

        return id.All(_allowedChars.Contains);
    }
}
