using CrestApps.OrchardCore.ContactCenter.Migrations;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterMigrationSqlTests
{
    [Fact]
    public void BuildCreateUniqueIndexStatement_QuotesEveryIdentifierThroughTheDialectAndPrefixesTheIndex()
    {
        // Arrange
        var dialect = new Mock<ISqlDialect>();
        dialect.SetupGet(d => d.PrefixIndex).Returns(true);
        dialect.Setup(d => d.FormatIndexName(It.IsAny<string>())).Returns<string>(name => $"{name}_fmt");
        dialect.Setup(d => d.QuoteForColumnName(It.IsAny<string>())).Returns<string>(name => $"[{name}]");

        // Act
        var statement = ContactCenterMigrationSql.BuildCreateUniqueIndexStatement(
            dialect.Object,
            "tp_",
            "[tp_Table]",
            "UQ_Sample",
            "ColumnA",
            "ColumnB");

        // Assert
        Assert.Equal(
            "CREATE UNIQUE INDEX [tp_UQ_Sample_fmt] ON [tp_Table] ([ColumnA], [ColumnB])",
            statement);
    }

    [Fact]
    public void BuildCreateUniqueIndexStatement_HonorsDialectQuotingAndSkipsPrefixWhenNotRequired()
    {
        // Arrange: a different quoting style with no index prefix proves the SQL depends only on the
        // dialect and never hardcodes an engine-specific quote character or prefix rule.
        var dialect = new Mock<ISqlDialect>();
        dialect.SetupGet(d => d.PrefixIndex).Returns(false);
        dialect.Setup(d => d.FormatIndexName(It.IsAny<string>())).Returns<string>(name => name);
        dialect.Setup(d => d.QuoteForColumnName(It.IsAny<string>())).Returns<string>(name => $"`{name}`");

        // Act
        var statement = ContactCenterMigrationSql.BuildCreateUniqueIndexStatement(
            dialect.Object,
            "tp_",
            "`Table`",
            "UQ_Sample",
            "ColumnC");

        // Assert
        Assert.Equal(
            "CREATE UNIQUE INDEX `UQ_Sample` ON `Table` (`ColumnC`)",
            statement);
    }
}
