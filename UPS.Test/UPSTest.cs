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
        public void Enqueue_Without_Initialization_ThrowsExcep()
        {
            // Arrange
            FuncPriorityManager.Initialize(2, 5, 2000);

            var function = new Func<Task<object>>(async () =>
            {
                Console.WriteLine("TEST");
                return null;
            });
            // Act
            // Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await FuncPriorityManager.EnqueueAsync(function, Enums.Priority.High));
        }
    }
}