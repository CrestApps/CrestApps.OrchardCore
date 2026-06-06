using System.Data;
using System.Globalization;
using System.Linq;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Flows.Models;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class OmnichannelContactPartContentImportHandlerTests
{
    [Fact]
    public void GetColumns_ShouldDescribeBooleanPreferenceValues()
    {
        var handler = new OmnichannelContactPartContentImportHandler(
            Mock.Of<IClock>(),
            Mock.Of<IPhoneNumberService>(),
            new PassThroughStringLocalizer<OmnichannelContactPartContentImportHandler>());

        var columns = handler.GetColumns(new ImportContentPartContext
        {
            ContentTypePartDefinition = new ContentTypePartDefinition(
                nameof(OmnichannelContactPart),
                new ContentPartDefinition(nameof(OmnichannelContactPart)),
                new()),
        });

        var doNotCallColumn = Assert.Single(columns, column => column.Name == nameof(OmnichannelContactPart.DoNotCall));
        var doNotSmsColumn = Assert.Single(columns, column => column.Name == nameof(OmnichannelContactPart.DoNotSms));
        var doNotEmailColumn = Assert.Single(columns, column => column.Name == nameof(OmnichannelContactPart.DoNotEmail));
        var doNotChatColumn = Assert.Single(columns, column => column.Name == nameof(OmnichannelContactPart.DoNotChat));

        Assert.Equal(["true", "false"], doNotCallColumn.ValidValues);
        Assert.Equal(["true", "false"], doNotSmsColumn.ValidValues);
        Assert.Equal(["true", "false"], doNotEmailColumn.ValidValues);
        Assert.Equal(["true", "false"], doNotChatColumn.ValidValues);
        Assert.Contains("true or false", doNotCallColumn.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_ShouldNormalizePhoneNumbersUsingSelectedCountry()
    {
        var handler = new OmnichannelContactPartContentImportHandler(
            Mock.Of<IClock>(),
            new DefaultPhoneNumberService(),
            new PassThroughStringLocalizer<OmnichannelContactPartContentImportHandler>());
        var dataTable = new DataTable();
        dataTable.Columns.Add("Cell Phone");
        dataTable.Columns.Add("Phone");

        var row = dataTable.NewRow();
        row["Cell Phone"] = "2502000003";
        row["Phone"] = "7024993350";
        dataTable.Rows.Add(row);

        var contentItem = new ContentItem();
        var entry = new ContentTransferEntry();
        entry.Put(new OmnichannelContactImportOptionsPart
        {
            SelectedCountryCode = "CA",
        });

        var context = new ContentPartImportMapContext
        {
            ContentItem = contentItem,
            Entry = entry,
            Columns = dataTable.Columns,
            Row = row,
        };

        await handler.ImportAsync(context);

        var bagPart = contentItem.GetOrCreate<BagPart>(CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.NamedParts.ContactMethods);
        var phoneNumbers = bagPart.ContentItems
            .Where(item => item.ContentType == CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.ContentTypes.PhoneNumber)
            .Select(item =>
            {
                Assert.True(item.TryGet<PhoneNumberInfoPart>(out var phoneNumberPart));

                return phoneNumberPart;
            })
            .ToDictionary(part => part.Type.Text, part => part.Number.PhoneNumber, StringComparer.Ordinal);

        Assert.Equal("+12502000003", phoneNumbers["Cell"]);
        Assert.Equal("+17024993350", phoneNumbers["Home"]);
    }

    [Fact]
    public async Task ImportAsync_ShouldReplaceManagedContactMethodEntriesOnUpdate()
    {
        var handler = new OmnichannelContactPartContentImportHandler(
            Mock.Of<IClock>(),
            new DefaultPhoneNumberService(),
            new PassThroughStringLocalizer<OmnichannelContactPartContentImportHandler>());
        var dataTable = new DataTable();
        dataTable.Columns.Add("Email");
        dataTable.Columns.Add("Cell Phone");
        dataTable.Columns.Add("Phone");

        var row = dataTable.NewRow();
        row["Email"] = "new@example.com";
        row["Cell Phone"] = "+12502000003";
        row["Phone"] = "+17024993350";
        dataTable.Rows.Add(row);

        var contentItem = new ContentItem();
        var bagPart = new BagPart
        {
            ContentItems =
            [
                CreateEmailAddressContentItem("old@example.com"),
                CreatePhoneNumberContentItem("+15551112222", "Cell"),
                CreatePhoneNumberContentItem("+15553334444", "Home"),
            ],
        };
        contentItem.Apply(CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.NamedParts.ContactMethods, bagPart);

        var context = new ContentPartImportMapContext
        {
            ContentItem = contentItem,
            Entry = new ContentTransferEntry(),
            Columns = dataTable.Columns,
            Row = row,
        };

        await handler.ImportAsync(context);

        var updatedBagPart = contentItem.GetOrCreate<BagPart>(CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.NamedParts.ContactMethods);
        Assert.Collection(
            updatedBagPart.ContentItems.OrderBy(item => item.DisplayText, StringComparer.Ordinal),
            item => Assert.Equal("Cell: +12502000003", item.DisplayText),
            item => Assert.Equal("Home: +17024993350", item.DisplayText),
            item => Assert.Equal("new@example.com", item.DisplayText));
    }

    [Fact]
    public async Task ImportAsync_ShouldPreserveExistingE164NumbersWhenCountryDiffers()
    {
        var handler = new OmnichannelContactPartContentImportHandler(
            Mock.Of<IClock>(),
            new DefaultPhoneNumberService(),
            new PassThroughStringLocalizer<OmnichannelContactPartContentImportHandler>());
        var dataTable = new DataTable();
        dataTable.Columns.Add("Cell Phone");

        var row = dataTable.NewRow();
        row["Cell Phone"] = "+17024993350";
        dataTable.Rows.Add(row);

        var contentItem = new ContentItem();
        var entry = new ContentTransferEntry();
        entry.Put(new OmnichannelContactImportOptionsPart
        {
            SelectedCountryCode = "CA",
        });

        var context = new ContentPartImportMapContext
        {
            ContentItem = contentItem,
            Entry = entry,
            Columns = dataTable.Columns,
            Row = row,
        };

        await handler.ImportAsync(context);

        var bagPart = contentItem.GetOrCreate<BagPart>(CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.NamedParts.ContactMethods);
        Assert.Single(bagPart.ContentItems);
        Assert.Equal("Cell: +17024993350", bagPart.ContentItems[0].DisplayText);
    }

    [Fact]
    public async Task ImportAsync_ShouldOverrideExistingDoNotChatWhenColumnIsPresent()
    {
        var handler = new OmnichannelContactPartContentImportHandler(
            Mock.Of<IClock>(),
            new DefaultPhoneNumberService(),
            new PassThroughStringLocalizer<OmnichannelContactPartContentImportHandler>());
        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(OmnichannelContactPart.DoNotChat));

        var row = dataTable.NewRow();
        row[nameof(OmnichannelContactPart.DoNotChat)] = "TRUE";
        dataTable.Rows.Add(row);

        var contentItem = new ContentItem();
        contentItem.Apply(new OmnichannelContactPart
        {
            DoNotChat = false,
        });

        var context = new ContentPartImportMapContext
        {
            ContentItem = contentItem,
            Entry = new ContentTransferEntry(),
            Columns = dataTable.Columns,
            Row = row,
        };

        await handler.ImportAsync(context);

        Assert.True(contentItem.TryGet<OmnichannelContactPart>(out var updatedPart));
        Assert.True(updatedPart.DoNotChat);
    }

    [Fact]
    public async Task ImportAsync_ShouldClearExistingDoNotChatWhenColumnContainsFalse()
    {
        var handler = new OmnichannelContactPartContentImportHandler(
            Mock.Of<IClock>(),
            new DefaultPhoneNumberService(),
            new PassThroughStringLocalizer<OmnichannelContactPartContentImportHandler>());
        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(OmnichannelContactPart.DoNotChat));

        var row = dataTable.NewRow();
        row[nameof(OmnichannelContactPart.DoNotChat)] = "FALSE";
        dataTable.Rows.Add(row);

        var contentItem = new ContentItem();
        contentItem.Apply(new OmnichannelContactPart
        {
            DoNotChat = true,
            DoNotChatUtc = new DateTime(2026, 6, 5, 20, 0, 0, DateTimeKind.Utc),
        });

        var context = new ContentPartImportMapContext
        {
            ContentItem = contentItem,
            Entry = new ContentTransferEntry(),
            Columns = dataTable.Columns,
            Row = row,
        };

        await handler.ImportAsync(context);

        Assert.True(contentItem.TryGet<OmnichannelContactPart>(out var updatedPart));
        Assert.False(updatedPart.DoNotChat);
        Assert.Null(updatedPart.DoNotChatUtc);
    }

    private static ContentItem CreateEmailAddressContentItem(string email)
    {
        var contentItem = new ContentItem
        {
            ContentType = CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.ContentTypes.EmailAddress,
            DisplayText = email,
        };

        contentItem.Alter<EmailInfoPart>(part =>
        {
            part.Email = new TextField { Text = email };
        });

        return contentItem;
    }

    private static ContentItem CreatePhoneNumberContentItem(string number, string type)
    {
        var contentItem = new ContentItem
        {
            ContentType = CrestApps.OrchardCore.Omnichannel.Core.OmnichannelConstants.ContentTypes.PhoneNumber,
            DisplayText = $"{type}: {number}",
        };

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField { PhoneNumber = number };
            part.Type = new TextField { Text = type };
        });

        return contentItem;
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        public PassThroughStringLocalizer<T> WithCulture(CultureInfo culture) => this;
    }
}
