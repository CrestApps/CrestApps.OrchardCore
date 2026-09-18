using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.DataIngestion;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources.FileSources;

/// <summary>
/// Checks which reader claims <c>.html</c> once both document processing and file sources are registered.
/// </summary>
/// <remarks>
/// <para>
/// Both register a reader for <c>.html</c>: document processing registers the plain-text one, and file
/// sources overrides it with the HTML one. Keyed registrations resolve last-wins, so the outcome depends
/// entirely on which runs first -- and the failure is silent, because the wrong reader still returns text,
/// just text with the markup left in it.
/// </para>
/// <para>
/// The order is what the File Sources feature's dependency on AI Documents buys: Orchard Core composes a
/// feature's services after those of the features it depends on. This asserts the outcome under that
/// order, so a future change that drops the dependency or reverses the calls fails here rather than in
/// somebody's index.
/// </para>
/// </remarks>
public sealed class IngestionReaderPrecedenceTests
{
    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void FileSourcesRegisteredAfterDocumentProcessing_KeepsTheHtmlReader(string extension)
    {
        var services = new ServiceCollection();

        // The order Orchard Core uses when File Sources declares its dependency on AI Documents.
        services.AddCoreAIDocumentProcessing();
        services.AddCoreFileSources();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<HtmlIngestionDocumentReader>(provider.GetRequiredKeyedService<IngestionDocumentReader>(extension));
    }

    [Fact]
    public void TheOppositeOrder_LosesIt()
    {
        // Documents the hazard rather than endorsing it: with the calls reversed, plain text reclaims the
        // extension and a page is indexed with its markup. This is what the feature dependency prevents.
        var services = new ServiceCollection();

        services.AddCoreFileSources();
        services.AddCoreAIDocumentProcessing();

        using var provider = services.BuildServiceProvider();

        Assert.IsNotType<HtmlIngestionDocumentReader>(provider.GetRequiredKeyedService<IngestionDocumentReader>(".html"));
    }
}
