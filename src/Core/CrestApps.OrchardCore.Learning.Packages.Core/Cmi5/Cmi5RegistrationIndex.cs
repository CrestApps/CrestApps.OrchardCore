using CrestApps.OrchardCore.Learning.Packages.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;

/// <summary>
/// Indexes the cmi5 runtime states by registration, so a statement of a registration finds its learner's state.
/// </summary>
public sealed class Cmi5RegistrationIndex : MapIndex
{
    /// <summary>Gets or sets the registration.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the enrollment.</summary>
    public string EnrollmentId { get; set; }

    /// <summary>Gets or sets the package item.</summary>
    public string PackageItemId { get; set; }
}

/// <summary>
/// Maps the cmi5 runtime states.
/// </summary>
public sealed class Cmi5RegistrationIndexProvider : IndexProvider<PackageRuntimeState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Cmi5RegistrationIndexProvider"/> class.
    /// </summary>
    public Cmi5RegistrationIndexProvider()
    {
        CollectionName = PackagesConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PackageRuntimeState> context)
        => context.For<Cmi5RegistrationIndex>()
            .When(state => !string.IsNullOrEmpty(state.Cmi5?.Registration))
            .Map(state => new Cmi5RegistrationIndex
            {
                Registration = state.Cmi5.Registration,
                EnrollmentId = state.EnrollmentId,
                PackageItemId = state.PackageItemId,
            });
}

/// <summary>
/// Creates the index table of cmi5 registrations.
/// </summary>
public static class Cmi5IndexSchema
{
    /// <summary>
    /// Creates the table and its database index.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PackagesConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<Cmi5RegistrationIndex>(table => table
            .Column<string>("Registration", column => column.WithLength(36))
            .Column<string>("EnrollmentId", column => column.WithLength(26))
            .Column<string>("PackageItemId", column => column.WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<Cmi5RegistrationIndex>(table => table
            .CreateIndex("IDX_Learning_Cmi5Registration", "Registration"), collection: collection);
    }
}
