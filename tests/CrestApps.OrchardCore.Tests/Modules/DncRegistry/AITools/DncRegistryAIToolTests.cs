using System.Text;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.AI;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Media;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry.AITools;

/// <summary>
/// The local do-not-call tools go through the list manager the Local DNC Registry page uses, with the page's state
/// checks. Every tool refuses a caller who is not signed in or lacks <b>Manage DNC registry settings</b> before
/// anything is read or changed.
/// </summary>
public sealed class DncRegistryAIToolTests
{
    private readonly Mock<ILocalDncListManager> _lists = new();
    private readonly Mock<INationalDoNotCallRegistry> _local = new();
    private readonly Mock<INationalDoNotCallRegistry> _national = new();
    private readonly Mock<IPhoneNumberService> _phoneNumbers = new();
    private readonly Mock<IMediaFileStore> _media = new();

    public DncRegistryAIToolTests()
    {
        _local.SetupGet(registry => registry.Key).Returns(LocalDncRegistry.RegistryKey);
        _local.SetupGet(registry => registry.DisplayName).Returns("Local Do Not Call Registry");
        _national.SetupGet(registry => registry.Key).Returns("usa-ftc");
        _national.SetupGet(registry => registry.DisplayName).Returns("USA FTC Registry");
        _phoneNumbers.Setup(service => service.GetCountryCode("US")).Returns(1);
    }

