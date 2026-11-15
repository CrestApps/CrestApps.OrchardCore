using System.Text.Json;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Learning.Handlers;

/// <summary>
/// Keeps the staff and the enrollment mode of a course as they are stored when a signed-in person who may not change
/// them saves the course, whatever saves it: the editor, the content API, an import or any other code. The values
/// decide who gets in and who reads learner records, so <see cref="CourseSettingsPolicy"/> applies on every save, not
/// only in the editor. A save with no signed-in person, such as a recipe at setup or a background task, is not limited.
/// </summary>
internal sealed class CourseSettingsGuardHandler : ContentPartHandler<CoursePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CourseSettingsPolicy _policy;

    // The values as read from the store, by content item, captured the first time the course is loaded in this scope.
    private readonly Dictionary<string, StoredSettings> _stored = new(StringComparer.Ordinal);

    public CourseSettingsGuardHandler(IHttpContextAccessor httpContextAccessor, CourseSettingsPolicy policy)
    {
        _httpContextAccessor = httpContextAccessor;
        _policy = policy;
    }

    /// <inheritdoc />
    public override Task LoadedAsync(LoadContentContext context, CoursePart part)
    {
        _stored.TryAdd(context.ContentItem.ContentItemId, StoredSettings.From(part));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreateContentContext context, CoursePart part)
        => GuardAsync(context.ContentItem, part, original: null);

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdateContentContext context, CoursePart part)
        => GuardAsync(context.ContentItem, part, original: null);

    /// <inheritdoc />
    public override Task ImportingAsync(ImportContentContext context, CoursePart part)
        => GuardAsync(context.ContentItem, part, context.OriginalContentItem?.Get<CoursePart>(nameof(CoursePart)));

    private async Task GuardAsync(ContentItem contentItem, CoursePart part, CoursePart original)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true || await _policy.CanChangeEnrollmentSettingsAsync(user, contentItem.ContentItemId))
        {
            return;
        }

        // A course that was never stored keeps no staff and the default mode until someone who may sets them.
        var stored = _stored.TryGetValue(contentItem.ContentItemId, out var loaded)
            ? loaded
            : original is not null ? StoredSettings.From(original) : StoredSettings.Empty;

        part.Staff = stored.CopyStaff();
        part.EnrollmentMode = stored.EnrollmentMode;
        part.Apply();
    }

    private sealed record StoredSettings(string StaffJson, EnrollmentMode EnrollmentMode)
    {
        public static readonly StoredSettings Empty = new("[]", default);

        public static StoredSettings From(CoursePart part)
            => new(JsonSerializer.Serialize(part.Staff ?? []), part.EnrollmentMode);

        public IList<CourseStaffMember> CopyStaff()
            => JsonSerializer.Deserialize<List<CourseStaffMember>>(StaffJson) ?? [];
    }
}
