using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
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
/// Matches a row to the question imported before from the same source file with the same source identifier into the
/// same question bank, by writing that question's id in the row's <c>ContentItemId</c> column, so importing the file
/// again updates the questions instead of adding them a second time. Identifiers such as <c>item1</c> repeat across
/// packages, so nothing is matched without a chosen bank, a question from another file is never matched, and the person
/// who imports must be allowed to edit the question.
/// </summary>
internal sealed class QuestionSourceRowFilter : IContentImportRowFilter
{
    private const string SourceIdentifierColumn = "QuestionPart_SourceIdentifier";
    private const string ContentItemIdColumn = "ContentItemId";

    private const string SourceFileColumn = "QuestionPart_SourceFile";

    private readonly QuestionTypeOptions _questionTypes;
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IContentTransferFileFormatProvider> _formats;
    private readonly IServiceProvider _services;
    private string _bankId;
    private ContentTransferEntry _entry;
    private System.Security.Claims.ClaimsPrincipal _importer;
    private bool _importerRead;

    public QuestionSourceRowFilter(
        IOptions<QuestionTypeOptions> questionTypes,
        ISession session,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IEnumerable<IContentTransferFileFormatProvider> formats,
        IServiceProvider services)
    {
        _questionTypes = questionTypes.Value;
        _session = session;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _formats = formats;
        _services = services;
    }

    public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _bankId = context.Entry?.GetOrCreate<QuestionImportOptionsPart>().QuestionBankId;
        _entry = context.Entry;

        return Task.FromResult(context.ContentTypeDefinition is not null && _questionTypes.IsQuestion(context.ContentTypeDefinition.Name));
    }

    public async Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Without a chosen bank an identifier would match questions of every package on the site.
        if (string.IsNullOrEmpty(_bankId))
        {
            return;
        }

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
        var matches = await _session.QueryIndex<QuestionSourceIndex>(index => index.SourceIdentifier.IsIn(identifiers) && index.QuestionBankId == bankId).ListAsync(context.CancellationToken);
        var importer = await ImporterAsync();

        if (importer is null)
        {
            return;
        }

        foreach (var (row, identifier) in rows)
        {
            var sourceFile = SourceFileOf(row);

            foreach (var match in matches.Where(match => match.SourceIdentifier == identifier).OrderBy(match => match.Id))
            {
                var question = await _contentManager.GetAsync(match.ContentItemId, VersionOptions.Latest);

                if (question is null ||
                    !string.Equals(question.Get<QuestionPart>(nameof(QuestionPart))?.SourceFile, sourceFile, StringComparison.Ordinal) ||
                    !await _authorizationService.AuthorizeAsync(importer, CommonPermissions.EditContent, question))
                {
                    continue;
                }

                row[ContentItemIdColumn] = question.ContentItemId;

                break;
            }
        }
    }

    // The source file a question imported from this row keeps: the QTI reader's item path within the uploaded file
    // (as the QTI import handler writes it), or the column as it is for any other file.
    private string SourceFileOf(System.Data.DataRow row)
    {
        var path = row.Table.Columns.Contains(SourceFileColumn) ? row[SourceFileColumn]?.ToString() : null;

        return QtiMediaImportHandler.ReadByQti(_formats, _entry) ? QtiQuestionImportHandler.SourceFileOf(_entry?.UploadedFileName, path) : path;
    }

    // The person who queued the import: the background import runs without a request.
    private async Task<System.Security.Claims.ClaimsPrincipal> ImporterAsync()
    {
        if (_importerRead)
        {
            return _importer;
        }

        _importerRead = true;

        return _importer = await CrestApps.OrchardCore.Learning.Core.Services.LearningUserPrincipals.CreateAsync(_services, _entry?.Owner);
    }

    public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
        => Task.FromResult(false);
}
