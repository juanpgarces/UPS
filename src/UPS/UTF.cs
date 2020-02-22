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
    public static class UTF
    {
        private static List<ConcurrentQueue<ReferencedTask>> concurrentQueues = new List<ConcurrentQueue<ReferencedTask>>();
        private static ConcurrentQueue<ReferencedResult> completedTasks = new ConcurrentQueue<ReferencedResult>();

        private static int numberOfAttempts = 3;
        private static long completedTasksNumber;
        private static long maxThreadNumber = 1;
        private static long isInitiated = 0;
        private static long currentCount = 0;
        private static long isProcessing = 0;
        private static int queueNumber = 0;
        // Probably have something like you can pass in the amount of 'support/extra queues'
        // Give people the ability to decide their own priority levels?
        public static async Task Initialize(int extraQueuesNumber, int maxThreads)
        {
            maxThreadNumber = maxThreads;
            queueNumber = Enum.GetValues(typeof(Priority)).Length + extraQueuesNumber;
            for(int i = 0; i < queueNumber; i++)
            {
                concurrentQueues.Add(new ConcurrentQueue<ReferencedTask>());
            }

            Interlocked.Exchange(ref isInitiated, 1);
        }

        public static async Task Shutdown()
        {   
            Interlocked.Exchange(ref isInitiated, 0);
        }

        public static async Task<Guid> Enqueue<T>(Func<Task<T>> func, Priority priority)
        {
            var referencedTask = new ReferencedTask<T> { guid = Guid.NewGuid(), func = func };

            switch (priority)
            {
                case Priority.High:
                    concurrentQueues[0].Enqueue(referencedTask);
                    break;
                case Priority.Medium:
                    concurrentQueues[1].Enqueue(referencedTask);
                    break;
                case Priority.Low:
                    concurrentQueues[2].Enqueue(referencedTask);
                    break;
            }

            if(Interlocked.Read(ref isInitiated) == 1)
            {
                await StartProcessing();
            }

            return referencedTask.guid;
        }

        private static async Task StartProcessing()
        {
            if(Interlocked.Read(ref isProcessing) == 0)
            {
                await Task.Factory.StartNew(() => {

                    Interlocked.Exchange(ref isProcessing, 1);
                    foreach (var queue in concurrentQueues)
                    {
                        if (Interlocked.Read(ref currentCount) < maxThreadNumber)
                        {

                        }
                    }
                    Interlocked.Exchange(ref isProcessing, 0);
                }, TaskCreationOptions.LongRunning);
            }
        }
        
        private static async Task<bool> ExecuteAsync<T>(ReferencedTask<T> referencedTask)
        {
            object result = null;

            try
            {
                // try to run task and get result
                result = await referencedTask.func.Invoke();
            }
            catch(Exception ex)
            {
                // when tasks throws an exception
                // logic to retry when it is still within the limits of max number of retries
                // when it is over the amount of retries move it to a lower priority queue and repeat

                return false;
            }
            completedTasks.Enqueue(new ReferencedResult() { guid = referencedTask.guid, result = result});

            return true;
        }
    }
}
