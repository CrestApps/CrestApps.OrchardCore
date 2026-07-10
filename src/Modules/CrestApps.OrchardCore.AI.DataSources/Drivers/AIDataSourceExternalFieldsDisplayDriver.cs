using CrestApps.Core.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIDataSourceExternalFieldsDisplayDriver : DisplayDriver<AIDataSource>
{
    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context) => null;

    public override Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
        => Task.FromResult<IDisplayResult>(null);
}
