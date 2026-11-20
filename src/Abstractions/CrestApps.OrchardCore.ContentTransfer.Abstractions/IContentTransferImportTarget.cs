using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// A kind of record a file can be imported into: a content type, or the records of another store, such as skills. A
/// target says who may import it and which columns its file has; ContentTransfer does the rest (the upload, the
/// background run with its progress, pause and resume, and the file of failed rows).
/// </summary>
public interface IContentTransferImportTarget
{
    /// <summary>
    /// Gets the name an import is filed under (<see cref="ContentTransferEntry.ContentType"/>): the content type's
    /// technical name, or, for any other target, a name with a dot such as <c>Skills.Skill</c>, which no content type
    /// can have.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name people see, such as <c>Skills</c>.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets whether the target is a content type, imported by the content item pipeline (its import handlers, row
    /// filters and file reshapers).
    /// </summary>
    bool IsContentType { get; }

    /// <summary>
    /// Tells whether a person may import this target. Every target keeps its own check.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    Task<bool> AuthorizeAsync(ClaimsPrincipal user, IAuthorizationService authorizationService);

    /// <summary>
    /// Lists the columns of the target's file, which the import page describes and the template holds.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The columns.</returns>
    Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A target that is not a content type: it validates and saves a batch of rows to its own store, and fails each row it
/// cannot save. Register it with <c>services.AddContentTransferImportTarget&lt;T&gt;()</c>.
/// </summary>
public interface IContentTransferRecordImporter : IContentTransferImportTarget
{
    /// <summary>
    /// Validates and saves the rows of a batch. A row it cannot save is failed with
    /// <see cref="ContentTransferImportRow.Fail"/>, and is listed in the file of failed rows; every other row counts as
    /// imported. The batch is saved with the import's progress once this returns.
    /// </summary>
    /// <param name="batch">The rows.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the batch is saved.</returns>
    Task ImportAsync(ContentTransferImportBatch batch, CancellationToken cancellationToken = default);
}
