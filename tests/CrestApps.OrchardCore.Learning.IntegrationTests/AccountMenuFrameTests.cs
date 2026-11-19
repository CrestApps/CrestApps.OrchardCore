using System.Net;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The guard of "Your account": every page in its menu renders inside the account frame (the CommerceAccountPage
/// shape), with the account menu beside it. It walks the menu as an account admin who is a learner, so the Learning,
/// Accounts and Users pages are all in it, and fails for a page that renders without the frame.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class AccountMenuFrameTests
{
    private readonly LearningSiteFixture _fixture;

    public AccountMenuFrameTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EveryPageInTheAccountMenu_RendersInsideTheAccountFrame()
    {
        // Arrange: an account admin who is a learner sees every page the enabled modules add to the menu.
        var (adminName, _) = await AccountPagesTests.CreateAccountAsync(_fixture);
        using var client = await _fixture.CreateClientAsync(adminName);
        var links = await AccountPagesTests.MenuLinksAsync(client);

        // Act
        var unframed = new List<string>();

        foreach (var link in links)
        {
            var response = await client.GetAsync(link.TrimStart('/'), TestContext.Current.CancellationToken);
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            if (response.StatusCode != HttpStatusCode.OK || !html.Contains("class=\"commerce-account-nav\"", StringComparison.Ordinal))
            {
                unframed.Add($"{link} ({(int)response.StatusCode})");
            }
        }

        // Assert: the Learning, Accounts and Users pages are in the menu, so the walk covered them, and none was unframed.
        string[] expected = ["/learning", "/learning/transcript", "/learning/credentials", "/learning/certifications", "/learning/credits", "/learning/required", "/notifications", "/account/learning-seats", "/account/members"];

        Assert.Empty(expected.Except(links));
        Assert.Empty(unframed);
    }
}
