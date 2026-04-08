using CrestApps.Core.Data.EntityCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Data.EntityCore;

public sealed class CrestAppsEntityDbContext : DbContext
{
    private readonly EntityCoreDataStoreOptions _options;

    public CrestAppsEntityDbContext(
        DbContextOptions<CrestAppsEntityDbContext> options,
        IOptions<EntityCoreDataStoreOptions> storeOptions)
        : base(options)
    {
        _options = storeOptions.Value;
    }

    public DbSet<CatalogRecord> CatalogRecords => Set<CatalogRecord>();

    public DbSet<AIChatSessionRecord> AIChatSessionRecords => Set<AIChatSessionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tablePrefix = _options.TablePrefix ?? string.Empty;

        modelBuilder.Entity<CatalogRecord>(entity =>
        {
            entity.ToTable($"{tablePrefix}CatalogRecords");
            entity.HasKey(x => new { x.EntityType, x.ItemId });
            entity.Property(x => x.EntityType).IsRequired();
            entity.Property(x => x.ItemId).HasMaxLength(26);
            entity.Property(x => x.Name);
            entity.Property(x => x.DisplayText);
            entity.Property(x => x.Source);
            entity.Property(x => x.SessionId);
            entity.Property(x => x.ChatInteractionId);
            entity.Property(x => x.ReferenceId);
            entity.Property(x => x.ReferenceType);
            entity.Property(x => x.AIDocumentId);
            entity.Property(x => x.UserId);
            entity.Property(x => x.Type);
            entity.Property(x => x.Payload).IsRequired();

            entity.HasIndex(x => new { x.EntityType, x.Name });
            entity.HasIndex(x => new { x.EntityType, x.Source });
            entity.HasIndex(x => new { x.EntityType, x.SessionId });
            entity.HasIndex(x => new { x.EntityType, x.ChatInteractionId });
            entity.HasIndex(x => new { x.EntityType, x.ReferenceId, x.ReferenceType });
            entity.HasIndex(x => new { x.EntityType, x.AIDocumentId });
            entity.HasIndex(x => new { x.EntityType, x.UserId, x.Name });
            entity.HasIndex(x => new { x.EntityType, x.Type });
        });

        modelBuilder.Entity<AIChatSessionRecord>(entity =>
        {
            entity.ToTable($"{tablePrefix}AIChatSessions");
            entity.HasKey(x => x.SessionId);
            entity.Property(x => x.SessionId).HasMaxLength(26);
            entity.Property(x => x.ProfileId);
            entity.Property(x => x.Title);
            entity.Property(x => x.UserId);
            entity.Property(x => x.ClientId);
            entity.Property(x => x.Payload).IsRequired();

            entity.HasIndex(x => x.ProfileId);
            entity.HasIndex(x => x.LastActivityUtc);
        });
    }
}
