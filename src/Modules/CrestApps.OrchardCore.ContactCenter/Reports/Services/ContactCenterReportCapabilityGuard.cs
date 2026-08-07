using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Resolves report capability requirements against the features the tenant has actually enabled.
/// </summary>
public sealed class ContactCenterReportCapabilityGuard : IContactCenterReportCapabilityGuard
{
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IExtensionManager _extensionManager;
    private readonly IStringLocalizer S;

    private HashSet<string> _enabledFeatureIds;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportCapabilityGuard"/> class.
    /// </summary>
    /// <param name="shellFeaturesManager">The shell features manager used to read the tenant's enabled features.</param>
    /// <param name="extensionManager">The extension manager used to resolve a readable feature name.</param>
    /// <param name="stringLocalizer">The string localizer used for the notice text.</param>
    public ContactCenterReportCapabilityGuard(
        IShellFeaturesManager shellFeaturesManager,
        IExtensionManager extensionManager,
        IStringLocalizer<ContactCenterReportCapabilityGuard> stringLocalizer)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _extensionManager = extensionManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyCollection<string>> GetMissingFeaturesAsync(
        IReadOnlyCollection<string> requiredFeatureIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requiredFeatureIds);

        if (requiredFeatureIds.Count == 0)
        {
            return [];
        }

        _enabledFeatureIds ??= (await _shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.Ordinal);

        return [.. requiredFeatureIds.Where(featureId => !_enabledFeatureIds.Contains(featureId))];
    }

    /// <inheritdoc/>
    public ReportDocument DescribeUnavailable(IReadOnlyCollection<string> missingFeatureIds)
    {
        ArgumentNullException.ThrowIfNull(missingFeatureIds);

        var names = string.Join(", ", missingFeatureIds.Select(DescribeFeature));

        var section = new ReportSection
        {
            Title = S["Capability not enabled"].Value,
            Description = S["This report measures work produced by {0}, which this tenant has not enabled. No figures are shown, because an absent capability produces no measurements and reporting zero would read as an operational result.", names].Value,
            Kind = ReportSectionKind.Metrics,
        };

        return new ReportDocument().Add(section);
    }

    private string DescribeFeature(string featureId)
    {
        var feature = _extensionManager.GetFeatures().FirstOrDefault(candidate => string.Equals(candidate.Id, featureId, StringComparison.Ordinal));

        return feature is null
            ? featureId
            : $"{feature.Name} ({featureId})";
    }
}
