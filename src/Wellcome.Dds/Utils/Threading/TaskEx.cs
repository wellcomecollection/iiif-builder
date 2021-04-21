using System;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Threading
{
    public static class TaskEx
    {
        /// <summary>
        /// Add a timeout of specified ms to provided task.
        /// </summary>
        /// <param name="task">Task to add timeout for</param>
        /// <param name="millisecondsTimeout">How long, in ms, to wait for task to complete</param>
        /// <param name="throwOnTimeout">If true, throw <exception cref="TimeoutException">TimeoutException</exception>
        /// if timeout exceeded. If false return default{T}</param>
        /// <returns>Result of awaited task, or default{T}</returns>
        public static async Task<T> TimeoutAfter<T>(this Task<T> task, int millisecondsTimeout,
            bool throwOnTimeout = false)
        {
            using (var cts = new CancellationTokenSource())
            {
                var delay = Task.Delay(millisecondsTimeout, cts.Token);

                if (task == await Task.WhenAny(task, delay))
                {
                    cts.Cancel();
                    return await task;
                }
            }

            if (throwOnTimeout) throw new TimeoutException($"Task exceeded timeout of {millisecondsTimeout}ms");

            return default;
        }
    }
}