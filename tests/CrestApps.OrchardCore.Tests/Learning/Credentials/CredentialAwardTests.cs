using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Phase 8, slice 5b: credential awards. The rules award once, after the change is saved, through Learning's completion
/// evidence rules; an award copies its definition, holds a status slot of its own and carries a credential signed with
/// the credentials key set.
/// </summary>
public sealed class CredentialAwardTests
{
    private static readonly CredentialAwardRule _courseRule = new() { Kind = CredentialsConstants.RuleKinds.CourseCompleted, TargetId = "safety" };
    private static readonly CredentialAwardRule _quizRule = new() { Kind = CredentialsConstants.RuleKinds.QuizPassed, TargetId = "final" };

    [Fact]
    public async Task CompletingACourse_AwardsTheCredentialOnce_AfterTheCompletionIsSaved()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift", _courseRule);

        // Act
        var enrollment = await h.CompleteCourseAsync("ana");
        var beforeTheQueueRuns = await h.AwardsAsync("ana");
        var results = await h.RunQueuedAsync();
        var awards = await h.AwardsAsync("ana");

        // Assert
        Assert.Empty(beforeTheQueueRuns);
        Assert.True(Assert.Single(results).Created);

        var award = Assert.Single(awards);

        Assert.Equal("forklift", award.DefinitionId);
        Assert.Equal(CredentialsConstants.RuleKinds.CourseCompleted, award.RuleKind);
        Assert.Equal(enrollment.ItemId, award.SourceId);
        Assert.Equal("ana", award.RecipientUserId);
        Assert.Equal(LearningTestHarness.Now, award.IssuedUtc);
        Assert.Equal(32, award.PublicId.Length);
        Assert.False(string.IsNullOrEmpty(award.Jwt));
        Assert.Equal(CredentialUrls.Credential(CredentialTestHarness.BaseUrl, award.PublicId), JsonNode.Parse(award.Credential)["id"].GetValue<string>());
    }

    [Fact]
    public async Task AwardAsync_TwiceForTheSameSource_ReturnsTheSameAward()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift", _courseRule);
        var request = new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.CourseCompleted, SourceId = "enrollment1" };

        // Act
        var first = await h.AwardAsync(request);
        var second = await h.AwardAsync(request);

        // Assert
        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Award.ItemId, second.Award.ItemId);
        Assert.Single(await h.AwardsAsync("ana"));
    }

    [Fact]
    public async Task APersonHoldingAValidAward_GetsNoSecond_FromAnotherRule()
    {
        // Arrange: the definition is earned by the course or by the quiz; ana does both.
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift", _courseRule, _quizRule);

        // Act
        await h.CompleteCourseAsync("ana");
        await h.PassQuizAsync("ana");
        var results = await h.RunQueuedAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(1, results.Count(result => result.Created));
        Assert.Single(await h.AwardsAsync("ana"));
    }

    [Fact]
    public async Task ACompletionRestingOnABrowserResult_AwardsNothing_UnlessTheItemAllowsIt()
    {
        // Arrange: ana's course has a package whose result the browser reports; ben's lets such a result count.
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Inner.Courses.AddCourse("strict");
        h.Inner.Courses.AddItem("strict", "package-strict", "Package", CompletionModes.PackageReported);
        h.Inner.Courses.AddCourse("lenient");
        h.Inner.Courses.AddItem("lenient", "package-lenient", "Package", CompletionModes.PackageReported, configure: part => part.AllowSelfReported = true);
        h.Definitions.Add("strict-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.CourseCompleted, TargetId = "strict" });
        h.Definitions.Add("lenient-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.CourseCompleted, TargetId = "lenient" });

        // Act
        await ReportFromTheBrowserAsync(h, "ana", "strict", "package-strict");
        await ReportFromTheBrowserAsync(h, "ben", "lenient", "package-lenient");
        await h.RunQueuedAsync();

        // Assert
        Assert.Empty(await h.AwardsAsync("ana"));
        Assert.Equal("lenient-badge", Assert.Single(await h.AwardsAsync("ben")).DefinitionId);
    }

    [Fact]
    public async Task PassingAQuiz_Awards_AndFailingIt_DoesNot()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("exam-badge", _quizRule);

        // Act
        var passed = await h.PassQuizAsync("ana");
        await h.PassQuizAsync("ben", pass: false);
        await h.RunQueuedAsync();

        // Assert
        var award = Assert.Single(await h.AwardsAsync("ana"));

        Assert.Equal(CredentialsConstants.RuleKinds.QuizPassed, award.RuleKind);
        Assert.Equal(passed.ItemId, award.SourceId);
        Assert.Empty(await h.AwardsAsync("ben"));
    }

    [Fact]
    public async Task CompletingALearningPath_Awards()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("path-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.LearningPathCompleted, TargetId = "path1" });

        // Act
        await h.RunAsync(async services =>
        {
            foreach (var observer in services.GetServices<ILearningPathObserver>())
            {
                await observer.CompletedAsync(new LearningPathProgress { ItemId = "progress1", PathId = "path1", UserId = "ana", CompletedUtc = h.Clock.UtcNow });
            }
        });
        await h.RunQueuedAsync();

        // Assert
        var award = Assert.Single(await h.AwardsAsync("ana"));

        Assert.Equal(CredentialsConstants.RuleKinds.LearningPathCompleted, award.RuleKind);
        Assert.Equal("progress1", award.SourceId);
    }

    [Fact]
    public async Task ACertificateIssuedToAUser_AwardsTheHolder()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("cert-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.CertificationIssued, TargetId = "cert1" });

        // Act
        await h.RunAsync(async services =>
        {
            foreach (var observer in services.GetServices<ICertificationRecordObserver>())
            {
                await observer.IssuedAsync(new CertificationRecordContext
                {
                    Record = new CertificationRecord { ItemId = "record1", CertificationId = "cert1", SubjectKind = SkillSubjectKind.User, SubjectId = "ana", Status = CertificationStatus.Valid },
                });
            }
        });
        await h.RunQueuedAsync();

        // Assert
        var award = Assert.Single(await h.AwardsAsync("ana"));

        Assert.Equal(CredentialsConstants.RuleKinds.CertificationIssued, award.RuleKind);
        Assert.Equal("record1", award.SourceId);
    }

    [Fact]
    public async Task AManualAward_RecordsWhoAndWhy_AndAnotherWhileItIsValid_ReturnsIt()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");

        // Act
        var first = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin", Reason = "Passed the practical test" });
        var second = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" });
        await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(first.Award.ItemId, "admin", "Given to the wrong person"));
        var afterRevocation = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" });

        // Assert
        Assert.True(first.Created);
        Assert.Equal("admin", first.Award.AwardedBy);
        Assert.Equal("Passed the practical test", first.Award.AwardReason);
        Assert.False(string.IsNullOrEmpty(first.Award.SourceId));
        Assert.False(second.Created);
        Assert.Equal(first.Award.ItemId, second.Award.ItemId);
        Assert.True(afterRevocation.Created);
        Assert.NotEqual(first.Award.ItemId, afterRevocation.Award.ItemId);
        Assert.Equal(2, (await h.AwardsAsync("ana")).Count);
    }

    [Fact]
    public async Task Revoke_RecordsWhoWhenAndWhy_AndOnlyOnce()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var awarded = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" });

        // Act
        var noReason = await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(awarded.Award.ItemId, "admin", " "));
        var revoked = await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(awarded.Award.ItemId, "admin", "Cheating"));
        var again = await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(awarded.Award.ItemId, "admin", "Cheating"));
        var missing = await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync("nope", "admin", "Cheating"));
        var stored = Assert.Single(await h.AwardsAsync("ana"));

        // Assert
        Assert.Equal(CredentialAwardErrors.ReasonRequired, noReason.ErrorCode);
        Assert.True(revoked.Succeeded);
        Assert.Equal(CredentialAwardErrors.AlreadyRevoked, again.ErrorCode);
        Assert.Equal(CredentialAwardErrors.AwardNotFound, missing.ErrorCode);
        Assert.Equal("admin", stored.RevokedBy);
        Assert.Equal("Cheating", stored.RevokedReason);
        Assert.Equal(LearningTestHarness.Now, stored.RevokedUtc);
        Assert.Equal(CredentialAwardStatus.Revoked, stored.GetStatus(h.Clock.UtcNow));
    }

    [Fact]
    public async Task TheAward_KeepsTheDefinitionAsItWasAwarded()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        var definition = h.Definitions.Add("forklift");
        var awarded = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" });

        // Act: the definition changes later.
        definition.VersionId = "forklift-v2";
        definition.Achievement.Name = "Renamed";
        var stored = Assert.Single(await h.AwardsAsync("ana"));

        // Assert
        Assert.Equal("forklift-v1", stored.DefinitionVersionId);
        Assert.Equal("Forklift operator", stored.Achievement.Name);
        Assert.Equal(CredentialUrls.Achievement(CredentialTestHarness.BaseUrl, "forklift"), stored.Achievement.Id);
        Assert.Equal("Forklift operator", JsonNode.Parse(stored.Credential)["credentialSubject"]["achievement"]["name"].GetValue<string>());
        Assert.Equal(awarded.Award.ItemId, stored.ItemId);
    }

    [Fact]
    public async Task TheAward_ExpiresAfterTheDefinitionsValidity()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift").Achievement.ValidityMonths = 12;
        h.Definitions.Add("lifetime");

        // Act
        var expiring = (await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award;
        var lifetime = (await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "lifetime", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award;

        // Assert
        Assert.Equal(LearningTestHarness.Now.AddMonths(12), expiring.ExpiresUtc);
        Assert.Equal("2027-11-02T12:00:00Z", JsonNode.Parse(expiring.Credential)["validUntil"].GetValue<string>());
        Assert.Equal(new DateTimeOffset(LearningTestHarness.Now.AddMonths(12)).ToUnixTimeSeconds(), CredentialJws.Read(expiring.Jwt).Payload["exp"].GetValue<long>());
        Assert.Equal(CredentialAwardStatus.Expired, expiring.GetStatus(LearningTestHarness.Now.AddMonths(13)));
        Assert.Null(lifetime.ExpiresUtc);
        Assert.Null(JsonNode.Parse(lifetime.Credential)["validUntil"]);
        Assert.Equal(CredentialAwardStatus.Active, lifetime.GetStatus(LearningTestHarness.Now.AddYears(50)));
    }

    [Fact]
    public async Task EachAward_HoldsAStatusSlotOfItsOwn()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");

        // Act
        var awards = new List<CredentialAward>();

        foreach (var user in new[] { "ana", "ben", "cleo", "dan" })
        {
            awards.Add((await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = user, RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award);
        }

        // Assert
        Assert.All(awards, award => Assert.Equal(1, award.StatusListNumber));
        Assert.All(awards, award => Assert.InRange(award.StatusListIndex, 0, CredentialsConstants.StatusListCapacity - 1));
        Assert.Equal(4, awards.Select(award => award.StatusListIndex).Distinct().Count());
        Assert.All(awards, award => Assert.Equal(award.StatusListIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), JsonNode.Parse(award.Credential)["credentialStatus"]["statusListIndex"].GetValue<string>()));
    }

    [Fact]
    public async Task AnAward_IsRefused_ForAnUnknownPerson_APersonWithoutEmail_OrADefinitionNotPublished()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");

        // Act
        var ghost = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ghost", RuleKind = CredentialsConstants.RuleKinds.Manual });
        var noMail = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "nomail", RuleKind = CredentialsConstants.RuleKinds.Manual });
        var notPublished = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "draft", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual });
        var invalid = await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana" });

        // Assert
        Assert.Equal(CredentialAwardErrors.RecipientNotFound, ghost.ErrorCode);
        Assert.Equal(CredentialAwardErrors.RecipientHasNoAddress, noMail.ErrorCode);
        Assert.Equal(CredentialAwardErrors.DefinitionNotFound, notPublished.ErrorCode);
        Assert.Equal(CredentialAwardErrors.InvalidRequest, invalid.ErrorCode);
        Assert.Empty(await h.AwardsAsync("nomail"));
    }

    [Fact]
    public async Task TheCredential_NamesTheRecipientByASaltedHash_NeverThePlainAddress()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");

        // Act
        var award = (await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award;
        var stored = await h.RunAsync(services => services.GetRequiredService<CredentialAwardStore>().FindByIdAsync(award.ItemId).AsTask());

        // Assert
        Assert.Equal("emailAddress", stored.Recipient.IdentityType);
        Assert.True(CredentialIdentityHash.Matches("ana@example.com", stored.Recipient.Salt, stored.Recipient.IdentityHash));
        Assert.DoesNotContain("ana@example.com", stored.Credential, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana@example.com", CredentialJws.Read(stored.Jwt).Payload.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana@example.com", System.Text.Json.JsonSerializer.Serialize(stored), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheJwt_IsTheCredentialSignedWithTheKeySet_AndStillVerifiesAfterTheKeyRotates()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var award = (await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award;

        // Act
        h.Ring.Rotate();
        var (header, payload) = CredentialJws.Read(award.Jwt);
        var key = await h.RunAsync(services => services.GetRequiredService<CredentialSigningKeys>().FindPublishedKeyAsync(header["kid"].GetValue<string>()));

        // Assert
        Assert.Equal(CredentialUrls.Key(CredentialTestHarness.BaseUrl, "credentials-1"), header["kid"].GetValue<string>());
        Assert.NotNull(key);
        Assert.True(CredentialJws.Verify(award.Jwt, key));
        Assert.Equal(JsonNode.Parse(award.Credential)["id"].GetValue<string>(), payload["jti"].GetValue<string>());
        Assert.Equal(CredentialUrls.Issuer(CredentialTestHarness.BaseUrl), payload["iss"].GetValue<string>());
    }

    [Fact]
    public async Task ShowName_IsSetOnlyByTheRecipient()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var award = (await h.AwardAsync(new CredentialAwardRequest { DefinitionId = "forklift", UserId = "ana", RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award;

        // Act
        var byOther = await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().SetShowNameAsync(award.ItemId, "ben", true));
        var byRecipient = await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().SetShowNameAsync(award.ItemId, "ana", true));

        // Assert
        Assert.False(award.ShowName);
        Assert.False(byOther);
        Assert.True(byRecipient);
        Assert.True(Assert.Single(await h.AwardsAsync("ana")).ShowName);
    }

    private static async Task ReportFromTheBrowserAsync(CredentialTestHarness h, string userId, string courseId, string itemId)
    {
        var enrollment = await h.Inner.EnrollAsync(userId, courseId);

        await h.RunAsync(async services =>
        {
            var fresh = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId);
            var update = await services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(fresh, new CourseItemResult { ItemId = itemId, ReportedComplete = true, SelfReported = true });

            Assert.True(update.CourseCompleted);
        });
    }
}
