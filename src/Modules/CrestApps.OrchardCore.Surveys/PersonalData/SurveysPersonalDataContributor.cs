using System.Globalization;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.PersonalData;

/// <summary>
/// Exports a person's identified survey responses, with their answers, and their invitations (to their user account,
/// or guest invitations to one of their e-mail addresses, with the responses linked to them); erasure removes who gave
/// the responses and everything that could point back to them (context, variables and properties; the answers stay for
/// results, and the key an in-progress response is counted under goes too), deletes the files they uploaded, their
/// invitations, their accommodations (extra time or attempts, with the reason they were granted) and their participation
/// counts. Accommodations a staff member granted to others keep that staff member as their actor: that is a record of
/// staff work, not the staff member's personal data. Participation is found
/// by the keys it is really stored under: <c>u:{id}</c> (or its hash when long) on identified surveys, the key of each of
/// the person's invitations, and <c>a:{hmac}</c> on each anonymous survey, recomputed with that survey's protected
/// secret.
/// </summary>
internal sealed class SurveysPersonalDataContributor : IPersonalDataContributor
{
    private const string ResponseKind = "SurveyResponse";
    private const string AnswerKind = "SurveyAnswer";
    private const string InvitationKind = "SurveyInvitation";
    private const string ParticipationKind = "SurveyParticipation";
    private const string FileKind = "SurveyFile";
    private const string OverrideKind = "SurveyAccommodation";

    private readonly ISession _session;
    private readonly ISurveyVersionCache _versions;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyAnonymitySecrets _secrets;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="versions">The version cache.</param>
    /// <param name="registry">The question type registry.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="secrets">The anonymity secrets, to recompute the participation keys of anonymous surveys.</param>
    /// <param name="serviceProvider">The services, for the response files.</param>
    public SurveysPersonalDataContributor(
        ISession session,
        ISurveyVersionCache versions,
        ISurveyControlRegistry registry,
        SurveyStore surveyStore,
        SurveyAnonymitySecrets secrets,
        IServiceProvider serviceProvider)
    {
        _session = session;
        _versions = versions;
        _registry = registry;
        _surveyStore = surveyStore;
        _secrets = secrets;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var invitations = await InvitationsAsync(context.Subject, cancellationToken);

        foreach (var response in await ResponsesAsync(context.Subject, invitations, cancellationToken))
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

        foreach (var invitation in invitations)
        {
            var record = context.Add(InvitationKind, invitation.ItemId, invitation.SurveyId, invitation.CreatedUtc);

            record.Fields["Email"] = invitation.Email;
            record.Fields["Status"] = invitation.Status.ToString();
            record.Fields["Sent"] = invitation.SentUtc?.ToString("O", CultureInfo.InvariantCulture);
        }

        foreach (var accommodation in await OverridesAsync(context.Subject, cancellationToken))
        {
            var record = context.Add(OverrideKind, accommodation.ItemId, accommodation.SurveyId, accommodation.CreatedUtc);

            record.Fields["Survey"] = accommodation.SurveyId;
            record.Fields["ExtraMinutes"] = accommodation.ExtraMinutes.ToString(CultureInfo.InvariantCulture);
            record.Fields["ExtraAttempts"] = accommodation.ExtraAttempts.ToString(CultureInfo.InvariantCulture);
            record.Fields["Opens"] = accommodation.OpensUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields["Closes"] = accommodation.ClosesUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields["Reason"] = accommodation.Reason;
        }
    }

    /// <inheritdoc />
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var files = ActivatorUtilities.GetServiceOrCreateInstance<SurveyResponseFiles>(_serviceProvider);
        // Only the anonymous surveys (whose secrets derive the respondent keys) and the surveys of the person's invitations
        // are loaded, a chunk at a time, not every survey.
        var anonymous = nameof(AnonymityMode.Anonymous);
        var anonymousIds = (await _session.QueryIndex<SurveyIndex>(index => index.Anonymity == anonymous || index.Anonymity == null, collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken)).Select(row => row.ItemId);
        var surveys = await LoadSurveysAsync(anonymousIds, cancellationToken);
        var anonymousSecrets = new List<string>();

        foreach (var survey in surveys.Values.Where(SurveyAnonymity.IsAnonymous))
        {
            if (await RevealAsync(survey, cancellationToken) is { } secret)
            {
                anonymousSecrets.Add(secret);
            }
        }

        var invitations = await InvitationsAsync(context.Subject, cancellationToken);

        foreach (var (id, survey) in await LoadSurveysAsync(invitations.Select(invitation => invitation.SurveyId).Where(id => !string.IsNullOrEmpty(id) && !surveys.ContainsKey(id)), cancellationToken))
        {
            surveys[id] = survey;
        }

        foreach (var response in await ResponsesAsync(context.Subject, invitations, cancellationToken))
        {
            // Every file the response owns goes, not only those its answers still refer to.
            var removedFiles = await files.DeleteAllAsync(response, cancellationToken);

            if (removedFiles > 0)
            {
                context.Result.Count(FileKind, removedFiles);
            }

            foreach (var answer in response.Answers.Values)
            {
                answer.AttachmentIds = [];
            }

            // The answers stay for results; nothing that names the person, or points back to them, does.
            response.UserId = null;
            response.Respondent = null;
            response.IpAddress = null;
            response.InvitationId = null;
            response.InvitationSeal = null;
            response.ResumeTokenHash = null;

            // The key the response is counted under names the person on an identified survey (u:{id} or i:{id}); left
            // in place, a later deadline completion would count it again and write back the participation erased below.
            response.RespondentKey = null;
            response.Context = null;
            response.Variables.Clear();
            response.Properties.Clear();
            await _session.SaveAsync(response, collection: SurveysConstants.ResponsesCollectionName, cancellationToken: cancellationToken);
            context.Result.Count(ResponseKind);
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var userId in context.Subject.UserIds)
        {
            keys.UnionWith(SurveyRespondentKeys.ForUser(userId, anonymousSecrets));
        }