    public static TheoryData<string> ToolNames
        => new()
        {
            ListLocalDncListsTool.TheName,
            ImportLocalDncListTool.TheName,
            SetLocalDncListImportStateTool.TheName,
            DeleteLocalDncListTool.TheName,
        };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), Build(signedIn: false), Arguments());

        Assert.Equal(DncToolBase.NotAllowedMessage, result);
        AssertNothingReached();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), Build(authorize: (_, _) => false), Arguments());

        Assert.Equal(DncToolBase.NotAllowedMessage, result);
        AssertNothingReached();
    }

    [Fact]
    public async Task ListLocalDncLists_PagesTheListsOfACountryWithoutTheirRejectedRows()
    {
        _lists.Setup(manager => manager.GetCountAsync("US", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _lists.Setup(manager => manager.GetListsAsync(1, 20, "US", It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new LocalDncList
            {
                ListId = "list-1",
                Name = "State list 2026",
                CountryCode = "US",
                Status = LocalDncListStatus.Completed,
                PhoneNumberCount = 2,
                ErrorMessages = new() { [3] = "+17025550123 is not a valid number" },
                CreatedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        ]);

        var text = await AgentToolTestContext.InvokeAsync(new ListLocalDncListsTool(), Build(), new Dictionary<string, object> { ["countryCode"] = "us" });
        var result = AgentToolTestContext.ReadJson(text);

        var list = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("Completed", list.GetProperty("status").GetString());
        Assert.Equal(1, list.GetProperty("rejectedRows").GetInt32());
        Assert.DoesNotContain("5550123", text);
    }

    [Fact]
    public async Task ImportLocalDncList_QueuesInlineNumbersAsAOneColumnFile()
    {
        string written = null;
        _lists.Setup(manager => manager.QueueImportAsync("Complaints June", "US", "Complaints June.csv", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string _, Stream stream, CancellationToken _) => written = new StreamReader(stream, Encoding.UTF8).ReadToEnd())
            .ReturnsAsync(new LocalDncList { ListId = "list-2", Name = "Complaints June", CountryCode = "US", Status = LocalDncListStatus.Pending });

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ImportLocalDncListTool(), Build(), new Dictionary<string, object>
        {
            ["name"] = "Complaints June",
            ["countryCode"] = "US",
            ["phoneNumbers"] = new[] { "+1 702 555 0123", "(702) 555-0124" },
        }));

        Assert.Equal("PhoneNumber\n+1 702 555 0123\n(702) 555-0124", written);
        Assert.Equal("list-2", result.GetProperty("queued").GetProperty("id").GetString());
        Assert.Contains("within about 10 minutes", result.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ImportLocalDncList_RefusesNumbersAndAFileTogether()
    {
        var result = await AgentToolTestContext.InvokeAsync(new ImportLocalDncListTool(), Build(), new Dictionary<string, object>
        {
            ["name"] = "Complaints June",
            ["countryCode"] = "US",
            ["phoneNumbers"] = new[] { "+17025550123" },
            ["mediaPath"] = "dnc/list.csv",
        });

        Assert.Equal("Nothing was imported: give either phoneNumbers or mediaPath.", result);
        Assert.Empty(_lists.Invocations);
    }

    [Fact]
    public async Task ImportLocalDncList_RefusesAMediaPathOutsideTheLibrary()
    {
        var result = await AgentToolTestContext.InvokeAsync(new ImportLocalDncListTool(), Build(), new Dictionary<string, object>
        {
            ["name"] = "State list",
            ["countryCode"] = "US",
            ["mediaPath"] = "../App_Data/secrets.csv",
        });

        Assert.StartsWith("Nothing was imported: '../App_Data/secrets.csv' is not a media path.", result);
        Assert.Empty(_lists.Invocations);
        _media.Verify(store => store.GetFileInfoAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ImportLocalDncList_RefusesAnUnknownCountry()
    {
        var result = await AgentToolTestContext.InvokeAsync(new ImportLocalDncListTool(), Build(), new Dictionary<string, object>
        {
            ["name"] = "State list",
            ["countryCode"] = "ZZ",
            ["phoneNumbers"] = new[] { "+17025550123" },
        });

        Assert.StartsWith("Nothing was imported: countryCode must be", result);
        Assert.Empty(_lists.Invocations);
    }

    [Fact]
    public async Task SetLocalDncListImportState_ResumesAPausedList()
    {
        var list = new LocalDncList { ListId = "list-1", Name = "State list", Status = LocalDncListStatus.Paused };
        _lists.Setup(manager => manager.FindByIdAsync("list-1", It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var result = await AgentToolTestContext.InvokeAsync(new SetLocalDncListImportStateTool(), Build(), new Dictionary<string, object>
        {
            ["listId"] = "list-1",
            ["action"] = "process",
        });

        Assert.Contains("resumed", result);
        _lists.Verify(manager => manager.ResumeImportAsync("list-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetLocalDncListImportState_PausesOnlyAListBeingProcessed()
    {
        _lists.Setup(manager => manager.FindByIdAsync("list-1", It.IsAny<CancellationToken>())).ReturnsAsync(new LocalDncList { ListId = "list-1", Name = "State list", Status = LocalDncListStatus.Completed });

        var result = await AgentToolTestContext.InvokeAsync(new SetLocalDncListImportStateTool(), Build(), new Dictionary<string, object>
        {
            ["listId"] = "list-1",
            ["action"] = "pause",
        });

        Assert.Equal("Nothing was changed: the list 'State list' is Completed; only a list being processed can be paused.", result);
        _lists.Verify(manager => manager.PauseImportAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteLocalDncList_MarksTheListAsBeingDeleted()
    {
        _lists.Setup(manager => manager.FindByIdAsync("list-1", It.IsAny<CancellationToken>())).ReturnsAsync(new LocalDncList { ListId = "list-1", Name = "State list", Status = LocalDncListStatus.Completed, PhoneNumberCount = 40 });

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteLocalDncListTool(), Build(), new Dictionary<string, object>
        {
            ["listId"] = "list-1",
        }));

        Assert.Equal(40, result.GetProperty("deleting").GetProperty("phoneNumberCount").GetInt32());
        _lists.Verify(manager => manager.MarkAsDeletingAsync("list-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteLocalDncList_RefusesAListThatDoesNotExist()
    {
        var result = await AgentToolTestContext.InvokeAsync(new DeleteLocalDncListTool(), Build(), new Dictionary<string, object>
        {
            ["listId"] = "missing",
        });

        Assert.StartsWith("Nothing was deleted: no local do-not-call list has the id 'missing'.", result);
        _lists.Verify(manager => manager.MarkAsDeletingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListLocalDncListsTool.TheName => new ListLocalDncListsTool(),
            ImportLocalDncListTool.TheName => new ImportLocalDncListTool(),
            SetLocalDncListImportStateTool.TheName => new SetLocalDncListImportStateTool(),
            _ => new DeleteLocalDncListTool(),
        };

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["listId"] = "list-1",
            ["action"] = "pause",
            ["name"] = "State list",
            ["countryCode"] = "US",
            ["phoneNumbers"] = new[] { "+17025550123" },
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_lists.Object);
        services.AddSingleton(_local.Object);
        services.AddSingleton(_national.Object);
        services.AddSingleton(_phoneNumbers.Object);
        services.AddSingleton(_media.Object);

        return services.BuildServiceProvider();
    }

    private void AssertNothingReached()
    {
        Assert.Empty(_lists.Invocations);
        _local.Verify(registry => registry.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()), Times.Never);
        _phoneNumbers.Verify(service => service.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Never);
    }
}
