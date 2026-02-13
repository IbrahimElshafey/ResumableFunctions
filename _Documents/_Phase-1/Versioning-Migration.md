# Side by Side Versions
* When release a new version all active waits will be assigned to the old/current version
* Now we have waits that may be handled by version that is old
* When a call pushed to the system it found the matched waits and redirect them to the corresponding versions
* Each version has a puplish date and deactivation date
* Wait that created in range between puplish date and deactivation date will be handled by the version corresponding
* The current version dectivation date is Date.Max

* We may use service fabric to host services
* When to mark version as dead? 
	* When no active waits exist.
	* We mark service as dead for the purpose of clenaing it's resources from the hosted server

# Publish new version to production
* What about function class migration that is serialized?
	* Because old version is kept and it handle it's old waits, there is no problem.
* What happedn when resumable function database schema changed with a new version?
	* We must provide migration script
* What about HangfireDb schema
	* We will search how the solve this
* How to receive calls while upgrading? What about updating distributed system?