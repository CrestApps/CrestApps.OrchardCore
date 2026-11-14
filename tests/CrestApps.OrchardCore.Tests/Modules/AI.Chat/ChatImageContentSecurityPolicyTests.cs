using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// SEC-A16: chat output is sanitized with DOMPurify, which keeps images, audio and video, so model output could load a
/// URL that carries the conversation. Until the renderer itself restricts what it keeps (upstream, in CrestApps.Core),
/// a site can opt in to a policy on the pages that render chat. It is off by default: it covers the whole page, so it
/// also blocks map tiles, provider-hosted generated images and external avatars that the site doesn't list.
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
    public void Apply_ByDefault_SendsNothing()
    {
        var httpContext = new DefaultHttpContext();

        new ChatImageContentSecurityPolicy(Options.Create(new ChatImageContentSecurityPolicyOptions())).Apply(httpContext);

        Assert.False(httpContext.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public void Apply_WhenTurnedOn_LimitsImagesAndMedia()
    {
        var options = new ChatImageContentSecurityPolicyOptions { Enabled = true };
        options.ImageSources.Add("https://cdn.example.com");
        options.ImageSources.Add("https://evil.example; script-src *");
        var httpContext = new DefaultHttpContext();

        new ChatImageContentSecurityPolicy(Options.Create(options)).Apply(httpContext);

        Assert.Equal(
            "img-src 'self' data: blob: https://cdn.example.com; media-src 'self' data: blob: https://cdn.example.com",
            httpContext.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public void Apply_WhenTurnedOn_AllowsTheMediaCdn()
    {
        var options = new ChatImageContentSecurityPolicyOptions { Enabled = true, MediaCdnBaseUrl = "https://media.example.net/tenant-a/" };
        var httpContext = new DefaultHttpContext();

        new ChatImageContentSecurityPolicy(Options.Create(options)).Apply(httpContext);

        Assert.Equal(
            "img-src 'self' data: blob: https://media.example.net; media-src 'self' data: blob: https://media.example.net",
            httpContext.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public void Apply_KeepsThePolicyTheSiteAlreadySends_AndAddsItsOwnOnce()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self'";
        var policy = new ChatImageContentSecurityPolicy(Options.Create(new ChatImageContentSecurityPolicyOptions { Enabled = true }));

        policy.Apply(httpContext);
        policy.Apply(httpContext);

        Assert.Equal(["frame-ancestors 'self'", "img-src 'self' data: blob:; media-src 'self' data: blob:"], httpContext.Response.Headers.ContentSecurityPolicy.ToArray());
    }

    [Fact]
    public void EveryViewThatRendersChat_AppliesThePolicy()
    {
        var root = FindRepositoryRoot();
        var missing = _chatViews
            .Where(view => !File.ReadAllText(Path.Combine(root, view)).Contains("ChatImageContentSecurityPolicy.Apply(Context);", StringComparison.Ordinal))
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
