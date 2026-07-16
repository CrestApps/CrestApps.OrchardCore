namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

internal static class ContactCenterFormHelpers
{
    internal static IList<string> NormalizeList(IEnumerable<string> values)
    {
        return values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    internal static IList<string> ParseList(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    internal static string FormatList(IEnumerable<string> values)
    {
        return values is null
            ? null
            : string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
