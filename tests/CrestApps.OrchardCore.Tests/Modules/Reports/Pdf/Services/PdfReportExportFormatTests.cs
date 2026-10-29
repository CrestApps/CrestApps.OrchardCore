using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Pdf.Services;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.Extensions.Localization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Pdf.Services;

public sealed class PdfReportExportFormatTests
{
    [Fact]
    public void Serialize_WhenDocumentHasEverySectionKind_ShouldWriteTheirTextInOrder()
    {
        // Arrange
        var document = new ReportDocument
        {
            Title = "Agent performance",
        }
            .Add(ReportSection.ForMetrics("Summary", [new ReportMetric("Open conversations", "42", "Current period")]))
            .Add(ReportSection.ForTable("Queues", [new ReportColumn("Queue"), new ReportColumn("Count", ReportColumnAlign.End)], [new ReportRow(["Support", "18"]), new ReportRow(["Total", "18"], ReportRowKind.GrandTotal)]))
            .Add(ReportSection.ForBars("Channel mix", [new ReportBar("Voice", "12", 0.6), new ReportBar("Email", "8", 0.4)]))
            .Add(ReportSection.ForChart("Daily trend", new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = ["Monday", "Tuesday"],
                Datasets =
                [
                    new ReportChartDataset("Offered", [12, 18]),
                    new ReportChartDataset("Answered", [10, 15]),
                ],
            }));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var text = string.Join(' ', pdf.GetPages().Select(GetText));

