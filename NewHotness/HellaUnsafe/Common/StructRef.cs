using System;
using System.Collections.Generic;
using System.Text;

namespace HellaUnsafe.Common
{
    /// <summary>
    /// This is a loose alternative to holding a shared pointer to a struct, e.g.
    /// "my_struct_type* ptr" embedded inside of another struct. It is a boxed
    /// value which means that the reference actually contains the single value,
    /// so you have to be careful that all of the references to the same memory location
    /// actually use the same StructRef object, which works best if the StructRef itself
    /// is a static singleton pointing to a single global readonly struct.
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
