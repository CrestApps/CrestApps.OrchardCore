using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Writes the JSON of the services: a line item (Assignment and Grade Services 2.0, 3.2), a result (3.3) and a member of
/// a context (Names and Role Provisioning Services 2.0, 2.3).
/// </summary>
public static class LtiServiceJson
{
    /// <summary>
    /// Writes a line item.
    /// </summary>
    /// <param name="platform">The platform.</param>
    /// <param name="lineItem">The line item.</param>
    /// <returns>The line item JSON.</returns>
    public static JsonObject LineItem(LtiPlatformInfo platform, LtiLineItem lineItem)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(lineItem);

        var json = new JsonObject
        {
            ["id"] = platform.LineItemUrl(lineItem.CourseId, lineItem.ItemId),
            ["scoreMaximum"] = lineItem.ScoreMaximum,
            ["label"] = lineItem.Label,
        };

        AddIfPresent(json, "resourceId", lineItem.ResourceId);
        AddIfPresent(json, "tag", lineItem.Tag);
        AddIfPresent(json, "resourceLinkId", lineItem.ResourceLinkId);
        AddIfPresent(json, "startDateTime", Iso(lineItem.StartDateTimeUtc));
        AddIfPresent(json, "endDateTime", Iso(lineItem.EndDateTimeUtc));

        return json;
    }

    /// <summary>
    /// Writes the result of a learner's latest score on a line item: the score given scaled to the line item's maximum.
    /// </summary>
    /// <param name="platform">The platform.</param>
    /// <param name="lineItem">The line item.</param>
    /// <param name="score">The score.</param>
    /// <returns>The result JSON.</returns>
    public static JsonObject Result(LtiPlatformInfo platform, LtiLineItem lineItem, LtiScore score)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(lineItem);
        ArgumentNullException.ThrowIfNull(score);

        var lineItemUrl = platform.LineItemUrl(lineItem.CourseId, lineItem.ItemId);
        var json = new JsonObject
        {
            ["id"] = lineItemUrl + "/results/" + Uri.EscapeDataString(score.ItemId),
            ["scoreOf"] = lineItemUrl,
            ["userId"] = score.Subject,
        };

        if (score.ScoreGiven is { } given && score.ScoreMaximum is > 0)
        {
            json["resultScore"] = Math.Round(given / score.ScoreMaximum.Value * lineItem.ScoreMaximum, 4);
            json["resultMaximum"] = lineItem.ScoreMaximum;
        }

        AddIfPresent(json, "comment", score.Comment);

        return json;
    }

    /// <summary>
    /// Writes a member of a context.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="tool">The tool, whose privacy level decides the names and the e-mail address.</param>
    /// <returns>The member JSON.</returns>
    public static JsonObject Member(LtiCourseMember member, ExternalTool tool)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(tool);

        var json = new JsonObject
        {
            ["status"] = member.Active ? "Active" : "Inactive",
            ["user_id"] = LtiSubjects.For(tool, member.UserId),
            ["roles"] = new JsonArray(member.Roles.Select(role => (JsonNode)role).ToArray()),
        };

        if (tool.PrivacyLevel >= LtiPrivacyLevel.Name)
        {
            AddIfPresent(json, "name", member.Name);
            AddIfPresent(json, "given_name", member.GivenName);
            AddIfPresent(json, "family_name", member.FamilyName);
        }

        if (tool.PrivacyLevel >= LtiPrivacyLevel.NameAndEmail)
        {
            AddIfPresent(json, "email", member.Email);
        }

        return json;
    }

    private static string Iso(DateTime? utc)
        => utc is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) : null;

    private static void AddIfPresent(JsonObject json, string name, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            json[name] = value;
        }
    }
}
