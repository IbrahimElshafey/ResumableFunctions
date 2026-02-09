I think same can be handled by other alredy existing solutions.
# Aggregate Column Feature
* Create table [AggregateDefinition] with columns
	* EntityName (sush as Orders)
	* AggregateName (such as FailedOrdersCount,TotalPayments)
	* AggregateFunction (such as SUM, COUNT, AVG, LAST,...) or user defined
	* ResetValue (such as -100 default null)//when you want to recalculate aggregation  
	* KeepValuesAfterAggregation (true or false)
* Create table [AggregateValues] with 'No update just insersion and delete'
	* AggregateDefinitionId
	* Entity ID
	* Number Value
	* CreationDate (May delete this)
	* IsAggregation (boolean)

## Example
* Define Aggregate [DefineAggregate(forTable: "Post",name: "LikesCount",aggregateFunction: "SUM")]
* Use when like button click [post.AddAggregateValue("LikesCount",1)]
* Use when unlike button clicked [post.AddAggregateValue("LikesCount",-1)]
* When user change the content of the post [post.ResetAggregate("LikesCount")]
* When you wanty to display like counts [post.GetAggregate("LikesCount")]

# Table File Log
* This will be a separate test project to know more about reading/writing to files,database, concurrency, disk buffers,
* How database ACID work
* Use GroBuf to serialize object
	* GroBuf https://github.com/skbkontur/GroBuf

# Write ahead log
* Record Structure
	* Type (New Record,Old Record,Delete Record)
	* Record Type 16 Byte Hash for the type
	* Record ID 16 Byte GUID
	* Record Length 8 Bytes
	* Variable Length Byte array for Record Content
* Memory mapped file is used to simulate  file
* Data will written to disk if it exceeded one miga byte or 30 seconds passed after last record insertion
* Compaction process run when insertions goes down for 60 seconds
* While compaction you can provide aggregation function for the a type
* Compaction read log from buttom to top
* While compaction no append process done


#Links
* https://github.com/microsoft/FASTER
* https://github.com/microsoft/FishStore
* https://dotnet.github.io/dotNext/features/cluster/wal.html
* https://badrish.net/projects/


* https://github.com/Microsoft/AMBROSIA/
