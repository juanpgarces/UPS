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

        private static ConcurrentDictionary<string, bool> processingQueues = new ConcurrentDictionary<string, bool>();
        

        // Regular Operations
        private static int maxThreads = 5;

        private static long currentThreadCount = 0;

        // Error Handling
        private static Func<Exception, Task> funcExceptionLogger = null;

        /// <summary>
        /// Amount of Threads used to Run Tasks, this are the amount of Tasks that can be run at the same time overall in all Queues.
        /// </summary>
        public static int MaxThreads { get => maxThreads; set => maxThreads = value; }

        /// <summary>
        /// Enqueues a task for asynchronous execution.
        /// The method accepts a function that returns a Task<object>,
        /// a period in milliseconds for periodic checks on the queue, and an optional queue name.
        /// It initializes the queue with the given queue name and period, retrieves the queue from the concurrentQueues dictionary,
        /// and enqueues a new ReferencedFunc<object> into the queue.
        /// It then starts processing the tasks in the queue in a separate thread.
        /// The method returns the Guid of the enqueued ReferencedFunc<object>, which can be used to reference the task in the future.
        /// The periodMs parameter ensures that the queue is checked periodically, allowing for efficient management of tasks in the queue.
        /// </summary>
        /// <param name="func">A function that returns a Task<object>.</param>
        /// <param name="periodMs">The period in milliseconds for periodic checks on the queue.</param>
        /// <param name="queueName">The name of the queue. Default is "default".</param>
        /// <returns>The Guid of the enqueued task.</returns>
        public static async Task<Guid> EnqueueAsync(Func<Task<object>> func, int periodMs, string queueName = "default")
        {
            return await EnqueueAsync(func, null, periodMs, queueName);
        }

        /// <summary>
        /// Enqueues a task for asynchronous execution.
        /// The method accepts a function that returns a Task<object>, a checkpoint function that returns a Task<bool>,
        /// a period in milliseconds for periodic checks on the queue, and an optional queue name.
        /// It initializes the queue with the given queue name and period, retrieves the queue from the concurrentQueues dictionary,
        /// and enqueues a new ReferencedFunc<object> into the queue.
        /// It then starts processing the tasks in the queue in a separate thread.
        /// The method returns the Guid of the enqueued ReferencedFunc<object>, which can be used to reference the task in the future.
        /// The periodMs parameter ensures that the queue is checked periodically, allowing for efficient management of tasks in the queue.
        /// </summary>
        /// <param name="func">A function that returns a Task<object>.</param>
        /// <param name="checkpoint">A checkpoint function that returns a Task<bool>.</param>
        /// <param name="periodMs">The period in milliseconds for periodic checks on the queue.</param>
        /// <param name="queueName">The name of the queue. Default is "default".</param>
        /// <returns>The Guid of the enqueued task.</returns>
       public static async Task<Guid> EnqueueAsync(Func<Task<object>> func, Func<Task<bool>> checkpoint, int periodMs, string queueName = "default")
        {
            InitializeQueue(queueName, periodMs);
            concurrentQueues.TryGetValue(queueName, out var queue);

            var referencedTask = new ReferencedFunc<object> { guid = Guid.NewGuid(), func = func, checkpoint = checkpoint };

            queue.Enqueue(referencedTask);
            await StartProcessing(queueName);

            return referencedTask.guid;
        }

        /// <summary>
        ///  Sets the Error Logging Function. This function will be called when an Exception is thrown in the FuncMultipleManager.
        /// </summary>
        /// <param name="myFuncExceptionLogger"></param>
        /// <returns></returns>
        public static Task SetErrorLoggingFunction(Func<Exception, Task> myFuncExceptionLogger)
        {
            if (myFuncExceptionLogger != null)
            {
                funcExceptionLogger = myFuncExceptionLogger;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns the amount of items in the Queue. If the Queue does not exist, it will return 0. If the Queue is empty, it will return 0. 
        /// If the Queue is being processed, it will return the amount of items in the Queue at the time of the call.
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public static Task<int> QueueCountAsync(string queueName = "default")
        {
            int? result = null;

            if (concurrentQueues.TryGetValue(queueName, out var queue))
            {
                result = queue.Count;
            }

            return Task.FromResult(result ?? default);
        }

        private static async void CheckQueue(object state)
        {
            var queueName = (string)state;

            await StartProcessing(queueName);
        }

        /// <summary>
        /// Initializes the Queue if it does not exist.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="period"></param>
        /// <exception cref="ArgumentNullException"></exception>
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

        /// <summary>
        /// Starts Processing the Queue if it is not already being processed and there are items in the Queue.
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        private static async Task StartProcessing(string queueName)
        {
            concurrentQueues.TryGetValue(queueName, out var queue);

            if (queue.Count > 0 && !processingQueues.ContainsKey(queueName) && Interlocked.CompareExchange(ref currentThreadCount, 0, 0) < maxThreads)
            {
                await Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        Interlocked.Increment(ref currentThreadCount);
                        processingQueues.TryAdd(queueName, true);

                        while (!queue.IsEmpty)
                        {
                            await ProcessQueue(queue);
                        }
                    }
                    catch (Exception ex)
                    {
                        await (funcExceptionLogger?.Invoke(ex));
                    }
                    finally
                    {
                        Interlocked.Decrement(ref currentThreadCount);
                        processingQueues.TryRemove(queueName, out _);
                    }
                }, TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// Processes the Queue until it is empty. If the Queue is empty, it will stop processing.
        /// </summary>
        /// <param name="queue"></param>
        /// <returns></returns>
        private static async Task ProcessQueue(ConcurrentQueue<ReferencedFunc<object>> queue)
        {
            try
            {
                while (queue.Count > 0)
                {
                    await ProcessQueueItem(queue);
                }
            }
            catch (Exception ex)
            {
                await (funcExceptionLogger?.Invoke(ex));
            }
        }

        /// <summary>
        /// Processes a single item from the Queue. If the Queue is empty, it will stop processing. If the Task is not ready to be executed, it will be requeued.
        /// </summary>
        /// <param name="queue"></param>
        /// <returns></returns>
        private static async Task ProcessQueueItem(ConcurrentQueue<ReferencedFunc<object>> queue)
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
            }
            else
            {
                queue.TryDequeue(out referencedTask);
            }
        }

        /// <summary>
        /// Executes the Task
        /// </summary>
        /// <param name="referencedTask"></param>
        /// <returns></returns>
        private static async Task ExecuteAsync(ReferencedFunc<object> referencedTask)
        {
            await (referencedTask?.func?.Invoke());
        }      
    }
}
