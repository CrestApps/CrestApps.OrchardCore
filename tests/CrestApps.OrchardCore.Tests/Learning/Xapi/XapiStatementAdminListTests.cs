using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiStatementAdminListTests
{
    [Fact]
    public async Task AdminList_FiltersByVerbActivityActorRegistrationAndDate_NewestFirst_VoidedIncluded()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var learner = XapiSamples.Simple(XapiSamples.NewId());
        var account = XapiSamples.Simple(XapiSamples.NewId(), verb: XapiSamples.Attempted, activity: XapiSamples.Course);

        learner["context"] = new JsonObject { ["registration"] = XapiSamples.Registration };
        account["actor"] = XapiAgents.Account("https://site.example.com", "user-1", "Learner One");

        await lrs.PostAsync(learner);
        lrs.Clock.UtcNow = lrs.Clock.UtcNow.AddDays(2);
        await lrs.PostAsync(account);
        await lrs.PostAsync(XapiSamples.Voiding(learner["id"].GetValue<string>()));

        var all = await PageAsync(lrs, new XapiStatementAdminFilter());
        var byVerb = await PageAsync(lrs, new XapiStatementAdminFilter { Verb = XapiSamples.Attempted });
        var byActivity = await PageAsync(lrs, new XapiStatementAdminFilter { Activity = XapiSamples.Course });
        var byAccount = await PageAsync(lrs, new XapiStatementAdminFilter { ActorAccountName = "user-1" });
        var byMailbox = new XapiStatementAdminFilter();

        byMailbox.ActorKeys.Add("mbox:mailto:learner@example.com");

        var byRegistration = await PageAsync(lrs, new XapiStatementAdminFilter { Registration = XapiSamples.Registration.ToUpperInvariant() });
        var early = await PageAsync(lrs, new XapiStatementAdminFilter { ToUtc = LearningTestHarness.Now.AddDays(1) });

        Assert.Equal(3, all.Count);
        Assert.Equal(XapiSamples.Attempted, byVerb.Single().Verb);
        Assert.Equal(account["id"].GetValue<string>(), byActivity.Single().StatementId);
        Assert.Equal(account["id"].GetValue<string>(), byAccount.Single().StatementId);
        Assert.True((await PageAsync(lrs, byMailbox)).Single().Voided);
        Assert.Equal(learner["id"].GetValue<string>(), byRegistration.Single().StatementId);
        Assert.Equal(learner["id"].GetValue<string>(), early.Single().StatementId);
        Assert.Equal("voided", all[0].Verb.Split('/')[^1]);
    }

    private static async Task<IReadOnlyList<CrestApps.OrchardCore.Learning.Xapi.Models.XapiStatementRecord>> PageAsync(XapiTestHarness lrs, XapiStatementAdminFilter filter)
        => (await lrs.RunAsync(services => services.GetRequiredService<XapiStatementStore>().PageForAdminAsync(filter, 0, 50))).Items;
}
