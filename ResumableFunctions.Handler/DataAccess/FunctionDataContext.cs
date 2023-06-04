﻿using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection.Emit;
using System.Text.Json;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Hangfire;
using Medallion.Threading;
using Newtonsoft.Json.Linq;

namespace ResumableFunctions.Handler.DataAccess;
public class FunctionDataContext : DbContext
{
    private readonly ILogger<FunctionDataContext> _logger;
    private readonly BinaryToObjectConverter _binarytConverter;

    public FunctionDataContext(
        ILogger<FunctionDataContext> logger,
        IResumableFunctionsSettings settings,
        IDistributedLockProvider lockProvider,
        BinaryToObjectConverter binarytConverter) : base(settings.WaitsDbConfig.Options)
    {
        _logger = logger;
        _binarytConverter = binarytConverter;
        try
        {
            using (lockProvider.AcquireLock(Database.GetDbConnection().Database))
            {
                Database.EnsureCreated();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error when call `Database.EnsureCreated()` for `FunctionDataContext`", ex);
        }
    }

    public DbSet<ResumableFunctionState> FunctionStates { get; set; }

    public DbSet<MethodIdentifier> MethodIdentifiers { get; set; }
    public DbSet<WaitMethodIdentifier> WaitMethodIdentifiers { get; set; }
    public DbSet<ResumableFunctionIdentifier> ResumableFunctionIdentifiers { get; set; }

    public DbSet<Wait> Waits { get; set; }
    public DbSet<MethodWait> MethodWaits { get; set; }
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
        ConfigurConcurrencyToken(modelBuilder);
        ConfigurSoftDeleteFilter(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void ConfigurSoftDeleteFilter(ModelBuilder modelBuilder)
    {
        //todo:query filter by interface https://haacked.com/archive/2019/07/29/query-filter-by-interface/
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

        entityTypeBuilder
            .HasMany(x => x.Waits)
            .WithOne(wait => wait.Service)
            .HasForeignKey(x => x.ServiceId)
            .HasConstraintName("FK_Waits_For_Service");
    }

    private void ConfigurePushedCalls(ModelBuilder modelBuilder)
    {
        var pushedCallBuilder = modelBuilder.Entity<PushedCall>();
        pushedCallBuilder
            .Property(x => x.Data)
            .HasConversion(
            obj => _binarytConverter.ConvertToBinary(obj),
            bytes => _binarytConverter.ConvertToObject<InputOutput>(bytes));

        pushedCallBuilder
           .Property(x => x.MethodData)
           .HasConversion(
            obj => _binarytConverter.ConvertToBinary(obj),
            bytes => _binarytConverter.ConvertToObject<MethodData>(bytes));

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
        modelBuilder.Entity<Wait>()
            .HasMany(x => x.ChildWaits)
            .WithOne(wait => wait.ParentWait)
            .HasForeignKey(x => x.ParentWaitId)
            .HasConstraintName("FK_ChildWaits_For_Wait");

        modelBuilder.Entity<Wait>()
            .Property(x => x.ExtraData)
            .HasConversion(
            obj => _binarytConverter.ConvertToBinary(obj),
            bytes => _binarytConverter.ConvertToObject<WaitExtraData>(bytes));

        modelBuilder.Entity<MethodWait>()
          .Property(mw => mw.MatchIfExpressionValue)
          .HasColumnName(nameof(MethodWait.MatchIfExpressionValue));

        modelBuilder.Entity<MethodWait>()
            .Property(mw => mw.SetDataExpressionValue)
            .HasColumnName(nameof(MethodWait.SetDataExpressionValue));

        modelBuilder.Entity<WaitsGroup>()
           .Property(mw => mw.GroupMatchExpressionValue)
           .HasColumnName(nameof(WaitsGroup.GroupMatchExpressionValue));

        modelBuilder.Ignore<ReplayRequest>();
        modelBuilder.Ignore<TimeWait>();
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
          .WithOne(waitMid => waitMid.ParentMethodGroup)
          .OnDelete(DeleteBehavior.Restrict)
          .HasForeignKey(x => x.ParentMethodGroupId)
          .HasConstraintName("FK_Group_WaitMethodIdentifiers");

        modelBuilder.Entity<WaitMethodIdentifier>()
        .HasMany(x => x.WaitsRequestsForMethod)
        .WithOne(mw => mw.MethodToWait)
        .OnDelete(DeleteBehavior.Restrict)
        .HasForeignKey(x => x.MethodToWaitId)
        .HasConstraintName("FK_WaitsRequestsForMethod");

        modelBuilder.Entity<MethodsGroup>()
           .HasIndex(x => x.MethodGroupUrn)
            .HasDatabaseName("Index_MethodGroupUniqueUrn")
            .IsUnique(true);
    }

    private void ConfigureResumableFunctionState(EntityTypeBuilder<ResumableFunctionState> entityTypeBuilder)
    {
        entityTypeBuilder
            .HasMany(x => x.Waits)
            .WithOne(wait => wait.FunctionState)
            .HasForeignKey(x => x.FunctionStateId);

        entityTypeBuilder
           .Property(x => x.StateObject)
           .HasConversion(
            _binarytConverter.ToBinary,
            _binarytConverter.ToObject);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<Type>()
            .HaveConversion<TypeToStringConverter>();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var entries = ChangeTracker.Entries().ToList();
            SetDates(entries);
            NeverUpdateFirstWait(entries);
            HandleSoftDelete(entries);
            ExcludeFalseAddEntries(entries);
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

    private async Task SetWaitsPaths(CancellationToken cancellationToken)
    {
        try
        {
            var waits =
                ChangeTracker
                .Entries()
                    .Where(x => x.Entity is Wait)
                    .Select(x => (Wait)x.Entity)
                    .ToList();
            foreach (var wait in waits)
            {
                wait.Path = GetWaitPath(wait);
            }
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
                    .Where(x => x.Entity is ObjectWithLog entityWithLog && entityWithLog.Logs.Any())
                    .Select(x => (ObjectWithLog)x.Entity)
                    .ToList();
            foreach (var entity in entitiesWithLog)
            {
                entity.Logs.ForEach(logRecord =>
                {
                    if (logRecord.EntityId <= 0 || logRecord.EntityId == null)
                        logRecord.EntityId = ((IEntity)entity).Id;
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

    private void NeverUpdateFirstWait(List<EntityEntry> entries)
    {
        var waitEntries = entries
            .Where(x =>
                x.Entity is Wait wait &&
                wait.IsFirst &&
                wait.IsDeleted == false);
        foreach (var entityEntry in waitEntries)
        {
            if (entityEntry.State == EntityState.Modified)
                entityEntry.State = EntityState.Unchanged;
            var wait = entityEntry.Entity as Wait;
            if (Entry(wait.FunctionState).State == EntityState.Modified)
                Entry(wait.FunctionState).State = EntityState.Unchanged;
        }

    }

    private void HandleSoftDelete(List<EntityEntry> entries)
    {
        foreach (var entityEntry in entries)
        {
            switch (entityEntry.State)
            {
                case EntityState.Deleted when entityEntry.Entity is IEntityWithDelete:
                    entityEntry.Property(nameof(IEntityWithDelete.IsDeleted)).CurrentValue = true;
                    entityEntry.State = EntityState.Modified;
                    break;
            }
        }
    }

    private void SetDates(List<EntityEntry> entries)
    {
        foreach (var entityEntry in entries)
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
    }

    private void ExcludeFalseAddEntries(List<EntityEntry> entries)
    {
        entries
            .Where(x => x.State == EntityState.Added && x.IsKeySet)
            .ToList()
            .ForEach(x => x.State = EntityState.Unchanged);
    }
}