using CrestApps.OrchardCore.AI.Sms.Handlers;
using CrestApps.OrchardCore.AI.Sms.Indexes;
using CrestApps.OrchardCore.AI.Sms.Migrations;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Sms;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IOmnichannelEventHandler, SmsOmnichannelEventHandler>();

        services
            .AddDataMigration<OminchannelActivityAIChatSessionIndexMigrations>()
            .AddIndexProvider<OminchannelActivityAIChatSessionIndexProvider>();
    }
}

