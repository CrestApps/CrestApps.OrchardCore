using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

internal sealed record AgentWorkforceReportDefinition(
    string Name,
    Func<LocalizedString> DisplayName,
    Func<LocalizedString> Description,
    AgentWorkforceReportKind Kind,
    string Category,
    IReadOnlyCollection<string> FilterNames);
