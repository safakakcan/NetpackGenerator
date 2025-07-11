﻿using Netpack;
using System;
using System.Diagnostics;

namespace Netpack
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (true)
            {
                byte[] bytes = new byte[1024];
                int index = 0;
                var x = new TestMessage();
                TestMessage y = new TestMessage();
                
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                for (int i = 0; i < 10_000; i++)
                {
                    x.Serialize(bytes);
                    index = 0;
                    var span = bytes.AsSpan();
                    span.Deserialize(ref y);
                    index = 0;
                }
                stopwatch.Stop();
                
                Console.WriteLine("Serialize + Deserialize = " + stopwatch.ElapsedMilliseconds.ToString());
                Console.WriteLine($"{x.Id} => {y.Id}");
                Console.WriteLine($"{x.Stat[0].Speed} => {y.Stat[0].Speed}");
                Console.WriteLine($"{x.Stat[0].RelatedIds[2]} => {y.Stat[0].RelatedIds[2]}");
                Console.WriteLine($"{x.Text} => {y.Text}");
                Console.WriteLine($"{x.TextArray[0]} => {y.TextArray[0]}");
                Console.WriteLine($"{x.TextArray[1]} => {y.TextArray[1]}");
            }
            else
            {
                Generator.Generate();
            }
        }
    }
}