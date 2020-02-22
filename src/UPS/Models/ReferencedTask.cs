using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UPS.Enums;

namespace UPS.Models
{
    public class ReferencedTask
    {
        public Guid guid { get; set; }
        public Func<Task<object>> func { get; set; }
        public int priority { get; set; }
        public long currentAttempt { get; set; }
    }
}
