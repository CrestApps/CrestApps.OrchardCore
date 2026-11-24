using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Accounts.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The pages of "Your account" agree with its menu: a link is shown only to people its page lets in. An account admin
/// reaches Course seats and Account members, a plain member reaches Account members only, and the site's admin, who
/// is in no account, reaches neither and is not offered them.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed partial class AccountPagesTests
{
    private const string SeatsPath = "/account/learning-seats";
    private const string MembersPath = "/account/members";

    private readonly LearningSiteFixture _fixture;

    public AccountPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnAccountAdmin_SeesCourseSeatsAndAccountMembers_AndOpensBoth()
    {
        // Arrange
        var (adminName, _) = await CreateAccountAsync(_fixture);
        using var client = await _fixture.CreateClientAsync(adminName);

        // Act
        var menu = await MenuLinksAsync(client);
        var seats = await client.GetAsync(SeatsPath, TestContext.Current.CancellationToken);
        var members = await client.GetAsync(MembersPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(SeatsPath, menu);
        Assert.Contains(MembersPath, menu);
        Assert.Equal(HttpStatusCode.OK, seats.StatusCode);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
    }

    [Fact]
    public async Task APlainMember_SeesAccountMembersOnly_AndIsNotOfferedCourseSeats()
    {
        // Arrange
        var (_, memberName) = await CreateAccountAsync(_fixture);
        using var client = await _fixture.CreateClientAsync(memberName);

        // Act
        var menu = await MenuLinksAsync(client);
        var seats = await client.GetAsync(SeatsPath, TestContext.Current.CancellationToken);
        var members = await client.GetAsync(MembersPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(SeatsPath, menu);
        Assert.NotEqual(HttpStatusCode.OK, seats.StatusCode);
        Assert.Contains(MembersPath, menu);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
    }

    [Fact]
    public async Task TheSiteAdmin_InNoAccount_IsNotOfferedTheAccountPages()
    {
        // Arrange
        using var client = await _fixture.CreateClientAsync("admin");

        // Act
        var menu = await MenuLinksAsync(client);
        var seats = await client.GetAsync(SeatsPath, TestContext.Current.CancellationToken);
        var members = await client.GetAsync(MembersPath, TestContext.Current.CancellationToken);

        // Assert: neither link is offered, and neither page lets them in.
        Assert.DoesNotContain(SeatsPath, menu);
        Assert.DoesNotContain(MembersPath, menu);
        Assert.NotEqual(HttpStatusCode.OK, seats.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, members.StatusCode);
    }

    // The links of the account menu, read from the account's home page.
    internal static async Task<IReadOnlyList<string>> MenuLinksAsync(HttpClient client)
    {
        var response = await client.GetAsync("account", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var nav = NavPattern().Match(html);

        Assert.True(nav.Success, "The account page has no account menu.");

        return [.. HrefPattern().Matches(nav.Value).Select(match => WebUtility.HtmlDecode(match.Groups["href"].Value)).Distinct(StringComparer.Ordinal)];
    }

    // An account of this test only, with an admin and a plain member who are both learners.
    internal static async Task<(string AdminName, string MemberName)> CreateAccountAsync(LearningSiteFixture fixture)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminName = "acctadmin" + suffix;
        var memberName = "acctmember" + suffix;
        var adminId = await fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, adminName));
        var memberId = await fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, memberName));

        await fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var account = await contentManager.NewAsync("Account");

            account.DisplayText = "Account " + suffix;
            account.Apply(new AccountMembersPart
            {
                Members =
                [
                    new AccountMember { UserId = adminId, UserName = adminName, Email = adminName + "@example.com", Role = AccountMemberRole.Admin },
                    new AccountMember { UserId = memberId, UserName = memberName, Email = memberName + "@example.com", Role = AccountMemberRole.Member },
                ],
            });

            await contentManager.CreateAsync(account, VersionOptions.Published);
        });

        return (adminName, memberName);
    }

    [GeneratedRegex("<nav class=\"commerce-account-nav\"[\\s\\S]*?</nav>")]
    private static partial Regex NavPattern();

    [GeneratedRegex("href=\"(?<href>[^\"]+)\"")]
    private static partial Regex HrefPattern();
}
