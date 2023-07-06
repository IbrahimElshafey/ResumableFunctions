﻿using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.DataAccess;
internal sealed class WaitsDataContext : DbContext
{
    private readonly ILogger<WaitsDataContext> _logger;
    private readonly IResumableFunctionsSettings _settings;

    public WaitsDataContext(
        ILogger<WaitsDataContext> logger,
        IResumableFunctionsSettings settings,
        IDistributedLockProvider lockProvider) : base(settings.WaitsDbConfig.Options)
    {
        _logger = logger;
        _settings = settings;
        try
        {
            var database = Database.GetDbConnection().Database;
            settings.CurrentDbName = database;
            using var loc = lockProvider.AcquireLock(database);
            Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when call `Database.EnsureCreated()` for `WaitsDataContext`");
        }
    }

    public DbSet<ScanState> ScanStates { get; set; }
    public DbSet<ResumableFunctionState> FunctionStates { get; set; }

    public DbSet<MethodIdentifier> MethodIdentifiers { get; set; }
    public DbSet<WaitMethodIdentifier> WaitMethodIdentifiers { get; set; }
    public DbSet<ResumableFunctionIdentifier> ResumableFunctionIdentifiers { get; set; }

    public DbSet<Wait> Waits { get; set; }
    public DbSet<MethodWait> MethodWaits { get; set; }
    public DbSet<WaitTemplate> WaitTemplates { get; set; }
    public DbSet<MethodsGroup> MethodsGroups { get; set; }
    public DbSet<FunctionWait> FunctionWaits { get; set; }

    public DbSet<PushedCall> PushedCalls { get; set; }
    public DbSet<WaitForCall> WaitsForCalls { get; set; }


    public DbSet<ServiceData> ServicesData { get; set; }

    public DbSet<LogRecord> Logs { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureResumableFunctionState(modelBuilder.Entity<ResumableFunctionState>());
        ConfigureMethodIdentifier(modelBuilder);
        ConfigurePushedCalls(modelBuilder);
        ConfigureServiceData(modelBuilder.Entity<ServiceData>());
        ConfigureWaits(modelBuilder);
        ConfigureMethodWaitTemplate(modelBuilder);
        ConfigurConcurrencyToken(modelBuilder);
        ConfigurSoftDeleteFilter(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }



    private void ConfigurSoftDeleteFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wait>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<ResumableFunctionState>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<PushedCall>().HasQueryFilter(p => !p.IsDeleted);
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

    private void ConfigureServiceData(EntityTypeBuilder<ServiceData> entityTypeBuilder)
    {
        //entityTypeBuilder.Property(x => x.Modified).HasDefaultValue(DateTime.MinValue);
        entityTypeBuilder.HasIndex(x => x.AssemblyName);

        //entityTypeBuilder
        //    .HasMany(x => x.Waits)
        //    .WithOne(wait => wait.Service)
        //    .HasForeignKey(x => x.ServiceId)
        //    .HasConstraintName("FK_Waits_For_Service");
    }

    private void ConfigurePushedCalls(ModelBuilder modelBuilder)
    {
        var pushedCallBuilder = modelBuilder.Entity<PushedCall>();

        pushedCallBuilder
           .HasMany(x => x.WaitsForCall)
           .WithOne(waitForCall => waitForCall.PushedCall)
           .HasForeignKey(waitForCall => waitForCall.PushedCallId)
           .HasConstraintName("FK_Waits_For_Call");

        var waitForCallBuilder = modelBuilder.Entity<WaitForCall>();
        waitForCallBuilder.HasIndex(x => x.ServiceId, "WaitForCall_ServiceId_Idx");
        waitForCallBuilder.HasIndex(x => x.FunctionId, "WaitForCall_FunctionId_Idx");
    }

    private void ConfigureWaits(ModelBuilder modelBuilder)
    {
        var waitBuilder = modelBuilder.Entity<Wait>();
        waitBuilder
            .HasMany(x => x.ChildWaits)
            .WithOne(wait => wait.ParentWait)
            .HasForeignKey(x => x.ParentWaitId)
            .HasConstraintName("FK_ChildWaits_For_Wait");

        waitBuilder
            .HasIndex(x => x.Status)
            .HasFilter($"{nameof(Wait.Status)} = {(int)WaitStatus.Waiting}")
            .HasDatabaseName("Index_ActiveWaits");

        var methodWaitBuilder = modelBuilder.Entity<MethodWait>();
        methodWaitBuilder
          .Property(x => x.MethodToWaitId)
          .HasColumnName(nameof(MethodWait.MethodToWaitId));
        methodWaitBuilder
          .Property(x => x.CallId)
          .HasColumnName(nameof(MethodWait.CallId));

        modelBuilder.Entity<WaitsGroup>()
           .Property(mw => mw.GroupMatchExpressionValue)
           .HasColumnName(nameof(WaitsGroup.GroupMatchExpressionValue));

        modelBuilder.Ignore<ReplayRequest>();
        modelBuilder.Ignore<TimeWait>();
    }

    private void ConfigureMethodWaitTemplate(ModelBuilder modelBuilder)
    {
        var entityBuilder = modelBuilder.Entity<WaitTemplate>();
        entityBuilder.Property(x => x.MatchExpressionValue);
        entityBuilder.Property(x => x.CallMandatoryPartExpressionValue);
        entityBuilder.Property(x => x.InstanceMandatoryPartExpressionValue);
        entityBuilder.Property(x => x.SetDataExpressionValue);
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


        //modelBuilder.Entity<WaitMethodIdentifier>()
        //.HasMany(x => x.WaitsRequestsForMethod)
        //.WithOne(mw => mw.MethodToWait)
        //.OnDelete(DeleteBehavior.Restrict)
        //.HasForeignKey(x => x.MethodToWaitId)
        //.HasConstraintName("FK_WaitsRequestsForMethod");

        modelBuilder.Entity<MethodsGroup>()
           .HasIndex(x => x.MethodGroupUrn)
            .HasDatabaseName("Index_MethodGroupUniqueUrn")
            .IsUnique();
    }

    private void ConfigureResumableFunctionState(EntityTypeBuilder<ResumableFunctionState> entityTypeBuilder)
    {
        entityTypeBuilder
            .HasMany(x => x.Waits)
            .WithOne(wait => wait.FunctionState)
            .HasForeignKey(x => x.FunctionStateId)
            .HasConstraintName("FK_WaitsForFunctionState");
    }


    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            BeforeSaveData();
            var result = await base.SaveChangesAsync(cancellationToken);
            await SetWaitsPaths(cancellationToken);
            await SaveEntitiesLogs(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when save changes.");
            throw;
        }

    }

