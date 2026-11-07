using System.IO;
using CrestApps.OrchardCore.Tabular;

namespace CrestApps.OrchardCore.Tests.Abstractions.Tabular;

public sealed class CsvReaderTests
{
    [Fact]
    public void ParseRecords_ReadsQuotedCommasQuotesAndLineBreaks()
    {
        // Arrange
        var text = "a,\"b,c\",\"say \"\"hi\"\"\"\r\n\"two\nlines\",x,\r\n";

        // Act
        var records = CsvReader.ParseRecords(text);

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(["a", "b,c", "say \"hi\""], records[0]);
        Assert.Equal(["two\nlines", "x", string.Empty], records[1]);
    }

    [Fact]
    public void ParseRecords_SkipsAByteOrderMark_AndReadsALastRecordWithoutALineBreak()
    {
        // Act
        var records = CsvReader.ParseRecords("﻿Name,Code\nWidget,W1");

        // Assert
        Assert.Equal(2, records.Count);
        Assert.Equal(["Name", "Code"], records[0]);
        Assert.Equal(["Widget", "W1"], records[1]);
    }

    [Fact]
    public void ParseRecords_OfEmptyText_IsEmpty()
    {
        // Act
        var records = CsvReader.ParseRecords(string.Empty);

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public void ReadRecords_StreamsTheSameRecordsAsParseRecords()
    {
        // Arrange
        var text = "﻿Sku,OnHand\r\n\"A,1\",5\r\n\r\nB,\"7\"";
        using var reader = new StringReader(text);

        // Act
        var streamed = CsvReader.ReadRecords(reader).ToList();

        // Assert
        Assert.Equal(CsvReader.ParseRecords(text), streamed);
        Assert.Equal(["A,1", "5"], streamed[1]);
        Assert.Equal([string.Empty], streamed[2]);
    }

    [Fact]
    public void ParseLine_ReadsOneLine()
    {
        // Act
        var fields = CsvReader.ParseLine("\"+1 (555) 010-0000\", \"a,b\" ,c");

        // Assert
        Assert.Equal(["+1 (555) 010-0000", " a,b ", "c"], fields);
    }

    [Fact]
    public void Parse_ReadsRowsByColumnName_InAnyCase_AndSkipsEmptyRows()
    {
        // Act
        var (headers, rows) = CsvReader.Parse("Name, Code\r\nWidget,W1\r\n,\r\nGadget,G2\r\n");

        // Assert
        Assert.Equal(["Name", "Code"], headers);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Widget", rows[0]["name"]);
        Assert.Equal("W1", rows[0]["CODE"]);
        Assert.Equal(2, rows[0].Number);
        Assert.Equal(4, rows[1].Number);
        Assert.Null(rows[1]["Missing"]);
    }

    [Fact]
    public void ToRows_NumbersRowsFromTheHeader()
    {
        // Act
        var rows = CsvReader.ToRows(["Name"], [["A"], [string.Empty], ["B"]]);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].Number);
        Assert.Equal("B", rows[1]["Name"]);
        Assert.Equal(4, rows[1].Number);
    }
}
