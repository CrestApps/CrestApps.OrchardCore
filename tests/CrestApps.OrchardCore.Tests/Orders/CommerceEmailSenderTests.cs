using System.Globalization;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// Every commerce email is built in its culture, passes through the store's template of its kind, and is sent, and a
/// send that fails is reported without throwing.
/// </summary>
public sealed class CommerceEmailSenderTests
{
    private readonly List<MailMessage> _sent = [];

    [Fact]
    public async Task AnEmailOfAKind_GoesThroughTheStoresTemplate_WithItsValues()
    {
        // Arrange
        OrderEmailContext applied = null;
        var templates = new Mock<IOrderEmailTemplates>();
        templates
            .Setup(value => value.ApplyAsync(It.IsAny<OrderEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<OrderEmailContext, CancellationToken>((context, _) => applied = context)
            .ReturnsAsync(new OrderEmailMessage("Templated", "Templated text", "<p>Templated</p>"));

        var order = new Order { OrderNumber = "261021-MAIL01" };
        var sender = new CommerceEmailSender(Services(EmailService(succeeds: true)), templates.Object, NullLogger<CommerceEmailSender>.Instance);

        // Act
        var sent = await sender.SendAsync(Email(OrderEmailKinds.Confirmation, order), _ => Task.FromResult(Content()), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(sent);
        Assert.Equal(OrderEmailKinds.Confirmation, applied.Kind);
        Assert.Same(order, applied.Order);
        Assert.Equal("https://store.test/orders/view/token", applied.Values["OrderUrl"]);

        var message = Assert.Single(_sent);
        Assert.Equal("sam@example.com", message.To);
        Assert.Equal("Templated", message.Subject);
        Assert.Equal("Templated text", message.TextBody);
        Assert.Equal("<p>Templated</p>", message.HtmlBody);
    }

    [Fact]
    public async Task AnEmailWithoutAKind_GoesAsBuilt()
    {
        // Arrange
        var templates = new Mock<IOrderEmailTemplates>(MockBehavior.Strict);
        var sender = new CommerceEmailSender(Services(EmailService(succeeds: true)), templates.Object, NullLogger<CommerceEmailSender>.Instance);

        // Act
        var sent = await sender.SendAsync(Email(kind: null), _ => Task.FromResult(Content()), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(sent);

        var message = Assert.Single(_sent);
        Assert.Equal("Built", message.Subject);
        Assert.Equal("Built text", message.TextBody);
        Assert.Equal("<p>Built</p>", message.HtmlBody);
    }

    [Fact]
    public async Task AnEmail_IsBuiltInItsCulture()
    {
        // Arrange
        string builtIn = null;
        var sender = CommerceEmailSenders.Create(Services(EmailService(succeeds: true)));
        var email = new CommerceEmail { To = "sam@example.com", Culture = "fr-FR" };

        // Act
        await sender.SendAsync(email, _ =>
        {
            builtIn = CultureInfo.CurrentUICulture.Name;

            return Task.FromResult(Content());
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("fr-FR", builtIn);
    }

    [Fact]
    public async Task WithoutTheEmailService_ARecipient_OrAnythingBuilt_NothingIsSent()
    {
        // Arrange
        var built = 0;
        var withoutEmail = CommerceEmailSenders.Create(new ServiceCollection().BuildServiceProvider());
        var withEmail = CommerceEmailSenders.Create(Services(EmailService(succeeds: true)));

        Task<CommerceEmailContent> Build(CancellationToken cancellationToken)
        {
            built++;

            return Task.FromResult(Content());
        }

        // Act
        var noService = await withoutEmail.SendAsync(Email(kind: null), Build, TestContext.Current.CancellationToken);
        var noRecipient = await withEmail.SendAsync(new CommerceEmail { To = " " }, Build, TestContext.Current.CancellationToken);
        var nothingBuilt = await withEmail.SendAsync(Email(kind: null), _ => Task.FromResult<CommerceEmailContent>(null), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(withoutEmail.CanSend);
        Assert.False(noService);
        Assert.False(noRecipient);
        Assert.False(nothingBuilt);
        Assert.Equal(0, built);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task ASendThatFailsOrThrows_ReturnsFalse()
    {
        // Arrange
        var failing = CommerceEmailSenders.Create(Services(EmailService(succeeds: false)));

        var throwingService = new Mock<IEmailService>();
        throwingService
            .Setup(value => value.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The mail server is down."));

        var throwing = CommerceEmailSenders.Create(throwingService.Object);

        // Act
        var failed = await failing.SendAsync(Email(kind: null), _ => Task.FromResult(Content()), TestContext.Current.CancellationToken);
        var threw = await throwing.SendAsync(Email(kind: null), _ => Task.FromResult(Content()), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(failed);
        Assert.False(threw);
    }

    [Fact]
    public void AKindRegisteredTwice_IsListedOnce_WithTheLastTitle()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddCommerceEmailKind("booking", LocalizationSource.Create<CommerceEmailSenderTests>("Booking"))
            .AddCommerceEmailKind(OrderEmailKinds.Confirmation, LocalizationSource.Create<CommerceEmailSenderTests>("Order confirmation"))
            .AddCommerceEmailKind("booking", LocalizationSource.Create<CommerceEmailSenderTests>("Booking confirmed"));

        // Act
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<CommerceEmailOptions>>().Value;

        // Assert
        Assert.Equal(new[] { "booking", OrderEmailKinds.Confirmation }, options.Kinds.Select(kind => kind.Name));
        Assert.Equal("Booking confirmed", options.Kinds[0].Title.Value);
        Assert.True(options.HasKind("booking"));
        Assert.False(options.HasKind(OrderEmailKinds.Shipped));
    }

    private static CommerceEmail Email(string kind, Order order = null)
        => new()
        {
            Kind = kind,
            To = "sam@example.com",
            Order = order,
            Reference = "test",
        };

    private static CommerceEmailContent Content()
        => new()
        {
            Subject = "Built",
            Text = "Built text",
            Html = "<p>Built</p>",
            Values = new Dictionary<string, string>
            {
                ["OrderUrl"] = "https://store.test/orders/view/token",
            },
        };

    private static ServiceProvider Services(IEmailService emailService)
        => new ServiceCollection().AddSingleton(emailService).BuildServiceProvider();

    private IEmailService EmailService(bool succeeds)
    {
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(value => value.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => _sent.Add(message))
            .ReturnsAsync(succeeds ? Result.Success() : Result.Failed());

        return emailService.Object;
    }
}
