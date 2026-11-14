using CrestApps.OrchardCore.AI.Chat.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Tells the admin chat widget which content item the user is editing or viewing, or which content type they are
/// creating, on the Orchard Core content admin pages.
/// </summary>
/// <remarks>
/// The edit page has already loaded the item in the request's session, so reading it again costs no query.
/// </remarks>
public sealed class ContentItemPageContextProvider : IAIChatPageContextProvider
{
    private const string ContentsArea = "OrchardCore.Contents";

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemPageContextProvider"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentItemPageContextProvider(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<ContentItemPageContextProvider> stringLocalizer)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task PopulateAsync(AIChatPageContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(context.GetRouteValue("area"), ContentsArea, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(context.GetRouteValue("controller"), "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var action = context.GetRouteValue("action");

        if (string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase))
        {
            var typeName = context.GetRouteValue("id");

            if (string.IsNullOrEmpty(typeName))
            {
                return;
            }

            var createdType = await _contentDefinitionManager.GetTypeDefinitionAsync(typeName);

            context.Add(S["Content item being created"].Value, S["A new {0} (content type {1})", createdType?.DisplayName ?? typeName, typeName].Value);

            return;
        }

        if (!string.Equals(action, "Edit", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(action, "Display", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var contentItemId = context.GetRouteValue("contentItemId");

        if (string.IsNullOrEmpty(contentItemId))
        {
            return;
        }

        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return;
        }

        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
        var typeDisplayName = typeDefinition?.DisplayName ?? contentItem.ContentType;
        var status = contentItem.Published ? S["published"].Value : S["draft"].Value;
        var description = string.Equals(action, "Edit", StringComparison.OrdinalIgnoreCase)
            ? S["Content item being edited"].Value
            : S["Content item being viewed"].Value;
        var title = string.IsNullOrWhiteSpace(contentItem.DisplayText) ? S["(no title)"].Value : contentItem.DisplayText;

        context.Add(description, S["\"{0}\", a {1} (content type {2}, content item id {3}, {4})", title, typeDisplayName, contentItem.ContentType, contentItem.ContentItemId, status].Value);
    }
}
