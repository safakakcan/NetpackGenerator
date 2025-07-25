﻿using System;
using System.Collections.Generic;

namespace Netpack
{
    public class TestMessage : INetpack
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
        public int[] RelatedIds;

        public InnerStruct()
        {
            Id = 13;
            Speed = 100;
            Data = new byte[4];
            Data[2] = 12;
            RelatedIds = new int[8];
            RelatedIds[2] = 24;
        }
    }
}
