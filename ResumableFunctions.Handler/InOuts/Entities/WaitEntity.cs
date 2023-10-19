﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Core;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts.Entities;

public abstract class WaitEntity : IEntity<long>, IEntityWithUpdate, IEntityWithDelete, IOnSaveEntity
{
    public long Id { get; set; }
    public DateTime Created { get; set; }
    public string Name { get; set; }

    public WaitStatus Status { get; set; } = WaitStatus.Waiting;
    public bool IsFirst { get; set; }
    public bool WasFirst { get; set; }
    public int StateBeforeWait { get; set; }
    public int StateAfterWait { get; set; }
    public bool IsRootNode { get; set; }
    public bool IsReplay { get; set; }

    [NotMapped]
    public WaitExtraData ExtraData { get; set; }
    public byte[] ExtraDataValue { get; set; }

    public int? ServiceId { get; set; }

    public WaitType WaitType { get; set; }
    public DateTime Modified { get; set; }
    public string ConcurrencyToken { get; set; }
    public bool IsDeleted { get; set; }

    internal ResumableFunctionState FunctionState { get; set; }

    internal int FunctionStateId { get; set; }


    /// <summary>
    ///     The resumable function that initiated/created/requested the wait.
    /// </summary>
    internal ResumableFunctionIdentifier RequestedByFunction { get; set; }

    internal int RequestedByFunctionId { get; set; }

    /// <summary>
    ///     If not null this means that wait requested by a sub function
    ///     not
    /// </summary>
    internal WaitEntity ParentWait { get; set; }

    internal List<WaitEntity> ChildWaits { get; set; } = new();
    public object Locals { get; private set; }
    public object Closure { get; private set; }

    internal long? ParentWaitId { get; set; }
    public string Path { get; set; }

    [NotMapped]
    internal ResumableFunctionsContainer CurrentFunction { get; set; }

    internal bool CanBeParent => this is FunctionWaitEntity || this is WaitsGroupEntity;
    internal long? CallId { get; set; }
    public int InCodeLine { get; set; }
    public string CallerName { get; set; }

    //AfterMatch,CancelAction,GroupCheckFilter
    protected object CallMethodByName(string methodFullName, params object[] parameters)
    {
        var parts = methodFullName.Split('#');
        var methodName = parts[1];
        var className = parts[0];
        object instance = CurrentFunction;
        var classType = instance.GetType();
        var methodInfo = classType.GetMethod(methodName, Flags());

        if (methodInfo != null)
            return methodInfo.Invoke(instance, parameters);

        var lambdasClass = classType.Assembly.GetType(className);
        if (lambdasClass != null)
        {
            methodInfo = lambdasClass.GetMethod(methodName, Flags());
            instance = GetClosureAsType(lambdasClass);

            SetClosureCaller(instance);
            if (methodInfo != null)
            {
                var result = methodInfo.Invoke(instance, parameters);
                SetClosure(instance, true);
                return result;
            }
        }

        throw new NullReferenceException(
            $"Can't find method [{methodName}] in class [{classType.Name}]");
    }

    private void SetClosureCaller(object closureInstance)
    {
        var closureType = closureInstance.GetType();
        if (!closureType.Name.StartsWith(Constants.CompilerClosurePrefix)) return;
        var thisField = closureType
            .GetFields()
            .FirstOrDefault(x => x.Name.EndsWith(Constants.CompilerCallerSuffix) && x.FieldType == CurrentFunction.GetType());
        if (thisField != null)
        {
            thisField.SetValue(closureInstance, CurrentFunction);
        }
        else
        {
            var parentClosuresFields = closureType
                .GetFields()
                .Where(x => x.FieldType.Name.StartsWith(Constants.CompilerClosurePrefix));
            foreach (var closureField in parentClosuresFields)
            {
                SetClosureCaller(closureField.GetValue(closureInstance));
            }
        }
    }

