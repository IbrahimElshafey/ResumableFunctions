using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Core;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;
using System.ComponentModel.DataAnnotations.Schema;
using System.Dynamic;

namespace ResumableFunctions.Handler.InOuts.Entities;

public abstract class WaitEntity : IEntity<long>, IEntityWithUpdate, IEntityWithDelete, IBeforeSaveEntity, IAfterChangesSaved
{

    public long Id { get; internal set; }
    public DateTime Created { get; internal set; }
    public string Name { get; internal set; }
    public WaitStatus Status { get; internal set; } = WaitStatus.Waiting;
    public bool IsFirst { get; internal set; }
    public bool WasFirst { get; internal set; }
    public int StateAfterWait { get; internal set; }
    public bool IsRoot { get; internal set; }
    public int RootFunctionId { get; internal set; }

    [NotMapped]
    public dynamic ExtraData { get; internal set; }
    public byte[] ExtraDataValue { get; internal set; }

    public int? ServiceId { get; internal set; }

    public WaitType WaitType { get; internal set; }
    public DateTime Modified { get; internal set; }
    public string ConcurrencyToken { get; internal set; }
    public bool IsDeleted { get; internal set; }

    /// <summary>
    /// The state object of current resumable function container.
    /// </summary>
    internal ResumableFunctionState FunctionState { get; set; }

    internal int FunctionStateId { get; set; }


    /// <summary>
    ///  The resumable function that initiated/created/requested the wait.
    ///  May be resumable function or sub resumable function.
    /// </summary>
    internal ResumableFunctionIdentifier RequestedByFunction { get; set; }

    internal int RequestedByFunctionId { get; set; }

    /// <summary>
    ///     If not null this means that wait requested by a sub function
    ///     not
    /// </summary>
    internal WaitEntity ParentWait { get; set; }
    internal long? ParentWaitId { get; set; }

    internal List<WaitEntity> ChildWaits { get; set; } = new();

    /// <summary>
    /// Local variables in method at the wait point where current wait requested
    /// It's the runner class serialized we can rename this to RunnerState
    /// </summary>
    public PrivateData Locals { get; internal set; }
    public long? LocalsId { get; internal set; }


    public PrivateData ClosureData { get; internal set; }
    public long? ClosureDataId { get; internal set; }

    [NotMapped]
    public object ClosureObject { get; private set; }


    public string Path { get; internal set; }

    [NotMapped]
    internal ResumableFunctionsContainer CurrentFunction { get; set; }

    internal bool CanBeParent => this is FunctionWaitEntity || this is WaitsGroupEntity;
    internal long? CallId { get; set; }
    public int InCodeLine { get; internal set; }
    public string CallerName { get; internal set; }

    //MethodWait.AfterMatch(Action<TInput, TOutput>)
    //MethodWait.WhenCancel(Action cancelAction)
    //WaitsGroup.MatchIf(Func<WaitsGroup, bool>)
    //The method may update closure  
    protected object InvokeCallback(string methodFullName, params object[] parameters)
    {
        var parts = methodFullName.Split('#');
        var methodName = parts[1];
        var className = parts[0];//may be the RFContainer calss or closure class

        //is local method in current function class
        object rfClassInstance = CurrentFunction;
        var rfClassType = rfClassInstance.GetType();
        var localMethodInfo = rfClassType.GetMethod(methodName, CoreExtensions.DeclaredWithinTypeFlags());
        if (localMethodInfo != null)
            return localMethodInfo.Invoke(rfClassInstance, parameters);

        //is lambda method (closure exist)
        var closureType = rfClassType.Assembly.GetType(className);
        if (closureType != null)
        {
            var closureMethodInfo = closureType.GetMethod(methodName, CoreExtensions.DeclaredWithinTypeFlags());
            var closureInstance = ClosureData?.AsType(closureType) ?? Activator.CreateInstance(closureType);

            SetClosureCallerFunctionClass(closureInstance);

            if (closureMethodInfo != null)
            {
                var result = closureMethodInfo.Invoke(closureInstance, parameters);

                //todo:Review create closure
                if (ClosureData != null)
                    ClosureData.Value = closureInstance;
                return result;
            }
        }

        throw new NullReferenceException(
            $"Can't find method [{methodName}] in class [{rfClassType.Name}]");
    }

    private void SetClosureCallerFunctionClass(object closureInstance)
    {
        if (closureInstance == null) return;

        var closureType = closureInstance.GetType();
        bool notClosureClass = !closureType.Name.StartsWith(Constants.CompilerClosurePrefix);
        if (notClosureClass) return;

        var thisField = closureType
            .GetFields()
            .FirstOrDefault(x => x.Name.EndsWith(Constants.CompilerCallerSuffix) && x.FieldType == CurrentFunction.GetType());
        if (thisField != null)
        {
            thisField.SetValue(closureInstance, CurrentFunction);
        }
        // may be multiple closures in same IAsyncEnumrable where clsoure C1 is field in closure C2 and so on.
        else
        {
            var parentClosuresFields = closureType
                .GetFields()
                .Where(x => x.FieldType.Name.StartsWith(Constants.CompilerClosurePrefix));
            foreach (var closureField in parentClosuresFields)
            {
                SetClosureCallerFunctionClass(closureField.GetValue(closureInstance));
            }
        }
    }

