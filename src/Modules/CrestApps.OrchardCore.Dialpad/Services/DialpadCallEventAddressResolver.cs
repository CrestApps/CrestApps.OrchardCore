namespace CrestApps.OrchardCore.Dialpad.Services;

internal static class DialpadCallEventAddressResolver
{
    public static string ResolveFromAddress(DialpadCallEvent callEvent)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        return IsOutbound(callEvent.Direction)
            ? NormalizeAddress(callEvent.SelectedCallerId)
            : NormalizeAddress(callEvent.ExternalNumber);
    }

    public static string ResolveToAddress(DialpadCallEvent callEvent)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        return IsOutbound(callEvent.Direction)
            ? NormalizeAddress(callEvent.ExternalNumber)
            : ResolveServiceAddress(callEvent);
    }

    public static string ResolveServiceAddress(DialpadCallEvent callEvent)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        return NormalizeAddress(callEvent.InternalNumber) ?? NormalizeAddress(callEvent.Target);
    }

    private static bool IsOutbound(string direction)
    {
        return string.Equals(direction?.Trim(), "outbound", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        return string.Equals(normalized, "blocked", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }
}
