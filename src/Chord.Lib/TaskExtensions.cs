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

    public static async Task TryRepeat(
        this Func<Task> taskFactory,
        IList<int> repetitionTimeouts,
        Action<Exception> onError = null)
    {
        int errorCount = 0;

        do
        {
            var task = taskFactory();

            try {
                await task;
                return;
            } catch (Exception ex) {
                onError?.Invoke(ex);
                await Task.Delay(repetitionTimeouts[errorCount++]);
            }
        }
        while (errorCount < repetitionTimeouts.Count);
    }

    public static async Task<TResult> TryRepeat<TResult>(
        this Func<Task<TResult>> taskFactory,
        IList<int> repetitionTimeouts,
        TResult defaultValue,
        Action<Exception> onError = null)
    {
        int errorCount = 0;
        Action<Exception> onErrorOverride = async (ex) => {
            onError?.Invoke(ex);
            await Task.Delay(repetitionTimeouts[errorCount]);
            errorCount++;
        };

        do
        {
            var task = taskFactory();
            int errorsBefore = errorCount;
            var result = await task.TryRun(onErrorOverride, defaultValue);
            if (errorCount == errorsBefore)
                return result;
        }
        while (errorCount < repetitionTimeouts.Count);

        return defaultValue;
    }
}

public static class TaskTimeoutEx
{
    public static async Task Timeout(
        this Task task,
        int timeoutInMilliseconds,
        CancellationToken? token = null)
    {
        var cancelCallback = new CancellationTokenSource();
        var timeoutTask = token != null
            ? Task.Delay(timeoutInMilliseconds, token.Value)
            : Task.Delay(timeoutInMilliseconds);
        var workerTask = Task.Run(async () => await task, cancelCallback.Token);

        bool timeout = await Task.WhenAny(timeoutTask, workerTask) == timeoutTask;
        if (timeout)
            cancelCallback.Cancel();
    }

    public static async Task<TResult> Timeout<TResult>(
        this Task<TResult> task,
        int timeoutInMilliseconds,
        TResult defaultValue = default(TResult),
        CancellationToken? token = null)
    {
        var cancelCallback = new CancellationTokenSource();
        var timeoutTask = token != null
            ? Task.Delay(timeoutInMilliseconds, token.Value)
            : Task.Delay(timeoutInMilliseconds);
        var workerTask = Task.Run(async () => await task, cancelCallback.Token);

        bool timeout = await Task.WhenAny(timeoutTask, workerTask) == timeoutTask;
        if (timeout)
            cancelCallback.Cancel();

        return timeout ? defaultValue : workerTask.Result;
    }
}
