using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UPS.Models;

namespace UPS
{
    /// <summary>
    ///
    /// </summary>
    public static class FuncMultipleManager
    {
        // Lists and Queues
        private static ConcurrentDictionary<string, ConcurrentQueue<ReferencedFunc<object>>> concurrentQueues = new ConcurrentDictionary<string, ConcurrentQueue<ReferencedFunc<object>>>();

        private static ConcurrentDictionary<string, Timer> concurrentTimers = new ConcurrentDictionary<string, Timer>();
        //private static List<ReferencedResult> completedFunctions = new List<ReferencedResult>();
        //private static List<ReferencedException> failedFunctions = new List<ReferencedException>();

        private static int maxThreads = 5;

        // Regular Operations
        private static long currentThreadCount = 0;

        // Error Handling
        private static Func<Exception, Task> funcExceptionLogger = null;

        /// <summary>
        /// Amount of Threads used to Run Tasks
        /// </summary>
        public static int MaxThreads { get => maxThreads; set => maxThreads = value; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="func"></param>
        /// <param name="periodMs"></param>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public static async Task<Guid> EnqueueAsync(Func<Task<object>> func, int periodMs, string queueName = "default")
        {
            return await EnqueueAsync(func, null, periodMs, queueName);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="func"></param>
        /// <param name="checkpoint"></param>
        /// <param name="periodMs"></param>
        /// <returns></returns>
        public static async Task<Guid> EnqueueAsync(Func<Task<object>> func, Func<Task<bool>> checkpoint, int periodMs, string queueName = "default")
        {
            InitializeQueue(queueName, periodMs);
            concurrentQueues.TryGetValue(queueName, out var queue);

            var referencedTask = new ReferencedFunc<object> { guid = Guid.NewGuid(), func = func, checkpoint = checkpoint };

            queue.Enqueue(referencedTask);
            await StartProcessing(queueName);

            return referencedTask.guid;
        }

        public static Task SetErrorLoggingFunction(Func<Exception, Task> myFuncExceptionLogger)
        {
            if (myFuncExceptionLogger != null)
            {
                funcExceptionLogger = myFuncExceptionLogger;
            }

            return Task.CompletedTask;
        }

        public static Task<int> QueueCountAsync(string queueName = "default")
        {
            int? result = null;

            if (concurrentQueues.TryGetValue(queueName, out var queue))
            {
                result = queue.Count;
            }

            return Task.FromResult(result ?? default);
        }

        //public static async Task<ReferencedResult> GetResultIfExistsAsync(Guid guid)
        //{
        //    // maybe have some logic tha when there is no result found then it looks for that in the exceptions?
        //    // Issues with that is that I would have return a task<object> which I don't really like
        //    return completedFunctions.Find(rR => rR.guid == guid);
        //}

        //public static async Task<ReferencedException> GetExceptionIfExistsAsync(Guid guid)
        //{
        //    return failedFunctions.Find(rR => rR.guid == guid);
        //}

        private static async void CheckQueue(object state)
        {
            var queueName = (string)state;

            await StartProcessing(queueName);
        }

        private static void InitializeQueue(string queueName, int period)
        {
            if (queueName is null)
            {
                throw new ArgumentNullException(nameof(queueName));
            }

            if (!concurrentQueues.ContainsKey(queueName))
            {
                concurrentQueues.TryAdd(queueName, new ConcurrentQueue<ReferencedFunc<object>>());
            }
            if (!concurrentTimers.ContainsKey(queueName))
            {
                concurrentTimers.TryAdd(queueName, new Timer(CheckQueue, queueName, period, period));
            }
        }

        private static async Task StartProcessing(string queueName)
        {
            concurrentQueues.TryGetValue(queueName, out var queue);

            if (queue.Count > 0)
            {
                if (Interlocked.CompareExchange(ref currentThreadCount, 0, 0) < maxThreads)
                {
                    await Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            Interlocked.Increment(ref currentThreadCount);
                            while (!queue.IsEmpty)
                            {
                                try
                                {
                                    queue.TryPeek(out ReferencedFunc<object> referencedTask);
                                    if (referencedTask != null)
                                    {
                                        // Accounts for Tasks Not specifying a non-required Checkpoint                                
                                        if (referencedTask.checkpoint == null)
                                        {
                                            if (queue.TryDequeue(out ReferencedFunc<object> dequeuedReferencedTask))
                                                await ExecuteAsync(dequeuedReferencedTask);
                                        }
                                        else if (await (referencedTask.checkpoint?.Invoke()) == true)
                                        {
                                            if (queue.TryDequeue(out ReferencedFunc<object> dequeuedReferencedTask))
                                                await ExecuteAsync(dequeuedReferencedTask);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        queue.TryDequeue(out referencedTask);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await (funcExceptionLogger?.Invoke(ex));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            await (funcExceptionLogger?.Invoke(ex));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref currentThreadCount);
                        }
                    }, TaskCreationOptions.LongRunning);
                }
            }
        }

        private static async Task ExecuteAsync(ReferencedFunc<object> referencedTask)
        {
            await (referencedTask?.func?.Invoke());
        }

        //private static void AddReferencedResult(ReferencedResult referencedResult)
        //{
        //    while (completedFunctions.Count > maxCompletedReferences)
        //    {
        //        completedFunctions.RemoveRange(0, maxCompletedReferences);
        //    }
        //    completedFunctions.Add(referencedResult);
        //}

        //private static void AddReferencedException(ReferencedException referencedException)
        //{
        //    while (failedFunctions.Count > maxFailedReferences)
        //    {
        //        failedFunctions.RemoveRange(0, maxFailedReferences);
        //    }
        //    failedFunctions.Add(referencedException);
        //}

        //private static void EnqueueReferencedFunc(ReferencedFunc<object> referencedTask)
        //{
        //    GetQueue(referencedTask.priority).Enqueue(referencedTask);
        //}

        //private static ConcurrentQueue<ReferencedFunc<object>> GetQueue(int priority)
        //{
        //    return concurrentQueues[priority];
        //}
    }
}