# When to call a service from a RF 
```C#
Wait UserApplicantRegistration();
var score = CalcScore();
if(score > 70)
	SendForReview();
```
* If the `CalcScore` or `SendForReview` failed how to recover and resume function excecution?
* I'll use Inbox/Outbox pattern to store the call and the retry policy in case of failure.
