using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

internal sealed class OmnichannelContactCommunicationPreferenceIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<bool>("DoNotCall", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotCallUtc")
                .Column<bool>("DoNotSms", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotSmsUtc")
                .Column<bool>("DoNotEmail", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotEmailUtc")
                .Column<bool>("DoNotChat", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotChatUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
            .CreateIndex("IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc",
                "DocumentId",
                "DoNotCallUtc")
        );

        return 1;
    }
}
