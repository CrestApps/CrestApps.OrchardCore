using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Writes the user messages the coach and the replay judge are given. Kept apart from the services that send them
/// so what the model sees can be tested without a model.
/// </summary>
internal static class ProfileBuilderCoachPrompt
{
    /// <summary>
    /// The longest a single message or reply is quoted, in characters.
    /// </summary>
    internal const int MaxQuotedLength = 4000;

    /// <summary>
    /// How many of the latest conversation turns are shown to the coach.
    /// </summary>
    internal const int MaxTranscriptTurns = 40;

    /// <summary>
    /// Writes the request that asks the coach to apply <paramref name="newFeedback"/>.
    /// </summary>
    /// <param name="current">The current settings.</param>
    /// <param name="transcript">The conversation as it stands now.</param>
    /// <param name="history">Every earlier piece of feedback, oldest first.</param>
    /// <param name="revisions">Every revision so far, oldest first.</param>
    /// <param name="newFeedback">The feedback to apply now.</param>
    public static string BuildCoachRequest(
        ProfileBuilderSettings current,
        IReadOnlyList<ProfileBuilderTurn> transcript,
        IReadOnlyList<ProfileBuilderFeedback> history,
        IReadOnlyList<ProfileBuilderRevision> revisions,
        ProfileBuilderFeedback newFeedback)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(newFeedback);

        var builder = new StringBuilder();

        AppendSettings(builder, current);

        builder.AppendLine("# Conversation so far");
        builder.AppendLine("The author role-plays the customer. This is the conversation as it stands now; replies that were regenerated are no longer in it but are listed under the feedback history.");
        builder.AppendLine();
        AppendTranscript(builder, transcript);

        builder.AppendLine("# Feedback history (oldest first)");
        builder.AppendLine("Every rejected reply below must stay fixed, and every approved reply must stay possible, after your rewrite.");
        builder.AppendLine();

        var earlier = history?.Where(entry => entry.Id != newFeedback.Id).ToArray() ?? [];

        if (earlier.Length == 0)
        {
            builder.AppendLine("(none)");
            builder.AppendLine();
        }
        else
        {
            for (var index = 0; index < earlier.Length; index++)
            {
                AppendFeedback(builder, earlier[index], $"Feedback {index + 1}");
            }
        }

        builder.AppendLine("# Revision history");

