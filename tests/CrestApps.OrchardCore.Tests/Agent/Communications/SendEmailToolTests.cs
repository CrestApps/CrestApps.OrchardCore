using CrestApps.OrchardCore.AI.Agent.Communications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Agent.Communications;

public sealed class SendEmailToolTests
{
    [Fact]
    public async Task InvokeAsync_WithNullHttpContext_ShouldSendEmailWithoutSender()
    {
        // Arrange: simulate a background task where HttpContext is null.
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(x => x.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var services = CreateServiceProvider(emailService.Object, httpContextAccessor.Object);

        var tool = new SendEmailTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["to"] = "test@example.com",
            ["subject"] = "Test Subject",
            ["body"] = "<p>Test body</p>",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: email should be sent successfully without throwing NullReferenceException.
        emailService.Verify(x => x.SendAsync(It.Is<MailMessage>(m =>
        m.To == "test@example.com"
            && m.Subject == "Test Subject"
                && m.HtmlBody == "<p>Test body</p>"
                    && m.Sender == null
                        && m.From == null),
        It.IsAny<string>()),
        Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithHttpContext_ShouldSendEmailWithSenderFromUser()
    {
        // Arrange: simulate an HTTP request context with an authenticated user.
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(x => x.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var mockUser = new Mock<IUser>();
        var userManager = MockUserManager();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(mockUser.Object);
        userManager.Setup(x => x.GetEmailAsync(mockUser.Object))
            .ReturnsAsync("sender@example.com");

        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var services = CreateServiceProvider(emailService.Object, httpContextAccessor.Object, userManager.Object);

        var tool = new SendEmailTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["to"] = "recipient@example.com",
            ["subject"] = "Hello",
            ["body"] = "World",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: email should include sender from the authenticated user.
        emailService.Verify(x => x.SendAsync(It.Is<MailMessage>(m =>
        m.Sender == "sender@example.com"
            && m.From == "sender@example.com"),
        It.IsAny<string>()),
        Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithMissingTo_ShouldReturnErrorMessage()
    {
        // Arrange
        var emailService = new Mock<IEmailService>();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var services = CreateServiceProvider(emailService.Object, httpContextAccessor.Object);

        var tool = new SendEmailTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["subject"] = "Test",
            ["body"] = "Test",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: no email should be sent.
        emailService.Verify(x => x.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithMissingSubject_ShouldReturnErrorMessage()
    {
        // Arrange
        var emailService = new Mock<IEmailService>();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var services = CreateServiceProvider(emailService.Object, httpContextAccessor.Object);

        var tool = new SendEmailTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["to"] = "test@example.com",
            ["body"] = "Test",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        emailService.Verify(x => x.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithMissingBody_ShouldReturnErrorMessage()
    {
        // Arrange
        var emailService = new Mock<IEmailService>();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var services = CreateServiceProvider(emailService.Object, httpContextAccessor.Object);

        var tool = new SendEmailTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["to"] = "test@example.com",
            ["subject"] = "Test",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        emailService.Verify(x => x.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithCcAndBcc_ShouldSetOptionalFields()
    {
        // Arrange
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(x => x.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var services = CreateServiceProvider(emailService.Object, httpContextAccessor.Object);

        var tool = new SendEmailTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["to"] = "test@example.com",
            ["subject"] = "Test",
            ["body"] = "Body",
            ["cc"] = "cc@example.com",
            ["bcc"] = "bcc@example.com",
        })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        emailService.Verify(x => x.SendAsync(It.Is<MailMessage>(m =>
        m.Cc == "cc@example.com"
            && m.Bcc == "bcc@example.com"),
        It.IsAny<string>()),
        Times.Once);
    }

    private static ServiceProvider CreateServiceProvider(
        IEmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        UserManager<IUser> userManager = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(emailService);
        services.AddSingleton(httpContextAccessor);
        services.AddSingleton<ILogger<SendEmailTool>>(NullLogger<SendEmailTool>.Instance);

        if (userManager is not null)
        {
            services.AddSingleton(userManager);
        }
        else
        {
            services.AddSingleton(MockUserManager().Object);
        }

        return services.BuildServiceProvider();
    }

    private static Mock<UserManager<IUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<IUser>>();

        return new Mock<UserManager<IUser>>(
            store.Object, null, null, null, null, null, null, null, null);
    }
}
