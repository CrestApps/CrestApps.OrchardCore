using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.Services;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// Replaces the template's documents on a profile built from a template with copies the profile owns.
/// </summary>
/// <remarks>
/// Applying a template copies its documents metadata as it is, so the new profile starts out pointing at the
/// template's documents. The template's retrieval settings are kept; only the document list is replaced.
/// </remarks>
internal sealed class ProfileDocumentsTemplateApplicationHandler : IAIProfileTemplateApplicationHandler
{
    private readonly ProfileTemplateDocumentCloner _documentCloner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDocumentsTemplateApplicationHandler"/> class.
    /// </summary>
    /// <param name="documentCloner">The service that copies a template's documents to a profile.</param>
    public ProfileDocumentsTemplateApplicationHandler(ProfileTemplateDocumentCloner documentCloner)
    {
        _documentCloner = documentCloner;
    }

    /// <inheritdoc />
    public async Task AppliedAsync(AIProfileTemplateAppliedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var profile = context.Profile;

        if (!profile.TryGet<DocumentsMetadata>(out var appliedMetadata))
        {
            return;
        }

        // Work on a copy. Reading the metadata back can hand out the very instance the template holds, and
        // clearing its document list in place would strip the template of its own documents.
        var documentsMetadata = JsonSerializer.SerializeToNode(appliedMetadata).Deserialize<DocumentsMetadata>() ?? new DocumentsMetadata();
        documentsMetadata.Documents = [];

        await _documentCloner.CloneAsync(profile, context.Template, documentsMetadata);

        profile.Put(documentsMetadata);

        // Applying the template mirrors every property into the profile's settings as well. Keep that mirror
        // in step so it does not go on naming the template's documents.
        if (profile.Settings.ContainsKey(nameof(DocumentsMetadata)))
        {
            profile.Settings[nameof(DocumentsMetadata)] = JsonSerializer.SerializeToNode(documentsMetadata);
        }
    }
}
