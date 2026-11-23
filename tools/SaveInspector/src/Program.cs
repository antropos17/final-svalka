using System;

namespace JunkToNeon.SaveInspector
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            using (var stdout = Console.OpenStandardOutput()) return Cli.Run(args, stdout);
        }
    }
}
