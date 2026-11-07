using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Recipes;
using CrestApps.OrchardCore.DncRegistry.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.DncRegistry.Deployments;

/// <summary>
/// Writes every imported local do-not-call list, with its phone numbers, into a <c>LocalDncLists</c> recipe step. Lists
/// that are still importing, paused, failed or being deleted are left out.
/// </summary>
internal sealed class LocalDncListsDeploymentSource : DeploymentSourceBase<LocalDncListsDeploymentStep>
{
    private readonly ILocalDncListManager _manager;

    public LocalDncListsDeploymentSource(ILocalDncListManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(LocalDncListsDeploymentStep step, DeploymentPlanResult result)
    {
        var lists = new JsonArray();

        foreach (var list in (await _manager.GetListsAsync()).Where(list => list.Status == LocalDncListStatus.Completed).OrderBy(list => list.Name, StringComparer.OrdinalIgnoreCase))
        {
            var numbers = await _manager.GetPhoneNumbersAsync(list.ListId);

            lists.Add(new JsonObject
            {
                ["Name"] = list.Name,
                ["CountryCode"] = list.CountryCode,
                ["PhoneNumbers"] = new JsonArray([.. numbers.Select(number => (JsonNode)number)]),
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = LocalDncListsStep.StepKey,
            [LocalDncListsStep.EntriesName] = lists,
        });
    }
}
