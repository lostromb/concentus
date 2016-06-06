using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common.CPlusPlus
{
    public class BoxedValue<T>
    {
        public T Val;

        public BoxedValue(T v = default(T))
        {
            Val = v;
        }
    }
}
