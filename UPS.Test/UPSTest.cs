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
            FuncManager.Initialize(2, 5);

            var function = new Func<Task<object>>(async() => {
                Console.WriteLine("TEST");
                return null;
            });
            // Act
            // Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await FuncManager.EnqueueAsync(function, Enums.Priority.High)); 
        }
    }
}
