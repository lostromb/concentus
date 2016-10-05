using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common.CPlusPlus
{
    public class FakeUint32
    {
        private long _val;

        public FakeUint32(long val)
        {
            LongVal = val;
        }

        public FakeUint32(uint val)
        {
            LongVal = (long)val;
        }

        public long LongVal
        {
            get
            {
                return _val;
            }
            set
            {
                _val = (0xFFFFFFFFL & (int)value);
            }
        }

        public int IntVal
        {
            get
            {
                return (int)_val;
            }
            set
            {
                _val = (0xFFFFFFFFL & value);
            }
        }

        public FakeUint32 Add(FakeUint32 other)
        {
            return new FakeUint32(_val + other._val);
        }

        public FakeUint32 Add(int val)
        {
            return new FakeUint32(_val + val);
        }

        public FakeUint32 Subtract(FakeUint32 other)
        {
            return new FakeUint32(_val - other._val);
        }

        public FakeUint32 Subtract(int val)
        {
            return new FakeUint32(_val - val);
        }

        public FakeUint32 Or(FakeUint32 other)
        {
            return new FakeUint32(_val | other._val);
        }

        public FakeUint32 And(FakeUint32 other)
        {
            return new FakeUint32(_val & other._val);
        }

        public FakeUint32 RShift(int amount)
        {
            return new FakeUint32(_val >> amount);
        }

        public FakeUint32 LShift(int amount)
        {
            return new FakeUint32((_val << amount) & 0xFFFFFFFFL);
        }
    }
}
