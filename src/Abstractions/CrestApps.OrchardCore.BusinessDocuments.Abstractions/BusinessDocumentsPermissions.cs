using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.BusinessDocuments;

/// <summary>
/// The permissions of the business documents building block.
/// </summary>
public static class BusinessDocumentsPermissions
{
    /// <summary>
    /// Lets a user add, change and remove the templates business documents are printed with.
    /// </summary>
    public static readonly Permission ManageBusinessDocumentTemplates = new(
        "ManageBusinessDocumentTemplates",
        LocalizationSource.Create("Manage business document templates", typeof(BusinessDocumentsPermissions)));

    /// <summary>
    /// Lets a user change how the numbers of the registered number series are written.
    /// </summary>
    public static readonly Permission ManageNumberSeries = new(
        "ManageNumberSeries",
        LocalizationSource.Create("Manage number series", typeof(BusinessDocumentsPermissions)));
}