    internal async Task<WaitEntity> GetNextWait()
    {
        if (CurrentFunction == null)
            LoadUnmappedProps();
        var functionRunner = new FunctionRunner(this);
        if (functionRunner.ResumableFunctionExistInCode is false)
        {
            var errorMsg = $"Resumable function ({RequestedByFunction.MethodName}) not exist in code";
            FunctionState.AddError(errorMsg, StatusCodes.MethodValidation, null);
            throw new Exception(errorMsg);
        }

        try
        {
            var waitExist = await functionRunner.MoveNextAsync();
            if (waitExist)
            {
                var nextWait = functionRunner.CurrentWait;
                var replaySuffix = nextWait is ReplayRequest ? " - Replay" : "";

                FunctionState.AddLog(
                    $"Get next wait [{functionRunner.CurrentWait.Name}{replaySuffix}] " +
                    $"after [{Name}]", LogType.Info, StatusCodes.WaitProcessing);

                nextWait.ParentWaitId = ParentWaitId;
                FunctionState.StateObject = CurrentFunction;
                nextWait.FunctionState = FunctionState;
                nextWait.RequestedByFunctionId = RequestedByFunctionId;

                return nextWait = functionRunner.CurrentWait;
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
            CurrentFunction.Logs.ForEach(log => log.EntityType = nameof(ResumableFunctionState));
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

    public WaitEntity DuplicateWait()
    {
        WaitEntity result;
        switch (this)
        {
            case MethodWaitEntity methodWait:
                result = new MethodWaitEntity
                {
                    TemplateId = methodWait.TemplateId,
                    MethodGroupToWaitId = methodWait.MethodGroupToWaitId,
                    MethodToWaitId = methodWait.MethodToWaitId,
                    Closure = methodWait.Closure,
                };
                break;
            case FunctionWaitEntity:
                result = new FunctionWaitEntity();
                break;
            case WaitsGroupEntity waitsGroup:
                result = new WaitsGroupEntity
                {
                    GroupMatchFuncName = waitsGroup.GroupMatchFuncName
                };
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        result.CopyCommon(this);
        CopyChildTree(this, result);
        return result;
    }
    private void CopyChildTree(WaitEntity fromWait, WaitEntity toWait)
    {
        for (var index = 0; index < fromWait.ChildWaits.Count; index++)
        {
            var childWait = fromWait.ChildWaits[index];
            var duplicateWait = childWait.DuplicateWait();
            toWait.ChildWaits.Add(duplicateWait);
            if (childWait.CanBeParent)
                CopyChildTree(childWait, duplicateWait);
        }
    }

    private void CopyCommon(WaitEntity fromWait)
    {
        Name = fromWait.Name;
        Status = fromWait.Status;
        IsFirst = fromWait.IsFirst;
        StateBeforeWait = fromWait.StateBeforeWait;
        StateAfterWait = fromWait.StateAfterWait;
        Locals = fromWait.Locals;
        IsRootNode = fromWait.IsRootNode;
        IsReplay = fromWait.IsReplay;
        ExtraData = fromWait.ExtraData;
        WaitType = fromWait.WaitType;
        FunctionStateId = fromWait.FunctionStateId;
        FunctionState = fromWait.FunctionState;
        ParentWaitId = fromWait.ParentWaitId;
        RequestedByFunctionId = fromWait.RequestedByFunctionId;
        RequestedByFunction = fromWait.RequestedByFunction;
        CallerName = fromWait.CallerName;
    }

    internal virtual void Cancel() => Status = Status == WaitStatus.Waiting ? Status = WaitStatus.Canceled : Status;

    internal virtual bool ValidateWaitRequest()
    {
        var isNameDuplicated =
            FunctionState
            .Waits
            .Count(x => x.Name == Name) > 1;
        if (isNameDuplicated)
        {
            FunctionState.AddLog(
                $"The wait named [{Name}] is duplicated in function [{RequestedByFunction?.MethodName}] body,fix it to not cause a problem. If it's a loop concat the  index to the name",
                LogType.Warning, StatusCodes.WaitValidation);
        }

        var hasErrors = FunctionState.HasErrors();
        if (hasErrors)
        {
            Status = WaitStatus.InError;
            FunctionState.Status = FunctionInstanceStatus.InError;
        }
        return hasErrors is false;
    }


    internal void ActionOnParentTree(Action<WaitEntity> action)
    {
        action(this);
        if (ParentWait != null)
            ParentWait.ActionOnParentTree(action);
    }

    internal void ActionOnChildrenTree(Action<WaitEntity> action)
    {
        action(this);
        if (ChildWaits != null)
            foreach (var item in ChildWaits)
                item.ActionOnChildrenTree(action);
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

    public void OnSave()
    {
        var converter = new BinarySerializer();
        if (ExtraData != null)
            ExtraDataValue = converter.ConvertToBinary(ExtraData);
    }

    public void LoadUnmappedProps()
    {
        var converter = new BinarySerializer();
        if (ExtraDataValue != null)
            ExtraData = converter.ConvertToObject<WaitExtraData>(ExtraDataValue);
        if (FunctionState?.StateObject != null && CurrentFunction == null)
            CurrentFunction = (ResumableFunctionsContainer)FunctionState.StateObject;
    }

    internal string ValidateMethod(Delegate del, string methodName)
    {
        var method = del.Method;
        var functionClassType = CurrentFunction.GetType();
        var declaringType = method.DeclaringType;
        var containerType = del.Target?.GetType();

        var validConatinerCalss =
          (declaringType == functionClassType ||
          declaringType.Name == "<>c" ||
          declaringType.Name.StartsWith(Constants.CompilerClosurePrefix)) &&
          declaringType.FullName.StartsWith(functionClassType.FullName);

        if (validConatinerCalss is false)
            throw new Exception(
                $"For wait [{Name}] the [{methodName}:{method.Name}] must be a method in class " +
                $"[{functionClassType.Name}] or inline lambda method.");

        var hasOverload = functionClassType.GetMethods(Flags()).Count(x => x.Name == method.Name) > 1;
        if (hasOverload)
            throw new Exception(
                $"For wait [{Name}] the [{methodName}:{method.Name}] must not be over-loaded.");
        if (declaringType.Name.StartsWith(Constants.CompilerClosurePrefix))
            SetClosure(del.Target, true);

        //var runnerType = functionClassType
        //    .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SuppressChangeType)
        //    .FirstOrDefault(type =>
        //    type.Name.StartsWith($"<{CallerName}>") &&
        //    typeof(IAsyncEnumerable<Wait>).IsAssignableFrom(type));
        //if (declaringType.Name.StartsWith(Constants.CompilerClosurePrefix) && runnerType != null)
        //{
        //    var localsField =
        //            runnerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        //            .FirstOrDefault(x => x.FieldType == method.DeclaringType);
        //    if (localsField is null)
        //        throw new Exception(
        //            $"You use local variables in method [{CallerName}] for callback [{methodName}] " +
        //            $"while you wait [{Name}], " +
        //            $"The compiler didn't create the loclas as a field, " +
        //            $"to force it to create a one use/list your local varaibles at the end of the resuamble function. somthing like:\n" +
        //            $"Console.WriteLine(<your_local_var>);");
        //}
        return $"{method.DeclaringType.FullName}#{method.Name}";
    }


    protected static BindingFlags Flags() =>
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    protected object GetClosureAsType(Type closureClass)
    {
        Closure = Closure is JObject jobject ? jobject.ToObject(closureClass) : Closure;
        return Closure ?? Activator.CreateInstance(closureClass);
    }

    internal void SetClosure(object closure, bool deepCopy = false)
    {
        if (deepCopy && closure != null)
        {
            var closureString =
                JsonConvert.SerializeObject(closure, ClosureContractResolver.Settings);
            Closure = JsonConvert.DeserializeObject(closureString, closure.GetType());
        }
        else
            Closure = closure;
    }

    internal void SetLocals(object locals)
    {
        Locals = locals;
    }

    internal string LocalsDisplay()
    {
        if (Locals == null && Closure == null)
            return null;
        var result = new JObject();
        if (Locals != null && Locals.ToString() != "{}")
            result["Locals"] = Locals as JToken;
        if (Closure != null && Closure.ToString() != "{}")
            result["Closure"] = Closure as JToken;
        if (result?.ToString() != "{}")
            return result.ToString()?.Replace("<", "").Replace(">", "");
        return null;
    }


    internal Wait ToWait() => new Wait(this);
}