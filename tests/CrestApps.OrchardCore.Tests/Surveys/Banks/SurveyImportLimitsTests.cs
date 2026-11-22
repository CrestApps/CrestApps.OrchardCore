using System.Diagnostics;
using CrestApps.OrchardCore.Surveys.Core.Imports;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.Surveys.Banks;

/// <summary>
/// Imports can't be made to use much memory, and a check (dry run) writes nothing.
/// </summary>
public sealed class SurveyImportLimitsTests
{
    [Theory]
    [InlineData("A1", 0)]
    [InlineData("Z9", 25)]
    [InlineData("AA1", 26)]
    [InlineData("XFD1", ExcelSheetReader.MaxColumns - 1)]
    [InlineData("XFE1", ExcelSheetReader.MaxColumns)]
    [InlineData("ZZZZ1", ExcelSheetReader.MaxColumns)]
    [InlineData("ZZZZZZZZZZZZZZZZ1", ExcelSheetReader.MaxColumns)]
    [InlineData("", -1)]
    [InlineData("12", -1)]
    public void ColumnIndex_StopsAtExcelsLastColumn_WithoutOverflowing(string reference, int expected)
    {
        // Arrange

        // Act
        var index = ExcelSheetReader.ColumnIndex(reference);

        // Assert
        Assert.Equal(expected, index);
    }

    [Fact]
    public void TryRead_IgnoresFarAwayCells_SoTheyPadNothing()
    {
        // Arrange
        using var stream = Workbook(5_000, farReference: "ZZZZ");
        var started = Stopwatch.GetTimestamp();

        // Act
        var read = ExcelSheetReader.TryRead(stream, 5_001, out var headers, out var rows, out var tooLarge);

        // Assert
        Assert.True(read);
        Assert.False(tooLarge);
        Assert.Equal(["Type", "Question"], headers);
        Assert.Equal(5_000, rows.Count);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void TryRead_RefusesASheetWithMoreCellsThanTheLimit()
    {
        // Arrange
        using var stream = Workbook(200, farReference: null, headerWidth: ExcelSheetReader.MaxColumns);

        // Act
        var read = ExcelSheetReader.TryRead(stream, 5_001, out _, out var rows, out var tooLarge);

        // Assert
        Assert.False(read);
        Assert.True(tooLarge);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task QtiCheck_WritesNothingToTheMediaLibrary_WhileTheImportDoes()
    {
        // Arrange
        var store = new Mock<IMediaFileStore>();
        store.Setup(item => item.MapPathToPublicUrl(It.IsAny<string>())).Returns<string>(path => "/media/" + path);
        var options = Options.Create(new MediaOptions { AllowedFileExtensions = [".png"] });
        var format = new QtiQuestionImportFormat(store.Object, options, RecipeRoundTrip.Localizer<QtiQuestionImportFormat>());
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        SurveyImportResult checkedResult;
        using (var package = Package())
        {
            checkedResult = await format.CheckAsync("package.zip", package, cancellationToken);
        }

        var writesAfterCheck = store.Invocations.Count(invocation => invocation.Method.Name == nameof(IMediaFileStore.CreateFileFromStreamAsync));

        SurveyImportResult imported;
        using (var package = Package())
        {
            imported = await format.ReadAsync("package.zip", package, cancellationToken);
        }

        // Assert
        Assert.Empty(checkedResult.Errors);
        Assert.Single(checkedResult.Questions);
        Assert.Equal(0, writesAfterCheck);
        Assert.Single(imported.Questions);
        store.Verify(item => item.CreateFileFromStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), true), Times.Once());
    }

    private static MemoryStream Package()
        => BanksQtiSamples.Package(
            BanksQtiSamples.Manifest30(("choice", "items/choice.xml")),
            [("items/choice.xml", BanksQtiSamples.Choice30())],
            ("items/images/sign.png", BanksQtiSamples.Png));

    // A sheet with a header row and data rows; each data row may also hold one cell far to the right.
    private static MemoryStream Workbook(int dataRows, string farReference, int headerWidth = 2)
    {
        var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var worksheet = workbook.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            var header = new Row { RowIndex = 1 };

            header.Append(Text("A1", "Type"), Text("B1", "Question"));

            if (headerWidth > 2)
            {
                header.Append(Text(ColumnName(headerWidth - 1) + "1", "Last"));
            }

            data.Append(header);

            for (uint rowIndex = 2; rowIndex < dataRows + 2; rowIndex++)
            {
                var row = new Row { RowIndex = rowIndex };

                row.Append(Text("A" + rowIndex, "ShortText"), Text("B" + rowIndex, "Q" + rowIndex));

                if (farReference is not null)
                {
                    row.Append(Text(farReference + rowIndex, "far"));
                }

                if (headerWidth > 2)
                {
                    row.Append(Text(ColumnName(headerWidth - 1) + rowIndex, "last"));
                }

                data.Append(row);
            }

            worksheet.Worksheet = new Worksheet(data);
            workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(worksheet), SheetId = 1, Name = "Questions" }));
        }

        stream.Position = 0;

        return stream;
    }

    private static Cell Text(string reference, string value)
        => new() { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value)) };

    private static string ColumnName(int index)
    {
        var name = string.Empty;

        for (index++; index > 0; index = (index - 1) / 26)
        {
            name = (char)('A' + ((index - 1) % 26)) + name;
        }

        return name;
    }
}
