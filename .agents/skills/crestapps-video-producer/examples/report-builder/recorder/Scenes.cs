using Microsoft.Playwright;

namespace Recorder;

/// <summary>
/// The clips of the Report Builder video, one method per "clip" of storyboard.json.
/// </summary>
public static partial class Scenes
{
    public static string BaseUrl { get; } = Environment.GetEnvironmentVariable("VIDEO_BASE_URL") ?? "http://localhost:5330";

    public static string UserName { get; } = Environment.GetEnvironmentVariable("VIDEO_USER_NAME") ?? "admin";

    public static string Password { get; } = Environment.GetEnvironmentVariable("VIDEO_PASSWORD") ?? "Password1!";

    public static string Root { get; } = Environment.GetEnvironmentVariable("VIDEO_ROOT") ?? throw new InvalidOperationException("Set VIDEO_ROOT to the workspace.");

    public static string StatePath => Path.Combine(Root, "state.json");

    private static readonly (string Name, Func<IBrowser, string, IReadOnlyDictionary<string, double>, Task> Record)[] s_clips =
    [
        ("03-start", StartAsync),
        ("04-build", BuildAsync),
        ("05-columns", ColumnsAsync),
        ("06-filters", FiltersAsync),
        ("07-visuals", VisualsAsync),
        ("08-joins", JoinsAsync),
        ("09-kinds", KindsAsync),
        ("10-sources", SourcesAsync),
        ("11-publish", PublishAsync),
        ("12-views", ViewsAsync),
        ("13-share", ShareAsync),
    ];

    public static async Task RecordAsync(IBrowser browser, string outputRoot, IReadOnlyDictionary<string, double> durations, string[] names)
    {
        var selected = names is ["all"] ? s_clips : s_clips.Where(clip => names.Contains(clip.Name)).ToArray();

        foreach (var (name, record) in selected)
        {
            Console.WriteLine($"Recording {name}");
            await record(browser, Path.Combine(outputRoot, name), durations);
        }
    }

    private static Task<Scene> NewSceneAsync(IBrowser browser, string directory, IReadOnlyDictionary<string, double> durations)
        => Scene.StartAsync(browser, Path.GetFileName(directory), BaseUrl, StatePath, Path.GetDirectoryName(directory), durations);

    // The builder.
    private static ILocator Tab(Scene scene, string name)
        => scene.Page.Locator("[data-report-designer] .nav-link").Filter(new() { HasTextString = name });

    private static ILocator Field(Scene scene, string key)
        => scene.Page.Locator($".report-designer-field[data-field-key=\"{key}\"]");

    private static ILocator Shelf(Scene scene, string kind)
        => scene.Page.Locator($".report-designer-shelf-zone[data-shelf={kind}]");

    private static ILocator Pill(Scene scene, string text)
        => scene.Page.Locator(".report-designer-pill").Filter(new() { HasTextString = text }).First;

    private static ILocator Property(Scene scene, string label)
        => scene.Page.Locator(".report-designer-properties .mb-2").Filter(new() { Has = scene.Page.Locator("label", new() { HasTextString = label }) }).First;

    private static ILocator VisualProperty(Scene scene, string label)
        => scene.Page.Locator(".report-designer-visuals .mb-2, .report-designer-visuals .rd-well").Filter(new() { Has = scene.Page.Locator("label, .small", new() { HasTextString = label }) }).First;

    private static ILocator Check(Scene scene, string container, string label)
        => scene.Page.Locator($"{container} .form-check").Filter(new() { HasTextString = label }).First.Locator("input");

    // Opens the builder on a report, before the capture starts.
    private static async Task OpenReportAsync(Scene scene, string title)
    {
        await scene.OpenAsync("/Admin/reports/designs");
        var item = scene.Page.Locator(".list-group-item").Filter(new() { HasTextString = title }).First;
        var href = await item.Locator("a").Filter(new() { HasTextString = "Edit" }).First.GetAttributeAsync("href");
        await scene.OpenAsync(href);
        await CompactMenuAsync(scene);
        await scene.PauseAsync(1500);
    }

