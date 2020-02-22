using System;
using System.Collections.Generic;
using System.Text;

namespace UPS.Models
{
    public class ReferencedException
    {
        public Guid guid { get; set; }
        public Exception exception { get; set; }
    }
}
