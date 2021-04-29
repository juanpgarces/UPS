using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UPS.Enums;
using UPS.Models;

namespace UPS
{
    /// <summary>
    ///
    /// </summary>
    public static class FuncPriorityManager
    {
        // Lists and Queues
        private static List<ConcurrentQueue<ReferencedFunc<object>>> concurrentQueues = new List<ConcurrentQueue<ReferencedFunc<object>>>();

        private static List<ReferencedResult> completedFunctions = new List<ReferencedResult>();
        private static List<ReferencedException> failedFunctions = new List<ReferencedException>();

        // Operation and Initiation
        private static int isInitiated = 0;

        private static int isProcessing = 0;
        private static int maxQueues = 0;
        private static int maxThreads = 1;
        private static readonly int maxFailedReferences = 50;
        private static readonly int maxCompletedReferences = 50;

        // Regular Operations
        private static long currentCount = 0;

        // Timer
        private static Timer checkQueueTimer;

        // Error Handling
        private static Func<Exception, Task> funcExceptionLogger = null;

        /// <summary>
        ///
        /// </summary>
        /// <param name="extraQueueLevels"></param>
        /// <param name="maxThreads"></param>
        /// <param name="period"></param>
        /// <param name="myFuncExceptionLogger"></param>
        public static void Initialize(int extraQueueLevels, int maxThreads, int period, Func<Exception, Task> myFuncExceptionLogger = null)
        {
            if(myFuncExceptionLogger != null)
            {
                funcExceptionLogger = myFuncExceptionLogger;
            }

            if (Interlocked.CompareExchange(ref isInitiated, 0, 0) == 0)
            {
                FuncPriorityManager.maxThreads = maxThreads == 0 ? FuncPriorityManager.maxThreads : maxThreads;
                InitializePriorityQueues(Enum.GetValues(typeof(Priority)).Length + extraQueueLevels);
                Interlocked.Exchange(ref isInitiated, 1);

                checkQueueTimer = new Timer(checkQueue, null, period, period);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public static void Shutdown()
        {
            if (Interlocked.CompareExchange(ref isInitiated, 0, 0) == 1)
            {
                Interlocked.Exchange(ref isInitiated, 0);
                checkQueueTimer?.Dispose();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="func"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static async Task<Guid> EnqueueAsync(Func<Task<object>> func, Priority priority)
        {
            return await EnqueueAsync(func, null, priority);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="func"></param>
        /// <param name="checkpoint"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static async Task<Guid> EnqueueAsync(Func<Task<object>> func, Func<Task<bool>> checkpoint, Priority priority = Priority.High)
        {
            if (Interlocked.CompareExchange(ref isInitiated, 0, 0) == 1)
            {
                var referencedTask = new ReferencedFunc<object> { guid = Guid.NewGuid(), func = func, checkpoint = checkpoint };
                GetQueue((int)priority).Enqueue(referencedTask);

                if (Interlocked.CompareExchange(ref isProcessing, 0, 0) == 0)
                {
                    await StartProcessing();
                }

                return referencedTask.guid;
            }
            else
            {
                throw new InvalidOperationException("Service has not been Initiated.");
            }
        }

        public static async Task<ReferencedResult> GetResultIfExistsAsync(Guid guid)
        {
            // maybe have some logic tha when there is no result found then it looks for that in the exceptions?
            // Issues with that is that I would have return a task<object> which I don't really like
            return completedFunctions.Find(rR => rR.guid == guid);
        }

        public static async Task<ReferencedException> GetExceptionIfExistsAsync(Guid guid)
        {
            return failedFunctions.Find(rR => rR.guid == guid);
        }

        private static async void checkQueue(object state)
        {
            foreach (var queue in concurrentQueues)
            {
                if (queue.Count > 0)
                {
                    await StartProcessing();
                }
            }
        }

        private static void InitializePriorityQueues(int amountOfQueues)
        {
            for (int i = 0; i <= amountOfQueues; i++)
            {
                concurrentQueues.Add(new ConcurrentQueue<ReferencedFunc<object>>());
            }
            maxQueues = concurrentQueues.Count;
        }

        private static async Task StartProcessing()
        {
            if (Interlocked.CompareExchange(ref isInitiated, 0, 0) == 1 && Interlocked.CompareExchange(ref isProcessing, 0, 0) == 0)
            {
                await Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        Interlocked.Exchange(ref isProcessing, 1);
                        foreach (var queue in concurrentQueues)
                        {
                            if (Interlocked.CompareExchange(ref currentCount, 0, 0) < maxThreads)
                            {
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
                                            else if(await (referencedTask.checkpoint?.Invoke()) == true)
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
                        }               
                    }
                    catch (Exception ex)
                    {
                        await (funcExceptionLogger?.Invoke(ex));
                    }
                    finally
                    {
                        Interlocked.Exchange(ref isProcessing, 0);
                    }
                }, TaskCreationOptions.LongRunning);
            }
        }

        private static async Task ExecuteAsync(ReferencedFunc<object> referencedTask)
        {            
            await (referencedTask?.func?.Invoke());
        }

        private static void AddReferencedResult(ReferencedResult referencedResult)
        {
            while (completedFunctions.Count > maxCompletedReferences)
            {
                completedFunctions.RemoveRange(0, maxCompletedReferences);
            }
            completedFunctions.Add(referencedResult);
        }

        private static void AddReferencedException(ReferencedException referencedException)
        {
            while (failedFunctions.Count > maxFailedReferences)
            {
                failedFunctions.RemoveRange(0, maxFailedReferences);
            }
            failedFunctions.Add(referencedException);
        }

        private static void EnqueueReferencedFunc(ReferencedFunc<object> referencedTask)
        {
            GetQueue(referencedTask.priority).Enqueue(referencedTask);
        }

        private static ConcurrentQueue<ReferencedFunc<object>> GetQueue(int priority)
        {
            return concurrentQueues[priority];
        }
    }
}