    private void BeforeSaveData()
    {
        var entries = ChangeTracker.Entries().ToList();
        foreach (var entry in entries)
        {
            SetDates(entry);
            SetServiceId(entry);
            NeverUpdateFirstWait(entry);
            HandleSoftDelete(entry);
            ExcludeFalseAddEntries(entry);
            OnSaveEntity(entry);
        }
    }

    private void OnSaveEntity(EntityEntry entry)
    {
        if (entry.Entity is IOnSaveEntity saveEntity)
            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
                saveEntity.OnSave();

    }

    private void SetServiceId(EntityEntry entry)
    {
        if (entry.Entity is not IEntity entityInService || entry.State != EntityState.Added) return;

        switch (entityInService.ServiceId)
        {
            case > 0 when entityInService.ServiceId != _settings.CurrentServiceId:
                _logger.LogError(
                    $"Try to change `ServiceId` for entity `{entityInService.GetType().Name}:{entityInService.Id}`" +
                    $" from {entityInService.ServiceId} to {_settings.CurrentServiceId}");
                break;
            case > 0:
                return;
            default:
                Entry(entityInService).Property(x => x.ServiceId).CurrentValue = _settings.CurrentServiceId;
                break;
        }
    }

    private async Task SetWaitsPaths(CancellationToken cancellationToken)
    {
        try
        {
            var waitsWithNoPath =
                ChangeTracker
                .Entries()
                .Where(x => x.Entity is Wait { Path: null })
                .Select(x => (Wait)x.Entity)
                .ToList();
            foreach (var wait in waitsWithNoPath) 
                wait.Path = GetWaitPath(wait);

            await base.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when set waits paths.");
        }

        string GetWaitPath(Wait wait)
        {
            var path = $"/{wait.Id}";
            while (wait?.ParentWait != null)
            {
                path = $"/{wait.ParentWaitId}" + path;
                wait = wait.ParentWait;
            }
            return path;
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
                        logRecord.EntityId = ((IEntity)entity).Id;
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
        if (entityEntry.Entity is not Wait { IsFirst: true, IsRootNode: true, IsDeleted: false } wait) return;

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
                entityEntry.Property(nameof(IEntityWithUpdate.Modified)).CurrentValue = DateTime.Now;
                entityEntry.Property(nameof(IEntityWithUpdate.ConcurrencyToken)).CurrentValue = Guid.NewGuid().ToString();
                break;
            case EntityState.Added when entityEntry.Entity is IEntityWithUpdate:
                entityEntry.Property(nameof(IEntityWithUpdate.Created)).CurrentValue = DateTime.Now;
                entityEntry.Property(nameof(IEntityWithUpdate.ConcurrencyToken)).CurrentValue = Guid.NewGuid().ToString();
                break;
            case EntityState.Added when entityEntry.Entity is IEntity:
                entityEntry.Property(nameof(IEntityWithUpdate.Created)).CurrentValue = DateTime.Now;
                break;
        }
    }

    private void ExcludeFalseAddEntries(EntityEntry entry)
    {
        if (entry.State == EntityState.Added && entry.IsKeySet)
            entry.State = EntityState.Unchanged;
    }
}