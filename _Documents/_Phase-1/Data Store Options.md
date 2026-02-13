# Data Store Enhancements for (Pushed Calls,Waits, Logging, and States)
* I need database that
	* Support EF
	* Keeps hot data in memory
	* Saves cold data to disk
	* 
## Pushed Calls
* Used 
	* When method executed and call pushed
	* When clean database to delete old pushed calls
* Store pushed calls in different store that support:
	* Fast insertion
	* Fast query by 
		* ID
		* Service ID
		* Creation Date
		* Method URN
## Waits
* Store waits in store that support:
	* Fast queries
	* Fast wait insertion
	
## Logs
* Fast logging
	* Separate data store for log
	* Logs can be queried by
		* Date
		* Service Id
		* EntityName, EntityId
		* Status
	* I need structured logging 
	* No Custom implementation for logging (IResumableFunctionLogging)
	* May I use InfluxDB,RocksDB or Faster

## States and private data
	* 


# We may need In-Memory DB that:
* Can be used with EF
* Can be used with Hangfire
* Support Snapshoting and clustering
* Mark some tables as on disk only
* Keep rows in memory based on condition
* Keep rows in memory based on root node is live
* Execute quries againist memory nad don't hit db
