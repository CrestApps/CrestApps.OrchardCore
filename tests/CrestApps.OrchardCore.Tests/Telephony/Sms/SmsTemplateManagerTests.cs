using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsTemplateManagerTests
{
    [Fact]
    public async Task GetEnabledAsync_ReturnsTheStoresEnabledTemplates()
    {
        var templates = new SmsTemplate[]
        {
            new() { ItemId = "t1", Name = "Greeting", Body = "Hi!", Enabled = true },
            new() { ItemId = "t2", Name = "Closing", Body = "Thanks!", Enabled = true },
        };

        var store = new Mock<ISmsTemplateStore>();
        store.Setup(s => s.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(templates);

        var manager = new SmsTemplateManager(store.Object, [], NullLogger<CrestApps.Core.Services.CatalogManager<SmsTemplate>>.Instance);

        var result = await manager.GetEnabledAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "Greeting");
        store.Verify(s => s.GetEnabledAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
