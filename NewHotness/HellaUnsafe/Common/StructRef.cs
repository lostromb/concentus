using System;
using System.Collections.Generic;
using System.Text;

namespace HellaUnsafe.Common
{
    internal class StructRef<T> where T : struct
    {
        internal T Value;

        public StructRef(T value)
        {
            Value = value;
        }
    }
}
