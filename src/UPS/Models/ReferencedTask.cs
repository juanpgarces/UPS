using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UPS.Enums;

namespace UPS.Models
{
    public class ReferencedTask<T>
    {
        public Guid guid { get; set; }
        public Func<Task<T>> func { get; set; }
        public Priority priority { get; set; }
        public long currentAttempt { get; set; }
    }
}
