using System.Net;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The tutor template on the New AI Profile picker, the tutor switch on the AI profile editor and the learning
/// assistant switch on the course editor, off by default.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningTutorPagesTests
{
    private readonly LearningSiteFixture _site;

    public LearningTutorPagesTests(LearningSiteFixture site)
    {
        _site = site;
    }

    [Fact]
    public async Task NewProfilePicker_ListsTheLearningTutorTemplate()
    {
        using var admin = await _site.CreateClientAsync("admin");

        // "New AI profile" opens the profile list with the picker; the tutor's card leads to its setup step.
        var (listStatus, listHtml) = await GetFollowingAsync(admin, "Admin/ai/profile/new");
        var setup = await admin.GetAsync("Admin/ai/profile/new/learning-tutor", TestContext.Current.CancellationToken);
        var setupHtml = await setup.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(listStatus == HttpStatusCode.OK, $"The profile list answered {listStatus}: {listHtml[..Math.Min(300, listHtml.Length)]}");
        Assert.Contains("Learning tutor", listHtml, StringComparison.Ordinal);
        Assert.True(setup.StatusCode == HttpStatusCode.OK, $"The setup step answered {setup.StatusCode} to {setup.Headers.Location}.");
        Assert.Contains("Learning tutor", setupHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CourseEditor_ShowsTheLearningAssistantSwitch_OffByDefault()
    {
        using var admin = await _site.CreateClientAsync("admin");

        var response = await admin.GetAsync($"Admin/Contents/ContentItems/{_site.OtherCourseId}/Edit", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("LearningAssistantPart.Enabled", html, StringComparison.Ordinal);
        Assert.Contains("Let the learning tutor answer this course", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("name=\"LearningAssistantPart.Enabled\"[^>]*checked", html);
    }

    private static async Task<(HttpStatusCode Status, string Html)> GetFollowingAsync(HttpClient client, string url)
    {
        var target = new Uri(url, UriKind.Relative);

        for (var hop = 0; hop < 5; hop++)
        {
            var response = await client.GetAsync(target, TestContext.Current.CancellationToken);

            if (response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently) || response.Headers.Location is null)
            {
                return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            }

            target = response.Headers.Location;
        }

        return (HttpStatusCode.LoopDetected, string.Empty);
    }
}