        foreach (var invitation in invitations)
        {
            // A participation counted by invitation (a guest who opened the personal link, the i: key, or its HMAC on an
            // anonymous survey) is the person's too.
            if (surveys.TryGetValue(invitation.SurveyId ?? string.Empty, out var survey)
                && (!SurveyAnonymity.IsAnonymous(survey) || _secrets.CanReveal(survey)))
            {
                keys.Add(await _secrets.RespondentKeyAsync(survey, new SurveyRespondentIdentity(), invitation, cancellationToken));
            }

            _session.Delete(invitation, SurveysConstants.CollectionName);
            context.Result.Count(InvitationKind);
        }

        // The person's accommodations name them and say why they need them (a disability, say): they go.
        foreach (var accommodation in await OverridesAsync(context.Subject, cancellationToken))
        {
            _session.Delete(accommodation, SurveysConstants.CollectionName);
            context.Result.Count(OverrideKind);
        }

        foreach (var batch in keys.Where(key => !string.IsNullOrEmpty(key)).Chunk(200))
        {
            var participation = await _session.Query<SurveyParticipation, SurveyParticipationIndex>(index => index.RespondentKey.IsIn(batch), collection: SurveysConstants.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var row in participation)
            {
                _session.Delete(row, SurveysConstants.CollectionName);
                context.Result.Count(ParticipationKind);
            }
        }
    }

    // A secret this server can't read (missing Data Protection keys) can't recompute any key, so its survey is skipped
    // rather than stopping the whole erasure.
    private async Task<string> RevealAsync(Survey survey, CancellationToken cancellationToken)
    {
        try
        {
            return await _secrets.RevealAsync(survey, cancellationToken);
        }
        catch (SurveyAnonymitySecretUnreadableException)
        {
            return null;
        }
    }

    // The person's responses: those given while signed in, and those linked to one of their invitations (a guest who
    // answered an e-mail invitation on an identified survey).
    private async Task<IReadOnlyList<SurveyResponse>> ResponsesAsync(PersonalDataSubject subject, IReadOnlyList<SurveyInvitation> invitations, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, SurveyResponse>(StringComparer.Ordinal);
        var userIds = subject.UserIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (userIds.Length > 0)
        {
            foreach (var response in await _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.UserId.IsIn(userIds), collection: SurveysConstants.ResponsesCollectionName).ListAsync(cancellationToken))
            {
                found.TryAdd(response.ItemId, response);
            }
        }

        foreach (var batch in invitations.Select(invitation => invitation.ItemId).Where(id => !string.IsNullOrEmpty(id)).Chunk(200))
        {
            foreach (var response in await _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.InvitationId.IsIn(batch), collection: SurveysConstants.ResponsesCollectionName).ListAsync(cancellationToken))
            {
                found.TryAdd(response.ItemId, response);
            }
        }

        return found.Values.OrderBy(response => response.StartedUtc).ToList();
    }

    // The accommodations granted to the person's user accounts.
    private async Task<IReadOnlyList<SurveyOverride>> OverridesAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new List<SurveyOverride>();

        foreach (var batch in subject.UserIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Chunk(200))
        {
            found.AddRange(await _session.Query<SurveyOverride, SurveyOverrideIndex>(index => index.UserId.IsIn(batch), collection: SurveysConstants.CollectionName).ListAsync(cancellationToken));
        }

        return found;
    }

    // The person's invitations: those sent to their user account, and guest invitations sent to one of their e-mail
    // addresses.
    private async Task<IReadOnlyList<SurveyInvitation>> InvitationsAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, SurveyInvitation>(StringComparer.Ordinal);
        var userIds = subject.UserIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var emails = subject.Emails.Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();

        if (userIds.Length > 0)
        {
            foreach (var invitation in await _session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.UserId.IsIn(userIds), collection: SurveysConstants.CollectionName).ListAsync(cancellationToken))
            {
                found.TryAdd(invitation.ItemId, invitation);
            }
        }

        if (emails.Length > 0)
        {
            foreach (var invitation in await _session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.Email.IsIn(emails), collection: SurveysConstants.CollectionName).ListAsync(cancellationToken))
            {
                found.TryAdd(invitation.ItemId, invitation);
            }
        }

        return found.Values.ToList();
    }

    private async Task<Dictionary<string, Survey>> LoadSurveysAsync(IEnumerable<string> surveyIds, CancellationToken cancellationToken)
    {
        var surveys = new Dictionary<string, Survey>(StringComparer.Ordinal);

        foreach (var chunk in SurveyIdChunks.Of(surveyIds))
        {
            foreach (var survey in await _surveyStore.GetAsync(chunk, cancellationToken))
            {
                surveys[survey.ItemId] = survey;
            }
        }

        return surveys;
    }
}
