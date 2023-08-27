using Netpack;
using System;

namespace Netpack
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (true)
            {
                int index = 0;
                byte[] bytes = new byte[1024];
                var x = new TestMessage();
                x.Serialize(bytes, ref index);
                Console.WriteLine(string.Join(' ', bytes) + "\n");
                index = 0;
                var span = bytes.AsSpan();
                span.Deserialize(ref index, out var y);
                Console.WriteLine($"{x.Id} => {y.Id}");
                Console.WriteLine($"{x.Stat[0].Speed} => {y.Stat[0].Speed}");
                Console.WriteLine($"{x.Text} => {y.Text}");
                Console.WriteLine($"{x.TextArray[0]} => {y.TextArray[0]}");
                Console.WriteLine($"{x.TextArray[1]} => {y.TextArray[1]}");
                Console.ReadKey();
            }
            else
            {
                Generator.Generate(typeof(TestMessage));
            }
        }
    }
}