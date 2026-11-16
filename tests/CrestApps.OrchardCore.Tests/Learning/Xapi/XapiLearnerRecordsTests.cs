using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi.Recipes;
using CrestApps.OrchardCore.Learning.Xapi.Reports;
using CrestApps.OrchardCore.Learning.Xapi.Services;
using CrestApps.OrchardCore.Learning.Xapi.Workflows;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

#pragma warning disable CA5350 // xAPI's mbox_sha1sum is the SHA-1 hash of a mailbox by definition.

/// <summary>
/// Statements as learner records: the data set of Data Pipelines and the Report Builder (no e-mail address), personal
/// data export and erasure by account and mailbox, the recipe and deployment steps, and the workflow input.
/// </summary>
public sealed class XapiLearnerRecordsTests
{
    private const string HomePage = "http://localhost";

    private static readonly ClaimsPrincipal _reporter = CalendarTestHarness.User("reporter", LearningPermissions.ViewLearningReports.Name);

    [Fact]
    public async Task DataSet_HasOneRowPerStatement_WithoutAnyEmailAddress()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(AddDataSource);
        var byMailbox = XapiSamples.Full(XapiSamples.NewId());
        var byAccount = XapiSamples.Simple(XapiSamples.NewId());

        byAccount["actor"] = XapiAgents.Account(HomePage, "user-1", "Learner One");
        byAccount["object"] = XapiSamples.Agent("mailto:object@example.com", "Object person");

        await lrs.PostAsync(new JsonArray(byMailbox, byAccount));

