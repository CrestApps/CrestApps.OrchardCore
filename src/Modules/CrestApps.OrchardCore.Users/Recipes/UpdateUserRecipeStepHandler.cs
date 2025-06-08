using System.Text.Json.Nodes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Users.Recipes;

public sealed class UpdateUserRecipeStepHandler : NamedRecipeStepHandler
{
    private const int DefaultBatchSize = 250;

    private int _batchSize = DefaultBatchSize;

    private readonly ISession _session;

    public UpdateUserRecipeStepHandler(ISession session)
        : base("IndexUsers")
    {
        _session = session;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var step = context.Step.ToObject<UpdateUserRecipeStepModel>();

        if (step.BatchSize.HasValue && step.BatchSize.Value > 0)
        {
            // Even if the user provided a batch size greater  than 1000, we batch with max of 1000 records.
            _batchSize = Math.Min(1000, step.BatchSize.Value);
        }

        await GetTotalAsync(step);

        var currentBatch = 0;

        while (true)
        {
            var users = await GetNextBatchAsync(step, currentBatch);

            var totalUsers = users.Count();

            if (totalUsers == 0)
            {
                break;
            }

            currentBatch += Math.Min(_batchSize, totalUsers);

            foreach (var user in users)
            {
                await _session.SaveAsync(user);
            }

            await _session.SaveChangesAsync();
        }
    }

    private Task<IEnumerable<User>> GetNextBatchAsync(UpdateUserRecipeStepModel step, int currentBatch)
    {
        if (step.IncludeDisabledUsers)
        {
            return _session.Query<User, UserIndex>()
                .OrderBy(u => u.DocumentId)
                .Skip(currentBatch)
                .Take(_batchSize)
                .ListAsync();
        }

        return _session.Query<User, UserIndex>(u => u.IsEnabled)
            .OrderBy(x => x.DocumentId)
            .Skip(currentBatch)
            .Take(_batchSize)
            .ListAsync();
    }

    private Task<int> GetTotalAsync(UpdateUserRecipeStepModel step)
    {
        if (step.IncludeDisabledUsers)
        {
            return _session.Query<User>().CountAsync();
        }

        return _session.Query<User, UserIndex>(u => u.IsEnabled).CountAsync();
    }

    private sealed class UpdateUserRecipeStepModel
    {
        public bool IncludeDisabledUsers { get; set; }

        public int? BatchSize { get; set; }
    }
}