    internal async Task<WaitEntity> GetNextWait()
    {
        if (CurrentFunction == null)
            LoadUnmappedProps();
        var functionRunner = new FunctionRunner(this);
        //if (functionRunner.ResumableFunctionExistInCode is false)
        //{
        //    var errorMsg = $"Resumable function ({RequestedByFunction.MethodName}) not exist in code";
        //    FunctionState.AddError(errorMsg, StatusCodes.MethodValidation, null);
        //    throw new Exception(errorMsg);
        //}

        try
        {
            var waitExist = await functionRunner.MoveNextAsync();
            if (waitExist)
            {
                var nextWait = functionRunner.CurrentWaitEntity;

                FunctionState.AddLog(
                    $"Get next wait [{functionRunner.CurrentWaitEntity.Name}] " +
                    $"after [{Name}]", LogType.Info, StatusCodes.WaitProcessing);

                nextWait.ParentWaitId = ParentWaitId;
                FunctionState.StateObject = CurrentFunction;
                nextWait.FunctionState = FunctionState;
                nextWait.RequestedByFunctionId = RequestedByFunctionId;
                nextWait.RootFunctionId = RootFunctionId;

                return nextWait = functionRunner.CurrentWaitEntity;
            }

            return null;
        }
        catch (Exception ex)
        {
            FunctionState.AddError(
                $"An error occurred after resuming execution after wait [{this}].", StatusCodes.WaitProcessing, ex);
            FunctionState.Status = FunctionInstanceStatus.InError;
            throw;
        }
        finally
        {
            CurrentFunction.Logs.ForEach(log => log.EntityType = EntityType.FunctionInstanceLog);
            FunctionState.Logs.AddRange(CurrentFunction.Logs);
            FunctionState.Status =
              CurrentFunction.HasErrors() || FunctionState.HasErrors() ?
              FunctionInstanceStatus.InError :
              FunctionInstanceStatus.InProgress;
        }
    }

    internal virtual bool IsCompleted() => Status == WaitStatus.Completed;


    public virtual void CopyCommonIds(WaitEntity oldWait)
    {
        FunctionState = oldWait.FunctionState;
        FunctionStateId = oldWait.FunctionStateId;
        RequestedByFunction = oldWait.RequestedByFunction;
        RequestedByFunctionId = oldWait.RequestedByFunctionId;
    }

    internal virtual void Cancel() => Status = Status == WaitStatus.Waiting ? Status = WaitStatus.Canceled : Status;

    internal virtual bool ValidateWaitRequest()
    {
        var hasErrors = FunctionState.HasErrors();
        if (hasErrors)
        {
            Status = WaitStatus.InError;
            FunctionState.Status = FunctionInstanceStatus.InError;
        }
        return hasErrors is false;
    }


    /// <summary>
    /// Including the current one
    /// </summary>
    internal void ActionOnParentTree(Action<WaitEntity> action)
    {
        action(this);
        if (ParentWait != null)
            ParentWait.ActionOnParentTree(action);
    }

    /// <summary>
    /// Including the current one
    /// </summary>
    internal void ActionOnChildrenTree(Action<WaitEntity> action)
    {
        action(this);
        if (ChildWaits != null)
            foreach (var item in ChildWaits)
                item.ActionOnChildrenTree(action);
    }

    internal IEnumerable<WaitEntity> GetTreeItems()
    {
        yield return this;
        if (ChildWaits != null)
            foreach (var item in ChildWaits)
            {
                foreach (var item2 in item.GetTreeItems())
                {
                    yield return item2;
                }
            }
    }

    internal IEnumerable<WaitEntity> GetAllParent()
    {
        yield return this;
        if (ParentWait != null)
            ParentWait.GetAllParent();
    }

    internal virtual void OnAddWait()
    {
        if (!IsRoot) return;
        SetRuntimeClosure();
        SetRootFunctionId();
    }

    private void SetRootFunctionId()
    {
        ActionOnChildrenTree(x => x.RootFunctionId = RequestedByFunctionId);
    }

    private void SetRuntimeClosure()
    {
        var waitsGroupedByClosure =
                    GetTreeItems().
                    Where(x => x.ClosureObject != null).
                    GroupBy(x => x.ClosureObject);
        foreach (var group in waitsGroupedByClosure)
        {
            var runtimeClosure = group.
                Where(x => x.ClosureData != null).
                Select(x => x.ClosureData).
                FirstOrDefault();
            if (runtimeClosure == null)
            {
                runtimeClosure = new PrivateData
                {
                    Value = group.Key,
                };
            }

            foreach (var waitInGroup in group)
            {
                waitInGroup.ClosureData = runtimeClosure;
            }
        }
    }

