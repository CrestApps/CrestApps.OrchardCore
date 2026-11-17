using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.SharedLinks;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent.DataPipelines;

/// <summary>
/// Builds what the data pipeline and shared link tool tests need: two registered step types, a data source with two data
/// sets of which the caller may read one, a shared link manager over an in-memory list, a fixed clock and a localizer
/// factory that returns the source texts.
/// </summary>
internal sealed class DataPipelineToolHarness
{
    public static readonly DateTime UtcNow = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    public bool SignedIn { get; init; } = true;

    public Func<Permission, object, bool> Authorize { get; init; }

    public FakeSharedLinkManager SharedLinks { get; } = new();

    public FakeDataSource DataSource { get; } = new();

    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, Authorize);

        services.AddSingleton<IStringLocalizerFactory, PassThroughLocalizerFactory>();
        services.AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == UtcNow));

        services.AddScoped<RecordsSourceStepType>();
        services.AddScoped<UploadFilesStepType>();
        services.Configure<DataPipelineStepTypeOptions>(options =>
        {
            options.Add(DataPipelineStepTypeDescriptor.Create<RecordsSourceStepType>());
            options.Add(DataPipelineStepTypeDescriptor.Create<UploadFilesStepType>());
        });

        services.AddSingleton(DataSource);
        services.AddSingleton<IDataSourceManager>(provider => new FakeDataSourceManager(provider.GetRequiredService<FakeDataSource>()));

        services.AddSingleton(SharedLinks);
        services.AddSingleton<ISharedLinkManager>(provider => provider.GetRequiredService<FakeSharedLinkManager>());
        services.Configure<SharedLinkHandlerOptions>(options =>
            options.Add(new SharedLinkHandlerDescriptor("DataPipelines.File", LocalizationSource.Create("Pipeline file"), typeof(object))));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A source step type with one records output.
    /// </summary>
    public sealed class RecordsSourceStepType : IDataPipelineStepType
    {
        public static string Name => "DataSource";

        public static LocalizationSource DisplayName => LocalizationSource.Create("Read a data source");

        public static LocalizationSource Description => LocalizationSource.Create("Reads the rows of a data set.");

        public static DataPipelineStepCategory Category => DataPipelineStepCategory.Source;

        public IReadOnlyList<DataPipelinePort> GetInputs(DataPipelineStep step) => [];

        public IReadOnlyList<DataPipelinePort> GetOutputs(DataPipelineStep step) => [new DataPipelinePort(DataPipelinePort.Output, "Rows")];

        public Task DescribeAsync(DataPipelineDescribeContext context) => Task.CompletedTask;

        public Task ExecuteAsync(DataPipelineStepContext context) => throw new InvalidOperationException("A tool must never run a step.");
    }

    /// <summary>
    /// A destination step type with one required files input.
    /// </summary>
    public sealed class UploadFilesStepType : IDataPipelineStepType
    {
        public static string Name => "UploadToSftp";

        public static LocalizationSource DisplayName => LocalizationSource.Create("Upload to SFTP");

        public static LocalizationSource Description => LocalizationSource.Create("Uploads files to an SFTP server.");

        public static DataPipelineStepCategory Category => DataPipelineStepCategory.Destination;

        public IReadOnlyList<DataPipelinePort> GetInputs(DataPipelineStep step)
            => [new DataPipelinePort(DataPipelinePort.Input, "Files", DataPipelinePortKind.Files) { IsRequired = true, AllowsMany = true }];

        public IReadOnlyList<DataPipelinePort> GetOutputs(DataPipelineStep step) => [];

        public Task DescribeAsync(DataPipelineDescribeContext context) => Task.CompletedTask;

        public Task ExecuteAsync(DataPipelineStepContext context) => throw new InvalidOperationException("A tool must never run a step.");
    }

    /// <summary>
    /// A data source with an Orders data set the caller may read and a Payroll data set nobody may read, which records
    /// the user it was asked with.
    /// </summary>
    public sealed class FakeDataSource : IDataSource
    {
        public static string Name => "Shop";

        public static LocalizationSource DisplayName => LocalizationSource.Create("Shop data");

        public static LocalizationSource Description => LocalizationSource.Create("Orders of the shop.");

        public List<ClaimsPrincipal> Users { get; } = [];

        public Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        {
            Users.Add(context.User);

            IReadOnlyList<DataSetDescriptor> dataSets = context.User is null
                ? []
                : [new DataSetDescriptor("Orders", "Orders", "Orders placed in the shop.") { Group = "Sales", DefaultDateField = "PlacedUtc" }];

            return Task.FromResult(dataSets);
        }

        public Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
        {
            Users.Add(context.User);

            if (context.User is null || dataSet != "Orders")
            {
                return Task.FromResult<DataSetSchema>(null);
            }

            return Task.FromResult(new DataSetSchema
            {
                DataSet = new DataSetDescriptor("Orders", "Orders", "Orders placed in the shop.") { Group = "Sales", DefaultDateField = "PlacedUtc" },
                Fields =
                [
                    new DataField("OrderId", "Order ID", DataFieldType.Text) { IsIdentifier = true },
                    new DataField("Total", "Total", DataFieldType.Decimal, "Amounts"),
                    new DataField("CustomerId", "Customer", DataFieldType.Text)
                    {
                        IsKeyFilterable = true,
                        References = [new DataFieldReference("Users", "Users", "UserId")],
                    },
                ],
            });
        }

        public IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A schema tool must never read rows.");
    }

    private sealed class FakeDataSourceManager : IDataSourceManager
    {
        private readonly FakeDataSource _dataSource;
        private readonly DataSourceDescriptor _descriptor = DataSourceDescriptor.Create<FakeDataSource>();

        public FakeDataSourceManager(FakeDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public IReadOnlyList<DataSourceDescriptor> GetDataSources() => [_descriptor];

        public DataSourceDescriptor GetDescriptor(string name)
            => string.Equals(name, _descriptor.Name, StringComparison.OrdinalIgnoreCase) ? _descriptor : null;

        public IDataSource GetDataSource(string name)
            => GetDescriptor(name) is null ? null : _dataSource;
    }

    /// <summary>
    /// A shared link manager over an in-memory list that records what it revoked and deleted.
    /// </summary>
    public sealed class FakeSharedLinkManager : ISharedLinkManager
    {
        public FakeSharedLinkManager()
        {
            Links.Add(new SharedLink
            {
                Id = 1,
                LinkId = "link-finance",
                Kind = "DataPipelines.File",
                ResourceId = "resource-finance",
                Title = "finance-october.xlsx",
                SourceName = "Monthly finance export",
                TokenHash = "0123456789abcdef",
                RecipientUserIds = ["user-7"],
                RecipientNames = ["Dana"],
                CreatedUtc = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc),
                ExpiresUtc = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc),
                MaxUses = 5,
                Uses = 2,
            });

            Links.Add(new SharedLink
            {
                Id = 2,
                LinkId = "link-old",
                Kind = "Reports.Share",
                Title = "Sales report",
                SourceName = "Sales",
                TokenHash = "fedcba9876543210",
                RecipientNames = ["Lee"],
                CreatedUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
                ExpiresUtc = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc),
            });
        }

        public List<SharedLink> Links { get; } = [];

        public List<string> Revoked { get; } = [];

        public List<string> Deleted { get; } = [];

        public Task<SharedLinkResult> CreateAsync(SharedLinkRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No tool creates a shared link.");

        public Task<SharedLink> GetAsync(string linkId, CancellationToken cancellationToken = default)
            => Task.FromResult(Links.FirstOrDefault(link => link.LinkId == linkId));

        public Task<(SharedLinkAccessResult Result, SharedLink Link)> UseAsync(string linkId, string token, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No tool uses a shared link.");

        public Task RevokeAsync(SharedLink link, CancellationToken cancellationToken = default)
        {
            Revoked.Add(link.LinkId);
            link.RevokedUtc ??= UtcNow;

            return Task.CompletedTask;
        }

        public Task DeleteAsync(SharedLink link, CancellationToken cancellationToken = default)
        {
            Deleted.Add(link.LinkId);
            Links.Remove(link);

            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<SharedLink> Links, int Count)> ListAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
        {
            var matches = Links
                .Where(link => search is null || link.Title.Contains(search, StringComparison.Ordinal) || link.SourceName.Contains(search, StringComparison.Ordinal))
                .OrderByDescending(link => link.CreatedUtc)
                .ToList();

            return Task.FromResult<(IReadOnlyList<SharedLink>, int)>((matches.Skip(skip).Take(take).ToList(), matches.Count));
        }

        public Task<IReadOnlyList<SharedLink>> ListAsync(string kind, string resourceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No tool lists the links of one resource.");
    }

    private sealed class PassThroughLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => new PassThroughLocalizer();

        public IStringLocalizer Create(string baseName, string location) => new PassThroughLocalizer();
    }

    private sealed class PassThroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
