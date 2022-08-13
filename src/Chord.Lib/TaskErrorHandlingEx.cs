namespace System.Threading.Tasks;

public static class TaskErrorHandlingEx
{
    public static async Task<TResult> TryRun<TResult>(
            this Task<TResult> task,
            Action<Exception> onError,
            TResult defaultValue)
        => await task.TryRun((r) => r, onError, defaultValue);

    public static async Task<TResult> TryRun<TResult, TIntermResult>(
        this Task<TIntermResult> task,
        Func<TIntermResult, TResult> resultSelector, 
        Action<Exception> onError,
        TResult defaultValue)
    {
        try
        {
            return resultSelector(await task);
        }
        catch (Exception ex)
        {
            onError(ex);
            return defaultValue;
        }
    } 
}
