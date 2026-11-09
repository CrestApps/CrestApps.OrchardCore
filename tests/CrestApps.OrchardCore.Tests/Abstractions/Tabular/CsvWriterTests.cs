using System.IO;
using System.Text;
using CrestApps.OrchardCore.Tabular;

namespace CrestApps.OrchardCore.Tests.Abstractions.Tabular;

public sealed class CsvWriterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("carriage\rreturn", "\"carriage\rreturn\"")]
    public void Escape_QuotesOnlyValuesThatNeedIt(string value, string expected)
    {
        // Act
        var actual = CsvWriter.Escape(value);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1+cmd|' /C calc'!A0", "'+1+cmd|' /C calc'!A0")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1:A2)", "'@SUM(A1:A2)")]
    [InlineData("\tstarts with a tab", "'\tstarts with a tab")]
    [InlineData("\rstarts with a return", "\"'\rstarts with a return\"")]
    public void Escape_PrefixesAnApostrophe_WhenACellWouldRunAsAFormula(string value, string expected)
    {
        // Act
        var actual = CsvWriter.Escape(value);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("-5.00")]
    [InlineData("+15551234567")]
    [InlineData("-1,234.50")]
    public void Escape_LeavesNumbersAlone(string value)
    {
        // Act
        var actual = CsvWriter.Escape(value);

        // Assert
        Assert.Equal(value.Contains(',') ? $"\"{value}\"" : value, actual);
    }

    [Fact]
    public void Escape_WithoutTheGuard_KeepsTheValueAsItIs()
    {
        // Act
        var actual = CsvWriter.Escape("- a bullet", guardFormula: false);

        // Assert
        Assert.Equal("- a bullet", actual);
    }

    [Fact]
    public void AppendRow_SeparatesCellsWithCommasAndEndsWithCrLf()
    {
        // Arrange
        var builder = new StringBuilder();

        // Act
        CsvWriter.AppendRow(builder, "a", null, "b,c", "=1");
        CsvWriter.AppendRow(builder, ["d"]);

        // Assert
        Assert.Equal("a,,\"b,c\",'=1\r\nd\r\n", builder.ToString());
    }

    [Fact]
    public void WriteRow_WritesTheSameRowToATextWriter()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        CsvWriter.WriteRow(writer, "a", "b \"c\"");

        // Assert
        Assert.Equal("a,\"b \"\"c\"\"\"\r\n", writer.ToString());
    }

    [Fact]
    public void WhatTheWriterWrites_TheReaderReadsBack()
    {
        // Arrange
        string[] values = ["plain", "a,b", "say \"hi\"", "two\r\nlines", string.Empty, "12.5"];
        var builder = new StringBuilder();

        CsvWriter.AppendRow(builder, values);

        // Act
        var records = CsvReader.ParseRecords(builder.ToString());

        // Assert
        var record = Assert.Single(records);
        Assert.Equal(values, record);
    }
}
