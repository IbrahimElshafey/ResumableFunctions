using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.DataAccess;
using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.UiService;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using static System.Linq.Expressions.Expression;

namespace ResumableFunctions.Handler.Helpers;

public static class CoreExtensions
{
    //internal static IServiceProvider GetServiceProvider() => _ServiceProvider;
    public static void AddResumableFunctionsCore(this IServiceCollection services, IResumableFunctionsSettings settings)
    {


        // ReSharper disable once RedundantAssignment
        ResolveDbInterfaces(services, settings);

        services.AddScoped<IFirstWaitProcessor, FirstWaitProcessor>();
        services.AddScoped<IWaitsProcessor, WaitsProcessor>();
        services.AddScoped<IServiceQueue, ServiceQueue>();
        services.AddScoped<ICallPusher, CallPusher>();
        services.AddScoped<ICleaningJob, CleaningJob>();
        services.AddScoped<Scanner>();
        services.AddScoped<BackgroundJobExecutor>();




        services.AddHttpClient();
        services.AddSingleton(settings);
        services.AddSingleton(settings.DistributedLockProvider);


        services.AddScoped<IUiService, UiService.UiService>();
        if (settings.HangfireConfig != null)
        {
            // ReSharper disable once RedundantAssignment
            services.AddHangfire(x => x = settings.HangfireConfig);
            services.AddSingleton<IBackgroundProcess, HangfireBackgroundProcess>();
            services.AddHangfireServer();
        }
        else
            services.AddSingleton<IBackgroundProcess, NoBackgroundProcess>();
    }

    private static void ResolveDbInterfaces(IServiceCollection services, IResumableFunctionsSettings settings)
    {
        services.AddDbContext<WaitsDataContext>(optionsBuilder => optionsBuilder = settings.WaitsDbConfig);
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMethodIdsRepo, MethodIdsRepo>();
        services.AddScoped<IPrivateDataRepo, PrivateDataRepo>();
        services.AddScoped<IWaitsRepo, WaitsRepo>();
        services.AddScoped<IServiceRepo, ServiceRepo>();//todo: why AddTransient?
        services.AddScoped<IWaitTemplatesRepo, WaitTemplatesRepo>();
        services.AddScoped<IPushedCallsRepo, PushedCallsRepo>();
        services.AddScoped<IDatabaseCleaning, DatabaseCleaning>();
        services.AddScoped<IWaitProcessingRecordsRepo, WaitProcessingRecordsRepo>();

        services.AddTransient<ILogsRepo, LogsRepo>();//todo: why AddTransient?
        services.AddTransient<IScanLocksRepo, ScanLocksRepo>();//todo: why AddTransient?
    }

    public static void UseResumableFunctions(this IServiceProvider services)
    {
        GlobalConfiguration.Configuration.UseActivator(new HangfireActivator(services));
        CreateScanAndCleanBackgroundTasks(services);
    }


    private static void CreateScanAndCleanBackgroundTasks(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var backgroundJobClient = scope.ServiceProvider.GetService<IBackgroundProcess>();

        var scanner = scope.ServiceProvider.GetService<Scanner>();
        backgroundJobClient.Enqueue(() => scanner.Start());

        var cleaningJob = scope.ServiceProvider.GetService<ICleaningJob>();
        cleaningJob.ScheduleCleaningJob();
    }


    internal static (bool IsFunctionData, MemberExpression NewExpression) GetDataParamterAccess(
        this MemberExpression node,
        ParameterExpression functionInstanceArg)
    {
        var propAccessStack = new Stack<MemberInfo>();
        var isFunctionData = IsDataAccess(node);
        if (isFunctionData)
        {
            var newAccess = MakeMemberAccess(functionInstanceArg, propAccessStack.Pop());
            while (propAccessStack.Count > 0)
            {
                var currentProp = propAccessStack.Pop();
                newAccess = MakeMemberAccess(newAccess, currentProp);
            }

            return (true, newAccess);
        }

        return (false, null);

        bool IsDataAccess(MemberExpression currentNode)
        {
            propAccessStack.Push(currentNode.Member);
            var subNode = currentNode.Expression;
            if (subNode == null) return false;
            //is function data access 
            var isFunctionDataAccess =
                subNode.NodeType == ExpressionType.Constant && subNode.Type == functionInstanceArg.Type;
            if (isFunctionDataAccess)
                return true;
            if (subNode.NodeType == ExpressionType.MemberAccess)
                return IsDataAccess((MemberExpression)subNode);
            return false;
        }
    }