        if (revisions is null || revisions.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var revision in revisions)
            {
                builder.Append("- Revision ")
                    .Append(revision.Number.ToString(CultureInfo.InvariantCulture))
                    .Append(" (")
                    .Append(revision.Source)
                    .Append("): ")
                    .AppendLine(string.IsNullOrWhiteSpace(revision.Summary) ? "(no summary)" : OneLine(revision.Summary));
            }
        }

        builder.AppendLine();
        builder.AppendLine("# New feedback to apply now");
        AppendFeedback(builder, newFeedback, "New feedback");

        return builder.ToString();
    }

    /// <summary>
    /// Writes the request that asks the judge whether replayed replies honour the feedback they were replayed for.
    /// </summary>
    /// <param name="current">The settings the replies were produced with.</param>
    /// <param name="cases">The replayed cases, each with the feedback given on that situation.</param>
    public static string BuildJudgeRequest(
        ProfileBuilderSettings current,
        IReadOnlyList<(string CaseId, string CustomerMessage, string Reply, IReadOnlyList<ProfileBuilderFeedback> Feedback)> cases)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(cases);

        var builder = new StringBuilder();

        builder.AppendLine("# Instructions the replies were produced with");
        AppendQuoted(builder, current.SystemMessage);
        builder.AppendLine();

        builder.AppendLine("# Cases");
        builder.AppendLine();

        foreach (var (caseId, customerMessage, reply, feedback) in cases)
        {
            builder.Append("## Case ").AppendLine(caseId);
            builder.AppendLine("Customer said:");
            AppendQuoted(builder, customerMessage);
            builder.AppendLine("New reply:");
            AppendQuoted(builder, reply);
            builder.AppendLine("Feedback the author gave on earlier replies to this message:");

            foreach (var entry in feedback)
            {
                builder.Append("- ").Append(DescribeKind(entry.Kind)).Append(": ");
                builder.AppendLine(string.IsNullOrWhiteSpace(entry.Comment) ? "(no comment)" : OneLine(entry.Comment));
                builder.AppendLine("  Reply it was about:");
                builder.Append("  ").AppendLine(OneLine(Truncate(entry.AssistantReply, 1000)));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendSettings(StringBuilder builder, ProfileBuilderSettings settings)
    {
        builder.AppendLine("# Current settings");
        builder.AppendLine("System message:");
        AppendQuoted(builder, settings.SystemMessage);
        builder.AppendLine("Opening message (initial prompt):");
        AppendQuoted(builder, settings.InitialPrompt);
        AppendParameter(builder, "Temperature", settings.Temperature);
        AppendParameter(builder, "TopP", settings.TopP);
        AppendParameter(builder, "FrequencyPenalty", settings.FrequencyPenalty);
        AppendParameter(builder, "PresencePenalty", settings.PresencePenalty);
        AppendParameter(builder, "MaxTokens", settings.MaxTokens);
        AppendParameter(builder, "PastMessagesCount", settings.PastMessagesCount);
        builder.AppendLine();
    }

    private static void AppendParameter(StringBuilder builder, string name, float? value)
        => builder.Append(name).Append(": ").AppendLine(value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "model default");

    private static void AppendParameter(StringBuilder builder, string name, int? value)
        => builder.Append(name).Append(": ").AppendLine(value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "model default");

    private static void AppendTranscript(StringBuilder builder, IReadOnlyList<ProfileBuilderTurn> transcript)
    {
        if (transcript is null || transcript.Count == 0)
        {
            builder.AppendLine("(empty)");
            builder.AppendLine();

            return;
        }

        var skipped = Math.Max(0, transcript.Count - MaxTranscriptTurns);

        if (skipped > 0)
        {
            builder.Append('(').Append(skipped.ToString(CultureInfo.InvariantCulture)).AppendLine(" earlier turns omitted)");
        }

        foreach (var turn in transcript.Skip(skipped))
        {
            builder.Append(IsAssistant(turn.Role) ? "Assistant: " : "Customer: ");
            builder.AppendLine(Truncate(turn.Text, MaxQuotedLength));
        }

        builder.AppendLine();
    }

    private static void AppendFeedback(StringBuilder builder, ProfileBuilderFeedback feedback, string heading)
    {
        builder.Append("## ").Append(heading).Append(" - ").AppendLine(DescribeKind(feedback.Kind));

        if (feedback.RevisionNumber.HasValue)
        {
            builder.Append("Led to revision ").AppendLine(feedback.RevisionNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine("Customer said:");
        AppendQuoted(builder, feedback.CustomerMessage);

        builder.AppendLine(feedback.Kind == ProfileBuilderFeedbackKind.Approved
            ? "Assistant replied (APPROVED, keep this behavior):"
            : feedback.WasRegenerated
                ? "Assistant replied (REJECTED and removed from the conversation):"
                : "Assistant replied (the author was not satisfied):");
        AppendQuoted(builder, feedback.AssistantReply);

        builder.AppendLine("Author's comment:");
        AppendQuoted(builder, feedback.Comment);
        builder.AppendLine();
    }

    private static string DescribeKind(ProfileBuilderFeedbackKind kind)
        => kind switch
        {
            ProfileBuilderFeedbackKind.Approved => "approved reply",
            ProfileBuilderFeedbackKind.FeedbackAndRegenerate => "feedback, reply regenerated",
            _ => "feedback",
        };

    private static void AppendQuoted(StringBuilder builder, string text)
    {
        builder.AppendLine("<<<");
        builder.AppendLine(string.IsNullOrWhiteSpace(text) ? "(empty)" : Truncate(text.Trim(), MaxQuotedLength));
        builder.AppendLine(">>>");
    }

    private static bool IsAssistant(string role)
        => string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    internal static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return string.Concat(text.AsSpan(0, maxLength), " [...]");
    }

    private static string OneLine(string text)
        => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
}
