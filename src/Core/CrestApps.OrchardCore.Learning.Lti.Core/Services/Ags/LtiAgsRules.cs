using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services.Ags;

/// <summary>
/// Reads and checks what tools send to Assignment and Grade Services 2.0: scores (section 3.4: <c>userId</c>,
/// <c>activityProgress</c>, <c>gradingProgress</c> and an ISO 8601 <c>timestamp</c> are required, <c>scoreMaximum</c> is
/// required with <c>scoreGiven</c>, neither may be negative and the maximum must be more than 0) and line items (section
/// 3.2: <c>scoreMaximum</c> more than 0 and <c>label</c> are required).
/// </summary>
public static class LtiAgsRules
{
    /// <summary>
    /// The grading progress of a final score, the only one that reaches the learner's progress.
    /// </summary>
    public const string FullyGraded = "FullyGraded";

    private const int MaxText = 4000;

    /// <summary>
    /// The activity progress values.
    /// </summary>
    public static IReadOnlyList<string> ActivityProgressValues { get; } = ["Initialized", "Started", "InProgress", "Submitted", "Completed"];

    /// <summary>
    /// The grading progress values.
    /// </summary>
    public static IReadOnlyList<string> GradingProgressValues { get; } = [FullyGraded, "Pending", "PendingManual", "Failed", "NotReady"];

    /// <summary>
    /// Reads a score.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="score">The score, when valid.</param>
    /// <param name="error">Why it is not valid.</param>
    /// <returns><see langword="true"/> when the score is valid.</returns>
    public static bool TryReadScore(JsonNode body, out LtiScoreInput score, out string error)
    {
        score = null;

        if (body is not JsonObject json)
        {
            error = "The score must be a JSON object.";

            return false;
        }

        var userId = Text(json, "userId");
        var activity = Text(json, "activityProgress");
        var grading = Text(json, "gradingProgress");

        if (string.IsNullOrEmpty(userId))
        {
            error = "userId is required.";

            return false;
        }

        if (activity is null || !ActivityProgressValues.Contains(activity, StringComparer.Ordinal))
        {
            error = "activityProgress must be one of " + string.Join(", ", ActivityProgressValues) + ".";

            return false;
        }

        if (grading is null || !GradingProgressValues.Contains(grading, StringComparer.Ordinal))
        {
            error = "gradingProgress must be one of " + string.Join(", ", GradingProgressValues) + ".";

            return false;
        }

        if (!TryReadTimestamp(Text(json, "timestamp"), out var timestamp))
        {
            error = "timestamp is required, in ISO 8601 with a time zone.";

            return false;
        }

        if (!TryReadNumber(json, "scoreGiven", out var given) || !TryReadNumber(json, "scoreMaximum", out var maximum))
        {
            error = "scoreGiven and scoreMaximum must be numbers.";

            return false;
        }

        if (given is not null && maximum is null)
        {
            error = "scoreMaximum is required with scoreGiven.";

            return false;
        }

        if (given < 0 || maximum <= 0)
        {
            error = "scoreGiven may not be negative and scoreMaximum must be more than 0.";

            return false;
        }

        var comment = Text(json, "comment");

        score = new LtiScoreInput
        {
            UserId = userId,
            ScoreGiven = given,
            ScoreMaximum = maximum,
            Comment = comment is { Length: > MaxText } ? comment.Substring(0, MaxText) : comment,
            ActivityProgress = activity,
            GradingProgress = grading,
            TimestampUtc = timestamp,
        };

        error = null;

        return true;
    }

    /// <summary>
    /// Reads a line item.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="lineItem">The line item, when valid.</param>
    /// <param name="error">Why it is not valid.</param>
    /// <returns><see langword="true"/> when the line item is valid.</returns>
    public static bool TryReadLineItem(JsonNode body, out LtiLineItemInput lineItem, out string error)
    {
        lineItem = null;

        if (body is not JsonObject json)
        {
            error = "The line item must be a JSON object.";

            return false;
        }

        if (!TryReadNumber(json, "scoreMaximum", out var maximum) || maximum is not > 0)
        {
            error = "scoreMaximum is required and must be more than 0.";

            return false;
        }

        var label = Text(json, "label");

        if (string.IsNullOrWhiteSpace(label))
        {
            error = "label is required.";

            return false;
        }

        DateTime? start = null;
        DateTime? end = null;

        if (Text(json, "startDateTime") is { } startText)
        {
            if (!TryReadTimestamp(startText, out var value))
            {
                error = "startDateTime must be ISO 8601 with a time zone.";

                return false;
            }

            start = value;
        }

        if (Text(json, "endDateTime") is { } endText)
        {
            if (!TryReadTimestamp(endText, out var value))
            {
                error = "endDateTime must be ISO 8601 with a time zone.";

                return false;
            }

            end = value;
        }

        var resourceId = Text(json, "resourceId");
        var tag = Text(json, "tag");

        if (label.Length > 255 || resourceId?.Length > 255 || tag?.Length > 255)
        {
            error = "label, resourceId and tag may have at most 255 characters.";

            return false;
        }

        lineItem = new LtiLineItemInput
        {
            ScoreMaximum = maximum.Value,
            Label = label.Trim(),
            ResourceId = resourceId,
            Tag = tag,
            ResourceLinkId = Text(json, "resourceLinkId"),
            StartDateTimeUtc = start,
            EndDateTimeUtc = end,
        };

        error = null;

        return true;
    }

    private static bool TryReadTimestamp(string value, out DateTime utc)
    {
        utc = default;

        // A time zone is required: a time without one cannot be compared with the stored one.
        if (string.IsNullOrEmpty(value) || !value.Contains('T') ||
            !(value.EndsWith('Z') || value.EndsWith('z') || value.LastIndexOfAny(['+', '-']) > value.IndexOf('T')))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return false;
        }

        utc = parsed.UtcDateTime;

        return true;
    }

    private static bool TryReadNumber(JsonObject json, string name, out decimal? value)
    {
        value = null;

        if (json[name] is null)
        {
            return true;
        }

        if (json[name] is JsonValue number && number.GetValueKind() == JsonValueKind.Number && number.TryGetValue<decimal>(out var result))
        {
            value = result;

            return true;
        }

        return false;
    }

    private static string Text(JsonObject json, string name)
        => json[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
