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
            }
        }
        
        public static void Deserialize(this Span<byte> Data, ref int Index, out Netpack.TestMessage TestMessage) {
            TestMessage = new();
            // Read value of TestMessage.Id
            TestMessage.Id = MemoryMarshal.Read<int>(Data.Slice(Index, sizeof(Int32)));
            Index = Index + sizeof(Int32);
            // Read value of TestMessage.Value
            TestMessage.Value = MemoryMarshal.Read<float>(Data.Slice(Index, sizeof(Single)));
            Index = Index + sizeof(Single);
        }
    }
}