    internal MethodWaitEntity GetChildMethodWait(string name)
    {
        if (this is TimeWaitEntity tw)
            return tw.TimeWaitMethod;

        var result = this
            .Flatten(x => x.ChildWaits)
            .FirstOrDefault(x => x.Name == name && x is MethodWaitEntity);
        if (result == null)
            throw new NullReferenceException($"No MethodWait with name [{name}] exist in ChildWaits tree [{Name}]");
        return (MethodWaitEntity)result;
    }

    public override string ToString()
    {
        return $"Name:{Name}, Type:{WaitType}, Id:{Id}, Status:{Status}";
    }

    public void BeforeSave()
    {
        var converter = new BinarySerializer();
        if (ExtraData != null)
            ExtraDataValue = converter.ConvertToBinary(ExtraData);
    }

    public void LoadUnmappedProps()
    {
        var converter = new BinarySerializer();
        if (ExtraDataValue != null)
            ExtraData = converter.ConvertToObject<dynamic>(ExtraDataValue);
        if (FunctionState?.StateObject != null && CurrentFunction == null)
            CurrentFunction = (ResumableFunctionsContainer)FunctionState.StateObject;
    }

    /// <summary>
    /// Validate delegate that used for groupMatchFilter,AfterMatchAction,CancelAction and return:
    /// $"{method.DeclaringType.FullName}#{method.Name}"
    /// </summary>
    internal string ValidateCallback(Delegate callback, string methodName)
    {
        var method = callback.Method;
        var functionClassType = CurrentFunction.GetType();
        var declaringType = method.DeclaringType;
        var containerType = callback.Target?.GetType();

        var validConatinerCalss =
          (declaringType == functionClassType ||
          declaringType.Name == Constants.CompilerStaticLambdas ||
          declaringType.Name.StartsWith(Constants.CompilerClosurePrefix)) &&
          declaringType.FullName.StartsWith(functionClassType.FullName);

        if (validConatinerCalss is false)
            throw new Exception(
                $"For wait [{Name}] the [{methodName}:{method.Name}] must be a method in class " +
                $"[{functionClassType.Name}] or inline lambda method.");

        var hasOverload = functionClassType.GetMethods(CoreExtensions.DeclaredWithinTypeFlags()).Count(x => x.Name == method.Name) > 1;
        if (hasOverload)
            throw new Exception(
                $"For wait [{Name}] the [{methodName}:{method.Name}] must not be over-loaded.");
        if (declaringType.Name.StartsWith(Constants.CompilerClosurePrefix))
            SetClosureObject(callback.Target);
        return $"{method.DeclaringType.FullName}#{method.Name}";
    }

    internal void SetClosureObject(object closure)
    {
        if (closure == null) return;
        if (ClosureObject == null)
        {
            ClosureObject = closure;
            return;
        }

        var currentClosureType = ClosureObject.GetType();
        var incomingClosureType = closure.GetType();
        var nestedClosure =
            currentClosureType.Name.StartsWith(Constants.CompilerClosurePrefix) is true &&
            incomingClosureType.Name.StartsWith(Constants.CompilerClosurePrefix);
        var sameType = currentClosureType == incomingClosureType;
        if(sameType is false && nestedClosure is false)
        {
            throw new Exception(
                $"For method wait [{Name}] the closure must be the same for AfterMatchAction, CancelAction, and MatchExpression.");
        }
        ClosureObject = closure;
    }
    public object GetClosure(Type closureType)
    {
        if (ClosureData?.Value == null) Activator.CreateInstance(closureType);
        var matchClosure = ClosureData?.Value;
        matchClosure = matchClosure is JObject jobject ? jobject.ToObject(closureType) : matchClosure;
        return matchClosure ?? Activator.CreateInstance(closureType);
    }


    internal string PrivateDataDisplay()
    {
        var locals = Locals?.Value;
        var closure = ClosureData?.Value;
        if (locals == null && closure == null)
            return null;
        var result = new JObject();
        if (locals != null && locals.ToString() != "{}")
            result["Locals"] = locals as JToken;
        if (closure != null && closure.ToString() != "{}")
            result["Closure"] = closure as JToken;
        if (result?.ToString() != "{}")
            return result.ToString(Formatting.Indented)?.Replace("<", "").Replace(">", "");
        return null;
    }


    internal Wait ToWait() => new Wait(this);

    public void AfterChangesSaved()
    {
        var wait = this;
        var path = $"/{wait.Id}";
        while (wait?.ParentWait != null)
        {
            path = $"/{wait.ParentWaitId}" + path;
            wait = wait.ParentWait;
        }
        Path = path;
    }
}