    internal static bool SameMatchSignature(LambdaExpression replayMatch, LambdaExpression methodMatch)
    {
        var isEqual = replayMatch != null && methodMatch != null;
        if (isEqual is false) return false;
        if (replayMatch.ReturnType != methodMatch.ReturnType)
            return false;
        //if (replayMatch.Parameters.Count != methodMatch.Parameters.Count)
        //    return false;
        var minParamsCount = Math.Min(replayMatch.Parameters.Count, methodMatch.Parameters.Count);
        for (var i = 0; i < minParamsCount; i++)
            if (replayMatch.Parameters[i].Type != methodMatch.Parameters[i].Type)
                return false;
        return true;
    }

    internal static bool IsAsyncMethod(this MethodBase method)
    {
        var attribute =
            method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) ??
            method.GetCustomAttribute(typeof(AsyncIteratorStateMachineAttribute));

        if (attribute != null) return true;

        return
            method is MethodInfo { ReturnType.IsGenericType: true } mi &&
            mi.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
    }


    internal static string GetFullName(this MethodBase method)
    {
        return $"{method.DeclaringType.FullName}.{method.Name}";
    }
    internal static MethodInfo GetInterfaceMethod(this MethodInfo method)
    {
        var type = method.DeclaringType;
        foreach (var interf in type.GetInterfaces())
        {
            foreach (var interfaceMethod in interf.GetMethods())
            {
                var sameSiganture =
                    interfaceMethod.Name == method.Name &&
                    interfaceMethod.ReturnType == method.ReturnType &&
                    interfaceMethod.GetParameters().Select(x => x.ParameterType).SequenceEqual(method.GetParameters().Select(x => x.ParameterType));
                if (sameSiganture)
                    return interfaceMethod;
            }
        }
        return null;
    }

    internal static MethodInfo GetMethodInfo(string AssemblyName, string ClassName, string MethodName, string MethodSignature)
    {
        MethodInfo _methodInfo = null;
        var assemblyPath = $"{AppContext.BaseDirectory}{AssemblyName}.dll";
        if (File.Exists(assemblyPath))
            if (AssemblyName != null && ClassName != null && MethodName != null)
            {
                _methodInfo = Assembly.LoadFrom(assemblyPath)
                    .GetType(ClassName)
                    ?.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(x => x.Name == MethodName && MethodData.CalcSignature(x) == MethodSignature);
                return _methodInfo;
            }

        return _methodInfo;
    }

    internal static IEnumerable<T> Flatten<T>(
           this IEnumerable<T> e,
           Func<T, IEnumerable<T>> f) =>
               e.SelectMany(c => f(c).Flatten(f)).Concat(e);

    internal static IEnumerable<T> Flatten<T>(this T value, Func<T, IEnumerable<T>> childrens)
    {
        foreach (var currentItem in childrens(value))
        {
            foreach (var currentChild in Flatten(currentItem, childrens))
            {
                yield return currentChild;
            }
        }
        yield return value;
    }

    internal static void CascadeSet<T, Prop>(
        this T objectToSet,
        Expression<Func<IEnumerable<T>>> childs,
        Expression<Func<Prop>> prop,
        Prop value)
    {
        //IsDeleted = true;
        //foreach (var child in ChildWaits)
        //{
        //    child.CascadeSetDeleted();
        //}
    }
    internal static IEnumerable<Prop> CascadeGet<T, Prop>(
       this T objectToSet,
       Expression<Func<IEnumerable<T>>> childs,
       Expression<Func<Prop>> prop,
       Prop value)
    {
        //IsDeleted = true;
        //foreach (var child in ChildWaits)
        //{
        //    child.CascadeSetDeleted();
        //}
        return null;
    }


    //https://www.newtonsoft.com/json/help/html/serializationguide.htm
    //https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/built-in-types
    //https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/constants
    internal static bool IsConstantType(this Type type)
    {
        var types = new[] { typeof(bool), typeof(byte), typeof(sbyte), typeof(char), typeof(decimal), typeof(double), typeof(float), typeof(int), typeof(uint), typeof(nint), typeof(nuint), typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(string) };
        return types.Contains(type);
    }
    internal static bool CanConvertToSimpleString(this Type type) =>
        type.IsConstantType() || type == typeof(DateTime) || type == typeof(Guid) || type.IsEnum;

    internal static bool CanConvertToSimpleString(this object ob) =>
        ob != null && ob.GetType().CanConvertToSimpleString();

    internal static MethodInfo GetMethodInfo<T>(Expression<Func<T, object>> methodSelector) =>
        GetMethodInfoWithType(methodSelector).MethodInfo;
    internal static (MethodInfo MethodInfo, Type OwnerType) GetMethodInfoWithType<T>(Expression<Func<T, object>> methodSelector)
    {
        MethodInfo mi = null;
        Type ownerType = null;
        var visitor = new GenericVisitor();
        visitor.OnVisitMethodCall(VisitMethod);
        visitor.OnVisitConstant(VisitConstant);
        visitor.Visit(methodSelector);
        return (mi, ownerType);

        Expression VisitMethod(MethodCallExpression node)
        {
            if (IsInCurrentType(node.Method))
                mi = node.Method;
            return node;
        }
        Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is MethodInfo info && IsInCurrentType(info))
                mi = info;
            return node;
        }

        bool IsInCurrentType(MethodInfo methodInfo)
        {
            var isExtension = methodInfo.IsDefined(typeof(ExtensionAttribute), true);
            if (isExtension)
            {
                var extensionOnType = methodInfo.GetParameters()[0].ParameterType;
                var canBeAppliedToCurrent = extensionOnType.IsAssignableFrom(typeof(T));
                if (canBeAppliedToCurrent)
                {
                    ownerType = extensionOnType;
                    return true;
                }

                return false;
            }

            var inCurrentType = methodInfo.ReflectedType.IsAssignableFrom(typeof(T));
            if (inCurrentType)
                ownerType = methodInfo.ReflectedType;
            return inCurrentType;
        }

    }

    internal static string GetRealTypeName(this Type t)
    {
        if (!t.IsGenericType)
            return t.Name;

        StringBuilder sb = new StringBuilder();
        sb.Append(t.Name.Substring(0, t.Name.IndexOf('`')));
        sb.Append('<');
        bool appendComma = false;
        foreach (Type arg in t.GetGenericArguments())
        {
            if (appendComma) sb.Append(',');
            sb.Append(GetRealTypeName(arg));
            appendComma = true;
        }
        sb.Append('>');
        return sb.ToString();
    }

    internal static Expression ToConstantExpression(this object result)
    {
        if (result.GetType().IsConstantType())
        {
            return Constant(result);
        }
        //else if (expression.NodeType == ExpressionType.New)
        //    return expression;

        if (result is DateTime date)
        {
            return Constant(date.Ticks);
        }

        if (result is Guid guid)
        {
            return Constant(guid.ToString());
        }

        if (JsonConvert.SerializeObject(result) is string json)
        {
            return Constant(json);
        }
        throw new NotSupportedException(message:
               $"Can't evaluate object [{result}] to constant expression.");
    }

    internal static Type GetUnderlyingType(this MemberInfo member)
    {
        switch (member.MemberType)
        {
            case MemberTypes.Event:
                return ((EventInfo)member).EventHandlerType;
            case MemberTypes.Field:
                return ((FieldInfo)member).FieldType;
            case MemberTypes.Method:
                return ((MethodInfo)member).ReturnType;
            case MemberTypes.Property:
                return ((PropertyInfo)member).PropertyType;
            default:
                throw new ArgumentException
                (
                 "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                );
        }
    }

    internal static void MergeIntoObject<T>(this JToken value, T target) where T : class
    {
        using (var sr = value.CreateReader())
        {
            JsonSerializer.Create(PrivateDataResolver.Settings).Populate(sr, target);
        }
    }

    internal static BindingFlags DeclaredWithinTypeFlags() =>
    BindingFlags.DeclaredOnly |
    BindingFlags.Public |
    BindingFlags.NonPublic |
    BindingFlags.Static |
    BindingFlags.Instance;
}
