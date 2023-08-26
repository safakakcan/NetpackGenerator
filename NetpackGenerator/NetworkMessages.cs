using System;
using System.Collections.Generic;

namespace Netpack
{
    public class TestMessage
    {
        public int Id;
        public float Value;
        public InnerStruct[] Stat;
        public string Text;
        public string[] TextArray;

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
            Text = "Test";
            TextArray = new string[]
            {
                "Line 1",
                "Line 2"
            };
        }
    }

    public class InnerStruct
    {
        public int Id;
        public float Speed;
        public byte[] Data;

        public InnerStruct()
        {
            Id = 13;
            Speed = 100;
            Data = new byte[4];
        }
    }
}
