using System;
using System.Collections.Generic;
using System.Text;

namespace HellaUnsafe.Common
{
    internal class StructPtr<T> where T : struct
    {
        internal T Value { get; set; }
    }
}
