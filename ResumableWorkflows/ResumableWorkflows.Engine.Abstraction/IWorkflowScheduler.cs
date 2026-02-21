using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    public interface IWorkflowScheduler
    {
        string EnqueueJob(Expression<Func<Task>> methodCall);
        bool Delete(string jobId);
        string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay);
        string Schedule(Expression<Action> methodCall, TimeSpan delay);
        void AddOrUpdateRecurringJob<TClass>(
            string recurringJobId,
            Expression<Func<TClass, Task>> methodCall,
            string cronExpression);
    }
}
