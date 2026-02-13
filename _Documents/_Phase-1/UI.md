# UI Todo
* UI service must not use EF context directly
* UI when click errors link show log errors not all

* Filtering controls at top for:
	* Function Instances
	* Waits in method group
* Infinite scroll for:
	* Logs view
	* Pushed Calls view
	* Waits in method group
	* Function Instances
* Tabels problem on small screens
* Restrict access to UI from servers only



# UI V2
* Actions on Wait 
	* Cancel Wait
	* Cancel all and re-wait specefic wait
	* Set Wait Status
* Simulate push call
* Actions on service
	* Find dead methods
	* Everything Okay Checks
		* Verify start waits exist in db for each RF
		* Wait methods in same method group must have the same signature and attribute props
		* Instance in progress but not wait anything check
		* No failed instancs
		* Closures classes are serializable
		* Instance classes are serializable
		* Get Errors in current service
		* Registerd Method Ids exist in code
	* Validate URN duplication when scan if different method signature
	* Stop resumable function creation of new instances
* Date range filter for:
	* Logs view
	* Pushed Calls view
* Localization For UI and log messages