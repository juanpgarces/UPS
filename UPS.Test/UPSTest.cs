using System;
using System.Threading.Tasks;
using Xunit;

namespace UPS.Test
{
    public class UPSTest
    {
        [Fact]
        public void Initialization_With_NullValues()
        {
            // Arrange

            // Act

            // Assert
        }

        [Fact]
        public async Task Enqueue_Without_Initialization_ThrowsExcep()
        {
            // Arrange
            var function = new Func<Task<object>>(async () =>
            {
                Console.WriteLine("TEST");
                return null;
            });
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await FuncPriorityManager.EnqueueAsync(function, Enums.Priority.High));
        }
    }
}