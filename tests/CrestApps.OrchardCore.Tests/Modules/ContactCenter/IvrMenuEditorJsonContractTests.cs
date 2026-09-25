using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.ModelBinders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The entry point's visual IVR menu editor posts the same JSON field the raw textarea did. The fixture it is tested
/// against (tests/fixtures/ivr-menu/all-action-kinds.json) is shared with the editor's unit tests, so the shape the
/// browser writes and the shape the server binds cannot drift apart unnoticed.
/// </summary>
public sealed class IvrMenuEditorJsonContractTests
{
    private const string FieldName = "IvrFlowJson";

    // The options the entry point driver shows the menu with.
    private static readonly JsonSerializerOptions _displayOptions = new(ContactCenterDeploymentSerializer.Options)
    {
        WriteIndented = true,
    };

    [Fact]
    public async Task Binder_ReadsTheEditorsJson_WithEveryActionKind()
    {
        // Arrange
        var json = ReadFixture();

        // Act
        var (result, modelState) = await BindAsync(json);

        // Assert
        Assert.True(result.IsModelSet);
        Assert.Equal(0, modelState.ErrorCount);

        var flow = Assert.IsType<IvrFlow>(result.Model);

        Assert.Equal("main", flow.RootNodeId);
        Assert.Equal(2, flow.MaxRetries);
        Assert.Equal(IvrActionKind.RouteToQueue, flow.FallbackAction.Kind);
        Assert.Equal("queue-general", flow.FallbackAction.TargetId);
        Assert.Equal(2, flow.Nodes.Count);

        var main = flow.Nodes[0];

        Assert.Equal(
            [IvrActionKind.RouteToQueue, IvrActionKind.SubMenu, IvrActionKind.RouteToAgent, IvrActionKind.ExternalTransfer, IvrActionKind.Voicemail, IvrActionKind.Repeat],
            main.Options.Select(option => option.Action.Kind));
        Assert.Null(main.PromptMediaId);

        var support = flow.Nodes[1];

        Assert.Null(support.Prompt);
        Assert.Equal("support-menu-recording", support.PromptMediaId);
        Assert.Equal(["1", "*", "#"], support.Options.Select(option => option.Digit));
    }

    [Fact]
    public async Task EditorsJson_IsARunnableFlow()
    {
        // Arrange
        var (result, _) = await BindAsync(ReadFixture());

        // Act
        var errors = IvrFlowValidator.Validate((IvrFlow)result.Model);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Binder_TreatsAnEmptyField_AsNoMenu()
    {
        // Arrange
        // An editor with no menus posts an empty field, which must keep meaning "route straight to the target".

        // Act
        var (result, modelState) = await BindAsync(string.Empty);

        // Assert
        Assert.True(result.IsModelSet);
        Assert.Null(result.Model);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public async Task Binder_ReportsMalformedJson_AgainstTheField()
    {
        // Arrange
        // The editor keeps an invalid advanced-mode edit in the textarea rather than discarding it, and the server
        // stays the authority on what is refused.

        // Act
        var (result, modelState) = await BindAsync("{ \"RootNodeId\": ");

        // Assert
        Assert.False(result.IsModelSet);
        Assert.True(modelState.ContainsKey(FieldName));
        Assert.Single(modelState[FieldName].Errors);
    }

    [Fact]
    public void EditorShownJson_NamesActionKinds_TheWayTheEditorReadsThem()
    {
        // Arrange
        // The driver fills the field with this serialization when the editor opens. The editor reads kind names, and
        // falls back to the enum's order for numbers, so the order is pinned here too.
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "main",
                    Prompt = "Hello",
                    Options = [new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.SubMenu, TargetId = "main" } }],
                },
            ],
        };

        // Act
        var json = JsonSerializer.Serialize(flow, _displayOptions);
        using var document = JsonDocument.Parse(json);
        var kind = document.RootElement.GetProperty("Nodes")[0].GetProperty("Options")[0].GetProperty("Action").GetProperty("Kind");

        // Assert
        Assert.True(
            kind.ValueKind == JsonValueKind.String
                ? kind.GetString() == nameof(IvrActionKind.SubMenu)
                : kind.GetInt32() == (int)IvrActionKind.SubMenu,
            json);
        Assert.Equal(
            ["RouteToQueue", "RouteToAgent", "Voicemail", "ExternalTransfer", "Repeat", "SubMenu"],
            Enum.GetNames<IvrActionKind>());
    }

    private static async Task<(ModelBindingResult Result, ModelStateDictionary ModelState)> BindAsync(string json)
    {
        var localizer = new Mock<IStringLocalizer<IvrFlowJsonModelBinder>>();
        localizer
            .Setup(instance => instance[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] arguments) => new LocalizedString(name, string.Format(CultureInfo.InvariantCulture, name, arguments)));

        var binder = new IvrFlowJsonModelBinder(localizer.Object);
        var modelState = new ModelStateDictionary();
        var context = new DefaultModelBindingContext
        {
            ModelName = FieldName,
            ModelState = modelState,
            ValueProvider = new FormValueProvider(
                BindingSource.Form,
                new FormCollection(new Dictionary<string, StringValues> { [FieldName] = json }),
                CultureInfo.InvariantCulture),
        };

        await binder.BindModelAsync(context);

        return (context.Result, modelState);
    }

    private static string ReadFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "ivr-menu", "all-action-kinds.json");

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Unable to locate the IVR menu editor fixture from the test assembly location.");
    }
}
