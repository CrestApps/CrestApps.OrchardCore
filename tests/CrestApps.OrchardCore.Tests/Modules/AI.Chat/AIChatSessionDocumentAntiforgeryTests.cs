using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Filters;
using CrestApps.OrchardCore.AI.Chat.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// MD6: the package's chat session document endpoints carry <see cref="CookieAuthenticationAntiforgeryEndpointFilter"/>.
/// A chat that uploads documents is given the request's antiforgery token in its configuration, which the vendored
/// scripts send as the <c>RequestVerificationToken</c> header; the filter accepts that post and refuses one without it.
/// </summary>
public sealed class AIChatSessionDocumentAntiforgeryTests
{
    private const string HeaderName = "RequestVerificationToken";

    private static readonly object _passed = new();

    [Fact]
    public async Task ChatWithDocumentUploads_GetsAToken_TheEndpointFilterAccepts()
    {
        using var services = CreateServices();
        var user = SignedInUser();
        var page = NewContext(services, user, HttpMethods.Get);

        var config = JsonNode.Parse(CreateConfiguration(services, page).SerializeChatConfig(new
        {
            uploadDocumentUrl = "/ai/chat-sessions/upload-document",
            removeDocumentUrl = "/ai/chat-sessions/remove-document",
        }))!.AsObject();

        var token = config["antiforgeryToken"]?.GetValue<string>();

        Assert.False(string.IsNullOrEmpty(token));

        var upload = NewContext(services, user, HttpMethods.Post);
        upload.Request.Headers.Cookie = CookieHeader(page);
        upload.Request.Headers[HeaderName] = token;

        var result = await InvokeFilterAsync(upload);

        Assert.Same(_passed, result);
    }

    [Fact]
    public async Task UploadWithoutTheToken_IsRefused()
    {
        using var services = CreateServices();
        var user = SignedInUser();
        var page = NewContext(services, user, HttpMethods.Get);

        CreateConfiguration(services, page).SerializeChatConfig(new { uploadDocumentUrl = "/ai/chat-sessions/upload-document" });

        var upload = NewContext(services, user, HttpMethods.Post);
        upload.Request.Headers.Cookie = CookieHeader(page);

        var result = await InvokeFilterAsync(upload);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public void ChatWithoutDocumentUploads_GetsNoToken()
    {
        using var services = CreateServices();
        var page = NewContext(services, SignedInUser(), HttpMethods.Get);

        var widget = JsonNode.Parse(CreateConfiguration(services, page).SerializeWidgetConfig(new
        {
            chatConfig = new { uploadDocumentUrl = (string)null, removeDocumentUrl = (string)null },
        }))!.AsObject();

        Assert.Null(widget["chatConfig"]!["antiforgeryToken"]);
        Assert.False(page.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public void WidgetWithDocumentUploads_GetsTheTokenInItsChatConfiguration()
    {
        using var services = CreateServices();
        var page = NewContext(services, SignedInUser(), HttpMethods.Get);

        var widget = JsonNode.Parse(CreateConfiguration(services, page).SerializeWidgetConfig(new
        {
            chatConfig = new { uploadDocumentUrl = "/ai/chat-sessions/upload-document" },
        }))!.AsObject();

        Assert.False(string.IsNullOrEmpty(widget["chatConfig"]!["antiforgeryToken"]?.GetValue<string>()));
    }

    private static ValueTask<object> InvokeFilterAsync(DefaultHttpContext context)
        => new CookieAuthenticationAntiforgeryEndpointFilter()
            .InvokeAsync(new DefaultEndpointFilterInvocationContext(context), _ => ValueTask.FromResult(_passed));

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAntiforgery();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

        return services.BuildServiceProvider();
    }

    private static AIChatUIConfiguration CreateConfiguration(IServiceProvider services, DefaultHttpContext page)
        => new(
            services.GetRequiredService<IOptions<AIToolDefinitionOptions>>(),
            new HttpContextAccessor { HttpContext = page },
            services.GetRequiredService<IAntiforgery>(),
            new PassThroughLocalizer());

    private static ClaimsPrincipal SignedInUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Name, "admin")], "Identity.Application"));

    private static DefaultHttpContext NewContext(IServiceProvider services, ClaimsPrincipal user, string method)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = user,
        };

        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("site.example");

        return context;
    }

    /// <summary>
    /// The cookies the page's response set, as the browser sends them back on the upload.
    /// </summary>
    private static string CookieHeader(DefaultHttpContext page)
        => string.Join("; ", page.Response.Headers.SetCookie.Select(cookie => cookie.Split(';')[0]));

    private sealed class PassThroughLocalizer : IStringLocalizer<AIChatUIConfiguration>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
