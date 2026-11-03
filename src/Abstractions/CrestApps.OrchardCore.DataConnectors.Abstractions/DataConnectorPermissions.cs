using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// The permissions of data connections and connected data sets.
/// </summary>
public static class DataConnectorPermissions
{
    /// <summary>
    /// Create, change and delete connections, including their secrets.
    /// </summary>
    public static readonly Permission ManageDataConnections = new("ManageDataConnections", LocalizationSource.Create("Manage data connections and their credentials", typeof(DataConnectorPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Create, change and delete connected data sets.
    /// </summary>
    public static readonly Permission ManageConnectedDataSets = new("ManageConnectedDataSets", LocalizationSource.Create("Manage connected data sets", typeof(DataConnectorPermissions)));

    /// <summary>
    /// Refresh imported data sets on demand.
    /// </summary>
    public static readonly Permission RefreshConnectedDataSets = new("RefreshConnectedDataSets", LocalizationSource.Create("Refresh imported data sets", typeof(DataConnectorPermissions)), [ManageConnectedDataSets]);

    /// <summary>
    /// Read every connected data set, live or imported.
    /// </summary>
    public static readonly Permission ReadAllConnectedDataSets = new("ReadAllConnectedDataSets", LocalizationSource.Create("Read every connected data set", typeof(DataConnectorPermissions)));

    private static readonly Permission _readDataSetTemplate = new("ReadConnectedDataSet_{0}", LocalizationSource.Create("Read the connected data set - {0}", typeof(DataConnectorPermissions)), [ReadAllConnectedDataSets]);

    /// <summary>
    /// Creates the permission to read one connected data set, live or imported.
    /// </summary>
    /// <param name="dataSetName">The data set's technical name.</param>
    /// <returns>The permission, implied by <see cref="ReadAllConnectedDataSets"/>.</returns>
    public static Permission CreateReadPermission(string dataSetName)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetName);

        // The description is the template's text with a name filled in at run time, so it cannot be extracted.
#pragma warning disable OCLOC103 // The value of the localization source is not a compile-time constant
        return new Permission(
            string.Format(_readDataSetTemplate.Name, dataSetName),
            LocalizationSource.Create(string.Format(_readDataSetTemplate.Description?.Value, dataSetName), typeof(DataConnectorPermissions)),
            _readDataSetTemplate.ImpliedBy ?? []);
#pragma warning restore OCLOC103
    }
}
