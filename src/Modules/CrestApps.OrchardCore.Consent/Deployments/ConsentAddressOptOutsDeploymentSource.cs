using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Consent.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Consent.Deployments;

/// <summary>
/// Writes every opt-out for one purpose recorded against an address into a <c>ConsentAddressOptOuts</c> recipe step,
/// reading the store a page at a time.
/// </summary>
internal sealed class ConsentAddressOptOutsDeploymentSource : DeploymentSourceBase<ConsentAddressOptOutsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly IConsentAddressOptOutStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentAddressOptOutsDeploymentSource"/> class.
    /// </summary>
    /// <param name="store">The store of address opt-outs.</param>
    public ConsentAddressOptOutsDeploymentSource(IConsentAddressOptOutStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    protected override async Task ProcessAsync(ConsentAddressOptOutsDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = new JsonArray();
        long afterId = 0;

        do
        {
            var (records, nextId) = await _store.PageAsync(afterId, PageSize);

            foreach (var record in records)
            {
                entries.Add(new JsonObject
                {
                    ["Channel"] = record.Channel,
                    ["Address"] = record.Address,
                    ["Purpose"] = record.Purpose,
                    ["Source"] = record.Source,
                    ["OptedOutUtc"] = DateTime.SpecifyKind(record.OptedOutUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
                });
            }

            afterId = nextId;
        }
        while (afterId > 0);

        result.Steps.Add(new JsonObject
        {
            ["name"] = ConsentAddressOptOutsStep.StepKey,
            [ConsentAddressOptOutsStep.EntriesName] = entries,
        });
    }
}
