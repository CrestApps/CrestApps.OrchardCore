using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterCatalogCloneTests
{
    private const string SourceId = "source-1";
    private const string NewId = "new-1";

    [Fact]
    public async Task CloneQueue_OpensTheEditorOnACopyWithEverySetting()
    {
        var source = Queue();
        var harness = Harness<ActivityQueue>.Create(source);
        var controller = Attach(new QueuesController(
            (IActivityQueueManager)harness.Manager.Object,
            harness.Authorization,
            harness.UpdateModelAccessor,
            harness.DisplayManager.Object,
            harness.Notifier,
            new TestHtmlLocalizer<QueuesController>(),
            new TestStringLocalizer<QueuesController>()));

        var result = await controller.Create(SourceId);

        Assert.IsType<ViewResult>(result);

        var clone = Assert.Single(harness.Edited);
        Assert.Equal(NewId, clone.ItemId);
        Assert.Equal("Copy of Support", clone.Name);
        Assert.Equal(60, clone.MaxWaitSeconds);
        Assert.Equal(QueueMaxWaitAction.Voicemail, clone.MaxWaitAction);
        Assert.Equal("1", clone.Treatment.CallbackDtmfKey);
        Assert.Equal(["billing"], clone.RequiredSkills);

        // The copy is a separate object: nothing done to it reaches the queue it came from.
        Assert.NotSame(source.Treatment, clone.Treatment);
        Assert.Equal("Support", source.Name);
    }

    // The post rebuilds the copy before binding the form, so the save creates a new queue and never touches the
    // source; settings the editor does not show are kept too.
    [Fact]
    public async Task CloneQueue_SaveCreatesANewQueueAndLeavesTheSourceAlone()
    {
        var harness = Harness<ActivityQueue>.Create(Queue());
        var controller = Attach(new QueuesController(
            (IActivityQueueManager)harness.Manager.Object,
            harness.Authorization,
            harness.UpdateModelAccessor,
            harness.DisplayManager.Object,
            harness.Notifier,
            new TestHtmlLocalizer<QueuesController>(),
            new TestStringLocalizer<QueuesController>()));

        var result = await controller.CreatePost(SourceId);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Manager.Verify(manager => manager.CreateAsync(
            It.Is<ActivityQueue>(queue => queue.ItemId == NewId && queue.Name == "Copy of Support" && queue.MaxWaitSeconds == 60),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ActivityQueue>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloneQueue_ThatNoLongerExists_IsNotFound()
    {
        var harness = Harness<ActivityQueue>.Create(Queue());
        var controller = Attach(new QueuesController(
            (IActivityQueueManager)harness.Manager.Object,
            harness.Authorization,
            harness.UpdateModelAccessor,
            harness.DisplayManager.Object,
            harness.Notifier,
            new TestHtmlLocalizer<QueuesController>(),
            new TestStringLocalizer<QueuesController>()));

        var result = await controller.Create("missing");

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(harness.Edited);
    }

    // A number is answered by one enabled entry point on each channel. A copy that kept the source's numbers could
    // never be saved, so it starts without them and keeps everything else, including the phone menu and the settings
    // other features store on the entry point.
    [Fact]
    public async Task CloneEntryPoint_KeepsItsSettingsButNotItsNumbers()
    {
        var source = new ContactCenterEntryPoint
        {
            ItemId = SourceId,
            Name = "Main line",
            Channel = "Phone",
            AddressIds = ["address-1"],
            DialedNumbers = ["+17025550100"],
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = "queue-1",
            WelcomeMessage = "Thanks for calling.",
            IvrFlow = new IvrFlow { RootNodeId = "main" },
        };
        source.Properties ??= new Dictionary<string, object>();
        source.Properties["ExtraSettings"] = new JsonObject { ["Mode"] = "Routed" };

        var harness = Harness<ContactCenterEntryPoint>.Create(source);
        var controller = Attach(new EntryPointsController(
            (IContactCenterEntryPointManager)harness.Manager.Object,
            harness.Authorization,
            harness.UpdateModelAccessor,
            harness.DisplayManager.Object,
            harness.Notifier,
            new TestHtmlLocalizer<EntryPointsController>(),
            new TestStringLocalizer<EntryPointsController>()));

        await controller.Create(SourceId);

        var clone = Assert.Single(harness.Edited);
        Assert.Equal(NewId, clone.ItemId);
        Assert.Equal("Copy of Main line", clone.Name);
        Assert.Empty(clone.AddressIds);
        Assert.Empty(clone.DialedNumbers);
        Assert.Equal("Phone", clone.Channel);
        Assert.Equal("queue-1", clone.TargetQueueId);
        Assert.Equal("Thanks for calling.", clone.WelcomeMessage);
        Assert.Equal("main", clone.IvrFlow.RootNodeId);
        Assert.True(clone.Properties.ContainsKey("ExtraSettings"));

        Assert.Equal(["address-1"], source.AddressIds);
    }

    private static TController Attach<TController>(TController controller)
        where TController : Controller
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
            },
        };

        return controller;
    }

    private static ActivityQueue Queue()
        => new()
        {
            ItemId = SourceId,
            Name = "Support",
            MaxWaitSeconds = 60,
            MaxWaitAction = QueueMaxWaitAction.Voicemail,
            RequiredSkills = ["billing"],
            Treatment = new QueueTreatmentSettings { CallbackDtmfKey = "1" },
        };

    private sealed class Harness<T>
        where T : CatalogItem, new()
    {
        public Mock<ICatalogManager<T>> Manager { get; private set; }

        public Mock<IDisplayManager<T>> DisplayManager { get; } = new();

        public List<T> Edited { get; } = [];

        public IAuthorizationService Authorization { get; private set; }

        public IUpdateModelAccessor UpdateModelAccessor { get; private set; }

        public INotifier Notifier { get; } = Mock.Of<INotifier>();

        public static Harness<T> Create(T source)
        {
            var harness = new Harness<T>();

            // The concrete controllers take their own manager interfaces; both derive from ICatalogManager<T>.
            harness.Manager = typeof(T) == typeof(ActivityQueue)
                ? new Mock<IActivityQueueManager>().As<ICatalogManager<T>>()
                : new Mock<IContactCenterEntryPointManager>().As<ICatalogManager<T>>();

            harness.Manager
                .Setup(manager => manager.FindByIdAsync(source.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);

            // As the entry handlers do: a new id, then every portable member of the data.
            harness.Manager
                .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((JsonNode data, CancellationToken _) =>
                {
                    var entry = new T { ItemId = NewId };
                    ContactCenterDeploymentSerializer.Populate(entry, data);

                    return entry;
                });

            harness.Manager
                .Setup(manager => manager.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResultDetails());

            harness.DisplayManager
                .Setup(manager => manager.BuildEditorAsync(It.IsAny<T>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((T model, IUpdateModel _, bool _, string _, string _) => harness.Edited.Add(model))
                .ReturnsAsync(Mock.Of<IShape>());

            harness.DisplayManager
                .Setup(manager => manager.UpdateEditorAsync(It.IsAny<T>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((T model, IUpdateModel _, bool _, string _, string _) => harness.Edited.Add(model))
                .ReturnsAsync(Mock.Of<IShape>());

            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());
            harness.Authorization = authorization.Object;

            var updater = new Mock<IUpdateModel>();
            updater.SetupGet(value => value.ModelState).Returns(new ModelStateDictionary());
            harness.UpdateModelAccessor = Mock.Of<IUpdateModelAccessor>(accessor => accessor.ModelUpdater == updater.Object);

            return harness;
        }
    }

    private sealed class TestHtmlLocalizer<T> : IHtmlLocalizer<T>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class TestStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