    private static async Task<string> RunUrlAsync(Scene scene, string title)
    {
        await scene.OpenAsync("/Admin/reports/designs");
        var item = scene.Page.Locator(".list-group-item").Filter(new() { HasTextString = title }).First;

        return await item.Locator("a").Filter(new() { HasTextString = "Run" }).First.GetAttributeAsync("href");
    }

    private static ILocator Button(Scene scene, string name)
        => scene.Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).First;

    // Collapses the admin menu, so the builder has the whole width.
    private static async Task CompactMenuAsync(Scene scene)
    {
        await scene.Page.EvaluateAsync("() => { if (!document.body.classList.contains('left-sidebar-compact')) document.querySelector('.leftbar-compactor')?.click(); }");
        await Task.Delay(400);
    }

    private static async Task AddDataSetAsync(Scene scene, string source, string dataSet)
    {
        await scene.ClickAsync(Button(scene, "Add data set"), pauseAfter: 700);
        var modal = scene.Page.Locator(".modal.show");
        await scene.ClickAsync(modal.Locator(".nav-link").Filter(new() { HasTextString = source }), pauseAfter: 600);
        var card = modal.Locator(".card").Filter(new() { Has = scene.Page.Locator(".card-title", new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^\\s*{dataSet}\\s*$") }) }).First;
        await scene.MoveToAsync(card);
        await scene.PauseAsync(300);
        await scene.ClickAsync(card.Locator(".card-footer button").First, pauseAfter: 1500);
    }

    private static async Task StartAsync(IBrowser browser, string directory, IReadOnlyDictionary<string, double> durations)
    {
        await using var scene = await NewSceneAsync(browser, directory, durations);
        await scene.OpenAsync("/Admin/Features");
        await scene.StartCaptureAsync();

        ILocator Row(string feature)
            => scene.Page.Locator($"#btn-enable-{feature}, #btn-disable-{feature}").Locator("xpath=ancestor::*[contains(concat(' ', normalize-space(@class), ' '), ' list-group-item ')][1]").First;

        await scene.StepAsync("start-1", async () =>
        {
            await scene.TypeAsync(scene.Page.Locator("#search-box"), "report", delay: 90);
            await scene.PauseAsync(500);
            await scene.MoveToAsync(Row("CrestApps_OrchardCore_Reports_Builder"), -300);
            await scene.HighlightAsync(Row("CrestApps_OrchardCore_Reports_Builder"), "Report Builder");
            await scene.PauseAsync(2600);
            await scene.ClearHighlightsAsync();
            await scene.MoveToAsync(Row("CrestApps_OrchardCore_Reports_OpenXml"), -300);
            await scene.HighlightAsync(Row("CrestApps_OrchardCore_Reports_OpenXml"), "Excel exports");
        });

        await scene.ClearHighlightsAsync();

        await scene.StepAsync("start-2", async () =>
        {
            await scene.NavigateAsync("/Admin/reports/designs");
            var reports = scene.Page.Locator("button.nav-group-label.reports").First;
            var expanded = await scene.Page.Locator("a[href='/Admin/reports/designs']").Filter(new() { Visible = true }).CountAsync() > 0;

            if (!expanded)
            {
                await scene.ClickAsync(reports, pauseAfter: 700);
            }
            var menu = scene.Page.Locator("a[href='/Admin/reports/designs']").Filter(new() { Visible = true }).First;
            await scene.MoveToAsync(menu);
            await scene.HighlightAsync(menu);
            await scene.PauseAsync(2500);
            await scene.ClearHighlightsAsync();
            var status = scene.Page.Locator("form .bootstrap-select, form select").Filter(new() { Visible = true }).First;
            await scene.MoveToAsync(status);
            await scene.HighlightAsync(status, "Published or drafts");
        });

        await scene.ClearHighlightsAsync();

        await scene.StepAsync("start-3", async () =>
        {
            var item = scene.Page.Locator(".list-group-item").Filter(new() { HasTextString = "Activity outcomes by agent" }).First;
            await scene.HighlightAsync(item.Locator("a, button").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("^(Run|Edit|Delete)$") }).First);
            await scene.MoveToAsync(item.Locator("a").Filter(new() { HasTextString = "Run" }).First);
            await scene.PauseAsync(1800);
            await scene.ClearHighlightsAsync();
            await scene.ClickAsync(item.Locator(".dropdown-toggle.actions"), pauseAfter: 600);
            var clone = item.Locator(".dropdown-menu .dropdown-item").Filter(new() { HasTextString = "Clone" });
            await scene.MoveToAsync(clone);
            await scene.HighlightAsync(clone, "Your own copy");
        });

        await scene.ClearHighlightsAsync();
    }

    private static async Task BuildAsync(IBrowser browser, string directory, IReadOnlyDictionary<string, double> durations)
    {
        await using var scene = await NewSceneAsync(browser, directory, durations);
        await scene.OpenAsync("/Admin/reports/designs");
        await CompactMenuAsync(scene);
        await scene.StartCaptureAsync();

        await scene.StepAsync("build-1", async () =>
        {
            await scene.ClickAndWaitForNavigationAsync(scene.Page.Locator("a").Filter(new() { HasTextString = "New Report" }).First);
            await scene.PauseAsync(800);
            await scene.HighlightAsync(scene.Page.Locator("[data-report-designer] .nav").First);
        });

        await scene.ClearHighlightsAsync();

        await scene.StepAsync("build-2", async () =>
        {
            await scene.ClickAsync(Tab(scene, "Settings"), pauseAfter: 500);
            var settings = scene.Page.Locator(".report-designer-settings");
            await scene.TypeAsync(settings.Locator("input[type=text]").First, "Activities by channel", delay: 60);
            await scene.TypeAsync(settings.Locator("textarea").First, "How many activities each channel handles, and how they end.", delay: 25);
            await scene.TypeAsync(settings.Locator("input[type=text]").Nth(1), "Operations", delay: 60);
            await scene.HighlightAsync(settings.Locator(".form-check").First);
            await scene.HighlightAsync(settings.Locator(".form-check").Nth(1));
        });

        await scene.ClearHighlightsAsync();

        await scene.StepAsync("build-3", async () =>
        {
            await scene.ClickAsync(Tab(scene, "Design"), pauseAfter: 400);
            await AddDataSetAsync(scene, "Omnichannel", "Activities");
        });

        await scene.StepAsync("build-4", async () =>
        {
            await scene.DragToAsync(Field(scene, "Activities.Channel"), Shelf(scene, "columns"));
            await scene.PauseAsync(1500);
            await scene.DragToAsync(Field(scene, "Activities.Status"), Shelf(scene, "columns"), 120);
        });

        await scene.StepAsync("build-5", async () =>
        {
            await scene.Page.Locator(".report-designer-data").Locator("input[type=search], input").First.FillAsync("");
            await scene.DragToAsync(Field(scene, "$count"), Shelf(scene, "columns"), 220);
            await scene.PauseAsync(1200);
            var sort = scene.Page.Locator("select[aria-label='Add sort']");
            await scene.SelectAsync(sort, "Number of rows", pauseAfter: 900);
            await scene.ClickAsync(scene.Page.Locator(".report-designer-shelves button[title='Change direction']").First, pauseAfter: 900);
            await scene.HighlightAsync(scene.Page.Locator(".report-designer-preview table").First);
        });

        await scene.ClearHighlightsAsync();

        await scene.StepAsync("build-6", async () =>
        {
            await scene.ClickAsync(Pill(scene, "Created").Locator("button").First, pauseAfter: 600);
            await scene.HighlightAsync(Property(scene, "Default period"));
            await scene.PauseAsync(2500);
            await scene.SelectAsync(Property(scene, "Default period").Locator("select"), "365");
        });

        await scene.ClearHighlightsAsync();
    }
}
