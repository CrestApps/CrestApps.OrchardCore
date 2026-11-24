using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Records;

/// <summary>
/// A record type an owner module exposes to the generic record tools (<c>listRecords</c>, <c>getRecord</c>,
/// <c>saveRecord</c> and <c>deleteRecord</c>) under a short name the model passes as <c>kind</c>. A kind is registered
/// with <see cref="AIRecordServiceCollectionExtensions.AddAIRecordKind{T}"/>; the tools read the kinds from
/// <see cref="AIRecordKindOptions"/>.
/// </summary>
public abstract class AIRecordKind
{
    private readonly HashSet<string> _secretFields = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="AIRecordKind"/> class.
    /// </summary>
    /// <param name="name">The camelCase name of the kind.</param>
    /// <param name="recordType">The type of the records.</param>
    private protected AIRecordKind(string name, Type recordType)
    {
        Name = name;
        RecordType = recordType;
    }

    /// <summary>
    /// Gets the camelCase name the model passes as <c>kind</c>, such as <c>taxCategory</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the records.
    /// </summary>
    public Type RecordType { get; }

    /// <summary>
    /// Gets the short title of the kind, such as "Tax category".
    /// </summary>
    public string Title { get; internal set; }

    /// <summary>
    /// Gets the description the model reads to choose the kind: what a record is and what it is used for.
    /// </summary>
    public string Description { get; internal set; }

    /// <summary>
    /// Gets the permission the caller needs to create, change and delete records, the one the admin pages check.
    /// </summary>
    public Permission Permission { get; internal set; }

    /// <summary>
    /// Gets the permission the caller needs to list and read records, when it differs from <see cref="Permission"/>.
    /// </summary>
    public Permission ReadPermission { get; internal set; }

    /// <summary>
    /// Gets the permission checked for reads: <see cref="ReadPermission"/>, or <see cref="Permission"/> when no read
    /// permission was given.
    /// </summary>
    public Permission EffectiveReadPermission
        => ReadPermission ?? Permission;

    /// <summary>
    /// Gets the settings group of the editor the writes replay, or <see langword="null"/> for the default editor.
    /// </summary>
    public string EditorGroupId { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the kind can be written. Writes replay the record's admin editor through its
    /// display drivers; a kind registered with <see cref="AIRecordKindBuilder{T}.ReadOnly"/> is only listed and read.
    /// </summary>
    public bool CanWrite { get; internal set; } = true;

    /// <summary>
    /// Gets the name of the dedicated AI tool that saves records of a read-only kind, such as
    /// <c>saveContactCenterQueue</c>, or <see langword="null"/> when there is none. <c>saveRecord</c> names it when it
    /// refuses the kind, and the kind's description in the record tools' schema mentions it. Set with
    /// <see cref="AIRecordKindBuilder{T}.ReadOnly"/>.
    /// </summary>
    public string SaveToolName { get; internal set; }

    /// <summary>
    /// Gets the name of the dedicated AI tool that deletes records of a read-only kind, such as
    /// <c>deleteContactCenterRecord</c>, or <see langword="null"/> when there is none. <c>deleteRecord</c> names it when
    /// it refuses the kind, and the kind's description in the record tools' schema mentions it. Set with
    /// <see cref="AIRecordKindBuilder{T}.ReadOnly"/>.
    /// </summary>
    public string DeleteToolName { get; internal set; }

    /// <summary>
    /// Gets the names of the editor fields that hold a secret. Such a field is never returned (only whether it is set)
    /// and never accepted. Fields whose name contains password, secret, token, API key, connection string or private
    /// key are treated as secrets without being listed.
    /// </summary>
    public IReadOnlyCollection<string> SecretFields
        => _secretFields;

    /// <summary>
    /// Calls the visitor with the typed kind, so a caller works with the record type without reflection.
    /// </summary>
    /// <typeparam name="TResult">The type of the visitor's result.</typeparam>
    /// <param name="visitor">The visitor.</param>
    /// <returns>The visitor's result.</returns>
    public abstract TResult Accept<TResult>(IAIRecordKindVisitor<TResult> visitor);

    internal void AddSecretField(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _secretFields.Add(name.Trim());
        }
    }
}
