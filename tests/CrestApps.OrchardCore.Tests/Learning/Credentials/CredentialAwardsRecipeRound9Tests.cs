using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Review round 9, Low (credentials): an imported award must hold a slot inside its status list; one outside it would
/// make the whole list fail to encode.
/// </summary>
public sealed class CredentialAwardsRecipeRound9Tests
{
    [Theory]
    [InlineData(1, CredentialsConstants.StatusListCapacity)]
    [InlineData(1, -1)]
    [InlineData(0, 5)]
    public async Task Round9Low_AnImportedAwardOutsideItsStatusList_IsSkipped(int listNumber, int listIndex)
    {
        await using var h = await CredentialTestHarness.CreateAsync();
        var publicId = Guid.NewGuid().ToString("n");
        var context = new RecipeExecutionContext
        {
            Name = CredentialsConstants.RecipeStepName,
            Step = new JsonObject
            {
                ["name"] = CredentialsConstants.RecipeStepName,
                ["Awards"] = new JsonArray(new JsonObject
                {
                    ["PublicId"] = publicId,
                    ["DefinitionId"] = "forklift",
                    ["RecipientUserName"] = "ana",
                    ["StatusListNumber"] = listNumber,
                    ["StatusListIndex"] = listIndex,
                    ["IssuedUtc"] = "2026-11-01T00:00:00Z",
                }),
            },
        };

        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<LearningCredentialAwardsRecipeStep>(services, new AnaLookup()).ExecuteAsync(context);
        });

        Assert.NotEmpty(context.Errors);
        Assert.Null(await h.RunAsync(services => services.GetRequiredService<CredentialAwardStore>().FindByPublicIdAsync(publicId)));
    }

    private sealed class AnaLookup : LearnerUserLookup
    {
        public AnaLookup()
            : base(null, null)
        {
        }

        public override Task<string> FindUserIdAsync(string userName)
            => Task.FromResult(userName is "ana" ? "ana" : null);
    }
}
