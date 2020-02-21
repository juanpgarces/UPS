using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UPS.Enums;

namespace UPS
{
    public static class UTF
    {
        private static List<ConcurrentQueue<Func<Task>>> concurrentQueues = new List<ConcurrentQueue<Func<Task>>>();


        // Probably have something like you can pass in the amount of 'support/extra queues'
        
        public static async Task Initialize()
        {

        }

        public static async Task<Guid> EnqueueTask(Func<Task> func, Priority priority)
        {
            var identifier = Guid.NewGuid();


            return identifier;
        }

        public static async Task<bool> DequeueTask(Guid guid)
        {
            return true;
        }



        private static async Task Execute(Func<Task> func)
        {

        }
    }
}
