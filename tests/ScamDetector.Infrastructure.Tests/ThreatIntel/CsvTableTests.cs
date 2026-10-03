using ScamDetector.Infrastructure.ThreatIntel;

namespace ScamDetector.Infrastructure.Tests.ThreatIntel;

public sealed class CsvTableTests
{
    [Fact]
    public void Parse_HeaderOnly_ReturnsNoRows()
    {
        var rows = CsvTable.Parse("domain,source\n");

        Assert.Empty(rows);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsNoRows()
    {
        Assert.Empty(CsvTable.Parse(string.Empty));
    }

    [Fact]
    public void Parse_SimpleRows_MapsColumnsByHeader()
    {
        var rows = CsvTable.Parse("domain,source\nvcb-xacminh.com,khonggianmang\nabc.xyz,chongluadao\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal("vcb-xacminh.com", rows[0]["domain"]);
        Assert.Equal("chongluadao", rows[1]["source"]);
    }

    [Fact]
    public void Parse_QuotedFieldWithCommaQuoteAndNewline_KeepsFieldIntact()
    {
        var rows = CsvTable.Parse("id,text\ntpl_000001,\"Bấm \"\"link\"\", nhận quà\ndòng hai\"\n");

        Assert.Equal("Bấm \"link\", nhận quà\ndòng hai", Assert.Single(rows)["text"]);
    }

    [Fact]
    public void Parse_CrLfLineEndingsAndBom_ParsesLikeLf()
    {
        var rows = CsvTable.Parse("﻿domain,source\r\nabc.xyz,manual\r\n");

        Assert.Equal("abc.xyz", Assert.Single(rows)["domain"]);
    }

    [Fact]
    public void Parse_MissingTrailingNewlineAndShortRow_FillsMissingColumnsWithEmpty()
    {
        var rows = CsvTable.Parse("domain,source,evidence\nabc.xyz");

        var row = Assert.Single(rows);
        Assert.Equal("abc.xyz", row["domain"]);
        Assert.Equal(string.Empty, row["evidence"]);
    }

    [Fact]
    public void Parse_BlankLines_AreSkipped()
    {
        var rows = CsvTable.Parse("domain\n\nabc.xyz\n\n");

        Assert.Equal("abc.xyz", Assert.Single(rows)["domain"]);
    }
}
