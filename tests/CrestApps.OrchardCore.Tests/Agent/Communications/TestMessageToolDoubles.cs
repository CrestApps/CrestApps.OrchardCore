using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.Email;
using OrchardCore.Email.Services;
using OrchardCore.Infrastructure;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Agent.Communications;

/// <summary>
/// Stands in for the Email and SMS services in a tool test: two enabled providers and a disabled one for each, an
/// email service and SMS providers that record what they were asked to send, and a signed-in caller with an email
/// address. A provider can be made to fail with an error that holds <see cref="Canary"/>, as a provider could answer
/// with the host and the account of its settings.
/// </summary>
internal sealed class TestMessageToolDoubles
{
    /// <summary>
    /// The text the provider settings a failure names start with.
    /// </summary>
    public const string Canary = "CANARY";

    /// <summary>
    /// The email address of the signed-in caller.
    /// </summary>
    public const string CallerEmail = "admin@contoso.com";

    /// <summary>
    /// Gets the emails the email service was asked to send, with the provider each went through.
    /// </summary>
    public List<(MailMessage Message, string Provider)> Emails { get; } = [];

    /// <summary>
    /// Gets the text messages the SMS providers were asked to send, with the provider each went through.
    /// </summary>
    public List<(SmsMessage Message, string Provider)> TextMessages { get; } = [];

    /// <summary>
    /// Gets or sets the name of the default email provider.
    /// </summary>
    public string DefaultEmailProvider { get; set; } = "SMTP";

    /// <summary>
    /// Gets or sets the names of the SMS providers that are enabled.
    /// </summary>
    public string[] EnabledSmsProviders { get; set; } = ["Twilio", "Log"];

    /// <summary>
    /// Gets or sets a value indicating whether the providers throw when they send.
    /// </summary>
    public bool ProvidersThrow { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the providers answer that they could not send.
    /// </summary>
    public bool ProvidersFail { get; set; }

    /// <summary>
    /// Adds the doubles to the services of a tool call.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="signedIn">Whether a user is signed in.</param>
    public void AddTo(IServiceCollection services, bool signedIn = true)
    {
        if (signedIn)
        {
            // The caller of the shared test context has no email address; this one has.
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, AgentToolTestContext.UserId),
                    new Claim(ClaimTypes.Name, AgentToolTestContext.UserName),
                    new Claim(ClaimTypes.Email, CallerEmail),
                ], "Test")),
            };

            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);

            services.AddSingleton(httpContextAccessor.Object);
        }

        AddEmail(services);
        AddSms(services);
    }

    private void AddEmail(IServiceCollection services)
    {
        var providerOptions = new EmailProviderOptions();
        providerOptions.TryAddProvider("SMTP", new EmailProviderTypeOptions(typeof(FakeEmailProvider)) { IsEnabled = true });
        providerOptions.TryAddProvider("Azure", new EmailProviderTypeOptions(typeof(FakeEmailProvider)) { IsEnabled = true });
        providerOptions.TryAddProvider("Disabled", new EmailProviderTypeOptions(typeof(FakeEmailProvider)));

        var resolver = new Mock<IEmailProviderResolver>();
        resolver
            .Setup(r => r.GetAsync(It.IsAny<string>()))
            .Returns((string name) => new ValueTask<IEmailProvider>(new FakeEmailProvider(name)));

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((MailMessage message, string provider, CancellationToken _) =>
            {
                Emails.Add((message, provider));

                if (ProvidersThrow)
                {
                    throw new InvalidOperationException($"Could not sign in to smtp.contoso.com with the password {Canary}-SMTP-PASSWORD.");
                }

                return Task.FromResult(ProvidersFail
                    ? Result.Failed(new ResultError { Message = new LocalizedString("error", "The sender address is not allowed.") })
                    : Result.Success());
            });

        services.AddSingleton(Options.Create(providerOptions));
        services.AddSingleton(Options.Create(new EmailOptions { DefaultProviderName = DefaultEmailProvider }));
        services.AddSingleton(resolver.Object);
        services.AddSingleton(emailService.Object);
    }

    private void AddSms(IServiceCollection services)
    {
        var providerOptions = new SmsProviderOptions();

        foreach (var name in new[] { "Twilio", "Log", "Disabled" })
        {
            providerOptions.TryAddProvider(name, new SmsProviderTypeOptions(typeof(FakeSmsProvider))
            {
                IsEnabled = EnabledSmsProviders.Contains(name),
            });
        }

        var resolver = new Mock<ISmsProviderResolver>();
        resolver
            .Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => new FakeSmsProvider(this, name));

        var phoneValidator = new Mock<IPhoneFormatValidator>();
        phoneValidator
            .Setup(validator => validator.IsValid(It.IsAny<string>()))
            .Returns((string phoneNumber) => phoneNumber.StartsWith('+') && phoneNumber.Length > 8 && phoneNumber.Skip(1).All(char.IsDigit));

        services.AddSingleton(Options.Create(providerOptions));
        services.AddSingleton(resolver.Object);
        services.AddSingleton(phoneValidator.Object);
    }

    private sealed class FakeEmailProvider : IEmailProvider
    {
        private readonly string _name;

        public FakeEmailProvider(string name)
        {
            _name = name;
        }

        public LocalizedString DisplayName
            => new(_name, $"{_name} provider");

        public Task<Result> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class FakeSmsProvider : ISmsProvider
    {
        private readonly TestMessageToolDoubles _doubles;
        private readonly string _name;

        public FakeSmsProvider(TestMessageToolDoubles doubles, string name)
        {
            _doubles = doubles;
            _name = name;
        }

        public LocalizedString Name
            => new(_name, _name);

        public Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
        {
            _doubles.TextMessages.Add((message, _name));

            if (_doubles.ProvidersThrow)
            {
                throw new InvalidOperationException($"The account rejected the token {Canary}-TWILIO-TOKEN.");
            }

            return Task.FromResult(_doubles.ProvidersFail
                ? Result.Failed(new ResultError { Message = new LocalizedString("error", "The number is not reachable.") })
                : Result.Success());
        }
    }
}
