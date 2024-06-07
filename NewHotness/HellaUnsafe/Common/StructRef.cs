using System;
using System.Collections.Generic;
using System.Text;

namespace HellaUnsafe.Common
{
    internal class StructRef<T> where T : struct
    {
        internal T Value;

        public StructRef()
        {
            Value = default(T);
        }

        public StructRef(T value)
        {
            Value = value;
        }
    }
}
