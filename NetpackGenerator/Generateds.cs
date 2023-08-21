namespace Netpack {
    using System;
    
    
    public static class Serializer {
        
        public static void Serialize(this TestMessage TestMessage, ref byte[] Data, ref int Index) {
            byte[] Bytes = new byte[0];
            Bytes = BitConverter.GetBytes(TestMessage.Id);
            Buffer.BlockCopy(Bytes, 0, Data, Index, Bytes.Length);
            Index = Index + Bytes.Length;
            Bytes = BitConverter.GetBytes(TestMessage.Value);
            Buffer.BlockCopy(Bytes, 0, Data, Index, Bytes.Length);
            Index = Index + Bytes.Length;
            for (int i = 0; i < TestMessage.Stat.Length; i++
            ) {
                Bytes = BitConverter.GetBytes(TestMessage.Stat[i]);
                Buffer.BlockCopy(Bytes, 0, Data, Index, Bytes.Length);
                Index = Index + Bytes.Length;
            }
        }
        
        public static void Deserialize(this byte[] Data, ref int Index, out Netpack.TestMessage TestMessage) {
            TestMessage = new();
            TestMessage.Id = BitConverter.ToInt32(Data, Index);
            Index = Index + sizeof(Int32);
            TestMessage.Value = BitConverter.ToSingle(Data, Index);
            Index = Index + sizeof(Single);
        }
    }
}
