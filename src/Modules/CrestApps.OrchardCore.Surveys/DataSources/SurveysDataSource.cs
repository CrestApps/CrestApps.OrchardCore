using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// The Surveys data source for reports and data pipelines: surveys, versions, questions, responses, answers, category
/// scores, page visits and invitations, plus one wide data set per published survey, named
/// <c>SurveyResponses_{surveyName}</c>, with a field per question. The per-survey data sets are listed at run time, so it
/// implements <see cref="IDataSource"/> itself rather than deriving from <see cref="RecordDataSource"/>. Its data sets hold
/// back the rows of anonymous surveys until enough responses pass the read's own conditions, so a Report Builder run
/// shares a read of them only between queries with the same conditions (<see cref="IReportSharedReadPolicy"/>).
/// </summary>
public sealed class SurveysDataSource : IDataSource, IReportSharedReadPolicy
{
    private readonly ISurveysDataSet[] _dataSets;
    private readonly SurveyWideDataSetFactory _wide;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The static data sets.</param>
    /// <param name="wide">Builds the per-survey data sets.</param>
    internal SurveysDataSource(IEnumerable<ISurveysDataSet> dataSets, SurveyWideDataSetFactory wide)
    {
        _dataSets = dataSets.ToArray();
        _wide = wide;
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "Surveys";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveysDataSource>("Surveys");

    /// <summary>
    /// Gets the data source's description.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveysDataSource>("Surveys, their versions and questions, responses, answers, scores and page visits, and one data set per survey with a field per question.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        var descriptors = new List<DataSetDescriptor>();

        foreach (var dataSet in _dataSets)
        {
            if (await dataSet.CanReadAsync(context, cancellationToken))
            {
                descriptors.Add(dataSet.Descriptor);
            }
        }

        descriptors.AddRange(await _wide.DescribeAsync(context, cancellationToken));

        return descriptors;
    }

    /// <inheritdoc />
    public async Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        var found = await FindReadableAsync(dataSet, context, cancellationToken);

        return found is null
            ? null
            : new DataSetSchema
            {
                DataSet = found.Descriptor,
                Fields = [.. found.Fields],
            };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dataSet = await FindReadableAsync(query.DataSet, query.Context, cancellationToken);

        if (dataSet is null)
        {
            yield break;
        }

        await foreach (var batch in dataSet.ReadAsync(query, cancellationToken))
        {
            yield return batch;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Never: a read that passed the small-group rule of <see cref="Core.Services.SurveyGroupGate"/> on its conditions
    /// would otherwise serve a query with more conditions, such as a dashboard click, filtered after the gate.
    /// </remarks>
    public bool ServesNarrowerReads(string dataSet)
        => false;

    private async Task<IRecordDataSet> FindReadableAsync(string name, DataSourceContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var dataSet = (IRecordDataSet)_dataSets.FirstOrDefault(candidate => string.Equals(candidate.Descriptor.Name, name, StringComparison.Ordinal))
            ?? await _wide.FindAsync(name, context, cancellationToken);

        return dataSet is not null && await dataSet.CanReadAsync(context, cancellationToken) ? dataSet : null;
    }
}
