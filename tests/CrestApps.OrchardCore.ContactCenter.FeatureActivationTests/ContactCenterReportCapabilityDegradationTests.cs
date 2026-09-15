using System.Text;
using CrestApps.OrchardCore.ContactCenter.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that a report whose subject matter belongs to a capability the tenant did not enable says so, instead of
/// publishing a zero that reads as an operational result.
/// </summary>
/// <remarks>
/// The reports deliberately do not depend on the voice or recording capabilities: forcing telephony onto a
/// chat-only tenant to read a queue report would be a worse outcome than the one being fixed. What they must not do is
/// keep publishing the reports those capabilities feed. A tenant without recording that reads "recording coverage: 0%"
/// has been told its calls are not being recorded, which is a compliance statement, not a measurement.
/// <para>
/// The oracle is a live tenant rather than a unit test over the guard, because the failure being prevented is a wiring
/// failure: a report registered without the guard, or a guard registered in the wrong feature, still compiles and still
/// returns a document full of zeroes.
/// </para>
/// </remarks>
public sealed class ContactCenterReportCapabilityDegradationTests
{
    private const string ReportAssemblyPrefix = "CrestApps.OrchardCore.ContactCenter";

    /// <summary>
    /// The words that only appear on a figure some capability had to produce. Withholding a whole report is the right
    /// answer only when its entire subject belongs to the absent capability; a report whose primary figures are real
    /// keeps running, and must drop the absent capability's columns rather than render them as zeroes.
    /// </summary>
    private static readonly (string FeatureId, string[] Labels)[] _capabilityOwnedLabels =
    [
        (ContactCenterConstants.Feature.Voice, ["transfer", "provider"]),
        (ContactCenterConstants.Feature.Recording, ["record"]),
    ];

