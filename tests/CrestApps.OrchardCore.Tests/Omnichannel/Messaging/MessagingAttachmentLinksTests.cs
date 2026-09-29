using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A picture reaches two audiences: the provider delivering it, which fetches it without signing in, and agents
/// reading the conversation. Neither link may be forged, and a view link must not open another conversation's picture.
/// </summary>
public sealed class MessagingAttachmentLinksTests
{
    private static readonly MessagingAttachment _attachment = new() { Id = "out-1", ContentType = "image/png", FileName = "form.png" };

    [Fact]
    public async Task GetPublicUrlAsync_BuildsALinkUnderTheSiteBaseUrl_ThatReadsBackToThePicture()
    {
        var links = CreateLinks("https://crm.example.test/tenant/");

        var url = await links.GetPublicUrlAsync(_attachment, TestContext.Current.CancellationToken);

        Assert.StartsWith("https://crm.example.test/tenant/messaging/attachments/", url);
        Assert.EndsWith("/image.png", url);

        var token = new Uri(url).Segments[^2].TrimEnd('/');

        Assert.True(links.TryReadPublicToken(token, out var attachmentId, out var contentType));
        Assert.Equal("out-1", attachmentId);
        Assert.Equal("image/png", contentType);
    }

    [Fact]
    public async Task GetPublicUrlAsync_WithoutASiteBaseUrlOrRequest_ReturnsNull()
    {
        var links = CreateLinks(baseUrl: null);

        Assert.Null(await links.GetPublicUrlAsync(_attachment, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TryReadPublicToken_RefusesATamperedToken()
    {
        Assert.False(CreateLinks("https://crm.example.test").TryReadPublicToken("not-a-token", out _, out _));
    }

    [Fact]
    public void TryReadViewToken_RefusesATokenIssuedForAnotherConversation()
    {
        var links = CreateLinks("https://crm.example.test");
        var token = links.CreateViewToken("conv-1", _attachment);

        Assert.True(links.TryReadViewToken("conv-1", token, out var attachmentId, out _, out var fileName));
        Assert.Equal("out-1", attachmentId);
        Assert.Equal("form.png", fileName);
        Assert.False(links.TryReadViewToken("conv-2", token, out _, out _, out _));
    }

    [Fact]
    public void AViewTokenIsNotAPublicToken()
    {
        // The anonymous endpoint must not accept the token agents see, which never expires.
        var links = CreateLinks("https://crm.example.test");

        Assert.False(links.TryReadPublicToken(links.CreateViewToken("conv-1", _attachment), out _, out _));
    }

    [Fact]
    public async Task TheStore_KeepsOnlyCiphertextOnDisk_AndReadsThePictureBack()
    {
        var folder = Path.Combine(Path.GetTempPath(), "messaging-attachments-" + Guid.NewGuid().ToString("N"));

        try
        {
            var store = new LocalEncryptedMessagingAttachmentStore(
                new FileSystemStore(folder, NullLogger<FileSystemStore>.Instance),
                new EphemeralDataProtectionProvider());

            await store.StoreAsync("in-1", TestImages.Png, TestContext.Current.CancellationToken);

            var onDisk = await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(folder)), TestContext.Current.CancellationToken);

            Assert.Null(MessagingFileFormats.Detect(onDisk, "in-1.png", null, MessagingFileFormats.All));
            Assert.Equal(TestImages.Png, await store.ReadAsync("in-1", TestContext.Current.CancellationToken));
            Assert.True(await store.DeleteAsync("in-1", TestContext.Current.CancellationToken));
            Assert.Null(await store.ReadAsync("in-1", TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static MessagingAttachmentLinks CreateLinks(string baseUrl)
    {
        var site = new Mock<ISite>();
        site.SetupGet(s => s.BaseUrl).Returns(baseUrl);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new MessagingAttachmentLinks(
            new EphemeralDataProtectionProvider(),
            siteService.Object,
            new HttpContextAccessor(),
            new OptionsWrapper<MessagingWorkspaceOptions>(new MessagingWorkspaceOptions()),
            NullLogger<MessagingAttachmentLinks>.Instance);
    }
}
