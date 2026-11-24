using CrestApps.Core;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal static class RowIds
{
    public static string Ensure(string id)
        => string.IsNullOrWhiteSpace(id) ? UniqueId.GenerateId() : id.Trim();
}
