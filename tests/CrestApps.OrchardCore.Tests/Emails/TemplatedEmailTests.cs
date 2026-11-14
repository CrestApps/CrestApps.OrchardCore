using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Emails;

/// <summary>
/// The site's templated emails, for any module: a kind's template comes from the first source that keeps it, with
/// that group's layout and values; a kind no source keeps goes as built; and the sender builds, applies and sends.
/// </summary>
public sealed class TemplatedEmailTests
{
    private readonly List<MailMessage> _sent = [];

    [Fact]
    public async Task AKindsTemplate_ComesFromTheSourceThatKeepsIt_WithItsLayoutAndValues()
    {
        // Arrange
        var templates = Templates(
            new FixedSource("ticket-received", new TemplatedEmailOverride
            {
                Template = new TemplatedEmailTemplate { IsEnabled = true, Subject = "[{{ Ticket.Number }}] {{ Subject }}", Body = "<p>Hi {{ Ticket.Requester }}, {{ DeskName }} got it.</p>" },
                Layout = "<div class=\"desk\">{{ Content }}</div>",
                Values = new Dictionary<string, string> { ["DeskName"] = "Help desk" },
            }),
            new FixedSource("ticket-received", new TemplatedEmailOverride { Layout = "<div class=\"other\">{{ Content }}</div>" }));

        // Act
        var message = await templates.ApplyAsync(Context("ticket-received"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("[T-1001] We got your request", message.Subject);
        Assert.Equal("<div class=\"desk\"><p>Hi Sam, Help desk got it.</p></div>", message.Html);
        Assert.Equal("Hi Sam, Help desk got it.", message.Text);
    }

    [Fact]
    public async Task AKindNoSourceKeeps_GoesAsBuilt()
    {
        // Arrange
        var templates = Templates(new FixedSource("ticket-received", new TemplatedEmailOverride { Layout = "<div>{{ Content }}</div>" }));

        // Act
        var message = await templates.ApplyAsync(Context("order-confirmation"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("We got your request", message.Subject);
        Assert.Equal("<p>Built</p>", message.Html);
        Assert.Equal("Built", message.Text);
    }

    [Fact]
    public async Task ATemplateThatFailsToRender_GivesTheBuiltEmail()
    {
        // Arrange
        var templates = Templates(new FixedSource("ticket-received", new TemplatedEmailOverride
        {
            Template = new TemplatedEmailTemplate { IsEnabled = true, Body = "{% if %}" },
        }));

        // Act
        var message = await templates.ApplyAsync(Context("ticket-received"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("<p>Built</p>", message.Html);
    }

    [Fact]
    public async Task TheSender_SendsTheTemplatedEmail_ToItsRecipient()
    {
        // Arrange
        var templates = Templates(new FixedSource("ticket-received", new TemplatedEmailOverride
        {
            Template = new TemplatedEmailTemplate { IsEnabled = true, Subject = "Re: {{ Subject }}" },
        }));
        var sender = new TemplatedEmailSender([EmailService()], templates, NullLoggerFactory.Instance);

        // Act
        var sent = await sender.SendAsync(
            new TemplatedEmail { Kind = "ticket-received", To = "sam@example.com", Models = new Dictionary<string, object> { ["Ticket"] = new { Number = "T-1001" } } },
            _ => Task.FromResult(new TemplatedEmailContent { Subject = "We got your request", Text = "Built", Html = "<p>Built</p>" }),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(sent);
        Assert.True(sender.CanSend);
        var message = Assert.Single(_sent);
        Assert.Equal("sam@example.com", message.To);
        Assert.Equal("Re: We got your request", message.Subject);
        Assert.Equal("<p>Built</p>", message.HtmlBody);
    }

    [Fact]
    public async Task ATemplate_ReadsOnlyTheEmailsValues_NeverTheRequestOrTheSite()
    {
        // Arrange: a template that reaches for objects Orchard's own Liquid would hand it.
        var templates = Templates(new FixedSource("ticket-received", new TemplatedEmailOverride
        {
            Template = new TemplatedEmailTemplate { IsEnabled = true, Body = "<p>[{{ Request.Headers.Cookie }}][{{ Site.SiteName }}][{{ Ticket.Number }}]</p>" },
        }));
        // Act
        var message = await templates.ApplyAsync(Context("ticket-received"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("<p>[][][T-1001]</p>", message.Html);
    }

    [Fact]
    public void AKindOfAnotherGroupWithTheSameName_IsRefused()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddTemplatedEmailKind("Tickets", "notice", LocalizationSource.Create<TemplatedEmailTests>("Notice"))
            .AddTemplatedEmailKind("Billing", "notice", LocalizationSource.Create<TemplatedEmailTests>("Notice"));

        // Act
        var exception = Record.Exception(() => services.BuildServiceProvider().GetRequiredService<IOptions<TemplatedEmailOptions>>().Value);

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Kinds_AreListedByGroup()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddTemplatedEmailKind("Tickets", "ticket-received", LocalizationSource.Create<TemplatedEmailTests>("Request received"))
            .AddTemplatedEmailKind("Tickets", "ticket-solved", LocalizationSource.Create<TemplatedEmailTests>("Request solved"))
            .AddTemplatedEmailKind("Billing", "invoice-due", LocalizationSource.Create<TemplatedEmailTests>("Invoice due"))
            .AddTemplatedEmailKind("Tickets", "ticket-received", LocalizationSource.Create<TemplatedEmailTests>("We got your request"));

        // Act
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<TemplatedEmailOptions>>().Value;

        // Assert
        Assert.Equal(["ticket-received", "ticket-solved"], options.GetKinds("Tickets").Select(kind => kind.Name));
        Assert.Equal("Billing", options.Find("invoice-due").Group);
        Assert.Null(options.Find("missing"));
    }

    private static LiquidTemplatedEmailTemplates Templates(params ITemplatedEmailTemplateSource[] sources)
    {
        var siteService = new Mock<ISiteService>();

        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(new SiteSettings { SiteName = "Test site" });

        return new LiquidTemplatedEmailTemplates(sources, siteService.Object, LiquidTestClocks.In("UTC"), RecipeRoundTrip.Localizer<LiquidTemplatedEmailTemplates>(), NullLoggerFactory.Instance);
    }

    private static TemplatedEmailContext Context(string kind)
        => new()
        {
            Kind = kind,
            Models = new Dictionary<string, object> { ["Ticket"] = new { Number = "T-1001", Requester = "Sam" } },
            Subject = "We got your request",
            Text = "Built",
            Html = "<p>Built</p>",
        };

    private IEmailService EmailService()
    {
        var emailService = new Mock<IEmailService>();

        emailService
            .Setup(value => value.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => _sent.Add(message))
            .ReturnsAsync(Result.Success());

        return emailService.Object;
    }

    private sealed class FixedSource : ITemplatedEmailTemplateSource
    {
        private readonly string _kind;
        private readonly TemplatedEmailOverride _found;

        public FixedSource(string kind, TemplatedEmailOverride found)
        {
            _kind = kind;
            _found = found;
        }

        public Task<TemplatedEmailOverride> FindAsync(string kind, CancellationToken cancellationToken = default)
            => Task.FromResult(kind == _kind ? _found : null);
    }
}
