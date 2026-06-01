using System.Globalization;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement.Metadata.Models;
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

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        public IStringLocalizer WithCulture(CultureInfo culture) => this;
    }
}
