using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Opens an item only to named people or to the members of a user audience (Moodle's restrictions to people and to
/// profile fields, where an audience of the Users module holds the rule). The audiences are read only when the User
/// Audiences feature is on; without it an audience opens the item to nobody.
/// </summary>
public sealed class PeopleReleaseRule : ICourseItemReleaseRule
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeopleReleaseRule"/> class.
    /// </summary>
    /// <param name="services">The services, to find the audiences when the feature is on.</param>
    public PeopleReleaseRule(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public string Name => LearningRuleNames.People;

    /// <inheritdoc />
    public async Task<ReleaseDecision> EvaluateAsync(ReleaseRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userId = context.Enrollment?.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return ReleaseDecision.Locked(LearningRefusals.Restricted);
        }

        if (UserIds(context.Settings).Contains(userId, StringComparer.Ordinal))
        {
            return ReleaseDecision.Released;
        }

        var audience = context.Settings?.Get("Audience")?.Trim();

        if (!string.IsNullOrEmpty(audience) &&
            _services.GetService<IUserAudienceService>() is { } audiences &&
            await audiences.IsMemberAsync(audience, userId))
        {
            return ReleaseDecision.Released;
        }

        return ReleaseDecision.Locked(LearningRefusals.Restricted);
    }

    /// <summary>
    /// Reads the people a rule names.
    /// </summary>
    /// <param name="settings">The rule.</param>
    /// <returns>Their user ids.</returns>
    public static IReadOnlyList<string> UserIds(LearningRuleSettings settings)
        => (settings?.Get("UserIds") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
