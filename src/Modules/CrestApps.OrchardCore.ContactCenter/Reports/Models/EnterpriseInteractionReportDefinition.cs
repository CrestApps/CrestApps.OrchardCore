using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

internal sealed record EnterpriseInteractionReportDefinition(
    string Name,
    Func<LocalizedString> DisplayName,
    Func<LocalizedString> Description,
    EnterpriseInteractionReportKind Kind,
    string Category,
    IReadOnlyCollection<string> FilterNames);
