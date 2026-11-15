using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;

/// <summary>
/// Indexes education credit types.
/// </summary>
public sealed class EducationCreditTypeIndex : CatalogItemIndex
{
    public string Name { get; set; }
}

/// <summary>
/// Indexes external learning.
/// </summary>
public sealed class ExternalLearningSubmissionIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public ExternalLearningState State { get; set; }

    public DateTime SubmittedUtc { get; set; }
}

/// <summary>
/// Maps education credit types.
/// </summary>
public sealed class EducationCreditTypeIndexProvider : IndexProvider<EducationCreditType>
{
    public EducationCreditTypeIndexProvider()
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<EducationCreditType> context)
        => context.For<EducationCreditTypeIndex>()
            .Map(type => new EducationCreditTypeIndex { ItemId = type.ItemId, Name = type.Name });
}

/// <summary>
/// Maps external learning.
/// </summary>
public sealed class ExternalLearningSubmissionIndexProvider : IndexProvider<ExternalLearningSubmission>
{
    public ExternalLearningSubmissionIndexProvider()
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ExternalLearningSubmission> context)
        => context.For<ExternalLearningSubmissionIndex>()
            .Map(submission => new ExternalLearningSubmissionIndex
            {
                ItemId = submission.ItemId,
                UserId = submission.UserId,
                State = submission.State,
                SubmittedUtc = submission.SubmittedUtc,
            });
}

/// <summary>
/// Creates the tables of education credits: credit types, external learning and the credit ledger.
/// </summary>
public static class EducationCreditsIndexSchema
{
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = CertificationsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<EducationCreditTypeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<EducationCreditTypeIndex>(table =>
            table.CreateIndex("IDX_LearningCreditType_ItemId", "ItemId"),
            collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ExternalLearningSubmissionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<int>("State")
            .Column<DateTime>("SubmittedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ExternalLearningSubmissionIndex>(table =>
        {
            table.CreateIndex("IDX_LearningExternal_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningExternal_User", "UserId");
            table.CreateIndex("IDX_LearningExternal_StateSubmitted", "State", "SubmittedUtc", "Id");
        }, collection: collection);

        await QuantityLedgerIndexSchema.CreateAsync(schemaBuilder, CertificationsConstants.CreditLedgerCollectionName);
    }
}
