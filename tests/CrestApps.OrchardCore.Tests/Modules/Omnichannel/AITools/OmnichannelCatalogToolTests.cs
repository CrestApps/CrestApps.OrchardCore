using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.AI;
using CrestApps.OrchardCore.Tests.Agent;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The cadence tool saves through the catalog manager with the editor's fields and rules, and the delete tool deletes a
/// cadence or a subject action after checking the permission of the kind it deletes, and leaves the kinds the generic
/// record tools delete to <c>deleteRecord</c>. Campaigns, campaign groups and dispositions are read and written with the
/// generic record tools (see <see cref="OmnichannelRecordKindTests"/>).
/// </summary>
public sealed class OmnichannelCatalogToolTests
{
    private readonly OmnichannelToolHarness _harness = new();

    public OmnichannelCatalogToolTests()
    {
        _harness.Cadences.Validate = FakeCatalogManager<Cadence>.RequireName;
    }

    [Fact]
    public async Task SaveCadence_ReplacesTheStepsAndDropsTheEmptyRowsAsTheEditorDoes()
    {
        // Arrange
        var cadence = _harness.Cadences.Add(new Cadence
        {
            ItemId = "cadence-1",
            DisplayText = "Lead nurture",
            Steps = [new CadenceStep { DelayMinutes = 60, Message = "Hi" }],
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOmnichannelCadenceTool(), _harness.Build(), new()
        {
            ["id"] = "cadence-1",
            ["steps"] = JsonSerializer.SerializeToElement(new object[]
            {
                new { delayMinutes = 60, message = " Hi " },
                new { delayMinutes = 0, message = "dropped" },
                new { delayMinutes = 2880, isAiGenerated = true },
            }),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("updated", json.GetProperty("saved").GetString());
        Assert.Equal(2, json.GetProperty("cadence").GetProperty("steps").GetArrayLength());
        Assert.Equal(2, cadence.Steps.Count);
        Assert.Equal("Hi", cadence.Steps[0].Message);
        Assert.True(cadence.Steps[1].IsAiGenerated);
        Assert.Equal(2880, cadence.Steps[1].DelayMinutes);
    }

    [Fact]
    public async Task SaveCadence_LeavesTheCadenceUnchangedWhenTheRulesFail()
    {
        // Arrange
        var cadence = _harness.Cadences.Add(new Cadence { ItemId = "cadence-1", DisplayText = "Lead nurture" });
        _harness.Cadences.Validate = model =>
        {
            var validation = new CrestApps.Core.Models.ValidationResultDetails();

            if (model.Steps.Any(step => !step.IsAiGenerated && string.IsNullOrWhiteSpace(step.Message)))
            {
                validation.Fail(new System.ComponentModel.DataAnnotations.ValidationResult("Each defined-message step needs its message text."));
            }

            return validation;
        };

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOmnichannelCadenceTool(), _harness.Build(), new()
        {
            ["id"] = "cadence-1",
            ["steps"] = JsonSerializer.SerializeToElement(new object[] { new { delayMinutes = 60 } }),
        });

        // Assert
        Assert.Equal("Nothing was saved: Each defined-message step needs its message text.", result);
        Assert.Empty(cadence.Steps);
        Assert.Empty(_harness.Cadences.Updated);
    }

    [Fact]
    public async Task DeleteRecord_DeletesACadence()
    {
        // Arrange
        _harness.Cadences.Add(new Cadence { ItemId = "cadence-1", DisplayText = "Lead nurture" });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteOmnichannelRecordTool(), _harness.Build(), new() { ["kind"] = "cadence", ["id"] = "cadence-1" });

        // Assert
        Assert.Equal("Lead nurture", AgentToolTestContext.ReadJson(result).GetProperty("deleted").GetProperty("name").GetString());
        Assert.Empty(_harness.Cadences.Items);
    }

    [Fact]
    public async Task DeleteRecord_ChecksThePermissionOfTheKindItDeletes()
    {
        // Arrange
        _harness.Cadences.Add(new Cadence { ItemId = "cadence-1", DisplayText = "Lead nurture" });
        var services = _harness.Build(authorize: (permission, _) => permission.Name == OmnichannelConstants.Permissions.ManageDispositions.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteOmnichannelRecordTool(), services, new() { ["kind"] = "cadence", ["id"] = "cadence-1" });

        // Assert
        Assert.Equal(CrestApps.OrchardCore.AI.Core.AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Single(_harness.Cadences.Items);
    }

    [Theory]
    [InlineData("disposition")]
    [InlineData("leadStatus")]
    [InlineData("opportunityStage")]
    public async Task DeleteRecord_LeavesTheWritableKindsToDeleteRecord(string kind)
    {
        // Arrange
        _harness.Dispositions.Add(new OmnichannelDisposition { ItemId = "d-1", Name = "Busy" });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteOmnichannelRecordTool(), _harness.Build(), new() { ["kind"] = kind, ["id"] = "d-1" });

        // Assert
        Assert.StartsWith("Nothing was deleted: kind must be cadence or subjectAction.", result);
        Assert.Contains("deleteRecord", result);
        Assert.Single(_harness.Dispositions.Items);
    }

    [Fact]
    public async Task DeleteRecord_RefusesAKindThatCannotBeDeleted()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteOmnichannelRecordTool(), _harness.Build(), new() { ["kind"] = "campaign", ["id"] = "campaign-1" });

        // Assert
        Assert.Contains("campaigns, campaign groups and addresses cannot be deleted", result);
    }

    [Fact]
    public async Task DeleteRecord_DeletesASubjectAction()
    {
        // Arrange
        _harness.SubjectActions.Add(new SubjectAction { ItemId = "a-1", DisplayText = "Retry", SubjectContentType = "SalesCall" });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteOmnichannelRecordTool(), _harness.Build(), new() { ["kind"] = "subjectAction", ["id"] = "a-1" });

        // Assert
        Assert.Equal("SalesCall", AgentToolTestContext.ReadJson(result).GetProperty("deleted").GetProperty("subjectContentType").GetString());
        Assert.Empty(_harness.SubjectActions);
    }
}
