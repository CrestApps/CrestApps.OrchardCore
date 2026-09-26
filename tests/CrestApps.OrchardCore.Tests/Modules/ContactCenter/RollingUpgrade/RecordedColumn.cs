using System.Globalization;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.RollingUpgrade;

/// <summary>
/// A column definition captured from a migration step, independent of whether the step executed.
/// </summary>
internal sealed class RecordedColumn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordedColumn"/> class.
    /// </summary>
    /// <param name="name">The column name.</param>
    /// <param name="dbType">The CLR type the column stores.</param>
    /// <param name="length">The declared length, when the column declares one.</param>
    /// <param name="isNotNull">Whether the column refuses null.</param>
    /// <param name="isUnique">Whether the column is declared unique.</param>
    /// <param name="defaultValue">The declared default, when the column declares one.</param>
    public RecordedColumn(
        string name,
        Type dbType,
        int? length,
        bool isNotNull,
        bool isUnique,
        object defaultValue)
    {
        Name = name;
        DbType = dbType;
        Length = length;
        IsNotNull = isNotNull;
        IsUnique = isUnique;
        DefaultValue = defaultValue;
    }

    /// <summary>
    /// Gets the column name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the CLR type the column stores.
    /// </summary>
    public Type DbType { get; }

    /// <summary>
    /// Gets the declared length, when the column declares one.
    /// </summary>
    public int? Length { get; }

    /// <summary>
    /// Gets a value indicating whether the column refuses null. This is the property that decides whether a
    /// node running the previous version can still write the table after the upgrade, because that node
    /// supplies no value for a column it does not know about.
    /// </summary>
    public bool IsNotNull { get; }

    /// <summary>
    /// Gets a value indicating whether the column is declared unique.
    /// </summary>
    public bool IsUnique { get; }

    /// <summary>
    /// Gets the declared default, when the column declares one.
    /// </summary>
    public object DefaultValue { get; }

    /// <summary>
    /// Renders the definition so two migration paths that declare the same column differently produce
    /// different text and therefore fail a comparison.
    /// </summary>
    /// <returns>A stable description of the column definition.</returns>
    public string Describe()
    {
        var defaultValue = DefaultValue is null
            ? "none"
            : Convert.ToString(DefaultValue, CultureInfo.InvariantCulture);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Name}:{DbType.Name}:len={Length?.ToString(CultureInfo.InvariantCulture) ?? "none"}:notnull={IsNotNull}:unique={IsUnique}:default={defaultValue}");
    }
}
