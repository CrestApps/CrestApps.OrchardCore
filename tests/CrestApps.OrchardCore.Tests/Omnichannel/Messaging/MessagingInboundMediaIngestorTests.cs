using System.Net;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A customer's picture is hosted by the provider only for a while, so it is copied into the workspace's own store
/// when the message arrives. A picture that cannot be copied must never cost the customer the words they sent.
/// </summary>
public sealed class MessagingInboundMediaIngestorTests
{
    [Fact]
    public async Task IngestAsync_StoresEachPicture_AndRecordsItOnTheMessage()
    {
        // Arrange
        var store = new InMemoryAttachmentStore();
        var ingestor = CreateIngestor(_ => Picture(TestImages.Png), store);
        var message = Inbound("https://media.provider.test/a.png");

        // Act
        var changed = await ingestor.IngestAsync(message, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(changed);

        var attachment = Assert.Single(message.GetAttachments());

        Assert.Equal(MessagingImageFormat.Png, attachment.ContentType);
        Assert.Equal(TestImages.Png, store.Items[attachment.Id]);
        Assert.Equal(0, message.GetSkippedAttachmentCount());
    }

    [Fact]
    public async Task IngestAsync_WhenTheSameMessageArrivesTwice_KeepsOneCopyOfEachPicture()
    {
        // A provider redelivers a message it is unsure we received. The picture keys come from the provider's message
        // id, so the second copy overwrites the first rather than piling up beside it.
        var store = new InMemoryAttachmentStore();
        var ingestor = CreateIngestor(_ => Picture(TestImages.Jpeg), store);

        var first = Inbound("https://media.provider.test/a.jpg");
        var second = Inbound("https://media.provider.test/a.jpg");

        await ingestor.IngestAsync(first, TestContext.Current.CancellationToken);
        await ingestor.IngestAsync(second, TestContext.Current.CancellationToken);

        Assert.Single(store.Items);
        Assert.Equal(first.GetAttachments()[0].Id, second.GetAttachments()[0].Id);
    }

    [Fact]
    public async Task IngestAsync_RefusesAFileThatIsNotAPicture_WhateverTheProviderCalledIt()
    {
        var store = new InMemoryAttachmentStore();
        var ingestor = CreateIngestor(
            _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"),
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

                return response;
            },
            store);

        var message = Inbound("https://media.provider.test/a.png");

        await ingestor.IngestAsync(message, TestContext.Current.CancellationToken);

        Assert.Empty(message.GetAttachments());
        Assert.Equal(1, message.GetSkippedAttachmentCount());
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task IngestAsync_WhenTheProviderFails_CountsThePictureAsSkipped_AndDoesNotThrow()
    {
        var ingestor = CreateIngestor(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), new InMemoryAttachmentStore());
        var message = Inbound("https://media.provider.test/a.png");

        var changed = await ingestor.IngestAsync(message, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Empty(message.GetAttachments());
        Assert.Equal(1, message.GetSkippedAttachmentCount());
    }

    [Fact]
    public async Task IngestAsync_RefusesAPictureOverTheSizeLimit()
    {
        var ingestor = CreateIngestor(
            _ => Picture([.. TestImages.Png, .. new byte[64]]),
            new InMemoryAttachmentStore(),
            new MessagingWorkspaceOptions { MaxInboundAttachmentBytes = 32 });

        var message = Inbound("https://media.provider.test/a.png");

        await ingestor.IngestAsync(message, TestContext.Current.CancellationToken);

        Assert.Empty(message.GetAttachments());
        Assert.Equal(1, message.GetSkippedAttachmentCount());
    }

    [Fact]
    public async Task IngestAsync_NeverFetchesAPlainHttpAddress()
    {
        var handler = new StubHttpMessageHandler(_ => Picture(TestImages.Png));
        var ingestor = new MessagingInboundMediaIngestor(
            new StubHttpClientFactory(handler),
            new InMemoryAttachmentStore(),
            [],
            new OptionsWrapper<MessagingWorkspaceOptions>(new MessagingWorkspaceOptions()),
            NullLogger<MessagingInboundMediaIngestor>.Instance);

        var message = Inbound("http://169.254.169.254/latest/meta-data");

        await ingestor.IngestAsync(message, TestContext.Current.CancellationToken);

        Assert.Empty(handler.Requests);
        Assert.Equal(1, message.GetSkippedAttachmentCount());
    }

    [Fact]
    public async Task IngestAsync_LeavesAMessageWithoutMediaUntouched()
    {
        var ingestor = CreateIngestor(_ => Picture(TestImages.Png), new InMemoryAttachmentStore());
        var message = Inbound();

        Assert.False(await ingestor.IngestAsync(message, TestContext.Current.CancellationToken));
    }

    private static OmnichannelMessage Inbound(params string[] mediaUrls)
        => new()
        {
            Channel = "SMS",
            IsInbound = true,
            Content = "look at this",
            ProviderMessageId = "provider-message-1",
            MediaReferences = mediaUrls.ToList(),
        };

    private static HttpResponseMessage Picture(byte[] bytes)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private static MessagingInboundMediaIngestor CreateIngestor(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        IMessagingAttachmentStore store,
        MessagingWorkspaceOptions options = null)
        => new(
            new StubHttpClientFactory(new StubHttpMessageHandler(respond)),
            store,
            [],
            new OptionsWrapper<MessagingWorkspaceOptions>(options ?? new MessagingWorkspaceOptions()),
            NullLogger<MessagingInboundMediaIngestor>.Instance);
}
