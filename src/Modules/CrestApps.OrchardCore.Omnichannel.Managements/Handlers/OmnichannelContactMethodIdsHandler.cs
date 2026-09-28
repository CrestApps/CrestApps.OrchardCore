using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Gives every item in a contact's <c>ContactMethods</c> bag an identifier of its own.
/// </summary>
/// <remarks>
/// The bag editor matches each posted phone number or email to the stored item by its identifier. An item stored
/// without one, or sharing one with another item, cannot be matched, so the save drops the edit and the old value
/// comes back with no error shown. Bulk-imported contacts were written that way. The identifiers are assigned when
/// a contact is loaded, so a record stored that way edits correctly at once and is repaired by its next save, and
/// again when one is created or updated, so no path can store such an item.
/// <para>
/// An assigned identifier is derived from the contact and the item's position rather than drawn at random. The
/// editor renders the item under the identifier it was given when the page was loaded, and the save loads the
/// contact again; a random identifier would differ between the two, so the posted item would still match nothing.
/// </para>
/// </remarks>
internal sealed class OmnichannelContactMethodIdsHandler : ContentHandlerBase
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactMethodIdsHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactMethodIdsHandler(ILogger<OmnichannelContactMethodIdsHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task LoadedAsync(LoadContentContext context)
    {
        EnsureIds(context.ContentItem);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreateContentContext context)
    {
        EnsureIds(context.ContentItem);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdateContentContext context)
    {
        EnsureIds(context.ContentItem);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Assigns a new identifier to every contact method that has none or repeats one already seen.
    /// </summary>
    /// <param name="contentItem">The content item, which may or may not be a contact.</param>
    /// <returns>The number of contact methods that were given a new identifier.</returns>
    internal int EnsureIds(ContentItem contentItem)
    {
        if (contentItem is null ||
            !contentItem.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) ||
            bag.ContentItems is not { Count: > 0 })
        {
            return 0;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assigned = 0;

        for (var position = 0; position < bag.ContentItems.Count; position++)
        {
            var method = bag.ContentItems[position];

            if (method is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(method.ContentItemId) || !seen.Add(method.ContentItemId))
            {
                method.ContentItemId = DeriveId(contentItem.ContentItemId, position, "item");
                method.ContentItemVersionId = DeriveId(contentItem.ContentItemId, position, "version");
                seen.Add(method.ContentItemId);
                assigned++;
            }
        }

        if (assigned == 0)
        {
            return 0;
        }

        contentItem.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Gave {Count} contact method(s) of contact '{ContentItemId}' an identifier they were stored without, so their edits can be saved.",
                assigned,
                contentItem.ContentItemId);
        }

        return assigned;
    }

    // The same 26-character, lowercase base-32 shape as a generated content identifier, so nothing that reads the
    // identifier can tell the two apart.
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    internal static string DeriveId(string contactId, int position, string purpose)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{contactId}|contact-method|{position}|{purpose}"));
        var id = new char[26];

        for (var i = 0; i < id.Length; i++)
        {
            id[i] = Alphabet[hash[i] & 31];
        }

        return new string(id);
    }
}
