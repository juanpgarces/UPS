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
    public static class UPS
    {
        private static List<ConcurrentQueue<ReferencedTask>> concurrentQueues = new List<ConcurrentQueue<ReferencedTask>>();
        private static ConcurrentQueue<ReferencedResult> completedTasks = new ConcurrentQueue<ReferencedResult>();
        private static ConcurrentQueue<ReferencedException> failedTasks = new ConcurrentQueue<ReferencedException>();

        private static int numberOfAttempts = 3;
        private static long maxThreadNumber = 1;
        private static long isInitiated = 0;
        private static long currentCount = 0;
        private static long isProcessing = 0;
        private static int queueNumber = 0;
        // Probably have something like you can pass in the amount of 'support/extra queues'
        // Give people the ability to decide their own priority levels?
        public static void Initialize(int extraQueuesNumber, int maxThreads)
        {
            maxThreadNumber = maxThreads == 0 ? 999 : maxThreads;
            queueNumber = Enum.GetValues(typeof(Priority)).Length + extraQueuesNumber;
            for (int i = 0; i <= queueNumber; i++)
            {
                concurrentQueues.Add(new ConcurrentQueue<ReferencedTask>());
            }

            Interlocked.Exchange(ref isInitiated, 1);
        }

        public static void Shutdown()
        {
            Interlocked.Exchange(ref isInitiated, 0);
        }

        public static async Task<Guid> Enqueue(Func<Task<object>> func, Priority priority)
        {
            var referencedTask = new ReferencedTask { guid = Guid.NewGuid(), func = func };

            GetQueue((int)priority).Enqueue(referencedTask);

            if (Interlocked.Read(ref isInitiated) == 1)
            {
                await StartProcessing();
            }

            return referencedTask.guid;
        }

        private static void EnqueueFailedFunction(ReferencedTask referencedTask)
        {
            GetQueue(referencedTask.priority).Enqueue(referencedTask);
        }

        private static async Task StartProcessing()
        {
            if(Interlocked.Read(ref isProcessing) == 0)
            {
                await Task.Factory.StartNew(async () => {

                    Interlocked.Exchange(ref isProcessing, 1);
                    foreach (var queue in concurrentQueues)
                    {
                        if (Interlocked.Read(ref currentCount) < maxThreadNumber)
                        {
                            queue.TryDequeue(out ReferencedTask referencedTask);
                            await ExecuteAsync(referencedTask);
                        }
                    }
                    Interlocked.Exchange(ref isProcessing, 0);
                }, TaskCreationOptions.LongRunning);
            }
        }

        private static async Task ExecuteAsync(ReferencedTask referencedTask)
        {
            object result = null;

            try
            {
                result = await referencedTask.func.Invoke();
            }
            catch(Exception ex)
            {
                // After trying the maximun number of attemtps, Enqueue to lower tier
                if(referencedTask.currentAttempt <= numberOfAttempts)
                {
                    referencedTask.currentAttempt++;
                    await ExecuteAsync(referencedTask);
                }
                else
                {
                    referencedTask.priority++;
                    if(referencedTask.priority <= queueNumber)
                    {
                        EnqueueFailedFunction(referencedTask);
                    }
                    else
                    {
                        failedTasks.Enqueue(new ReferencedException() { guid = referencedTask.guid, exception = ex });
                    }
                }
            }
            completedTasks.Enqueue(new ReferencedResult() { guid = referencedTask.guid, result = result});
        }

        private static ConcurrentQueue<ReferencedTask> GetQueue(int priority)
        {
            return concurrentQueues[priority];
        }
    }
}
