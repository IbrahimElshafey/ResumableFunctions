using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;

namespace ResumableFunctions.Handler.DataAccess;
internal sealed class WaitsDataContext : DbContext
{
    private readonly ILogger<WaitsDataContext> _logger;
    private readonly IResumableFunctionsSettings _settings;
    private readonly ValueComparer<object> _closureComparer = new ValueComparer<object>(
           (o1, o2) =>
               JsonConvert.SerializeObject(o1, PrivateDataResolver.Settings) == JsonConvert.SerializeObject(o2, PrivateDataResolver.Settings),
           oToHash =>
               oToHash == null ? 0 : JsonConvert.SerializeObject(oToHash, PrivateDataResolver.Settings).GetHashCode(),
           oToSnapShot =>
               JsonConvert.DeserializeObject<object>(JsonConvert.SerializeObject(oToSnapShot, PrivateDataResolver.Settings)));
    public WaitsDataContext(
        ILogger<WaitsDataContext> logger,
        IResumableFunctionsSettings settings,
        IDistributedLockProvider lockProvider) : base(settings.WaitsDbConfig.Options)
    {
        _logger = logger;
        _settings = settings;
        try
        {
            if (Database.IsInMemory())
            {
                Database.EnsureCreated();
            }
            else
            {
                var database = Database.GetDbConnection().Database;
                using var loc = lockProvider.AcquireLock(database);
                Database.EnsureCreated();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when call [Database.EnsureCreated()] for [WaitsDataContext]");
        }
    }

    public DbSet<PrivateData> PrivateData { get; set; }
    public DbSet<LockState> ScanLocks { get; set; }
    public DbSet<ResumableFunctionState> FunctionStates { get; set; }

    public DbSet<MethodIdentifier> MethodIdentifiers { get; set; }
    public DbSet<WaitMethodIdentifier> WaitMethodIdentifiers { get; set; }
    public DbSet<ResumableFunctionIdentifier> ResumableFunctionIdentifiers { get; set; }

    public DbSet<WaitEntity> Waits { get; set; }
    public DbSet<MethodWaitEntity> MethodWaits { get; set; }
    public DbSet<WaitTemplate> WaitTemplates { get; set; }
    public DbSet<MethodsGroup> MethodsGroups { get; set; }
    public DbSet<FunctionWaitEntity> FunctionWaits { get; set; }

    public DbSet<PushedCall> PushedCalls { get; set; }
    public DbSet<WaitProcessingRecord> WaitProcessingRecords { get; set; }


    public DbSet<ServiceData> ServicesData { get; set; }

    public DbSet<LogRecord> Logs { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureResumableFunctionState(modelBuilder.Entity<ResumableFunctionState>());
        ConfigureMethodIdentifier(modelBuilder);
        ConfigureWaitProcessingRecords(modelBuilder);
        ConfigureServiceData(modelBuilder.Entity<ServiceData>());
        ConfigureWaits(modelBuilder);
        ConfigureRuntimeClosures(modelBuilder.Entity<PrivateData>());
        ConfigureMethodWaitTemplate(modelBuilder);
        ConfigurConcurrencyToken(modelBuilder);
        ConfigurSoftDeleteFilter(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }


    private void ConfigurSoftDeleteFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WaitEntity>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<ResumableFunctionState>().HasQueryFilter(p => !p.IsDeleted);
    }

    private void ConfigurConcurrencyToken(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IEntityWithUpdate).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder
                    .Entity(entityType.ClrType)
                    .Property<string>(nameof(IEntityWithUpdate.ConcurrencyToken))
                    .IsConcurrencyToken();
            }
        }
    }

    private void ConfigureServiceData(EntityTypeBuilder<ServiceData> serviceDataBuilder)
    {
        serviceDataBuilder.HasIndex(x => x.AssemblyName);
    }

    private void ConfigureWaitProcessingRecords(ModelBuilder modelBuilder)
    {
        var waitProcessingRecordBuilder = modelBuilder.Entity<WaitProcessingRecord>();
        waitProcessingRecordBuilder.HasIndex(x => x.PushedCallId, "WaitForPushedCall_Idx");
    }

    private void ConfigureRuntimeClosures(EntityTypeBuilder<PrivateData> closureTable)
    {
        closureTable.HasKey(x => x.Id);

        closureTable
            .Property(x => x.Value)
            .HasConversion(
            x => JsonConvert.SerializeObject(x, PrivateDataResolver.Settings),
            y => JsonConvert.DeserializeObject(y));
        closureTable
            .Property(x => x.Value).Metadata.SetValueComparer(_closureComparer);

        closureTable
           .HasMany(x => x.ClosureLinkedWaits)
           .WithOne(wait => wait.ClosureData)
           .HasForeignKey(x => x.ClosureDataId)
           .HasConstraintName("FK_Closure_Waits");

        closureTable
         .HasMany(x => x.LocalsLinkedWaits)
         .WithOne(wait => wait.Locals)
         .HasForeignKey(x => x.LocalsId)
         .HasConstraintName("FK_Locals_Waits");
    }
    private void ConfigureWaits(ModelBuilder modelBuilder)
    {
        var waitBuilder = modelBuilder.Entity<WaitEntity>();
        waitBuilder
            .HasMany(x => x.ChildWaits)
            .WithOne(wait => wait.ParentWait)
            .HasForeignKey(x => x.ParentWaitId)
            .HasConstraintName("FK_ChildWaits_For_Wait");

        waitBuilder
            .HasIndex(x => x.Status)
            .HasFilter($"{nameof(WaitEntity.Status)} = {(int)WaitStatus.Waiting}")
            .HasDatabaseName("Index_ActiveWaits");


        var methodWaitBuilder = modelBuilder.Entity<MethodWaitEntity>();
        methodWaitBuilder
          .Property(x => x.MethodToWaitId)
          .HasColumnName(nameof(MethodWaitEntity.MethodToWaitId));
        methodWaitBuilder
          .Property(x => x.CallId)
          .HasColumnName(nameof(MethodWaitEntity.CallId));

        //methodWaitBuilder.HasOne(x => x.Template).WithMany(x => x.Waits);
        //methodWaitBuilder.HasOne(x => x.MethodToWait).WithMany(x => x.Waits);



        modelBuilder.Entity<WaitsGroupEntity>()
           .Property(mw => mw.GroupMatchFuncName)
           .HasColumnName(nameof(WaitsGroupEntity.GroupMatchFuncName));

        modelBuilder.Ignore<TimeWaitEntity>();
    }

    private void ConfigureMethodWaitTemplate(ModelBuilder modelBuilder)
    {
        var waitTemplateBuilder = modelBuilder.Entity<WaitTemplate>();
        waitTemplateBuilder.Property(x => x.MatchExpressionValue);
        waitTemplateBuilder.Property(x => x.CallMandatoryPartExpressionValue);
        waitTemplateBuilder.Property(x => x.InstanceMandatoryPartExpressionValue);
        waitTemplateBuilder
           .HasIndex(x => x.IsActive)
           .HasFilter($"{nameof(WaitTemplate.IsActive)} = 1")
           .HasDatabaseName("Index_ActiveWaits");
        modelBuilder.Entity<MethodsGroup>()
            .HasMany(x => x.WaitTemplates)
            .WithOne(x => x.MethodGroup)
            .HasForeignKey(x => x.MethodGroupId)
            .HasConstraintName("WaitTemplates_ForMethodGroup");
    }

    private void ConfigureMethodIdentifier(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResumableFunctionIdentifier>()
           .HasMany(x => x.ActiveFunctionsStates)
           .WithOne(wait => wait.ResumableFunctionIdentifier)
           .HasForeignKey(x => x.ResumableFunctionIdentifierId)
           .HasConstraintName("FK_FunctionsStates_For_ResumableFunction");

        modelBuilder.Entity<ResumableFunctionIdentifier>()
            .HasMany(x => x.WaitsCreatedByFunction)
            .WithOne(wait => wait.RequestedByFunction)
            .OnDelete(DeleteBehavior.Restrict)
            .HasForeignKey(x => x.RequestedByFunctionId)
            .HasConstraintName("FK_Waits_In_ResumableFunction");

        modelBuilder.Entity<MethodsGroup>()
            .HasMany(x => x.WaitRequestsForGroup)
            .WithOne(mw => mw.MethodGroupToWait)
            .OnDelete(DeleteBehavior.Restrict)
            .HasForeignKey(x => x.MethodGroupToWaitId)
            .HasConstraintName("FK_WaitsRequestsForGroup");

        modelBuilder.Entity<MethodsGroup>()
          .HasMany(x => x.WaitMethodIdentifiers)
          .WithOne(waitMid => waitMid.MethodGroup)
          .OnDelete(DeleteBehavior.Restrict)
          .HasForeignKey(x => x.MethodGroupId)
          .HasConstraintName("FK_Group_WaitMethodIdentifiers");

        modelBuilder.Entity<MethodsGroup>()
           .HasIndex(x => x.MethodGroupUrn)
            .HasDatabaseName("Index_MethodGroupUniqueUrn")
            .IsUnique();
    }

    private void ConfigureResumableFunctionState(EntityTypeBuilder<ResumableFunctionState> stateTypeBuilder)
    {
        stateTypeBuilder
            .HasMany(x => x.Waits)
            .WithOne(wait => wait.FunctionState)
            .HasForeignKey(x => x.FunctionStateId)
            .HasConstraintName("FK_WaitsForFunctionState");
    }


    public async Task<int> SaveChangesDirectly(
        CancellationToken cancellationToken = new CancellationToken())
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            if (Database.IsInMemory())
            {
                BeforeSaveData();
                var result = await base.SaveChangesAsync(cancellationToken);
                await SaveEntitiesLogs(cancellationToken);
                await AfterChangesSaved(cancellationToken);
                return result;
            }
            else
            {
                using var transaction = Database.BeginTransaction();
                BeforeSaveData();
                var result = await base.SaveChangesAsync(cancellationToken);
                await SaveEntitiesLogs(cancellationToken);
                await AfterChangesSaved(cancellationToken);
                transaction.Commit();
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when save changes.");
            throw;
        }

    }

    private async Task AfterChangesSaved(CancellationToken cancellationToken)
    {
        var entitiesWithAfterChanges =
                ChangeTracker
                .Entries()
                .Where(x => x.Entity is IAfterChangesSaved)
                .Select(x => (IAfterChangesSaved)x.Entity)
                .ToList();
        foreach (var item in entitiesWithAfterChanges)
        {
            item.AfterChangesSaved();
        }
        await base.SaveChangesAsync(cancellationToken);
    }

    private void BeforeSaveData()
    {
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            SetDates(entry);
            SetConcurrencyToken(entry);
            SetServiceId(entry);
            NeverUpdateFirstWait(entry);
            HandleSoftDelete(entry);
            ExcludeFalseAddEntries(entry);
            OnSaveEntity(entry);
        }
    }


    private void SetConcurrencyToken(EntityEntry entityEntry)
    {
        switch (entityEntry.State)
        {
            case EntityState.Modified when entityEntry.Entity is IEntityWithUpdate:
            case EntityState.Added when entityEntry.Entity is IEntityWithUpdate:
                entityEntry.Property(nameof(IEntityWithUpdate.ConcurrencyToken)).CurrentValue = Guid.NewGuid().ToString();
                break;
        }
    }

    private void OnSaveEntity(EntityEntry entry)
    {
        if (entry.Entity is IBeforeSaveEntity saveEntity)
            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
                saveEntity.BeforeSave();

    }

    private void SetServiceId(EntityEntry entry)
    {
        if (entry.Entity is not IEntity || entry.State != EntityState.Added) return;

        dynamic entityInService = entry.Entity;
        switch (entityInService.ServiceId)
        {
            case > 0 when entityInService.ServiceId != _settings.CurrentServiceId:
                _logger.LogError(
                    $"Try to change [ServiceId] for entity [{entityInService.GetType().Name}:{entityInService.Id}]" +
                    $" from {entityInService.ServiceId} to {_settings.CurrentServiceId}");
                break;
            case > 0:
                return;
            default:
                Entry(entityInService).Property(nameof(IEntity<int>.ServiceId)).CurrentValue = _settings.CurrentServiceId;
                break;
        }
    }



    private async Task SaveEntitiesLogs(CancellationToken cancellationToken)
    {
        try
        {
            var entitiesWithLog =
                ChangeTracker
                .Entries()
                .Where(x => x.Entity is IObjectWithLog entityWithLog && entityWithLog.Logs.Any())
                .Select(x => (IObjectWithLog)x.Entity)
                .ToList();
            foreach (var entity in entitiesWithLog)
            {
                entity.Logs.ForEach(logRecord =>
                {
                    if (logRecord.EntityId is <= 0 or null)
                        if (entity is IEntity<int> entityInt)
                            logRecord.EntityId = entityInt.Id;
                        else if (entity is IEntity<long> entityLong)
                            logRecord.EntityId = entityLong.Id;
                    logRecord.ServiceId = _settings.CurrentServiceId;
                });
                Logs.AddRange(entity.Logs.Where(x => x.Id == 0));
            }
            await base.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when save entity logs.");
        }
    }

    private void NeverUpdateFirstWait(EntityEntry entityEntry)
    {
        if (entityEntry.Entity is not WaitEntity { IsFirst: true, IsRoot: true, IsDeleted: false } wait) return;

        if (entityEntry.State == EntityState.Modified)
            entityEntry.State = EntityState.Unchanged;

        if (wait.FunctionState == null) return;
        var functionState = Entry(wait.FunctionState);
        if (functionState.State == EntityState.Modified)
            functionState.State = EntityState.Unchanged;
    }

    private void HandleSoftDelete(EntityEntry entityEntry)
    {
        switch (entityEntry.State)
        {
            case EntityState.Deleted when entityEntry.Entity is IEntityWithDelete:
                entityEntry.Property(nameof(IEntityWithDelete.IsDeleted)).CurrentValue = true;
                entityEntry.State = EntityState.Modified;
                break;
        }
    }
    private void SetDates(EntityEntry entityEntry)
    {
        switch (entityEntry.State)
        {
            case EntityState.Modified when entityEntry.Entity is IEntityWithUpdate:
                //to be deleted
                var creationDateProp1 = entityEntry.Property(nameof(IEntity.Created));
                if (DateTime.Compare((DateTime)creationDateProp1.CurrentValue, default) == 0)
                    creationDateProp1.CurrentValue = DateTime.UtcNow;

                entityEntry.Property(nameof(IEntityWithUpdate.Modified)).CurrentValue = DateTime.UtcNow;
                entityEntry.Property(nameof(IEntityWithUpdate.ConcurrencyToken)).CurrentValue = Guid.NewGuid().ToString();
                break;
            case EntityState.Added:
                var creationDateProp = entityEntry.Property(nameof(IEntity.Created));
                if (DateTime.Compare((DateTime)creationDateProp.CurrentValue, default) == 0)
                    creationDateProp.CurrentValue = DateTime.UtcNow;
                if (entityEntry.Entity is IEntityWithUpdate)
                    entityEntry.Property(nameof(IEntityWithUpdate.ConcurrencyToken)).CurrentValue = Guid.NewGuid().ToString();
                break;
        }
    }

    //todo:delete
    private void ExcludeFalseAddEntries(EntityEntry entry)
    {
        if (entry.State == EntityState.Added && entry.IsKeySet)
            entry.State = EntityState.Unchanged;
    }
}