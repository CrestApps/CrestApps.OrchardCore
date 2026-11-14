using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Reply templates: placeholders are filled in from the record a reply is for, a plain body never changes, a body that
/// fails to render or a site without Liquid gives the body as written, and other modules register the scopes a
/// template can be written for.
/// </summary>
public sealed class MessageTemplateRendererTests
{
    [Fact]
    public async Task Placeholders_AreFilledInFromTheRecords()
    {
        // Arrange
        var renderer = Renderer(withLiquid: true);
        var template = new MessageTemplate { Body = "Hi {{ Contact.DisplayName }}, your request {{ Ticket.Number }} is solved. <3" };

        // Act
        var body = await renderer.RenderAsync(template, new Dictionary<string, object>
        {
            ["Contact"] = new { DisplayName = "Sam & Co" },
            ["Ticket"] = new { Number = "T-1001" },
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Hi Sam & Co, your request T-1001 is solved. <3", body);
    }

    [Fact]
    public async Task APlainBody_ABrokenOne_OrASiteWithoutLiquid_GiveTheBodyAsWritten()
    {
        // Arrange
        var renderer = Renderer(withLiquid: true);
        var withoutLiquid = Renderer(withLiquid: false);
        var models = new Dictionary<string, object> { ["Contact"] = new { DisplayName = "Sam" } };

        // Act
        var plain = await renderer.RenderAsync(new MessageTemplate { Body = "Thanks, we're on it." }, models, TestContext.Current.CancellationToken);
        var broken = await renderer.RenderAsync(new MessageTemplate { Body = "Hi {% if %}" }, models, TestContext.Current.CancellationToken);
        var noLiquid = await withoutLiquid.RenderAsync(new MessageTemplate { Body = "Hi {{ Contact.DisplayName }}" }, models, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Thanks, we're on it.", plain);
        Assert.Equal("Hi {% if %}", broken);
        Assert.Equal("Hi {{ Contact.DisplayName }}", noLiquid);
    }

    [Fact]
    public void Scopes_AreRegisteredByOtherModules()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddMessageTemplateScope("Tickets", LocalizationSource.Create<MessageTemplateRendererTests>("Tickets"))
            .AddMessageTemplateScope("Tickets", LocalizationSource.Create<MessageTemplateRendererTests>("Help desk"));

        // Act
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<MessageTemplateScopeOptions>>().Value;

        // Assert
        var scope = Assert.Single(options.Scopes);
        Assert.Equal("Tickets", scope.Key);
    }

    private static LiquidMessageTemplateRenderer Renderer(bool withLiquid)
    {
        return new LiquidMessageTemplateRenderer(withLiquid ? [LiquidDoubles.Manager()] : [], NullLogger<LiquidMessageTemplateRenderer>.Instance);
    }
}
