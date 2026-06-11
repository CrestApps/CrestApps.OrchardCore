using System.Data;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Moq;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class OmnichannelContactImportRowFilterTests
{
    private readonly Mock<IOmnichannelContactDuplicateLookupService> _duplicateLookupService = new();
    private readonly Mock<ISiteService> _siteService = new();
    private readonly Mock<ISite> _site = new();
    private readonly DncRegistrySettings _settings = new();

    public OmnichannelContactImportRowFilterTests()
    {
        _site.Setup(x => x.GetOrCreate<DncRegistrySettings>())
            .Returns(_settings);
        _siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(_site.Object);
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldSkipFileAndDatabaseDuplicates()
    {
        // Arrange
        var filter = CreateFilter([]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = true,
        });

        _duplicateLookupService.Setup(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["5553334444"] = ["existing-contact"],
            });

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        var rowContexts = new[]
        {
            CreateRowContext(entry, 1, ("Cell Phone", "555-111-2222")),
            CreateRowContext(entry, 2, ("Phone", "(555) 111-2222")),
            CreateRowContext(entry, 3, ("Mobile", "555-333-4444")),
        };

        // Act & Assert
        Assert.False(await filter.ShouldSkipRowAsync(rowContexts[0]));

        Assert.True(await filter.ShouldSkipRowAsync(rowContexts[1]));
        Assert.Contains("already appeared earlier in the import file", rowContexts[1].SkipReason);

        Assert.True(await filter.ShouldSkipRowAsync(rowContexts[2]));
        Assert.Contains("already exists in the database", rowContexts[2].SkipReason);

        _duplicateLookupService.Verify(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldSkipDoNotCallNumbers()
    {
        // Arrange
        var registry = new Mock<INationalDoNotCallRegistry>();
        registry.SetupGet(x => x.Key).Returns("usa");
        registry.SetupGet(x => x.DisplayName).Returns("USA");
        registry.SetupGet(x => x.Description).Returns("USA registry");
        registry.Setup(x => x.GetRegisteredNumbersAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<NumberSearchContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["5557778888"]);

        var filter = CreateFilter([registry.Object]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = false,
            IgnoreDoNotCallNumbers = true,
            SelectedRegistryKeys = ["usa"],
        });

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        var rowContext = CreateRowContext(entry, 1, ("Cell Phone", "555-777-8888"));

        // Act & Assert
        Assert.True(await filter.ShouldSkipRowAsync(rowContext));
        Assert.Contains("is registered on a national do-not-call registry", rowContext.SkipReason);

        _duplicateLookupService.Verify(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldDetectCrossColumnDuplicates()
    {
        // Arrange - a number exists as a cell phone in the DB, but the import row uses the home phone column.
        var filter = CreateFilter([]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = true,
        });

        _duplicateLookupService.Setup(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["5551112222"] = ["existing-contact"],
            });

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        // Row uses "Home Phone" column, but the number exists in DB as a cell phone — should still be rejected.
        var row1 = CreateRowContext(entry, 1, ("Home Phone", "555-111-2222"));

        // Row uses "Cell Phone" column with a new number, then same number in "Phone" column in the next row.
        var row2 = CreateRowContext(entry, 2, ("Cell Phone", "555-999-8888"));
        var row3 = CreateRowContext(entry, 3, ("Home Phone", "(555) 999-8888"));

        // Act & Assert
        Assert.True(await filter.ShouldSkipRowAsync(row1));
        Assert.Contains("Home phone number", row1.SkipReason);
        Assert.Contains("already exists in the database", row1.SkipReason);

        Assert.False(await filter.ShouldSkipRowAsync(row2));

        Assert.True(await filter.ShouldSkipRowAsync(row3));
        Assert.Contains("Home phone number", row3.SkipReason);
        Assert.Contains("already appeared earlier in the import file", row3.SkipReason);
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldRejectSecondImportOfSamePhoneNumbers()
    {
        // Arrange - simulate a second import where all phone numbers already exist in the DB
        var filter = CreateFilter([]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = true,
        });

        _duplicateLookupService.Setup(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["5551112222"] = ["existing-contact-1"],
                ["5553334444"] = ["existing-contact-2"],
                ["5556667777"] = ["existing-contact-3"],
            });

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        // Act & Assert - every row should be rejected
        var row1 = CreateRowContext(entry, 1, ("Cell Phone", "555-111-2222"));
        var row2 = CreateRowContext(entry, 2, ("Cell Phone", "555-333-4444"));
        var row3 = CreateRowContext(entry, 3, ("Cell Phone", "555-666-7777"));

        Assert.True(await filter.ShouldSkipRowAsync(row1));
        Assert.Contains("already exists in the database", row1.SkipReason);
        Assert.Contains("555-111-2222", row1.SkipReason);

        Assert.True(await filter.ShouldSkipRowAsync(row2));
        Assert.Contains("already exists in the database", row2.SkipReason);

        Assert.True(await filter.ShouldSkipRowAsync(row3));
        Assert.Contains("already exists in the database", row3.SkipReason);
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldAllowUpdatingExistingOwner()
    {
        // Arrange
        var filter = CreateFilter([]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = true,
        });

        _duplicateLookupService.Setup(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["7024993350"] = ["4eb8n2j6ffk0q6dg9jevfavb5y"],
            });

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        var rowContext = CreateRowContext(
            entry,
            1,
            ("ContentItemId", "4eb8n2j6ffk0q6dg9jevfavb5y"),
            ("Phone", "7024993350"));

        // Act & Assert
        Assert.False(await filter.ShouldSkipRowAsync(rowContext));
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldAllowRepeatedPhoneForSameContentItemIdInFile()
    {
        // Arrange
        var filter = CreateFilter([]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = true,
        });

        _duplicateLookupService.Setup(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        var row1 = CreateRowContext(
            entry,
            1,
            ("ContentItemId", "4eb8n2j6ffk0q6dg9jevfavb5y"),
            ("Phone", "7024993350"));
        var row2 = CreateRowContext(
            entry,
            2,
            ("ContentItemId", "4eb8n2j6ffk0q6dg9jevfavb5y"),
            ("Phone", "(702) 499-3350"));

        // Act & Assert
        Assert.False(await filter.ShouldSkipRowAsync(row1));
        Assert.False(await filter.ShouldSkipRowAsync(row2));
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldRejectRepeatedPhoneForDifferentContentItemIdInFile()
    {
        // Arrange
        var filter = CreateFilter([]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = true,
        });

        _duplicateLookupService.Setup(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        var row1 = CreateRowContext(
            entry,
            1,
            ("ContentItemId", "first-contact"),
            ("Phone", "7024993350"));
        var row2 = CreateRowContext(
            entry,
            2,
            ("ContentItemId", "second-contact"),
            ("Phone", "(702) 499-3350"));

        // Act & Assert
        Assert.False(await filter.ShouldSkipRowAsync(row1));
        Assert.True(await filter.ShouldSkipRowAsync(row2));
        Assert.Contains("already appeared earlier in the import file", row2.SkipReason);
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldUseSelectedCountryForDncComparison()
    {
        // Arrange
        var lookupBatches = new List<string[]>();
        var registry = new Mock<INationalDoNotCallRegistry>();
        registry.SetupGet(x => x.Key).Returns("local-dnc");
        registry.SetupGet(x => x.DisplayName).Returns("Local");
        registry.SetupGet(x => x.Description).Returns("Local registry");
        registry.Setup(x => x.GetRegisteredNumbersAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<NumberSearchContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> numbers, NumberSearchContext _, CancellationToken _) =>
            {
                var batch = numbers.ToArray();
                lookupBatches.Add(batch);

                return batch.Contains("+12502000003", StringComparer.OrdinalIgnoreCase)
                    ? ["+12502000003"]
                    : [];
            });

        var filter = CreateFilter([registry.Object]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = false,
            IgnoreDoNotCallNumbers = true,
            SelectedCountryCode = "CA",
            SelectedRegistryKeys = ["local-dnc"],
        });

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        var rowContext = CreateRowContext(entry, 1, ("Cell Phone", "2502000003"));

        // Act & Assert
        Assert.True(await filter.ShouldSkipRowAsync(rowContext));
        Assert.Contains("is registered on a national do-not-call registry", rowContext.SkipReason);
        Assert.Contains(
            lookupBatches,
            batch => batch.Contains("+12502000003", StringComparer.OrdinalIgnoreCase));
        registry.Verify(x => x.GetRegisteredNumbersAsync(
            It.IsAny<IEnumerable<string>>(),
            It.Is<NumberSearchContext>(context => context.CountryCode == "CA"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShouldSkipRowAsync_ShouldNormalizeNumbersBeforeCheckingDoNotCallRegistry()
    {
        // Arrange
        var lookupBatches = new List<string[]>();
        var registry = new Mock<INationalDoNotCallRegistry>();
        registry.SetupGet(x => x.Key).Returns("local-dnc");
        registry.SetupGet(x => x.DisplayName).Returns("Local");
        registry.SetupGet(x => x.Description).Returns("Local registry");
        registry.Setup(x => x.GetRegisteredNumbersAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<NumberSearchContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> numbers, NumberSearchContext _, CancellationToken _) =>
            {
                var batch = numbers.ToArray();
                lookupBatches.Add(batch);

                return batch.Contains("+12502000003", StringComparer.OrdinalIgnoreCase)
                    ? ["+12502000003"]
                    : [];
            });

        var filter = CreateFilter([registry.Object]);
        var entry = CreateEntry(new OmnichannelContactImportOptionsPart
        {
            IgnoreDuplicateByPhoneNumber = true,
            IgnoreDoNotCallNumbers = true,
            SelectedCountryCode = "CA",
            SelectedRegistryKeys = ["local-dnc"],
        });

        _duplicateLookupService.Setup(x => x.GetAllExistingNormalizedPhoneNumberOwnersAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["7024993350"] = ["4eb8n2j6ffk0q6dg9jevfavb5y"],
            });

        var initialized = await filter.InitializeAsync(new ContentImportRowFilterInitContext
        {
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
        });

        Assert.True(initialized);

        var updateRow = CreateRowContext(
            entry,
            1,
            ("ContentItemId", "4eb8n2j6ffk0q6dg9jevfavb5y"),
            ("Phone", "7024993350"));
        var newRow = CreateRowContext(
            entry,
            2,
            ("Cell Phone", "2502000003"));

        // Act & Assert
        Assert.False(await filter.ShouldSkipRowAsync(updateRow));

        Assert.True(await filter.ShouldSkipRowAsync(newRow));
        Assert.Contains("is registered on a national do-not-call registry", newRow.SkipReason);
        Assert.Contains(
            lookupBatches,
            batch => batch.Contains("+12502000003", StringComparer.OrdinalIgnoreCase));
    }

    private OmnichannelContactImportRowFilter CreateFilter(IEnumerable<INationalDoNotCallRegistry> registries)
        => new(
            registries,
            _duplicateLookupService.Object,
            new DefaultPhoneNumberService(),
            _siteService.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<OmnichannelContactImportRowFilter>>());

    private static ContentTransferEntry CreateEntry(OmnichannelContactImportOptionsPart options)
    {
        var entry = new ContentTransferEntry();
        entry.Put(options);

        return entry;
    }

    private static ContentImportRowFilterContext CreateRowContext(
        ContentTransferEntry entry,
        int rowIndex,
        params (string ColumnName, string Value)[] values)
    {
        var table = new DataTable();
        foreach (var (columnName, _) in values)
        {
            table.Columns.Add(columnName);
        }

        var row = table.NewRow();

        for (var index = 0; index < values.Length; index++)
        {
            row[index] = values[index].Value;
        }

        return new ContentImportRowFilterContext
        {
            Row = row,
            Columns = table.Columns,
            ContentTypeDefinition = CreateContentTypeDefinition(),
            Entry = entry,
            RowIndex = rowIndex,
        };
    }

    private static ContentTypeDefinition CreateContentTypeDefinition()
    {
        var parts = new[]
        {
            new ContentTypePartDefinition(
                OmnichannelConstants.ContentParts.OmnichannelContact,
                new ContentPartDefinition(OmnichannelConstants.ContentParts.OmnichannelContact),
                new JsonObject()),
        };
        var contentTypeDefinition = new ContentTypeDefinition("Customer", "Customer", parts, new JsonObject());

        foreach (var part in parts)
        {
            part.ContentTypeDefinition = contentTypeDefinition;
        }

        return contentTypeDefinition;
    }
}