        Assert.Equal("Agent performance", pdf.Information.Title);
        AssertInOrder(text, "Agent performance", "Summary", "OPEN CONVERSATIONS", "42", "Current period", "Queues", "Queue", "Count", "Support", "Total", "Channel mix", "Voice", "12", "Email", "Daily trend");
        Assert.Contains("Offered", text);
        Assert.Contains("Answered", text);
        Assert.Contains("Monday", text);
    }

    [Fact]
    public void Serialize_WhenSectionsAreInContainersAndFilteredByPicks_ShouldWriteContainerTitlesAndWhatFiltersEachSection()
    {
        // Arrange
        var authors = ReportSection.ForTable("Authors", [new ReportColumn("Author")], [new ReportRow(["Alice"]) { Key = "alice" }]);
        authors.Id = "v1";
        authors.ContainerId = "k1";
        authors.Selection = new ReportSectionSelection { TargetIds = ["v3"], SelectedKeys = ["alice"], SelectedLabels = ["Alice"] };

        var notes = ReportSection.ForMetrics("Notes", [new ReportMetric("Drafts", "2")]);
        notes.Id = "v2";

        var posts = ReportSection.ForTable("Posts", [new ReportColumn("Title")], [new ReportRow(["Post 1"])]);
        posts.Id = "v3";
        posts.ContainerId = "k1";

        var document = new ReportDocument
        {
            Title = "Posts dashboard",
            Containers = [new ReportContainer { Id = "k1", Title = "Author scorecard" }],
        }
            .Add(authors)
            .Add(notes)
            .Add(posts);

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var text = string.Join(' ', pdf.GetPages().Select(GetText));

        AssertInOrder(text, "Posts dashboard", "Author scorecard", "Authors", "Alice", "Posts", "Filtered by Authors: Alice", "Post 1", "Notes", "DRAFTS");
    }

    [Fact]
    public void Serialize_WhenAChartShowsSmallWholeNumbers_ShouldLabelItsScaleWithoutRepeats()
    {
        // Arrange
        var document = new ReportDocument { Title = "Chart" }
            .Add(ReportSection.ForChart("Trend", new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = ["Monday", "Tuesday"],
                Datasets = [new ReportChartDataset("Posts", [3, 3])],
            }));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert: the numbers above the footer are the scale's.
        using var pdf = PdfDocument.Open(content);
        var numbers = pdf.GetPage(1).GetWords()
            .Where(word => word.BoundingBox.Bottom > 60)
            .Select(word => word.Text)
            .Where(text => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .ToList();

        Assert.Equal(["0", "1", "2", "3"], numbers.Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public void Serialize_WhenTableSpansPages_ShouldRepeatItsHeaderAndNumberEveryPage()
    {
        // Arrange
        var rows = Enumerable.Range(1, 200).Select(index => new ReportRow([$"Agent{index}", index.ToString(CultureInfo.InvariantCulture)]));
        var document = new ReportDocument { Title = "Agents" }
            .Add(ReportSection.ForTable("Agents", [new ReportColumn("AgentName"), new ReportColumn("Handled")], rows));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var pages = pdf.GetPages().ToList();

        Assert.True(pages.Count > 1);

        for (var index = 0; index < pages.Count; index++)
        {
            var text = GetText(pages[index]);

            Assert.Contains("AgentName", text);
            Assert.Contains($"Page {index + 1} of {pages.Count}", text);
        }

        Assert.Contains("Agent200", GetText(pages[^1]));
    }

    [Fact]
    public void Serialize_WhenPageNumbersAreTranslated_ShouldWriteTheFieldsWhereTheTranslationPutsThem()
    {
        // Arrange
        var document = new ReportDocument { Title = "Agents" }
            .Add(ReportSection.ForMetrics("Summary", [new ReportMetric("Calls", "8")]));
        var format = new PdfReportExportFormat(new TranslatingLocalizer("{1} pages, this is {0}"));

        // Act
        var content = format.Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);

        Assert.Contains("1 pages, this is 1", GetText(pdf.GetPage(1)));
    }

    [Fact]
    public void Serialize_WhenATableIsWide_ShouldLayThePagesOutInLandscape()
    {
        // Arrange
        var columns = Enumerable.Range(1, 9).Select(index => new ReportColumn($"Column {index}")).ToList();
        var cells = Enumerable.Range(1, 9).Select(index => $"A fairly long value {index}").ToList();
        var document = new ReportDocument { Title = "Wide" }
            .Add(ReportSection.ForTable("Wide", columns, [new ReportRow(cells)]));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var page = pdf.GetPage(1);

        Assert.Equal(792, page.Width, 0.5);
        Assert.Equal(612, page.Height, 0.5);
    }

    [Fact]
    public void Serialize_WhenTablesAreNarrow_ShouldLayThePagesOutInPortrait()
    {
        // Arrange
        var document = new ReportDocument { Title = "Narrow" }
            .Add(ReportSection.ForTable("Narrow", [new ReportColumn("Queue"), new ReportColumn("Count")], [new ReportRow(["Support", "18"])]));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var page = pdf.GetPage(1);

        Assert.True(page.Height > page.Width);
        Assert.Equal(612, page.Width, 0.5);
        Assert.Equal(792, page.Height, 0.5);
    }

    [Fact]
    public void Serialize_WhenValuesAreLongOrUnbroken_ShouldKeepEveryWordInsideThePageAndOffItsNeighbors()
    {
        // Arrange
        var columns = new[] { "Interaction ID", "Started", "Channel", "Queue", "Agent", "Contact", "Phone number", "Disposition", "Notes" }
            .Select(label => new ReportColumn(label))
            .ToList();
        var rows = Enumerable.Range(1, 30).Select(index => new ReportRow(
        [
            $"01JABCDEF{index:0000}XYZ0123456",
            "2026-10-02 09:01",
            "Voice",
            "Support - Tier 1",
            "Sophia Miller",
            $"Contact person {index}",
            $"+1 702 555 {index:0000}",
            "Callback requested",
            index % 3 == 0 ? new string('x', 90) : "Customer asked about the renewal of their plan and a follow-up email was sent.",
        ]));
        var document = new ReportDocument { Title = "Interaction log" }
            .Add(ReportSection.ForTable("Interaction log", columns, rows));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);

        foreach (var page in pdf.GetPages())
        {
            AssertWordsInsideThePageAndApart(page);
        }
    }

    [Theory]
    [InlineData(ReportChartType.Line, false)]
    [InlineData(ReportChartType.Bar, false)]
    [InlineData(ReportChartType.Bar, true)]
    [InlineData(ReportChartType.HorizontalBar, false)]
    [InlineData(ReportChartType.HorizontalBar, true)]
    [InlineData(ReportChartType.Area, false)]
    [InlineData(ReportChartType.Pie, false)]
    [InlineData(ReportChartType.Doughnut, false)]
    public void Serialize_WhenSectionIsAChart_ShouldDrawItsCategories(ReportChartType type, bool stacked)
    {
        // Arrange
        var document = new ReportDocument { Title = "Chart" }
            .Add(ReportSection.ForChart("Volume", new ReportChart
            {
                Type = type,
                Stacked = stacked,
                PercentageScale = type == ReportChartType.Line,
                Labels = ["Voice", "Email", "Chat"],
                Datasets =
                [
                    new ReportChartDataset("Inbound", [12, double.NaN, 30]),
                    new ReportChartDataset("Outbound", [4, 9]),
                ],
            }));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var text = GetText(pdf.GetPage(1));

        Assert.Contains("Volume", text);
        Assert.Contains("Voice", text);
        Assert.Contains("Chat", text);
    }

    [Fact]
    public void Serialize_WhenCellsAreStyledOrHoldControlCharacters_ShouldWriteTheirValues()
    {
        // Arrange
        var row = new ReportRow(["Line one\r\nLine two", "Tab\there", "Bell\u0007"])
            .WithStyle(ReportStyle.Create("#2563EB", "#EFF6FF", bold: true))
            .WithCellStyle(1, ReportStyle.Create("red", "not-a-color"));
        var document = new ReportDocument { Title = "Styled" }
            .Add(ReportSection.ForTable(
                "Styled",
                [new ReportColumn("First", headerStyle: ReportStyle.Create(backgroundColor: "#FDE68A")), new ReportColumn("Second", ReportColumnAlign.Center), new ReportColumn("Third", ReportColumnAlign.End)],
                [row, new ReportRow(["Subtotal", "1", "2"], ReportRowKind.Subtotal), new ReportRow(["Short row"])]));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var text = GetText(pdf.GetPage(1));

        // The second line of the first cell is read after the other cells of the first line.
        AssertInOrder(text, "First", "Second", "Third", "Line", "one", "Tab", "here", "Bell", "Line", "two", "Subtotal", "Short");
    }

    [Fact]
    public void Serialize_WhenDocumentIsEmpty_ShouldWriteOnePage()
    {
        // Arrange
        var document = new ReportDocument();

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);

        Assert.Equal(1, pdf.NumberOfPages);
    }

    [Fact]
    public void Serialize_WhenSectionsHaveNothingToShow_ShouldSkipThemWithoutFailing()
    {
        // Arrange
        var document = new ReportDocument { Title = "Nothing" }
            .Add(ReportSection.ForMetrics("No metrics", []))
            .Add(ReportSection.ForTable("No table", [], []))
            .Add(ReportSection.ForBars("No bars", [new ReportBar("Zero", "0", 0), new ReportBar("Full", "10", 1), new ReportBar("Over", "11", 3)]))
            .Add(ReportSection.ForChart("No chart", new ReportChart { Labels = ["Monday"] }))
            .Add(ReportSection.ForChart("Null chart", null));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var text = GetText(pdf.GetPage(1));

        AssertInOrder(text, "Nothing", "No metrics", "No table", "No bars", "Zero", "Full", "Over", "No chart", "Null chart");
    }

    [Fact]
    public void Serialize_WhenTextHasMoreCharactersThanOneEmbeddedFontHolds_ShouldWriteThemAll()
    {
        // Arrange
        Assert.SkipWhen(ReportPdfFonts.Installed.Regular is null, "No TrueType font is installed.");

        // The letters of Latin-1 and Latin Extended-A, and of ASCII: more than the 255 characters of one embedded font.
        var characters = string.Concat(Enumerable.Range(0x00C0, 0x0180 - 0x00C0).Select(code => (char)code))
            + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var words = characters.Chunk(8).Select(chunk => new string(chunk)).ToList();
        var document = new ReportDocument { Title = "Characters" }
            .Add(ReportSection.ForTable("Characters", [new ReportColumn("Text")], [new ReportRow([string.Join(' ', words)])]));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var written = pdf.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text).ToHashSet(StringComparer.Ordinal);

        Assert.All(words, word => Assert.Contains(word, written));
    }

    [Fact]
    public void Serialize_WhenARowIsTallerThanAPage_ShouldContinueItOnTheNextPages()
    {
        // Arrange
        var notes = string.Join('\n', Enumerable.Range(1, 150).Select(index => $"Line{index}"));
        var document = new ReportDocument { Title = "Notes" }
            .Add(ReportSection.ForTable("Notes", [new ReportColumn("Note"), new ReportColumn("Count")], [new ReportRow([notes, "1"]), new ReportRow(["After", "2"])]));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var pages = pdf.GetPages().ToList();
        var text = string.Join(' ', pages.Select(GetText));

        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.Contains("Note", GetText(page)));
        AssertInOrder(text, [.. Enumerable.Range(1, 150).Select(index => $"Line{index} "), "After"]);
    }

    [Fact]
    public void Serialize_WhenTheFirstRowIsTallerThanAPage_ShouldStartItUnderTheTitles()
    {
        // Arrange
        var notes = string.Join('\n', Enumerable.Range(1, 150).Select(index => $"Line{index}"));
        var document = new ReportDocument { Title = "Transcript" }
            .Add(ReportSection.ForTable("Call notes", [new ReportColumn("Remark"), new ReportColumn("Count")], [new ReportRow([notes, "1"])]));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);

        AssertInOrder(GetText(pdf.GetPage(1)), "Transcript", "Call notes", "Remark", "Line1 ");
        Assert.All(pdf.GetPages(), page => Assert.Contains("Remark", GetText(page)));
    }

    [Theory]
    [InlineData(ReportSectionKind.Chart, "Trend")]
    [InlineData(ReportSectionKind.Chart, null)]
    [InlineData(ReportSectionKind.Table, "Queues")]
    public void Serialize_WhenAContainerStartsLowOnAPage_ShouldKeepItsTitleBandWithItsFirstSection(ReportSectionKind kind, string title)
    {
        // Arrange: the container starts lower on the page as the rows above it grow, until it starts on the next page.
        var start = kind == ReportSectionKind.Chart
            ? "Monday"
            : "Support";
        var documents = Enumerable.Range(0, 50)
            .Select(rows =>
            {
                var section = kind == ReportSectionKind.Chart
                    ? ReportSection.ForChart(title, new ReportChart { Labels = ["Monday", "Tuesday"], Datasets = [new ReportChartDataset("Calls", [12, 18])] })
                    : ReportSection.ForTable(title, [new ReportColumn("Queue")], [new ReportRow(["Support"])]);

                section.ContainerId = "k1";

                return new ReportDocument
                {
                    Title = "Operations",
                    Containers = [new ReportContainer { Id = "k1", Title = "Scorecard" }],
                }
                    .Add(CreateRows(rows))
                    .Add(section);
            })
            .ToList();

        // Act
        var contents = documents.Select(document => CreateFormat().Serialize(document)).ToList();

        // Assert
        for (var rows = 0; rows < contents.Count; rows++)
        {
            using var pdf = PdfDocument.Open(contents[rows]);
            var pages = GetPageNumbers(pdf);
            var band = pages["Scorecard"];

            Assert.True(band == pages[start], $"With {rows} rows above it, the band is on page {band} and its section starts on the next.");
            Assert.True(title is null || band == pages[title], $"With {rows} rows above it, the band is on page {band} and the section title is not.");
        }
    }

    [Fact]
    public void Serialize_WhenATallBarChartStartsAContainerInLandscape_ShouldKeepTheBandAndTitleWithTheChart()
    {
        // Arrange
        var columns = Enumerable.Range(1, 9).Select(index => new ReportColumn($"Column {index}")).ToList();
        var cells = Enumerable.Range(1, 9).Select(index => $"A fairly long value {index}").ToList();
        var chart = ReportSection.ForChart("Backlog", new ReportChart
        {
            Type = ReportChartType.HorizontalBar,
            Labels = [.. Enumerable.Range(1, 40).Select(index => $"Queue{index}")],
            Datasets = [new ReportChartDataset("Waiting", [.. Enumerable.Range(1, 40).Select(index => (double)index)])],
        });

        chart.ContainerId = "k1";
        chart.Description = "The contacts waiting in each queue at the end of the day, across every channel the queue serves, " +
            "including the callbacks that were requested and the messages that were not answered yet.";

        var document = new ReportDocument
        {
            Title = "Operations",
            Containers = [new ReportContainer { Id = "k1", Title = "Scorecard" }],
        }
            .Add(ReportSection.ForTable("Wide", columns, [new ReportRow(cells)]))
            .Add(chart);

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var pages = GetPageNumbers(pdf);

        Assert.True(pdf.GetPage(1).Width > pdf.GetPage(1).Height);
        Assert.Equal(pages["Scorecard"], pages["Backlog"]);
        Assert.Equal(pages["Scorecard"], pages["Queue1"]);
    }

    [Fact]
    public void Serialize_WhenAShortFirstRowFallsAtTheEndOfAPage_ShouldMoveItWholeWithItsTitle()
    {
        // Arrange: the table starts lower on the page as the rows above it grow, until it starts on the next page.
        var documents = Enumerable.Range(0, 50)
            .Select(rows => new ReportDocument { Title = "Operations" }
                .Add(CreateRows(rows))
                .Add(ReportSection.ForTable("Callbacks", [new ReportColumn("Note")], [new ReportRow(["Alpha\nBravo\nCharlie"])])))
            .ToList();

        // Act
        var contents = documents.Select(document => CreateFormat().Serialize(document)).ToList();

        // Assert
        for (var rows = 0; rows < contents.Count; rows++)
        {
            using var pdf = PdfDocument.Open(contents[rows]);
            var pages = GetPageNumbers(pdf);
            var title = pages["Callbacks"];

            Assert.True(
                title == pages["Alpha"] && title == pages["Charlie"],
                $"With {rows} rows above it, the first row is split or apart from the title on page {title}.");
        }
    }

    [Theory]
    [InlineData(ReportChartType.Line)]
    [InlineData(ReportChartType.Bar)]
    [InlineData(ReportChartType.HorizontalBar)]
    public void Serialize_WhenAChartHasManyOrLongLabels_ShouldKeepItsTextInsideThePageAndApart(ReportChartType type)
    {
        // Arrange
        var labels = Enumerable.Range(1, 40).Select(index => $"A rather long category name {index}").ToList();
        var document = new ReportDocument { Title = "Chart" }
            .Add(ReportSection.ForChart("Volume", new ReportChart
            {
                Type = type,
                Labels = labels,
                Datasets =
                [
                    new ReportChartDataset("Inbound calls that were answered", [.. Enumerable.Range(1, 40).Select(index => index * 1.5)]),
                    new ReportChartDataset("Outbound", [.. Enumerable.Range(1, 40).Select(index => (double)(40 - index))]),
                ],
            }));

        // Act
        var content = CreateFormat().Serialize(document);

        // Assert
        using var pdf = PdfDocument.Open(content);

        foreach (var page in pdf.GetPages())
        {
            AssertWordsInsideThePageAndApart(page);
        }
    }

    private static void AssertWordsInsideThePageAndApart(Page page)
    {
        var words = page.GetWords().ToList();

        Assert.All(words, word => Assert.True(
            word.BoundingBox.Left >= 0 && word.BoundingBox.Right <= page.Width,
            $"'{word.Text}' runs off the page."));

        foreach (var line in words.GroupBy(word => Math.Round(word.BoundingBox.Bottom)))
        {
            var ordered = line.OrderBy(word => word.BoundingBox.Left).ToList();

            for (var index = 1; index < ordered.Count; index++)
            {
                Assert.True(
                    ordered[index - 1].BoundingBox.Right <= ordered[index].BoundingBox.Left + 0.5,
                    $"'{ordered[index - 1].Text}' runs into '{ordered[index].Text}'.");
            }
        }
    }

    private static PdfReportExportFormat CreateFormat()
    {
        return new PdfReportExportFormat(new PassThroughStringLocalizer<PdfReportExportFormat>());
    }

    // A table of one-line rows, which moves what follows it down the page a row at a time.
    private static ReportSection CreateRows(int count)
    {
        return ReportSection.ForTable("Activity", [new ReportColumn("Agent")], Enumerable.Range(1, count).Select(index => new ReportRow([$"Agent{index}"])));
    }

    // The number of the first page each word is on.
    private static Dictionary<string, int> GetPageNumbers(PdfDocument pdf)
    {
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var page in pdf.GetPages())
        {
            foreach (var word in page.GetWords())
            {
                numbers.TryAdd(word.Text, page.Number);
            }
        }

        return numbers;
    }

    private static void AssertInOrder(string text, params string[] expected)
    {
        var position = 0;

        foreach (var value in expected)
        {
            var index = text.IndexOf(value, position, StringComparison.Ordinal);

            Assert.True(index >= 0, $"'{value}' was not found after position {position} in: {text}");

            position = index + value.Length;
        }
    }

    private static string GetText(Page page)
    {
        return string.Join(' ', page.GetWords().Select(word => word.Text));
    }

    private sealed class TranslatingLocalizer : IStringLocalizer<PdfReportExportFormat>
    {
        private readonly string _pageNumbers;

        public TranslatingLocalizer(string pageNumbers)
        {
            _pageNumbers = pageNumbers;
        }

        public LocalizedString this[string name] => new(name, name == "Page {0} of {1}" ? _pageNumbers : name);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
