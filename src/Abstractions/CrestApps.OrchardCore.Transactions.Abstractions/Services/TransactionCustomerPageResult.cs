namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// A customer document built by <see cref="ITransactionCustomerPageService"/>, or the reason it was not.
/// </summary>
/// <typeparam name="TModel">The type of the document's model.</typeparam>
public sealed class TransactionCustomerPageResult<TModel>
    where TModel : class
{
    private TransactionCustomerPageResult(TransactionCustomerPageStatus status, TModel model)
    {
        Status = status;
        Model = model;
    }

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public TransactionCustomerPageStatus Status { get; }

    /// <summary>
    /// Gets the document's model, when <see cref="Status"/> is <see cref="TransactionCustomerPageStatus.Found"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public TModel Model { get; }

    /// <summary>
    /// Gets a value indicating whether the document was built.
    /// </summary>
    public bool Succeeded
        => Status == TransactionCustomerPageStatus.Found;

    /// <summary>
    /// Creates the result of a document that was built.
    /// </summary>
    /// <param name="model">The document's model.</param>
    public static TransactionCustomerPageResult<TModel> Found(TModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new TransactionCustomerPageResult<TModel>(TransactionCustomerPageStatus.Found, model);
    }

    /// <summary>
    /// Creates the result for a viewer who may not see their transactions at all.
    /// </summary>
    public static TransactionCustomerPageResult<TModel> Forbidden()
        => new(TransactionCustomerPageStatus.Forbidden, null);

    /// <summary>
    /// Creates the result for a document that does not exist, belongs to someone else, or cannot be built.
    /// </summary>
    public static TransactionCustomerPageResult<TModel> NotFound()
        => new(TransactionCustomerPageStatus.NotFound, null);
}