    [Fact]
    public async Task EveryContactCenterReport_DeclaresWhichCapabilitiesProduceItsData()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "report-capability-contract",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Queues, ReportsConstants.Feature],
        });

        var undeclared = await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
        {
            var violations = GetContactCenterReports(serviceProvider)
                .Where(report => report is not IContactCenterCapabilityDependentReport)
                .Select(report => $"{report.GetType().FullName} ({report.Name})")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult(violations);
        });

        Assert.True(
            undeclared.Length == 0,
            Describe(
                "Contact Center reports do not declare which capabilities produce the data they read, so they cannot " +
                "tell an absent capability apart from a measured zero.",
                $"Implement {nameof(IContactCenterCapabilityDependentReport)} and return the producing features.",
                undeclared));
    }

    [Fact]
    public async Task AReportWhoseProducingCapabilityIsAbsent_SaysSoInsteadOfReportingZero()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "report-capability-degradation",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Queues, ReportsConstants.Feature],
        });

        var result = await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var featureManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
            var enabledFeatureIds = (await featureManager.GetEnabledFeaturesAsync())
                .Select(feature => feature.Id)
                .ToHashSet(StringComparer.Ordinal);

            var filter = new ReportFilter();
            filter.SetDateRange(new ReportDateRange
            {
                FromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            });

            var context = new ReportContext(filter);

            var guarded = new List<string>();
            var leaked = new List<string>();

            foreach (var report in GetContactCenterReports(serviceProvider))
            {
                if (report is not IContactCenterCapabilityDependentReport dependent)
                {
                    continue;
                }

                var missing = dependent.RequiredFeatureIds
                    .Where(featureId => !enabledFeatureIds.Contains(featureId))
                    .ToArray();

                if (missing.Length == 0)
                {
                    continue;
                }

                guarded.Add(report.Name);

                var document = await report.RunAsync(context);
                var section = document.Sections.Count == 1
                    ? document.Sections[0]
                    : null;

                if (section is null ||
                    section.Rows.Count > 0 ||
                    section.Metrics.Count > 0 ||
                    section.Bars.Count > 0 ||
                    section.Chart is not null ||
                    missing.Any(featureId => !(section.Description ?? string.Empty).Contains(featureId, StringComparison.Ordinal)))
                {
                    leaked.Add($"{report.Name} (missing: {string.Join(", ", missing)})");
                }
            }

            return new
            {
                Guarded = guarded.ToArray(),
                Leaked = leaked.ToArray(),
            };
        });

        Assert.True(
            result.Guarded.Length > 0,
            "No Contact Center report declared a capability the analytics-only tenant is missing, so this test proves " +
            "nothing. Either the requirement map was emptied or the analytics feature now drags the producing " +
            "capabilities in, which is the coupling this split exists to prevent.");

        Assert.True(
            result.Leaked.Length == 0,
            Describe(
                "Reports rendered figures for a capability the tenant has not enabled. A zero produced by an absent " +
                "capability is indistinguishable from a measured zero and will be read as an operational result.",
                "Route the report through the capability guard so it returns the notice instead of data.",
                result.Leaked));
    }

    [Fact]
    public async Task NoReport_RendersAColumnOrMetricForAnAbsentCapability()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "report-capability-columns",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Queues, ReportsConstants.Feature],
        });

        var result = await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var featureManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
            var enabledFeatureIds = (await featureManager.GetEnabledFeaturesAsync())
                .Select(feature => feature.Id)
                .ToHashSet(StringComparer.Ordinal);

            var absentLabels = _capabilityOwnedLabels
                .Where(entry => !enabledFeatureIds.Contains(entry.FeatureId))
                .SelectMany(entry => entry.Labels)
                .ToArray();

            var filter = new ReportFilter();
            filter.SetDateRange(new ReportDateRange
            {
                FromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            });

            var context = new ReportContext(filter);

            var executed = 0;
            var leaked = new List<string>();

            foreach (var report in GetContactCenterReports(serviceProvider))
            {
                executed++;

                var document = await report.RunAsync(context);

                foreach (var section in document.Sections)
                {
                    var headings = section.Columns
                        .Select(column => column.Label)
                        .Concat(section.Metrics.Select(metric => metric.Label))
                        .Concat(section.Bars.Select(bar => bar.Label));

                    foreach (var heading in headings.Where(heading => !string.IsNullOrEmpty(heading)))
                    {
                        var offending = absentLabels
                            .Where(label => heading.Contains(label, StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        if (offending.Length > 0)
                        {
                            leaked.Add($"{report.Name} renders '{heading}'");
                        }
                    }
                }
            }

            return new
            {
                Executed = executed,
                AbsentLabels = absentLabels,
                Leaked = leaked.Distinct(StringComparer.Ordinal).OrderBy(entry => entry, StringComparer.Ordinal).ToArray(),
            };
        });

        Assert.True(result.Executed > 0, "No Contact Center report ran, so this test proves nothing.");
        Assert.True(result.AbsentLabels.Length > 0, "Every producing capability was enabled, so this test proves nothing.");

        Assert.True(
            result.Leaked.Length == 0,
            Describe(
                "Reports rendered a column or metric that only an absent capability can fill. Withholding the whole " +
                "report is not always right - most of these reports measure real work - but a structural zero in a " +
                "'Recording coverage' or 'Transfers' column is read as an operational result, not as a missing feature.",
                "Drop the column from the layout while the producing capability is absent.",
                result.Leaked));
    }

    [Fact]
    public async Task TheAnalyticsFeature_DoesNotDragTheProducingCapabilitiesIntoTheTenant()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "report-capability-independence",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Queues, ReportsConstants.Feature],
        });

        var dragged = await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var featureManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
            var enabledFeatureIds = (await featureManager.GetEnabledFeaturesAsync())
                .Select(feature => feature.Id)
                .ToHashSet(StringComparer.Ordinal);

            return new[]
            {
                ContactCenterConstants.Feature.Voice,
                ContactCenterConstants.Feature.Recording,
            }
            .Where(enabledFeatureIds.Contains)
            .ToArray();
        });

        Assert.True(
            dragged.Length == 0,
            Describe(
                "Enabling reporting activated telephony capabilities. A tenant that only wants to read chat and CRM " +
                "reports must not be made to run, secure and upgrade voice call handling to do it.",
                "Keep the capability out of the analytics dependency list and degrade the affected reports instead.",
                dragged));
    }

    private static IEnumerable<IReport> GetContactCenterReports(IServiceProvider serviceProvider)
    {
        return serviceProvider
            .GetServices<IReport>()
            .Where(report => report.GetType().Assembly.GetName().Name?
                .StartsWith(ReportAssemblyPrefix, StringComparison.Ordinal) == true);
    }

    private static string Describe(string problem, string remedy, IReadOnlyCollection<string> offenders)
    {
        var builder = new StringBuilder(problem)
            .AppendLine()
            .AppendLine()
            .AppendLine(remedy)
            .AppendLine();

        foreach (var offender in offenders)
        {
            builder.Append("  - ").AppendLine(offender);
        }

        return builder.ToString();
    }
}
