using System.Collections.Concurrent;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Creates the per-employer permissions, such as "Prepare pay runs for Contoso", each implied by its global one.
/// </summary>
public static class PayrollPermissionTemplates
{
    // One template per global permission, each implied by it; a per-employer "view" is also implied by the same
    // employer's "do" permissions, as the global ones are.
    private static readonly Dictionary<string, PermissionTemplate> _templates = CreateTemplates();

    private static readonly ConcurrentDictionary<(string Permission, string EmployerId), Permission> _created = new();

    private static Dictionary<string, PermissionTemplate> CreateTemplates()
    {
        var templates = new Dictionary<string, PermissionTemplate>(StringComparer.Ordinal);

        templates[PayrollPermissions.ManagePayrollSettings.Name] = new("ManagePayrollSettings_{0}", LocalizationSource.Create("Manage payroll settings and the employer {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ManagePayrollSettings) { IsSecurityCritical = true };
        templates[PayrollPermissions.ManagePayProfiles.Name] = new("ManagePayProfiles_{0}", LocalizationSource.Create("Manage pay profiles for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ManagePayProfiles);
        templates[PayrollPermissions.ViewTaxIds.Name] = new("ViewTaxIds_{0}", LocalizationSource.Create("View full tax ids for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ViewTaxIds) { IsSecurityCritical = true };
        templates[PayrollPermissions.ManageGarnishments.Name] = new("ManageGarnishments_{0}", LocalizationSource.Create("Manage garnishments for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ManageGarnishments);
        templates[PayrollPermissions.PreparePayRuns.Name] = new("PreparePayRuns_{0}", LocalizationSource.Create("Prepare pay runs for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.PreparePayRuns);
        templates[PayrollPermissions.ApprovePayRuns.Name] = new("ApprovePayRuns_{0}", LocalizationSource.Create("Approve pay runs for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ApprovePayRuns);
        templates[PayrollPermissions.ProcessPayRuns.Name] = new("ProcessPayRuns_{0}", LocalizationSource.Create("Process pay runs for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ProcessPayRuns) { IsSecurityCritical = true };
        templates[PayrollPermissions.VoidPayStatements.Name] = new("VoidPayStatements_{0}", LocalizationSource.Create("Void pay statements for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.VoidPayStatements);
        templates[PayrollPermissions.ExportPayrollData.Name] = new("ExportPayrollData_{0}", LocalizationSource.Create("Export payroll data for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ExportPayrollData) { IsSecurityCritical = true };
        templates[PayrollPermissions.ManagePayrollFilings.Name] = new("ManagePayrollFilings_{0}", LocalizationSource.Create("Manage payroll filings for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ManagePayrollFilings);
        templates[PayrollPermissions.ViewPayProfiles.Name] = new("ViewPayProfiles_{0}", LocalizationSource.Create("View pay profiles for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ViewPayProfiles) { ImpliedByTemplates = [templates[PayrollPermissions.ManagePayProfiles.Name]] };
        templates[PayrollPermissions.ViewPayRuns.Name] = new("ViewPayRuns_{0}", LocalizationSource.Create("View pay runs for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ViewPayRuns) { ImpliedByTemplates = [templates[PayrollPermissions.PreparePayRuns.Name], templates[PayrollPermissions.ApprovePayRuns.Name], templates[PayrollPermissions.ProcessPayRuns.Name], templates[PayrollPermissions.VoidPayStatements.Name]] };
        templates[PayrollPermissions.ViewPayrollReports.Name] = new("ViewPayrollReports_{0}", LocalizationSource.Create("View payroll reports for {0}", typeof(PayrollPermissionTemplates)), "Payroll: {1}", PayrollPermissions.ViewPayrollReports) { ImpliedByTemplates = [templates[PayrollPermissions.ExportPayrollData.Name]] };

        return templates;
    }

    /// <summary>
    /// Gets the per-employer permission of a global one.
    /// </summary>
    /// <param name="permission">The global permission, one of <see cref="PayrollPermissions.PerEmployer"/>.</param>
    /// <param name="employerId">The employer's id.</param>
    /// <param name="employerName">The employer's name, shown in the role editor.</param>
    /// <returns>The permission.</returns>
    public static Permission For(Permission permission, string employerId, string employerName = null)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentException.ThrowIfNullOrEmpty(employerId);

        if (employerName is null && _created.TryGetValue((permission.Name, employerId), out var cached))
        {
            return cached;
        }

        if (!_templates.TryGetValue(permission.Name, out var template))
        {
            throw new ArgumentException($"{permission.Name} has no per-employer permission.", nameof(permission));
        }

        var created = template.CreateDynamicPermission(employerId, employerName ?? employerId);

        _created[(permission.Name, employerId)] = created;

        return created;
    }

    /// <summary>
    /// Gets the global permission a per-employer permission name belongs to, and the employer.
    /// </summary>
    /// <param name="name">The permission's name.</param>
    /// <returns>The global permission and the employer, or <see langword="null"/>.</returns>
    public static (Permission Permission, string EmployerId)? Parse(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (var permission in PayrollPermissions.PerEmployer)
        {
            var prefix = permission.Name + "_";

            if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
            {
                return (permission, name[prefix.Length..]);
            }
        }

        return null;
    }
}
