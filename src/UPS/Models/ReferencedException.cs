using System;
using System.Collections.Generic;
using System.Text;

namespace UPS.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class ReferencedException
    {
        /// <summary>
        /// 
        /// </summary>
        public Guid guid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Exception exception { get; set; }
    }
}
