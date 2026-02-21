namespace ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;
public interface IEntityWithUpdate
{
    DateTime Modified { get; }
    string ConcurrencyToken { get; }
}