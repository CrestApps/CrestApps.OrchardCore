using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Settings;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Digital credentials on a running site: a credential definition content item, a credential awarded by a course
/// completion (after the request, in its own scope) and by hand, the anonymous public endpoints (the credential in
/// JSON-LD and as a JWT that verifies with the published key, the achievement, the issuer, the key set and the status
/// list), the verification page, and a revocation that the status list and the page show at once.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningCredentialsTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningCredentialsTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ACourseCompletion_AwardsTheCredential_AfterTheRequest()
    {
        // Arrange: a definition awarded by the other course; the spare learner completes it.
        await UseSiteAddressAsync();
        var definitionId = await CreateDefinitionAsync("Completion badge", _fixture.OtherCourseId);
        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, "credlearner1"));

        // Act: the enrollment completes by hand; the award runs as a deferred task when the scope ends.
        await _fixture.InTenantAsync(async services =>
        {
            var enrollments = services.GetRequiredService<IEnrollmentService>();
            var result = await enrollments.EnrollAsync(new EnrollmentRequest
            {
                UserId = userId,
                CourseId = _fixture.OtherCourseId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "credentials-test",
            });

            Assert.True(result.Succeeded, result.ErrorCode);

            var completed = await enrollments.ChangeStatusAsync(result.Enrollment.ItemId, EnrollmentStatus.Completed, "admin", "Finished in class");

            Assert.True(completed.Succeeded, completed.ErrorCode);
        });

        var awards = await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().ListForUserAsync(userId));

        // Assert
        var award = Assert.Single(awards);

        Assert.Equal(definitionId, award.DefinitionId);
        Assert.Equal(CredentialsConstants.RuleKinds.CourseCompleted, award.RuleKind);
    }

    [Fact]
    public async Task ThePublicEndpoints_ServeTheCredential_ItsJwt_TheAchievement_TheIssuer_AndTheKeys_SignedOut()
    {
        // Arrange
        var award = await AwardByHandAsync("credlearner2");
        using var http = await _fixture.CreateClientAsync();

        // Act
        var credential = await http.GetAsync($"learning/credentials/awards/{award.PublicId}", TestContext.Current.CancellationToken);
        var jwt = await http.GetAsync($"learning/credentials/awards/{award.PublicId}/jwt", TestContext.Current.CancellationToken);
        var token = await jwt.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = JsonNode.Parse(await credential.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var header = JsonNode.Parse(Base64Url.DecodeFromChars(token.Split('.')[0]));
        var kid = header["kid"].GetValue<string>();
        var key = await http.GetAsync(new Uri(kid).PathAndQuery.TrimStart('/'), TestContext.Current.CancellationToken);
        var keySet = JsonNode.Parse(await http.GetStringAsync("learning/credentials/jwks", TestContext.Current.CancellationToken));
        var achievement = await http.GetAsync(new Uri(json["credentialSubject"]["achievement"]["id"].GetValue<string>()).PathAndQuery.TrimStart('/'), TestContext.Current.CancellationToken);
        var issuer = await http.GetAsync(new Uri(json["issuer"]["id"].GetValue<string>()).PathAndQuery.TrimStart('/'), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, credential.StatusCode);
        Assert.Equal("application/ld+json", credential.Content.Headers.ContentType.MediaType);
        Assert.Equal("nosniff", string.Join(",", credential.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(HttpStatusCode.OK, jwt.StatusCode);
        Assert.Equal("application/jwt", jwt.Content.Headers.ContentType.MediaType);
        Assert.Equal("RS256", header["alg"].GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, key.StatusCode);
        Assert.True(Verifies(token, JsonNode.Parse(await key.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).AsObject()));
        Assert.Contains(keySet["keys"].AsArray(), item => item["kid"].GetValue<string>() == CredentialUrls.KeyIdOf(kid));
        Assert.DoesNotContain(keySet["keys"].AsArray(), item => item["d"] is not null);
        Assert.Equal(HttpStatusCode.OK, achievement.StatusCode);
        Assert.Contains("Forklift operator", await achievement.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, issuer.StatusCode);
        Assert.Contains("\"Profile\"", await issuer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.DoesNotContain("credlearner2@example.com", json.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ARevocation_ShowsInTheStatusList_AndOnTheVerificationPage_AtOnce()
    {
        // Arrange
        var award = await AwardByHandAsync("credlearner3");
        using var http = await _fixture.CreateClientAsync();
        var statusUrl = $"learning/credentials/status/{award.StatusListNumber}";

        // Act
        var pageBefore = await http.GetStringAsync($"learning/credentials/verify/{award.PublicId}", TestContext.Current.CancellationToken);
        var listBefore = JsonNode.Parse(await http.GetStringAsync(statusUrl, TestContext.Current.CancellationToken));
        await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(award.ItemId, "admin", "Awarded by mistake"));
        var listResponse = await http.GetAsync(statusUrl, TestContext.Current.CancellationToken);
        var listAfter = JsonNode.Parse(await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var pageAfter = await http.GetStringAsync($"learning/credentials/verify/{award.PublicId}", TestContext.Current.CancellationToken);
        var signedList = new HttpRequestMessage(HttpMethod.Get, statusUrl);
        signedList.Headers.Accept.ParseAdd("application/vc+jwt");
        var listJwt = await (await http.SendAsync(signedList, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-credential-status=\"Valid\"", pageBefore, StringComparison.Ordinal);
        Assert.False(BitstringStatusList.IsSet(listBefore["credentialSubject"]["encodedList"].GetValue<string>(), award.StatusListIndex));
        Assert.True(BitstringStatusList.IsSet(listAfter["credentialSubject"]["encodedList"].GetValue<string>(), award.StatusListIndex));
        Assert.Equal("no-store", listResponse.Headers.CacheControl.ToString());
        Assert.Contains("data-credential-status=\"Revoked\"", pageAfter, StringComparison.Ordinal);
        Assert.DoesNotContain("Awarded by mistake", pageAfter, StringComparison.Ordinal);
        Assert.Equal(3, listJwt.Split('.').Length);
    }

    [Fact]
    public async Task TheVerificationPage_ShowsTheRecipientsName_OnlyWhenTheyChoseTo()
    {
        // Arrange
        var award = await AwardByHandAsync("credlearner4");
        using var http = await _fixture.CreateClientAsync();

        // Act
        var hidden = await http.GetStringAsync($"learning/credentials/verify/{award.PublicId}", TestContext.Current.CancellationToken);
        await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().SetShowNameAsync(award.ItemId, award.RecipientUserId, true));
        var shown = await http.GetStringAsync($"learning/credentials/verify/{award.PublicId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("credlearner4", hidden, StringComparison.Ordinal);
        Assert.Contains("credlearner4", shown, StringComparison.Ordinal);
        Assert.Contains("Forklift operator", hidden, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCodes_AndListsThatDoNotExist_AreNotFound()
    {
        // Arrange
        using var http = await _fixture.CreateClientAsync();

        // Act
        var unknown = await http.GetAsync("learning/credentials/awards/0123456789abcdef0123456789abcdef", TestContext.Current.CancellationToken);
        var malformed = await http.GetAsync("learning/credentials/awards/not-a-code/jwt", TestContext.Current.CancellationToken);
        var page = await http.GetAsync("learning/credentials/verify/0123456789abcdef0123456789abcdef", TestContext.Current.CancellationToken);
        var list = await http.GetAsync("learning/credentials/status/9999", TestContext.Current.CancellationToken);
        var key = await http.GetAsync("learning/credentials/keys/no-such-key", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, key.StatusCode);
    }

    private async Task<CredentialAward> AwardByHandAsync(string userName)
    {
        await UseSiteAddressAsync();

        var definitionId = await CreateDefinitionAsync("Forklift operator", null);
        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, userName));
        var result = await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().AwardAsync(new CredentialAwardRequest
        {
            DefinitionId = definitionId,
            UserId = userId,
            RuleKind = CredentialsConstants.RuleKinds.Manual,
            Actor = "admin",
            Reason = "Practical test passed",
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Award;
    }

    private Task<string> CreateDefinitionAsync(string title, string courseId)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var definition = await contentManager.NewAsync(CredentialsConstants.ContentTypes.CredentialDefinition);

            definition.DisplayText = title;
            definition.Alter<TitlePart>(part => part.Title = title);
            definition.Alter<CredentialDefinitionPart>(part =>
            {
                part.Description = "Operates a forklift safely.";
                part.Criteria = "Completed the course.";
                part.AchievementType = "Badge";
                part.Rules = courseId is null ? [] : [new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.CourseCompleted, TargetId = courseId }];
            });
            await contentManager.CreateAsync(definition, VersionOptions.Published);

            return definition.ContentItemId;
        });

    // The deferred award and the tests' own scopes run without a request, so the site's base URL names the issuer.
    private Task UseSiteAddressAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var siteService = services.GetRequiredService<ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();
            var baseUrl = _fixture.BaseAddress.ToString().TrimEnd('/');

            if (site.BaseUrl != baseUrl)
            {
                site.BaseUrl = baseUrl;
                await siteService.UpdateSiteSettingsAsync(site);
            }
        });

    private static bool Verifies(string token, JsonObject jwk)
    {
        var parts = token.Split('.');

        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64Url.DecodeFromChars(jwk["n"].GetValue<string>()),
            Exponent = Base64Url.DecodeFromChars(jwk["e"].GetValue<string>()),
        });

        return rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
