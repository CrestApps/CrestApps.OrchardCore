using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;

/// <summary>
/// Creates the index tables of Digital Credentials.
/// </summary>
public static class CredentialsIndexSchema
{
    /// <summary>
    /// Creates the credential award index table and its database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = CredentialsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<CredentialAwardIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PublicId", column => column.Nullable().WithLength(64))
            .Column<string>("DefinitionId", column => column.Nullable().WithLength(26))
            .Column<string>("RecipientUserId", column => column.Nullable().WithLength(26))
            .Column<string>("RuleKind", column => column.Nullable().WithLength(64))
            .Column<string>("SourceId", column => column.Nullable().WithLength(255))
            .Column<DateTime>("IssuedUtc")
            .Column<DateTime>("ExpiresUtc", column => column.Nullable())
            .Column<bool>("Revoked")
            .Column<int>("StatusListNumber")
            .Column<int>("StatusListIndex"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CredentialAwardIndex>(table =>
            table.CreateIndex("IDX_LearningCredentials_Award_PublicId", "PublicId"), collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CredentialAwardIndex>(table =>
            table.CreateIndex("IDX_LearningCredentials_Award_Recipient", "RecipientUserId", "DefinitionId"), collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CredentialAwardIndex>(table =>
            table.CreateIndex("IDX_LearningCredentials_Award_Definition", "DefinitionId", "RuleKind", "SourceId"), collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CredentialAwardIndex>(table =>
            table.CreateIndex("IDX_LearningCredentials_Award_Status", "StatusListNumber", "StatusListIndex"), collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CredentialAwardIndex>(table =>
            table.CreateIndex("IDX_LearningCredentials_Award_Issued", "IssuedUtc", "Id"), collection: collection);
    }
}
