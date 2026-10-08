using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Covers the rules of the one-time code and the delegated access session, through the broker.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class DelegatedAccessBrokerTests
{
    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessBrokerTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public DelegatedAccessBrokerTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Redeem_ValidCode_ReturnsTheParentUserAndTheGrantedRoles()
    {
        // Act
        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);

        // Assert
        Assert.Equal(TenantHierarchyFixture.Alice, redemption.ParentUserName);
        Assert.Equal("Firm A", redemption.ParentDisplayName);
        Assert.Equal("http://firma.localhost", redemption.ParentAddress);
        Assert.Equal(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmA).TenantId, redemption.ParentTenantId);
        Assert.Contains("Administrator", redemption.ChildRoles);
        Assert.False(string.IsNullOrEmpty(redemption.SessionId));
    }

    [Fact]
    public async Task Redeem_TheSameCodeTwice_FailsTheSecondTime()
    {
        // Arrange
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, verifier);

        // Act
        var first = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, verifier);
        var replay = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, verifier);

        // Assert
        Assert.NotNull(first);
        Assert.Null(replay);
    }

    [Fact]
    public async Task Redeem_TwoRedemptionsAtOnce_OnlyOneSucceeds()
    {
        // Arrange
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, verifier);

        // Act
        var results = await Task.WhenAll(
            DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, verifier),
            DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, verifier));

        // Assert
        Assert.Single(results, result => result is not null);
    }

    [Fact]
    public async Task Redeem_WrongVerifier_FailsAndBurnsTheCode()
    {
        // Arrange
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, verifier);

        // Act
        var wrong = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, DelegatedAccessTokens.CreateToken());
        var right = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, verifier);

        // Assert
        Assert.Null(wrong);
        Assert.Null(right);
    }

    [Fact]
    public async Task Redeem_CodeForOneChild_BySibling_Fails()
    {
        // Arrange
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, verifier);

        // Act
        var bySibling = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessTwo.TenantName, code, verifier);

        // Assert
        Assert.Null(bySibling);
    }

    [Fact]
    public async Task Redeem_CodeOfOneParent_ByChildOfAnotherParent_Fails()
    {
        // Arrange
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, verifier);

        // Act
        var byOtherFirmsChild = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessFour.TenantName, code, verifier);

        // Assert
        Assert.Null(byOtherFirmsChild);
    }

    [Fact]
    public async Task Redeem_ExpiredCode_Fails()
    {
        // Arrange
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, verifier);

        await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var codes = services.GetRequiredService<DelegatedAccessCodeStore>();
            var record = await codes.FindByHashAsync(DelegatedAccessTokens.Hash(code));
            record.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
            await codes.CreateAsync(record);
        });

        // Act
        var redemption = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, verifier);

        // Assert
        Assert.Null(redemption);
    }

    [Fact]
    public async Task IssueCode_ForAnotherParentsChild_ReturnsNothing()
    {
        // Act
        var (code, callback) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessFour.TenantId, DelegatedAccessTokens.CreateToken());

        // Assert
        Assert.Null(code);
        Assert.Null(callback);
    }

    [Fact]
    public async Task IssueCode_ForUserWithoutAGrant_ReturnsNothing()
    {
        // Act: Carol has no role, so the default grant for administrators does not cover her.
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Carol, _fixture.BusinessOne.TenantId, DelegatedAccessTokens.CreateToken());

        // Assert
        Assert.Null(code);
    }

    [Fact]
    public async Task IssueCode_CallbackComesFromTheChildsSettings()
    {
        // Act
        var (_, callback) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, DelegatedAccessTokens.CreateToken());

        // Assert
        Assert.StartsWith("http://business1.firma.localhost/delegated-access/callback?code=", callback, StringComparison.Ordinal);
        Assert.Contains("state=state-value", callback, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateSession_AfterTheGrantIsRemoved_IsInactive()
    {
        // Arrange: Carol gets a user grant for Business Two only.
        var granted = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<AccessGrantManager>().AddAsync(AccessGrantPrincipalType.User, TenantHierarchyFixture.Carol, _fixture.BusinessTwo.EntryId, ["Editor"]));
        Assert.True(granted.Succeeded, granted.Error);

        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Carol, _fixture.BusinessTwo);
        var before = await ValidateAsync(_fixture.BusinessTwo.TenantName, redemption.SessionId);

        // Act
        await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var grants = services.GetRequiredService<AccessGrantManager>();
            var grant = (await grants.ListAsync(_fixture.BusinessTwo.EntryId)).Single(candidate => candidate.PrincipalName == TenantHierarchyFixture.Carol);
            await grants.RemoveAsync(grant.GrantId);
        });

        var after = await ValidateAsync(_fixture.BusinessTwo.TenantName, redemption.SessionId);
        var again = await ValidateAsync(_fixture.BusinessTwo.TenantName, redemption.SessionId);

        // Assert
        Assert.True(before.IsActive);
        Assert.Equal(["Editor"], before.ChildRoles);
        Assert.False(after.IsActive);
        Assert.False(again.IsActive);
    }

    [Fact]
    public async Task ValidateSession_AfterTheParentUserChangedTheirSecurityStamp_IsInactive()
    {
        // Arrange
        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);

        // Act
        await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByNameAsync(TenantHierarchyFixture.Alice);
            await userManager.UpdateSecurityStampAsync(user);
        });

        var validation = await ValidateAsync(_fixture.BusinessOne.TenantName, redemption.SessionId);

        // Assert
        Assert.False(validation.IsActive);
    }

    [Fact]
    public async Task ValidateSession_InAnotherChild_IsInactive()
    {
        // Arrange
        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);

        // Act: Business Two presents Business One's session identifier.
        var validation = await ValidateAsync(_fixture.BusinessTwo.TenantName, redemption.SessionId);

        // Assert
        Assert.False(validation.IsActive);
    }

    [Fact]
    public async Task EndSessionsOfSignIn_EndsTheSessionsThatSignInStarted()
    {
        // Arrange
        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);
        var parentSessionId = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var session = await services.GetRequiredService<DelegatedAccessSessionStore>().FindByHashAsync(DelegatedAccessTokens.Hash(redemption.SessionId));

            return session.ParentSessionId;
        });

        // Act
        var ended = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<DelegatedAccessIssuer>().EndSessionsOfSignInAsync(parentSessionId));
        var validation = await ValidateAsync(_fixture.BusinessOne.TenantName, redemption.SessionId);

        // Assert
        Assert.False(string.IsNullOrEmpty(parentSessionId));
        Assert.True(ended >= 1);
        Assert.False(validation.IsActive);
    }

    [Fact]
    public async Task EndAllSessions_EndsEverySessionOfTheUser()
    {
        // Arrange
        var one = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);
        var two = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessTwo);
        var aliceId = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
            ((User)await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync(TenantHierarchyFixture.Alice)).UserId);

        // Act
        await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<DelegatedAccessIssuer>().EndAllSessionsAsync(aliceId));

        // Assert
        Assert.False((await ValidateAsync(_fixture.BusinessOne.TenantName, one.SessionId)).IsActive);
        Assert.False((await ValidateAsync(_fixture.BusinessTwo.TenantName, two.SessionId)).IsActive);
    }

    [Fact]
    public async Task Redeem_RecordsTheEntryInTheParentsActivityLog()
    {
        // Arrange
        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);
        var reference = DelegatedAccessTokens.GetReference(DelegatedAccessTokens.Hash(redemption.SessionId));

        // Act
        var events = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<HierarchyAuditLog>().PageAsync(_fixture.BusinessOne.EntryId, 0, 200));

        // Assert
        Assert.Contains(events, auditEvent => auditEvent.Name == HierarchyAuditEventNames.Entered &&
            auditEvent.UserName == TenantHierarchyFixture.Alice &&
            auditEvent.SessionReference == reference);
    }

    private Task<DelegatedSessionValidation> ValidateAsync(string child, string sessionId)
        => _fixture.Host.InTenantAsync(child, services => services.GetRequiredService<ITenantHierarchyBroker>().ValidateSessionAsync(sessionId));
}
