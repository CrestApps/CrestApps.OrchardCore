using System.Text.Json.Nodes;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Services;

public sealed class SimpleAIProfileManager : IAIProfileManager
{
    private readonly ISession _session;
    public SimpleAIProfileManager(ISession session)
    {
        _session = session;
    }

    public async ValueTask<AIProfile> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return await _session.Query<AIProfile, AIProfileIndex>(x => x.ItemId == id).FirstOrDefaultAsync();
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAllAsync()
    {
        var items = await _session.Query<AIProfile, AIProfileIndex>().ListAsync();

        return items;
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type)
    {
        var all = await _session.Query<AIProfile, AIProfileIndex>().ListAsync();

        return all.Where(p => p.Type == type);
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var items = await _session.Query<AIProfile, AIProfileIndex>(x => x.Source == source).ListAsync();

        return items;
    }

    public async ValueTask<IEnumerable<AIProfile>> FindBySourceAsync(string source)
    {
        return await GetAsync(source);
    }

    public async ValueTask<AIProfile> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var all = await _session.Query<AIProfile, AIProfileIndex>(x => x.Name == name).ListAsync();

        return all.FirstOrDefault();
    }

    public async ValueTask<AIProfile> GetAsync(string name, string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(source);

        var items = await _session.Query<AIProfile, AIProfileIndex>(x => x.Name == name && x.Source == source).ListAsync();

        return items.FirstOrDefault();
    }

    public async ValueTask CreateAsync(AIProfile model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrEmpty(model.ItemId))
        {
            model.ItemId = Guid.NewGuid().ToString("N");
        }

        if (model.CreatedUtc == default)
        {
            model.CreatedUtc = DateTime.UtcNow;
        }

        await _session.SaveAsync(model);
        await _session.SaveChangesAsync();
    }

    public async ValueTask UpdateAsync(AIProfile model, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        await _session.SaveAsync(model);
        await _session.SaveChangesAsync();
    }

    public async ValueTask<bool> DeleteAsync(AIProfile model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _session.Delete(model);
        await _session.SaveChangesAsync();

        return true;
    }

    public ValueTask<AIProfile> NewAsync(JsonNode data = null)
    {
        var profile = new AIProfile
        {
            ItemId = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
        };

        return ValueTask.FromResult(profile);
    }

    public ValueTask<AIProfile> NewAsync(string source, JsonNode data = null)
    {
        var profile = new AIProfile
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Source = source,
            CreatedUtc = DateTime.UtcNow,
        };

        return ValueTask.FromResult(profile);
    }

    public ValueTask<ValidationResultDetails> ValidateAsync(AIProfile model)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult("Name is required.", [nameof(model.Name)]));
        }

        return ValueTask.FromResult(result);
    }

    public async ValueTask<PageResult<AIProfile>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<AIProfile, AIProfileIndex>();

        if (!string.IsNullOrEmpty(context?.Source))
        {
            query = query.Where(x => x.Source == context.Source);
        }

        var skip = (page - 1) * pageSize;
        var total = await query.CountAsync();
        var items = await query.Skip(skip).Take(pageSize).ListAsync();

        return new PageResult<AIProfile>
        {
            Count = total,
            Entries = items.ToArray(),
        };
    }
}
