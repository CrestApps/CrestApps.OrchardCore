namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A transaction as its owner sees it, one row of their own statement.
/// </summary>
/// <remarks>
/// The administration report lists the same records, but a customer is offered other actions (pay, view) and
/// never sees who else owns what, so their rows are built from a type of their own.
/// </remarks>
/// <param name="Transaction">The customer's transaction.</param>
public sealed record CustomerTransaction(Transaction Transaction);
