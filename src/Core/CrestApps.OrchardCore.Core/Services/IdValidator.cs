namespace CrestApps.OrchardCore.Core.Services;

public static class IdValidator
{
    private const int IdLength = 26;

    // Same alphabet used by GenerateId
    private static readonly string _encode32Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    private static readonly HashSet<char> _allowedChars =
        _encode32Alphabet.ToHashSet();

    public static bool IsValidId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        if (id.Length != IdLength)
        {
            return false;
        }

        foreach (var c in id)
        {
            if (!_allowedChars.Contains(c))
            {
                return false;
            }
        }

        return true;
    }
}
