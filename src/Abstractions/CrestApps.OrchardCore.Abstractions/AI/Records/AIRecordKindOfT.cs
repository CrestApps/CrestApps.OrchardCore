using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Records;

/// <summary>
/// A record kind of a given record type: how its records are found, listed, created, validated, saved and deleted,
/// and the allow-listed fields the tools return. Every operation defaults to the record's
/// <see cref="ICatalogManager{T}"/>, the manager the admin pages use, and can be replaced through
/// <see cref="AIRecordKindBuilder{T}"/>.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public sealed class AIRecordKind<T> : AIRecordKind
    where T : CatalogItem, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIRecordKind{T}"/> class with the catalog manager defaults.
    /// </summary>
    /// <param name="name">The camelCase name of the kind.</param>
    internal AIRecordKind(string name)
        : base(name, typeof(T))
    {
        UseManager<ICatalogManager<T>>();
        Copy = CopyRecord;
    }

    /// <summary>
    /// Gets the allow-list projection the tools return for a record in <c>listRecords</c> and <c>getRecord</c>. It
    /// builds a new object from named fields; the record and its <c>Properties</c> are never serialized.
    /// </summary>
    public Func<T, object> Projection { get; internal set; }

    /// <summary>
    /// Gets how a record is found by its ID; <see langword="null"/> when there is none.
    /// </summary>
    public Func<IServiceProvider, string, CancellationToken, ValueTask<T>> FindAsync { get; internal set; }

    /// <summary>
    /// Gets how a new, unsaved record is made, before its editor is replayed.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, ValueTask<T>> NewAsync { get; internal set; }

    /// <summary>
    /// Gets how a page of records is listed, filtered by an optional text.
    /// </summary>
    public Func<IServiceProvider, AIRecordQuery, CancellationToken, ValueTask<PageResult<T>>> ListAsync { get; internal set; }

    /// <summary>
    /// Gets how a record is validated after its editor bound the values: the rules of the record's handlers, which
    /// the admin pages check before they save.
    /// </summary>
    public Func<IServiceProvider, T, CancellationToken, ValueTask<ValidationResultDetails>> ValidateAsync { get; internal set; }

    /// <summary>
    /// Gets how a validated record is saved; the boolean says whether the record is new.
    /// </summary>
    public Func<IServiceProvider, T, bool, CancellationToken, ValueTask> SaveAsync { get; internal set; }

    /// <summary>
    /// Gets how a record is deleted; <see langword="false"/> when nothing was deleted.
    /// </summary>
    public Func<IServiceProvider, T, CancellationToken, ValueTask<bool>> DeleteAsync { get; internal set; }

    /// <summary>
    /// Gets the optional check run before a record is deleted. It returns why the record cannot be deleted (what still
    /// uses it), or <see langword="null"/> when it can.
    /// </summary>
    public Func<IServiceProvider, T, CancellationToken, ValueTask<string>> InUseAsync { get; internal set; }

    /// <summary>
    /// Gets how a record is copied, so a dry run or a refused save never changes the instance a store handed out.
    /// </summary>
    public Func<T, T> Copy { get; internal set; }

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAIRecordKindVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.Visit(this);
    }

    /// <summary>
    /// Finds, lists, makes, validates, saves and deletes the records through a catalog manager service, such as
    /// <c>INamedCatalogManager&lt;T&gt;</c> when the admin pages use that one.
    /// </summary>
    /// <typeparam name="TManager">The manager service type.</typeparam>
    internal void UseManager<TManager>()
        where TManager : ICatalogManager<T>
    {
        FindAsync = (services, id, cancellationToken) => GetManager<TManager>(services).FindByIdAsync(id, cancellationToken);
        NewAsync = (services, cancellationToken) => GetManager<TManager>(services).NewAsync(null, cancellationToken);
        ListAsync = (services, query, cancellationToken) => GetManager<TManager>(services).PageAsync(query.Page, query.PageSize, new QueryContext
        {
            Name = query.Text,
            Sorted = true,
        }, cancellationToken);
        ValidateAsync = (services, record, cancellationToken) => GetManager<TManager>(services).ValidateAsync(record, cancellationToken);
        SaveAsync = (services, record, isNew, cancellationToken) => SaveWithAsync<TManager>(services, record, isNew, cancellationToken);
        DeleteAsync = (services, record, cancellationToken) => GetManager<TManager>(services).DeleteAsync(record, cancellationToken);
    }

    private static ICatalogManager<T> GetManager<TManager>(IServiceProvider services)
        where TManager : ICatalogManager<T>
        => services.GetRequiredService<TManager>();

    private static async ValueTask SaveWithAsync<TManager>(IServiceProvider services, T record, bool isNew, CancellationToken cancellationToken)
        where TManager : ICatalogManager<T>
    {
        var manager = GetManager<TManager>(services);

        if (isNew)
        {
            await manager.CreateAsync(record, cancellationToken);

            return;
        }

        await manager.UpdateAsync(record, null, cancellationToken);
    }

    private static T CopyRecord(T record)
    {
        if (record is null)
        {
            return null;
        }

        if (record is ICloneable<T> cloneable)
        {
            return cloneable.Clone();
        }

        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(record, JOptions.Default), JOptions.Default);
    }
}
