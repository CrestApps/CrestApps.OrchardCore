using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The rules of a reply template, wherever it is saved from (the editor or a recipe): placeholders that don't parse are
/// refused, unless the template is plain text; a scope this site doesn't register is kept; and a scope is at most as
/// long as its index column.
/// </summary>
public sealed class MessageTemplateRulesTests
{
    [Fact]
    public async Task PlaceholdersThatDontParse_AreRefused_UnlessTheTemplateIsPlainText()
    {
        // Act
        var broken = await ValidateAsync(new MessageTemplate { Name = "Hi", Body = "Hi {{ Contact.DisplayName }" });
        var plain = await ValidateAsync(new MessageTemplate { Name = "Hi", Body = "Hi {{ Contact.DisplayName }", IsPlainText = true });
        var fine = await ValidateAsync(new MessageTemplate { Name = "Hi", Body = "Hi {{ Contact.DisplayName }}" });

        // Assert
        Assert.Contains(broken.Result.Errors, error => error.MemberNames.Contains(nameof(MessageTemplate.Body)));
        Assert.True(plain.Result.Succeeded);
        Assert.True(fine.Result.Succeeded);
    }

    [Fact]
    public async Task AScopeThisSiteDoesntRegister_IsKept_WithinItsLength()
    {
        // Arrange
        var unknown = new MessageTemplate { Name = "Macro", Body = "Done.", Scope = " Tickets " };

        // Act
        var kept = await ValidateAsync(unknown);
        var tooLong = await ValidateAsync(new MessageTemplate { Name = "Macro", Body = "Done.", Scope = new string('s', 65) });

        // Assert
        Assert.True(kept.Result.Succeeded);
        Assert.Equal("Tickets", unknown.Scope);
        Assert.Contains(tooLong.Result.Errors, error => error.MemberNames.Contains(nameof(MessageTemplate.Scope)));
    }

    private static async Task<ValidatingContext<MessageTemplate>> ValidateAsync(MessageTemplate template)
    {
        var handler = new MessageTemplateHandler(Mock.Of<IClock>(), new PassThrough());
        var context = new ValidatingContext<MessageTemplate>(template);

        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private sealed class PassThrough : IStringLocalizer<MessageTemplateHandler>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
