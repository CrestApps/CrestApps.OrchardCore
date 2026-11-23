using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// A verification link names its member, its nonce and its end. It verifies only before its end and only with the
/// member's current nonce; a second click on a verified member says "already confirmed", and a member who left is never
/// brought back by an old link.
/// </summary>
public sealed class MailingListVerificationTokensTests
{
    private static readonly DateTime _now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Format_ThenParse_GivesBackTheMemberNonceAndEnd()
    {
        // Arrange
        var value = MailingListVerificationTokens.Format("member1", "nonce1", _now.AddDays(7));

        // Act
        var ticket = MailingListVerificationTokens.Parse(value);

        // Assert
        Assert.Equal(new MailingListVerificationTicket("member1", "nonce1", _now.AddDays(7)), ticket);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("member1:nonce1")]
    [InlineData("member1:nonce1:notanumber")]
    [InlineData(":nonce1:1")]
    [InlineData("member1::1")]
    public void Parse_AValueThatIsNotALink_GivesNothing(string value)
    {
        // Act
        var ticket = MailingListVerificationTokens.Parse(value);

        // Assert
        Assert.Null(ticket);
    }

    [Fact]
    public void Read_ATokenOfThisSite_GivesItsTicket_AndATamperedOneGivesNothing()
    {
        // Arrange
        var clock = new MailingListTestClock { UtcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) };
        var tokens = SignedLinkDoubles.Tokens(clock: clock);
        var token = MailingListVerificationTokens.Create(tokens, "member1", "nonce1", clock.UtcNow, TimeSpan.FromDays(7));

        // Act
        var ticket = MailingListVerificationTokens.Read(tokens, token);
        var tampered = MailingListVerificationTokens.Read(tokens, token + "x");

        // Assert
        Assert.Equal("member1", ticket?.MemberId);
        Assert.Equal("nonce1", ticket?.Nonce);
        Assert.Null(tampered);
    }

    [Fact]
    public void Evaluate_APendingMemberWithTheSameNonceBeforeTheEnd_IsReady()
    {
        // Arrange
        var member = Member(MailingListMemberStatus.Pending, "nonce1");

        // Act
        var outcome = MailingListVerification.Evaluate(member, Ticket("nonce1", _now.AddDays(1)), _now);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.Ready, outcome);
    }

    [Fact]
    public void Evaluate_AnExpiredLink_IsExpired()
    {
        // Arrange
        var member = Member(MailingListMemberStatus.Pending, "nonce1");

        // Act
        var outcome = MailingListVerification.Evaluate(member, Ticket("nonce1", _now.AddMinutes(-1)), _now);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.Expired, outcome);
    }

    [Fact]
    public void Evaluate_ALinkWhoseNonceWasRotatedByANewerLink_IsExpired()
    {
        // Arrange
        var member = Member(MailingListMemberStatus.Pending, "nonce2");

        // Act
        var outcome = MailingListVerification.Evaluate(member, Ticket("nonce1", _now.AddDays(1)), _now);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.Expired, outcome);
    }

    [Fact]
    public void Evaluate_ASecondClickOnAVerifiedMember_IsAlreadyVerified_EvenAfterTheEnd()
    {
        // Arrange
        var member = Member(MailingListMemberStatus.Subscribed, nonce: null);
        member.EmailVerifiedUtc = _now.AddDays(-1);

        // Act
        var outcome = MailingListVerification.Evaluate(member, Ticket("nonce1", _now.AddDays(-2)), _now);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.AlreadyVerified, outcome);
    }

    [Theory]
    [InlineData(MailingListMemberStatus.Unsubscribed)]
    [InlineData(MailingListMemberStatus.Cleaned)]
    public void Evaluate_AMemberWhoLeft_IsNeverBroughtBackByAnOldLink(MailingListMemberStatus status)
    {
        // Arrange
        var member = Member(status, "nonce1");
        member.EmailVerifiedUtc = _now.AddDays(-10);

        // Act
        var outcome = MailingListVerification.Evaluate(member, Ticket("nonce1", _now.AddDays(1)), _now);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.Expired, outcome);
    }

    [Fact]
    public void Evaluate_ATicketOfAnotherMember_IsInvalid()
    {
        // Arrange
        var member = Member(MailingListMemberStatus.Pending, "nonce1");

        // Act
        var outcome = MailingListVerification.Evaluate(member, new MailingListVerificationTicket("member2", "nonce1", _now.AddDays(1)), _now);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.Invalid, outcome);
    }

    private static MailingListMember Member(MailingListMemberStatus status, string nonce)
        => new()
        {
            ItemId = "member1",
            ListId = "list1",
            NormalizedEmail = "ada@example.com",
            Status = status,
            VerificationNonce = nonce,
        };

    private static MailingListVerificationTicket Ticket(string nonce, DateTime expiresUtc)
        => new("member1", nonce, expiresUtc);
}
