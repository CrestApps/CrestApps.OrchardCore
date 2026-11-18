using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Xapi.Services;

/// <summary>
/// Exports and erases a person's records in the Learning Record Store: the statements whose actor is the person's
/// account (the account a launched course gave the user, on any home page) or mailbox (mbox or mbox_sha1sum of an
/// e-mail address of the person), and the state and agent profile documents of those agents (an account on any home
/// page, as for statements, so a changed or missing site address leaves nothing behind). Erasure deletes them with
/// their attachments: a statement cannot be changed, and Learning keeps the completion on its own records.
/// </summary>
internal sealed class XapiPersonalDataContributor : IPersonalDataContributor
{
    internal const string StatementKind = "LearningStatement";
    internal const string DocumentKind = "LearningXapiDocument";

    private readonly XapiStatementStore _statements;
    private readonly XapiDocumentStore _documents;
    private readonly XapiStatementService _service;
    private readonly XapiHomePage _homePage;

    public XapiPersonalDataContributor(XapiStatementStore statements, XapiDocumentStore documents, XapiStatementService service, XapiHomePage homePage)
    {
        _statements = statements;
        _documents = documents;
        _service = service;
        _homePage = homePage;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var record in await FindStatementsAsync(context.Subject, cancellationToken))
        {
            var statement = JsonNode.Parse(record.Statement).AsObject();
            var summary = XapiStatementSummary.Of(statement);
            var item = context.Add(StatementKind, record.StatementId, $"{summary.Verb} {summary.Object}".Trim(), record.StoredUtc);

            item.Fields["Verb"] = record.Verb;
            item.Fields["Object"] = record.ObjectId;
            item.Fields["Registration"] = record.Registration;
            item.Fields["Timestamp"] = record.TimestampUtc?.ToString("O");
            item.Fields["Voided"] = record.Voided ? "true" : "false";
            item.Fields["Statement"] = record.Statement;
        }

        foreach (var document in await _documents.ListByAgentsAsync(await AgentKeysAsync(context.Subject), context.Subject.UserIds, cancellationToken))
        {
            var item = context.Add(DocumentKind, document.ItemId, document.DocumentId, document.UpdatedUtc);

            item.Fields["Kind"] = document.Kind;
            item.Fields["Activity"] = document.ActivityId;
            item.Fields["Registration"] = document.Registration;
            item.Fields["ContentType"] = document.ContentType;
            item.Fields["Content"] = IsText(document.ContentType) ? Encoding.UTF8.GetString(document.Content ?? []) : Convert.ToBase64String(document.Content ?? []);
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        var statements = await FindStatementsAsync(context.Subject, cancellationToken);

        await _service.DeleteAsync(statements, cancellationToken);

        foreach (var _ in statements)
        {
            context.Result.Count(StatementKind);
        }

        foreach (var document in await _documents.ListByAgentsAsync(await AgentKeysAsync(context.Subject), context.Subject.UserIds, cancellationToken))
        {
            await _documents.DeleteAsync(document, cancellationToken);
            context.Result.Count(DocumentKind);
        }
    }

    private async Task<IReadOnlyList<global::CrestApps.OrchardCore.Learning.Xapi.Models.XapiStatementRecord>> FindStatementsAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var keys = MailboxKeys(subject).ToList();
        var found = new Dictionary<string, global::CrestApps.OrchardCore.Learning.Xapi.Models.XapiStatementRecord>(StringComparer.Ordinal);

        foreach (var userId in subject.UserIds.DefaultIfEmpty(null))
        {
            foreach (var record in await _statements.ListByActorAsync(keys, userId, cancellationToken))
            {
                found.TryAdd(record.StatementId, record);
            }
        }

        return [.. found.Values.OrderBy(record => record.Sequence)];
    }

    private async Task<List<string>> AgentKeysAsync(PersonalDataSubject subject)
    {
        var homePage = await _homePage.GetAsync();
        var keys = MailboxKeys(subject).ToList();

        keys.AddRange(subject.UserIds.Select(userId => XapiAgents.AccountKey(homePage, userId)));

        return keys;
    }

#pragma warning disable CA5350 // xAPI's mbox_sha1sum is the SHA-1 hash of the mailbox by definition.
    private static IEnumerable<string> MailboxKeys(PersonalDataSubject subject)
    {
        foreach (var email in subject.Emails.Where(email => !string.IsNullOrWhiteSpace(email)))
        {
            var mailbox = "mailto:" + email.Trim();

            yield return "mbox:" + mailbox;
            yield return "mbox:" + mailbox.ToLowerInvariant();
            yield return "mbox_sha1sum:" + Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(mailbox)));
            yield return "mbox_sha1sum:" + Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(mailbox.ToLowerInvariant())));
        }
    }
#pragma warning restore CA5350

    private static bool IsText(string contentType)
        => contentType is not null &&
            (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || contentType.Contains("json", StringComparison.OrdinalIgnoreCase));
}
