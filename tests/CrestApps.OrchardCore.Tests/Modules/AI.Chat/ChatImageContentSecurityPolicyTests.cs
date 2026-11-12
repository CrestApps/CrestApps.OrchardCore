using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// SEC-A16: chat output is sanitized with DOMPurify, which keeps images, so model output could load an image whose URL
/// carries the conversation. Pages that render chat send an img-src policy until the renderer itself restricts images
/// (upstream, in CrestApps.Core).
/// </summary>
public sealed class ChatImageContentSecurityPolicyTests
{
    private static readonly string[] _chatViews =
    [
        "src/Modules/CrestApps.OrchardCore.AI.Chat/Views/Widget-AIChat.cshtml",
        "src/Modules/CrestApps.OrchardCore.AI.Chat/Views/AIChatSessionChat.cshtml",
        "src/Modules/CrestApps.OrchardCore.AI.Chat/Views/AIChatAdminWidget.cshtml",
        "src/Modules/CrestApps.OrchardCore.AI.Chat/Views/Admin/Test.cshtml",
        "src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions/Views/ChatInteractionChat.cshtml",
    ];

    [Fact]
    public void Apply_SendsAnImageSourcePolicy()
    {
        var options = new ChatImageContentSecurityPolicyOptions();
        options.ImageSources.Add("https://cdn.example.com");
        options.ImageSources.Add("https://evil.example; script-src *");
        var httpContext = new DefaultHttpContext();

        new ChatImageContentSecurityPolicy(Options.Create(options)).Apply(httpContext);

        Assert.Equal("img-src 'self' data: blob: https://cdn.example.com", httpContext.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public void Apply_KeepsThePolicyTheSiteAlreadySends_AndAddsItsOwnOnce()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self'";
        var policy = new ChatImageContentSecurityPolicy(Options.Create(new ChatImageContentSecurityPolicyOptions()));

        policy.Apply(httpContext);
        policy.Apply(httpContext);

        Assert.Equal(["frame-ancestors 'self'", "img-src 'self' data: blob:"], httpContext.Response.Headers.ContentSecurityPolicy.ToArray());
    }

    [Fact]
    public void Apply_WhenTurnedOff_SendsNothing()
    {
        var options = new ChatImageContentSecurityPolicyOptions { Enabled = false };
        var httpContext = new DefaultHttpContext();

        new ChatImageContentSecurityPolicy(Options.Create(options)).Apply(httpContext);

        Assert.False(httpContext.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public void EveryViewThatRendersChat_AppliesThePolicy()
    {
        var root = FindRepositoryRoot();
        var missing = _chatViews
            .Where(view => !File.ReadAllText(Path.Combine(root, view)).Contains("ChatImageContentSecurityPolicy", StringComparison.Ordinal))
            .ToArray();

        Assert.True(missing.Length == 0, "These views render chat without the image policy: " + string.Join(", ", missing));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root was not found.");
    }
}
