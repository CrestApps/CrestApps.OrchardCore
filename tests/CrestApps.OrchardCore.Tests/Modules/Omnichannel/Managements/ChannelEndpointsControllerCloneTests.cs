using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Controllers;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
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

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

public sealed class ChannelEndpointsControllerCloneTests
{
    private const string SourceId = "address-1";
    private const string NewId = "address-2";

    // A clone is a new number set up like an existing one. What only one address may hold stays behind: the number
    // itself (an address is listed once), the ids merged into it (they would make it answer for the source), and the
    // agents who dial or text from it (an agent has one number per channel, so keeping them would refuse the save).
    [Fact]
    public async Task Clone_KeepsTheSetupButNotWhatOnlyOneAddressMayHold()
    {
        var source = Source();
        var harness = CreateHarness(source);

        var result = await harness.Controller.Create(OmnichannelAddressTypes.PhoneNumber, SourceId);

        Assert.IsType<ViewResult>(result);

        var clone = Assert.Single(harness.Edited);
        Assert.Equal(NewId, clone.ItemId);
        Assert.Equal("Copy of Support line", clone.DisplayText);
        Assert.Equal(OmnichannelAddressTypes.PhoneNumber, clone.AddressType);
        Assert.Equal(["Phone", "SMS"], clone.Capabilities);
        Assert.Equal("Twilio", clone.ProviderName);
        Assert.Equal("Main support number", clone.Description);
        Assert.Null(clone.Value);
        Assert.Empty(clone.MergedItemIds);
        Assert.False(clone.Properties?.ContainsKey("OutboundLineSettings") ?? false);

        // The source is not changed by building its copy.
        Assert.Equal("+17025550100", source.Value);
        Assert.Equal(["old-address"], source.MergedItemIds);
    }

    [Fact]
    public async Task Clone_SaveCreatesANewAddress()
    {
        var harness = CreateHarness(Source());

        var result = await harness.Controller.CreatePost(OmnichannelAddressTypes.PhoneNumber, SourceId);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Manager.Verify(manager => manager.CreateAsync(
            It.Is<OmnichannelChannelEndpoint>(address => address.ItemId == NewId && address.DisplayText == "Copy of Support line"),
            It.IsAny<CancellationToken>()), Times.Once);
        harness.Manager.Verify(manager => manager.UpdateAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The type in the route has to be the source's: a clone cannot turn a phone number into another kind of address.
    [Fact]
    public async Task Clone_UnderAnotherAddressType_IsNotFound()
    {
        var harness = CreateHarness(Source());

        var result = await harness.Controller.Create(OmnichannelAddressTypes.EmailAddress, SourceId);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(harness.Edited);
    }

    private static OmnichannelChannelEndpoint Source()
    {
        var source = new OmnichannelChannelEndpoint
        {
            ItemId = SourceId,
            DisplayText = "Support line",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = ["Phone", "SMS"],
            MergedItemIds = ["old-address"],
            Value = "+17025550100",
            Description = "Main support number",
            ProviderName = "Twilio",
        };

        source.Properties ??= new Dictionary<string, object>();
        source.Properties["OutboundLineSettings"] = new JsonObject { ["UserIds"] = new JsonArray("agent-1") };

        return source;
    }

    private static Harness CreateHarness(OmnichannelChannelEndpoint source)
    {
        var harness = new Harness();

        harness.Manager
            .Setup(manager => manager.FindByIdAsync(source.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);

        // As the address handler does: a new id, then every portable member of the data.
        harness.Manager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JsonNode data, CancellationToken _) =>
            {
                var entry = new OmnichannelChannelEndpoint { ItemId = NewId };
                OmnichannelDeploymentSerializer.Populate(entry, data);

                return entry;
            });

        harness.Manager
            .Setup(manager => manager.ValidateAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResultDetails());

        var displayManager = new Mock<IDisplayManager<OmnichannelChannelEndpoint>>();
        displayManager
            .Setup(manager => manager.BuildEditorAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback((OmnichannelChannelEndpoint model, IUpdateModel _, bool _, string _, string _) => harness.Edited.Add(model))
            .ReturnsAsync(Mock.Of<IShape>());
        displayManager
            .Setup(manager => manager.UpdateEditorAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback((OmnichannelChannelEndpoint model, IUpdateModel _, bool _, string _, string _) => harness.Edited.Add(model))
            .ReturnsAsync(Mock.Of<IShape>());

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var updater = new Mock<IUpdateModel>();
        updater.SetupGet(value => value.ModelState).Returns(new ModelStateDictionary());

        var addressOptions = new OmnichannelAddressOptions();
        addressOptions.AddressTypes[OmnichannelAddressTypes.PhoneNumber] = new OmnichannelAddressType { Name = OmnichannelAddressTypes.PhoneNumber };
        addressOptions.AddressTypes[OmnichannelAddressTypes.EmailAddress] = new OmnichannelAddressType { Name = OmnichannelAddressTypes.EmailAddress };
        addressOptions.Capabilities["Phone"] = new OmnichannelAddressCapability { Name = "Phone", AddressType = OmnichannelAddressTypes.PhoneNumber };
        addressOptions.Capabilities["SMS"] = new OmnichannelAddressCapability { Name = "SMS", AddressType = OmnichannelAddressTypes.PhoneNumber };
        addressOptions.Capabilities["Email"] = new OmnichannelAddressCapability { Name = "Email", AddressType = OmnichannelAddressTypes.EmailAddress };

        harness.Controller = new ChannelEndpointsController(
            harness.Manager.Object,
            authorization.Object,
            Mock.Of<IUpdateModelAccessor>(accessor => accessor.ModelUpdater == updater.Object),
            displayManager.Object,
            Mock.Of<INotifier>(),
            Microsoft.Extensions.Options.Options.Create(addressOptions),
            new TestHtmlLocalizer(),
            new TestStringLocalizer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
                },
            },
        };

        return harness;
    }

    private sealed class TestHtmlLocalizer : IHtmlLocalizer<ChannelEndpointsController>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class TestStringLocalizer : IStringLocalizer<ChannelEndpointsController>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class Harness
    {
        public Mock<ICatalogManager<OmnichannelChannelEndpoint>> Manager { get; } = new();

        public ChannelEndpointsController Controller { get; set; }

        public List<OmnichannelChannelEndpoint> Edited { get; } = [];
    }
}
