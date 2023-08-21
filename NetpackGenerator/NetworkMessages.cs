using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netpack
{
    public class TestMessage
    {
        public int Id;
        public float Value;
        public InnerStruct[] Stat;

        public TestMessage()
        {
            Id = 12;
            Value = 32.25f;
            Stat = new InnerStruct[]
            {
                new InnerStruct(),
                new InnerStruct(),
                new InnerStruct()
            };
        }
    }

    public class InnerStruct
    {
        public int Id;
        public float Speed;

        public InnerStruct()
        {
            Id = 13;
            Speed = 100;
        }
    }
}
