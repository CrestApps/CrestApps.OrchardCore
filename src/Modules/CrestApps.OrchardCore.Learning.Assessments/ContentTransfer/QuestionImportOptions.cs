using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Lists.Models;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;

/// <summary>
/// The question import options kept on the import: the question bank the questions go into.
/// </summary>
public sealed class QuestionImportOptionsPart
{
    /// <summary>
    /// Gets or sets the question bank, or <see langword="null"/> for no bank.
    /// </summary>
    public string QuestionBankId { get; set; }
}

/// <summary>
/// Adds the question bank to the import page of a question type. The person must be allowed to edit the bank.
/// </summary>
internal sealed class QuestionImportOptionsDisplayDriver : DisplayDriver<ImportContent>
{
    private const int MaxBanks = 500;

    private readonly QuestionTypeOptions _questionTypes;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public QuestionImportOptionsDisplayDriver(
        IOptions<QuestionTypeOptions> questionTypes,
        IContentManager contentManager,
        ISession session,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<QuestionImportOptionsDisplayDriver> stringLocalizer)
    {
        _questionTypes = questionTypes.Value;
        _contentManager = contentManager;
        _session = session;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ImportContent model, BuildEditorContext context)
    {
        if (!_questionTypes.IsQuestion(model.ContentTypeName))
        {
            return null;
        }

        return Initialize<QuestionImportOptionsViewModel>("QuestionImportOptions_Edit", async viewModel =>
        {
            viewModel.QuestionBankId = model.GetOrCreate<QuestionImportOptionsPart>().QuestionBankId;
            viewModel.Banks = await EditableBanksAsync();
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ImportContent model, UpdateEditorContext context)
    {
        if (!_questionTypes.IsQuestion(model.ContentTypeName))
        {
            return null;
        }

        var viewModel = new QuestionImportOptionsViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        string bankId = null;

        if (!string.IsNullOrWhiteSpace(viewModel.QuestionBankId))
        {
            var bank = await _contentManager.GetAsync(viewModel.QuestionBankId, VersionOptions.Latest);

            if (bank is null ||
                bank.ContentType != AssessmentsConstants.ContentTypes.QuestionBank ||
                !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CommonPermissions.EditContent, bank))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(viewModel.QuestionBankId), S["Choose a question bank you may edit."]);
            }
            else
            {
                bankId = bank.ContentItemId;
            }
        }

        model.Alter<QuestionImportOptionsPart>(options => options.QuestionBankId = bankId);

        return Edit(model, context);
    }

    private async Task<IList<SelectListItem>> EditableBanksAsync()
    {
        var banks = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == AssessmentsConstants.ContentTypes.QuestionBank && index.Latest)
            .OrderBy(index => index.DisplayText)
            .Take(MaxBanks)
            .ListAsync();
        var user = _httpContextAccessor.HttpContext?.User;
        var items = new List<SelectListItem>();

        foreach (var bank in banks)
        {
            if (await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, bank))
            {
                items.Add(new SelectListItem(bank.DisplayText ?? bank.ContentItemId, bank.ContentItemId));
            }
        }

        return items;
    }
}

/// <summary>
/// Puts each imported question in the question bank chosen on the import page.
/// </summary>
internal sealed class QuestionBankImportHandler : IContentImportHandler
{
    private readonly QuestionTypeOptions _questionTypes;

    public QuestionBankImportHandler(IOptions<QuestionTypeOptions> questionTypes)
    {
        _questionTypes = questionTypes.Value;
    }

    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(ImportContentContext context)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>([]);

    public Task ImportAsync(ContentImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bankId = context.Entry?.GetOrCreate<QuestionImportOptionsPart>().QuestionBankId;

        if (string.IsNullOrEmpty(bankId) || context.ContentItem is null || !_questionTypes.IsQuestion(context.ContentItem.ContentType))
        {
            return Task.CompletedTask;
        }

        context.ContentItem.Alter<ContainedPart>(part =>
        {
            if (!string.Equals(part.ListContentItemId, bankId, StringComparison.Ordinal))
            {
                part.Order = 0;
            }

            part.ListContentItemId = bankId;
            part.ListContentType = AssessmentsConstants.ContentTypes.QuestionBank;
        });

        return Task.CompletedTask;
    }

    public Task ExportAsync(ContentExportContext context)
        => Task.CompletedTask;
}

/// <summary>
/// Matches a row to the question imported before with the same source identifier into the same question bank, by
/// writing that question's id in the row's <c>ContentItemId</c> column, so importing the file again updates the
/// questions instead of adding them a second time.
/// </summary>
internal sealed class QuestionSourceRowFilter : IContentImportRowFilter
{
    private const string SourceIdentifierColumn = "QuestionPart_SourceIdentifier";
    private const string ContentItemIdColumn = "ContentItemId";

    private readonly QuestionTypeOptions _questionTypes;
    private readonly ISession _session;
    private string _bankId;

    public QuestionSourceRowFilter(IOptions<QuestionTypeOptions> questionTypes, ISession session)
    {
        _questionTypes = questionTypes.Value;
        _session = session;
    }

    public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _bankId = context.Entry?.GetOrCreate<QuestionImportOptionsPart>().QuestionBankId;

        return Task.FromResult(context.ContentTypeDefinition is not null && _questionTypes.IsQuestion(context.ContentTypeDefinition.Name));
    }

    public async Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rows = context.Rows
            .Where(row => row.Columns.Contains(SourceIdentifierColumn) && row.Columns.Contains(ContentItemIdColumn))
            .Select(row => (Row: row.Row, Identifier: QuestionSourceIndexProvider.Truncate(row.Row[SourceIdentifierColumn]?.ToString()?.Trim())))
            .Where(row => !string.IsNullOrEmpty(row.Identifier) && string.IsNullOrWhiteSpace(row.Row[ContentItemIdColumn]?.ToString()))
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        var identifiers = rows.Select(row => row.Identifier).Distinct(StringComparer.Ordinal).ToArray();
        var bankId = _bankId;
        var matches = string.IsNullOrEmpty(bankId)
            ? await _session.QueryIndex<QuestionSourceIndex>(index => index.SourceIdentifier.IsIn(identifiers) && index.QuestionBankId == null).ListAsync(context.CancellationToken)
            : await _session.QueryIndex<QuestionSourceIndex>(index => index.SourceIdentifier.IsIn(identifiers) && index.QuestionBankId == bankId).ListAsync(context.CancellationToken);
        var byIdentifier = matches
            .GroupBy(match => match.SourceIdentifier, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(match => match.Id).First().ContentItemId, StringComparer.Ordinal);

        foreach (var (row, identifier) in rows)
        {
            if (byIdentifier.TryGetValue(identifier, out var contentItemId))
            {
                row[ContentItemIdColumn] = contentItemId;
            }
        }
    }

    public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
        => Task.FromResult(false);
}
