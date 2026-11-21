using System.Globalization;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Lti.Services;

/// <summary>
/// Exports a person's scores from external tools and the id each registered tool knows them by, and erases the scores.
/// The ids are not stored (each is worked out from the user id and the tool's salt), so there is nothing else to erase
/// here; what a tool kept on its own servers is the tool's, and the export names the tools so the person can ask them.
/// A result a score already put in the learner's progress stays there, as Learning keeps it.
/// </summary>
internal sealed class LtiPersonalDataContributor : IPersonalDataContributor
{
    internal const string ScoreKind = "LearningLtiScore";
    internal const string SubjectKind = "LearningLtiId";

    private readonly LtiScoreStore _scores;
    private readonly IExternalToolStore _tools;

    public LtiPersonalDataContributor(LtiScoreStore scores, IExternalToolStore tools)
    {
        _scores = scores;
        _tools = tools;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        var tools = await _tools.ListAllAsync(cancellationToken);

        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var tool in tools.Where(tool => !string.IsNullOrEmpty(tool.SubjectSalt)))
            {
                var record = context.Add(SubjectKind, tool.ItemId + ":" + userId, tool.Name, tool.CreatedUtc);

                record.Fields["Tool"] = tool.Name;
                record.Fields["ClientId"] = tool.ClientId;
                record.Fields["Id"] = LtiSubjects.For(tool, userId);
                record.Fields["PrivacyLevel"] = tool.PrivacyLevel.ToString();
            }

            foreach (var score in await _scores.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(ScoreKind, score.ItemId, tools.FirstOrDefault(tool => tool.ItemId == score.ToolId)?.Name ?? score.ToolId, score.TimestampUtc);

                record.Fields["Course"] = score.CourseId;
                record.Fields["LineItem"] = score.LineItemId;
                record.Fields["ScoreGiven"] = score.ScoreGiven?.ToString("0.####", CultureInfo.InvariantCulture);
                record.Fields["ScoreMaximum"] = score.ScoreMaximum?.ToString("0.####", CultureInfo.InvariantCulture);
                record.Fields["ActivityProgress"] = score.ActivityProgress;
                record.Fields["GradingProgress"] = score.GradingProgress;
                record.Fields["Comment"] = score.Comment;
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        var tools = await _tools.ListAllAsync(cancellationToken);

        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var score in await _scores.ListByUserAsync(userId, cancellationToken))
            {
                await _scores.DeleteAsync(score, cancellationToken);
                context.Result.Count(ScoreKind);
            }
        }

        if (tools.Count > 0)
        {
            context.Result.Warnings.Add($"External tools keep their own records of the people they served ({string.Join(", ", tools.Select(tool => tool.Name))}); ask each tool's provider to erase them. The tool knows the person by the id in the export.");
        }
    }
}
