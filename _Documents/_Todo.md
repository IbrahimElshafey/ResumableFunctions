# Minor Todos
* Use AsyncIteratorStateMachineAttribute attributre to get class
* Delete unused Method Identifiers that do not exist in the code anymore
	* How to know if method is not used and safe to be deleted?
	* Method may be a 
		* resumable function or sub resumable function -> safe to delete if no waits requested by it
		* pushed calls -> safe to delete if no waits requested by it
* How to check that pushed call processed by all affected services so it can be deleted?

# Advanced Todos
* **Concurrency Scenarios**
	* Review unit of work blocks
	* Review concurrency with [pen and paper]
		* Review function state update lock
			* ExecuteAfterMatchAction
			* CancelMethodAction
			* GroupMatchFuncName
	* Update [dbo].[WaitProcessingRecord] row
	* [dbo].[MethodIdentifiers] is updatable
	* Different services may try to add same MethodGroup at same time 
	* Uniqe index exception handel
	* Find lock postions BackgroundJobExecutor.ExecuteWithLock
* Review all places where database update occurs
* 
* Use same dll in two different services must be allowed
* Remove SaveChangesAsync and,SaveChangesdDirectly from repo implementations
	* Use transactions
* Dynamic loaded dll??

* If found RF database with schema diffrence use new one and update appsettings

* Provide an advanced API for tests that enable complex queries for DB

* Cancel old scan jobs when rescan
	* Self cancel if job creation date less that last scan session date

* Parameter check lib use
* Confirm One Transaction per bussiness unit
	* Review SaveChanges call

* How to check that pushed call processed by all affected services so it can be deleted?

* Composite primary key for log record (id,entity_id,entity_type)


# High Impact Todos
* Use mass transit for reliable messaging between service 
* Unify logging and make separate DB for logs
* Use source generator for fast scan (no scan)
	* https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview
* Build Nuget package to target multiple runtimes and versions
* Publisher Project TODOs
* Side by Side Versions
* Analyzer
* Brighter is a framework for building messaging app with .NET and C# 
	* https://github.com/BrighterCommand/Brighter
* In-memory fast match for waits very fast matched
* Side by Side diffrent state store options (SqlDb, In-Memory Cache)

# Features to work on
* Find instances that failed when resume execution and re-run them
* Build Nuget package to target multiple runtimes and versions
* Security and IUserContext (out of scope)

* Workflow/Function priority/Matched Waits priority
	* How hangfire handle priority

* Performance Analysis/Monitoring
	* Pushed calls that is not necessary
	* How long processing a pushed call takes?
* Encryption option for sensitive data
	* Function state
	* Match and SetData Expressions
* Services registry is separate service

* ResumableFunctionsController must be relay on abstraction for IExternalCallHandler
	* IExternalCallHandler may relay as http service
	* IExternalCallHandler may relay as gRPC service
	* We need to use RF in any service or process
	* 
* Use Data access abstraction to support changing DB
* Optimize wait table indexes to enable fast wait insertion

* Support to stop/pause pushing calls for specific method
* Support to push to multiple providers/services
	* Pushing direct (to RF database)
	* Pushing via HTTP/TCP
	* Pushing to queues (RabbitMQ, Azure Service Bus, Mass Transit)
	* Pushing to Kafka