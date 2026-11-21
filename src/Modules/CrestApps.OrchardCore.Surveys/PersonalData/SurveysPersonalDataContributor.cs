using System.Globalization;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.PersonalData;

/// <summary>
/// Exports a person's identified survey responses, with their answers, and their invitations; erasure removes who gave
/// the responses (the answers stay for results), deletes the files they uploaded, their invitations and their
/// participation counts.
/// </summary>
internal sealed class SurveysPersonalDataContributor : IPersonalDataContributor
{
    private const string ResponseKind = "SurveyResponse";
    private const string AnswerKind = "SurveyAnswer";
    private const string InvitationKind = "SurveyInvitation";
    private const string ParticipationKind = "SurveyParticipation";
    private const string FileKind = "SurveyFile";

    private readonly ISession _session;
    private readonly ISurveyVersionCache _versions;
    private readonly ISurveyControlRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="versions">The version cache.</param>
    /// <param name="registry">The question type registry.</param>
    /// <param name="serviceProvider">The services, for the optional attachment service.</param>
    public SurveysPersonalDataContributor(
        ISession session,
        ISurveyVersionCache versions,
        ISurveyControlRegistry registry,
        IServiceProvider serviceProvider)
    {
        _session = session;
        _versions = versions;
        _registry = registry;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var response in await ResponsesAsync(userId, cancellationToken))
            {
                var version = await _versions.GetAsync(response.SurveyVersionId, cancellationToken);
                var definition = version?.Definition;
                var record = context.Add(ResponseKind, response.ItemId, definition?.Title ?? response.SurveyId, response.StartedUtc);

                record.Fields["Number"] = response.Number;
                record.Fields["Survey"] = response.SurveyId;
                record.Fields["Status"] = response.Status.ToString();
                record.Fields["Completed"] = response.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture);
                record.Fields["Score"] = response.Score?.ToString(CultureInfo.InvariantCulture);
                record.Fields["Percent"] = response.Percent?.ToString(CultureInfo.InvariantCulture);
                record.Fields["Feedback"] = response.Feedback;
                record.Fields["IpAddress"] = response.IpAddress;

                if (definition is null)
                {
                    continue;
                }

                var format = new SurveyFormatContext { Culture = CultureInfo.InvariantCulture, Definition = definition };

                foreach (var question in definition.Pages.SelectMany(page => page.Questions))
                {
                    if (question?.Id is null
                        || !response.Answers.TryGetValue(question.Id, out var answer)
                        || _registry.GetHandler(question.ControlType) is not { } handler)
                    {
                        continue;
                    }

                    var item = context.Add(AnswerKind, response.ItemId + ":" + question.Id, question.Title ?? question.Name, answer.AnsweredUtc == default ? null : answer.AnsweredUtc);

                    item.Fields["Question"] = question.Name;
                    item.Fields["Answer"] = handler.FormatAnswer(question, answer, format);
                    item.Fields["Score"] = answer.Score?.ToString(CultureInfo.InvariantCulture);
                    item.Fields["GraderComment"] = answer.GraderComment;
                }
            }

            foreach (var invitation in await InvitationsAsync(userId, cancellationToken))
            {
                var record = context.Add(InvitationKind, invitation.ItemId, invitation.SurveyId, invitation.CreatedUtc);

                record.Fields["Email"] = invitation.Email;
                record.Fields["Status"] = invitation.Status.ToString();
                record.Fields["Sent"] = invitation.SentUtc?.ToString("O", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <inheritdoc />
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attachments = _serviceProvider.GetService<IAttachmentService>();

        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var response in await ResponsesAsync(userId, cancellationToken))
            {
                foreach (var attachmentId in response.Answers.Values.SelectMany(answer => answer.AttachmentIds ?? []).Distinct(StringComparer.Ordinal).ToList())
                {
                    if (attachments is not null && await attachments.DeleteAsync(attachmentId, cancellationToken))
                    {
                        context.Result.Count(FileKind);
                    }
                }

                foreach (var answer in response.Answers.Values)
                {
                    answer.AttachmentIds = [];
                }

                // The answers stay for results; nothing that names the person does.
                response.UserId = null;
                response.Respondent = null;
                response.IpAddress = null;
                response.InvitationId = null;
                response.ResumeTokenHash = null;
                await _session.SaveAsync(response, collection: SurveysConstants.ResponsesCollectionName, cancellationToken: cancellationToken);
                context.Result.Count(ResponseKind);
            }

            foreach (var invitation in await InvitationsAsync(userId, cancellationToken))
            {
                _session.Delete(invitation, SurveysConstants.CollectionName);
                context.Result.Count(InvitationKind);
            }

            var participation = await _session.Query<SurveyParticipation, SurveyParticipationIndex>(index => index.RespondentKey == userId, collection: SurveysConstants.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var row in participation)
            {
                _session.Delete(row, SurveysConstants.CollectionName);
                context.Result.Count(ParticipationKind);
            }
        }
    }

    private async Task<IReadOnlyList<SurveyResponse>> ResponsesAsync(string userId, CancellationToken cancellationToken)
        => (await _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.UserId == userId, collection: SurveysConstants.ResponsesCollectionName)
            .OrderBy(index => index.StartedUtc)
            .ListAsync(cancellationToken)).ToList();

    private async Task<IReadOnlyList<SurveyInvitation>> InvitationsAsync(string userId, CancellationToken cancellationToken)
        => (await _session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.UserId == userId, collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken)).ToList();
}
