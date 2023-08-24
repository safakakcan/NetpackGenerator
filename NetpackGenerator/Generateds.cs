namespace Netpack {
    using System;
    using System.Runtime.InteropServices;
    
    
    public static class Serializer {
        
        public static void Serialize(this TestMessage TestMessage, System.Span<byte> Data, ref int Index) {
            ushort ArraySize;
            // Write value of TestMessage.Id
            MemoryMarshal.Write<int>(Data.Slice(Index), ref TestMessage.Id);
            Index = Index + sizeof(Int32);
            // Write value of TestMessage.Value
            MemoryMarshal.Write<float>(Data.Slice(Index), ref TestMessage.Value);
            Index = Index + sizeof(Single);
            // Write array size of TestMessage.Stat
            ArraySize = (ushort)TestMessage.Stat.Length;
            MemoryMarshal.Write<ushort>(Data.Slice(Index), ref ArraySize);
            Index = Index + sizeof(ushort);
            // Iterate TestMessage.Stat array
            for (int i = 0; i < TestMessage.Stat.Length; i++
            ) {
                // Write value of TestMessage.Stat[i].Id
                MemoryMarshal.Write<int>(Data.Slice(Index), ref TestMessage.Stat[i].Id);
                Index = Index + sizeof(Int32);
                // Write value of TestMessage.Stat[i].Speed
                MemoryMarshal.Write<float>(Data.Slice(Index), ref TestMessage.Stat[i].Speed);
                Index = Index + sizeof(Single);
                // Write array size of TestMessage.Stat[i].Data
                ArraySize = (ushort)TestMessage.Stat[i].Data.Length;
                MemoryMarshal.Write<ushort>(Data.Slice(Index), ref ArraySize);
                Index = Index + sizeof(ushort);
                // Iterate TestMessage.Stat[i].Data array
                for (int ii = 0; ii < TestMessage.Stat[i].Data.Length; ii++
                ) {
                    MemoryMarshal.Write<byte>(Data.Slice(Index), ref TestMessage.Stat[i].Data[ii]);
                    Index = Index + sizeof(Byte);
                }
            }
        }
        
        public static void Deserialize(this Span<byte> Data, ref int Index, out Netpack.TestMessage TestMessage) {
            ushort ArraySize;
            TestMessage = new();
            // Read value of TestMessage.Id
            TestMessage.Id = MemoryMarshal.Read<int>(Data.Slice(Index, sizeof(Int32)));
            Index = Index + sizeof(Int32);
            // Read value of TestMessage.Value
            TestMessage.Value = MemoryMarshal.Read<float>(Data.Slice(Index, sizeof(Single)));
            Index = Index + sizeof(Single);
            // Write array size of TestMessage.Stat
            ArraySize = MemoryMarshal.Read<ushort>(Data.Slice(Index, sizeof(ushort)));
            TestMessage.Stat = new InnerStruct[ArraySize];
            Index = Index + sizeof(ushort);
            // Iterate TestMessage.Stat array
            for (int i = 0; i < TestMessage.Stat.Length; i++
            ) {
                TestMessage.Stat[i] = new();
                // Read value of TestMessage.Stat[i].Id
                TestMessage.Stat[i].Id = MemoryMarshal.Read<int>(Data.Slice(Index, sizeof(Int32)));
                Index = Index + sizeof(Int32);
                // Read value of TestMessage.Stat[i].Speed
                TestMessage.Stat[i].Speed = MemoryMarshal.Read<float>(Data.Slice(Index, sizeof(Single)));
                Index = Index + sizeof(Single);
                // Write array size of TestMessage.Stat[i].Data
                ArraySize = MemoryMarshal.Read<ushort>(Data.Slice(Index, sizeof(ushort)));
                TestMessage.Stat[i].Data = new Byte[ArraySize];
                Index = Index + sizeof(ushort);
                // Iterate TestMessage.Stat[i].Data array
                for (int ii = 0; ii < TestMessage.Stat[i].Data.Length; ii++
                ) {
                    TestMessage.Stat[i].Data[ii] = MemoryMarshal.Read<byte>(Data.Slice(Index));
                    Index = Index + sizeof(Byte);
                }
            }
        }
    }
}
