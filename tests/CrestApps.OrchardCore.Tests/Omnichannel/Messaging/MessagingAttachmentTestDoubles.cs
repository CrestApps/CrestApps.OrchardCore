using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Hands out a predictable public link for each picture, or none at all when the site is meant to have no public
/// address.
/// </summary>
internal sealed class FakeAttachmentUrlProvider : IMessagingAttachmentUrlProvider
{
    public bool HasPublicAddress { get; init; } = true;

    public Task<string> GetPublicUrlAsync(MessagingAttachment attachment, CancellationToken cancellationToken = default)
        => Task.FromResult(HasPublicAddress ? $"https://site.test/messaging/attachments/{attachment.Id}" : null);
}

/// <summary>
/// An ingestor that leaves every message as it arrived.
/// </summary>
internal sealed class FakeInboundMediaIngestor : IMessagingInboundMediaIngestor
{
    public int Calls { get; private set; }

    public Task<bool> IngestAsync(OmnichannelMessage message, CancellationToken cancellationToken = default)
    {
        Calls++;

        return Task.FromResult(false);
    }
}

/// <summary>
/// Keeps pictures in memory.
/// </summary>
internal sealed class InMemoryAttachmentStore : IMessagingAttachmentStore
{
    public Dictionary<string, byte[]> Items { get; } = new(StringComparer.Ordinal);

    public Task StoreAsync(string attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        Items[attachmentId] = content.ToArray();

        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(string attachmentId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.TryGetValue(attachmentId, out var bytes) ? bytes : null);

    public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        Items.Remove(attachmentId);

        return Task.FromResult(true);
    }
}

/// <summary>
/// The smallest valid headers of each supported picture format.
/// </summary>
internal static class TestImages
{
    public static byte[] Png { get; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public static byte[] Jpeg { get; } = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    public static byte[] Gif { get; } = "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray();

    public static byte[] WebP { get; } = "RIFF$\u0000\u0000\u0000WEBPVP8 "u8.ToArray();
}