        var (dataSets, rows, hidden) = await lrs.RunAsync(async services =>
        {
            var source = services.GetRequiredService<LearningXapiDataSource>();
            var context = new DataSourceContext { User = _reporter };
            var table = await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery { DataSet = LearningStatementsReportDataSet.DataSetName, MaxRows = 100, Context = context }, TestContext.Current.CancellationToken);

            return (
                await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken),
                table.Rows.Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal)).ToList(),
                await source.GetDataSetsAsync(new DataSourceContext { User = CalendarTestHarness.User("learner") }, TestContext.Current.CancellationToken));
        });

        var mailboxRow = rows.Single(row => (string)row["StatementId"] == byMailbox["id"].GetValue<string>());
        var accountRow = rows.Single(row => (string)row["StatementId"] == byAccount["id"].GetValue<string>());

        Assert.Contains(dataSets, dataSet => dataSet.Name == LearningStatementsReportDataSet.DataSetName);
        Assert.Empty(hidden);
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows.SelectMany(row => row.Values), value => value?.ToString()?.Contains('@') == true);
        Assert.Equal(Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes("mailto:learner@example.com"))), mailboxRow["ActorMailboxHash"]);
        Assert.Equal(XapiSamples.Completed, mailboxRow["Verb"]);
        Assert.Equal("completed", mailboxRow["VerbLabel"]);
        Assert.Equal(XapiSamples.Activity, mailboxRow["ObjectId"]);
        Assert.Equal("Example meeting", mailboxRow["ObjectLabel"]);
        Assert.Equal(XapiSamples.Registration, mailboxRow["Registration"]);
        Assert.Equal(true, mailboxRow["Success"]);
        Assert.Equal(0.95m, Convert.ToDecimal(mailboxRow["ScoreScaled"]));
        Assert.Equal("user-1", accountRow["ActorAccountName"]);
        Assert.Null(accountRow["ObjectId"]);
        Assert.Equal("Object person", accountRow["ObjectLabel"]);
    }

    [Fact]
    public async Task PersonalData_ExportsAndErasesTheStatementsAndDocumentsOfTheAccountAndMailbox()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var byAccount = XapiSamples.Simple(XapiSamples.NewId());
        var byMailbox = XapiSamples.Simple(XapiSamples.NewId(), mbox: "mailto:Ana@Example.com");
        var bySha1 = XapiSamples.Simple(XapiSamples.NewId());
        var someoneElse = XapiSamples.Simple(XapiSamples.NewId(), mbox: "mailto:bob@example.com");
        var content = Encoding.UTF8.GetBytes("here is a simple attachment");

        byAccount["actor"] = XapiAgents.Account("https://elsewhere.example.com", "ana-id");
        bySha1["actor"] = new JsonObject { ["mbox_sha1sum"] = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes("mailto:ana@example.com"))) };
        byAccount["attachments"] = new JsonArray(new JsonObject
        {
            ["usageType"] = "http://example.com/attachment-usage/test",
            ["display"] = new JsonObject { ["en-US"] = "Attachment" },
            ["contentType"] = "text/plain",
            ["length"] = content.Length,
            ["sha2"] = Convert.ToHexStringLower(SHA256.HashData(content)),
        });

        var (body, contentType) = XapiMultipart.Write(
        [
            new XapiPart { Headers = { ["Content-Type"] = "application/json" }, Content = Encoding.UTF8.GetBytes(new JsonArray(byAccount, byMailbox, bySha1, someoneElse).ToJsonString()) },
            new XapiPart { Headers = { ["Content-Type"] = "text/plain", ["Content-Transfer-Encoding"] = "binary", [XapiConstants.Headers.Hash] = Convert.ToHexStringLower(SHA256.HashData(content)) }, Content = content },
        ]);
        var request = XapiRequest.Create("POST", "statements", body);

        request.Headers["Content-Type"] = contentType;
        Assert.Equal(200, (await lrs.StatementsAsync(request)).Status);

        await lrs.DocumentAsync("PUT", "agents/profile", new Dictionary<string, string> { ["agent"] = XapiAgents.Account(HomePage, "ana-id").ToJsonString(), ["profileId"] = "prefs" }, new JsonObject { ["lang"] = "fr" });
        await lrs.DocumentAsync("PUT", "agents/profile", new Dictionary<string, string> { ["agent"] = """{ "mbox": "mailto:bob@example.com" }""", ["profileId"] = "prefs" }, new JsonObject { ["lang"] = "en" });

        var export = new PersonalDataExport();

        await lrs.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<XapiPersonalDataContributor>(services).ExportAsync(new PersonalDataExportContext(new PersonalDataSubject(userId: "ana-id", email: "ana@example.com"), export));

            return true;
        });

        var result = new PersonalDataAnonymizeResult();

        await lrs.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<XapiPersonalDataContributor>(services).AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(userId: "ana-id", email: "ana@example.com"), null, result));

            return true;
        });

        var left = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string>())).Text)["statements"].AsArray().Select(statement => statement["id"].GetValue<string>()).ToList();

        Assert.Equal(3, export.Records.Count(record => record.Kind == XapiPersonalDataContributor.StatementKind));
        Assert.Single(export.Records, record => record.Kind == XapiPersonalDataContributor.DocumentKind);
        Assert.Equal(3, result.Records[XapiPersonalDataContributor.StatementKind]);
        Assert.Equal(1, result.Records[XapiPersonalDataContributor.DocumentKind]);
        Assert.Equal([someoneElse["id"].GetValue<string>()], left);
        Assert.Empty(lrs.Files.Stored);
        Assert.Equal(200, (await lrs.DocumentAsync("GET", "agents/profile", new Dictionary<string, string> { ["agent"] = """{ "mbox": "mailto:bob@example.com" }""", ["profileId"] = "prefs" })).Status);
    }

    [Fact]
    public async Task RecipeStep_ImportsStatementsAsStored_SkippingTheSameAndReportingConflicts()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var existing = XapiSamples.Simple(XapiSamples.NewId());

        await lrs.PostAsync(existing);

        var imported = XapiSamples.Simple(XapiSamples.NewId(), verb: XapiSamples.Attempted);

        imported["stored"] = "2025-01-02T03:04:05.678Z";
        imported["authority"] = XapiSamples.Agent("mailto:lrs@other.example.com", "Other LRS");

        var voided = XapiSamples.Simple(XapiSamples.NewId());

        voided["stored"] = "2025-01-02T03:04:06.000Z";
        voided["authority"] = XapiSamples.Agent("mailto:lrs@other.example.com", "Other LRS");

        var same = existing.DeepClone().AsObject();

        same["stored"] = "2025-01-01T00:00:00.000Z";
        same["authority"] = XapiSamples.Agent("mailto:lrs@other.example.com", "Other LRS");

        var conflict = XapiSamples.Simple(existing["id"].GetValue<string>(), verb: XapiSamples.Experienced);

        conflict["stored"] = "2025-01-01T00:00:00.000Z";
        conflict["authority"] = XapiSamples.Agent("mailto:lrs@other.example.com", "Other LRS");

        var context = new RecipeExecutionContext
        {
            Name = XapiConstants.StatementsRecipeStep,
            Step = new JsonObject
            {
                ["name"] = XapiConstants.StatementsRecipeStep,
                ["Statements"] = new JsonArray(
                    new JsonObject { ["Statement"] = imported, ["ClientId"] = "other-client", ["AuthorityKey"] = "client:other-client" },
                    new JsonObject { ["Statement"] = voided, ["Voided"] = true },
                    new JsonObject { ["Statement"] = same },
                    new JsonObject { ["Statement"] = conflict },
                    new JsonObject { ["Statement"] = new JsonObject { ["actor"] = XapiSamples.Agent() } }),
            },
        };

        await lrs.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<LearningStatementsRecipeStep>(services).ExecuteAsync(context);

            return true;
        });

        var stored = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = imported["id"].GetValue<string>() })).Text);

        Assert.Equal("2025-01-02T03:04:05.678Z", stored["stored"].GetValue<string>());
        Assert.Equal("mailto:lrs@other.example.com", stored["authority"]["mbox"].GetValue<string>());
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["voidedStatementId"] = voided["id"].GetValue<string>() })).Status);
        Assert.Equal(2, context.Errors.Count);
        Assert.Equal(XapiSamples.Completed, JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = existing["id"].GetValue<string>() })).Text)["verb"]["id"].GetValue<string>());
    }

    [Fact]
    public async Task DeploymentStep_ExportsEveryStatement_OnlyForAPersonWhoMayExportLearnerRecords()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(services => services.AddScoped<LearnerUserLookup>());
        var first = XapiSamples.Simple(XapiSamples.NewId());

        await lrs.PostAsync(new JsonArray(first, XapiSamples.Voiding(first["id"].GetValue<string>())));

        var (allowed, refused) = await lrs.RunAsync(async services =>
        {
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            var exported = new DeploymentPlanResult(null, new RecipeDescriptor());
            var nothing = new DeploymentPlanResult(null, new RecipeDescriptor());
            var source = ActivatorUtilities.CreateInstance<LearningStatementsDeploymentSource>(services);

            accessor.HttpContext = new DefaultHttpContext { User = CalendarTestHarness.User("exporter", LearningPermissions.ExportLearnerRecords.Name) };
            await source.ProcessDeploymentStepAsync(new LearningStatementsDeploymentStep(), exported);

            accessor.HttpContext = new DefaultHttpContext { User = CalendarTestHarness.User("viewer") };
            await source.ProcessDeploymentStepAsync(new LearningStatementsDeploymentStep(), nothing);

            return (exported, nothing);
        });

        var rows = allowed.Steps.Single()["Statements"].AsArray();

        Assert.Equal(2, rows.Count);
        Assert.True(rows[0]["Voided"].GetValue<bool>());
        Assert.Equal(first["id"].GetValue<string>(), rows[0]["Statement"]["id"].GetValue<string>());
        Assert.Empty(refused.Steps);
    }

    [Fact]
    public async Task WorkflowInput_NamesTheStatement_WithoutAnyEmailAddress()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var statement = XapiSamples.Full(XapiSamples.NewId());

        await lrs.PostAsync(statement);

        var record = await lrs.RunAsync(services => services.GetRequiredService<XapiStatementStore>().FindByStatementIdAsync(statement["id"].GetValue<string>()));
        var input = LearningStatementWorkflowObserver.Input(record);

        Assert.Equal(statement["id"].GetValue<string>(), input["StatementId"]);
        Assert.Equal(XapiSamples.Completed, input["Verb"]);
        Assert.Equal(XapiSamples.Activity, input["ObjectId"]);
        Assert.Equal(XapiSamples.Registration, input["Registration"]);
        Assert.DoesNotContain(input.Values, value => value?.ToString()?.Contains('@') == true);
    }

    private static void AddDataSource(IServiceCollection services)
    {
        services.AddScoped<LearningStatementsReportDataSet>();
        services.AddScoped<LearningXapiDataSource>();
    }
}
