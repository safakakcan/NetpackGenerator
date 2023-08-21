using Netpack;
using System;

namespace Netpack
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Generator.Generate(typeof(TestMessage));
            
        }
    }
}