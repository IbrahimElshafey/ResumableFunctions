using System.Linq.Expressions;

namespace ResumableFunctions.Handler.Core.Abstraction
{
    //todo: Candidate for Inbox/Outbox pattern
    public interface IBackgroundProcess
    {
        public string Enqueue(Expression<Func<Task>> methodCall);
        bool Delete(string jobId);
        string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay);
        string Schedule(Expression<Action> methodCall, TimeSpan delay);
        void AddOrUpdateRecurringJob<TClass>(
            string recurringJobId,
            Expression<Func<TClass, Task>> methodCall,
            string cronExpression);
    }
}