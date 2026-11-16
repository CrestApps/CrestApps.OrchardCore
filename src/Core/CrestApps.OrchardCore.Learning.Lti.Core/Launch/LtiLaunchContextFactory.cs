using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Gathers a launch from Learning's records: the course and the External tool item (published, in the course, launching
/// the hint's tool), the person's names and e-mail address within the tool's privacy level, and the line item of a
/// gradable item, made the first time a tool that may post grades launches it.
/// </summary>
public sealed class LtiLaunchContextFactory : ILtiLaunchContextFactory
{
    private readonly UserManager<IUser> _userManager;
    private readonly IEnumerable<IDisplayNameProvider> _displayNameProviders;
    private readonly ICourseStructureService _courseStructure;
    private readonly IContentManager _contentManager;
    private readonly LtiLineItemStore _lineItems;
    private readonly ILtiPlatform _platform;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiLaunchContextFactory"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="displayNameProviders">The display name providers.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="lineItems">The line item store.</param>
    /// <param name="platform">The platform.</param>
    /// <param name="clock">The clock.</param>
    public LtiLaunchContextFactory(
        UserManager<IUser> userManager,
        IEnumerable<IDisplayNameProvider> displayNameProviders,
        ICourseStructureService courseStructure,
        IContentManager contentManager,
        LtiLineItemStore lineItems,
        ILtiPlatform platform,
        IClock clock)
    {
        _userManager = userManager;
        _displayNameProviders = displayNameProviders;
        _courseStructure = courseStructure;
        _contentManager = contentManager;
        _lineItems = lineItems;
        _platform = platform;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<LtiLaunchContext> CreateAsync(LtiMessageHint hint, ExternalTool tool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hint);
        ArgumentNullException.ThrowIfNull(tool);

        if (await _userManager.FindByIdAsync(hint.UserId) is not { } user)
        {
            return null;
        }

        var course = await _courseStructure.GetCourseAsync(hint.CourseId);

        if (course is null)
        {
            return null;
        }

        var context = new LtiLaunchContext
        {
            MessageType = hint.MessageType,
            UserId = hint.UserId,
            Subject = LtiSubjects.For(tool, hint.UserId),
            Roles = hint.Roles ?? [],
            CourseId = course.ContentItemId,
            CourseTitle = course.DisplayText,
            ReturnUrl = hint.ReturnUrl,
            Locale = CultureInfo.CurrentUICulture.Name is { Length: > 0 } culture ? culture : "en-US",
            TargetLinkUri = tool.TargetLinkUri,
        };

        if (tool.PrivacyLevel >= LtiPrivacyLevel.Name)
        {
            await FillNamesAsync(context, user, cancellationToken);
        }

        if (tool.PrivacyLevel >= LtiPrivacyLevel.NameAndEmail && user is User { Email: { Length: > 0 } email })
        {
            context.Email = email;
        }

        if (hint.MessageType == LtiConstants.MessageTypes.DeepLinkingRequest)
        {
            context.TargetLinkUri = string.IsNullOrWhiteSpace(tool.DeepLinkingUrl) ? tool.TargetLinkUri : tool.DeepLinkingUrl;

            return context;
        }

        var item = await _contentManager.GetAsync(hint.ItemId, VersionOptions.Published);
        var part = item?.Get<ExternalToolItemPart>(nameof(ExternalToolItemPart));

        if (part is null ||
            !string.Equals(part.ToolId, tool.ItemId, StringComparison.Ordinal) ||
            !string.Equals(_courseStructure.GetCourseId(item), course.ContentItemId, StringComparison.Ordinal))
        {
            return null;
        }

        context.ItemId = item.ContentItemId;
        context.ItemTitle = item.DisplayText;
        context.LinkCustomParameters = part.CustomParameters;

        if (!string.IsNullOrWhiteSpace(part.TargetLinkUri))
        {
            context.TargetLinkUri = part.TargetLinkUri;
        }

        if (tool.AllowGrades && (part.ScoreMaximum is not null || item.Get<CourseItemPart>(nameof(CourseItemPart))?.Gradable == true))
        {
            var lineItem = await GetOrCreateLineItemAsync(tool, item, part, course.ContentItemId, cancellationToken);
            var platform = await _platform.GetAsync();

            context.LineItemUrl = platform.LineItemUrl(course.ContentItemId, lineItem.ItemId);
        }

        return context;
    }

    private async Task FillNamesAsync(LtiLaunchContext context, IUser user, CancellationToken cancellationToken)
    {
        string name = null;

        foreach (var provider in _displayNameProviders)
        {
            name = await provider.GetAsync(user, cancellationToken);

            if (!string.IsNullOrWhiteSpace(name))
            {
                break;
            }
        }

        context.Name = string.IsNullOrWhiteSpace(name) ? user.UserName : name;

        if (user is User { Properties: { } properties } && properties["UserFullNamePart"] is JsonObject fullName)
        {
            context.GivenName = fullName["FirstName"]?.GetValue<string>();
            context.FamilyName = fullName["LastName"]?.GetValue<string>();
        }
    }

    private async Task<LtiLineItem> GetOrCreateLineItemAsync(ExternalTool tool, ContentItem item, ExternalToolItemPart part, string courseId, CancellationToken cancellationToken)
    {
        var lineItem = await _lineItems.FindByResourceLinkAsync(item.ContentItemId, tool.ItemId, cancellationToken);

        if (lineItem is not null)
        {
            return lineItem;
        }

        lineItem = new LtiLineItem
        {
            ItemId = IdGenerator.GenerateId(),
            ToolId = tool.ItemId,
            CourseId = courseId,
            ResourceLinkId = item.ContentItemId,
            ResourceId = part.LineItemResourceId,
            Tag = part.LineItemTag,
            Label = string.IsNullOrWhiteSpace(part.LineItemLabel) ? item.DisplayText : part.LineItemLabel,
            ScoreMaximum = part.ScoreMaximum is > 0 ? part.ScoreMaximum.Value : 100,
            CreatedUtc = _clock.UtcNow,
        };

        await _lineItems.CreateAsync(lineItem, cancellationToken);

        return lineItem;
    }
}
