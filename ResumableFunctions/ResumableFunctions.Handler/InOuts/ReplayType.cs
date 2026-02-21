namespace ResumableFunctions.Handler.InOuts;

public enum ReplayType
{
    /// <summary>
    /// Execute code before wait `x` and then re-wait `x` again but with new match expression.
    /// </summary>
    GoBeforeWithNewMatch,
}