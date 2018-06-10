using System;
using System.IO;
using Chip8VM;

namespace Chip8
{

    internal static class Program
    {
        internal static void Main(string[] args)
        {
            try
            {
                Console.Title = "Chip-8 emulator";
                Console.SetWindowSize(64 * 2, 32+1);
                Console.SetBufferSize(64 * 2, 32+1);
                if (args.Length != 1)
                {
                    Console.WriteLine("No rom path was specified, exiting");
                    return;
                }

                if (!File.Exists(args[0]))
                {
                    Console.WriteLine("File not found");
                    return;
                }
                Console.CursorVisible = false;
                var vm = new VM(Path.GetFileNameWithoutExtension(args[0]));
                vm.LoadRom(args[0]);
                vm.Run();
            }
            finally
            {
                Console.CursorVisible = true;
            }
            Console.WriteLine("\n\nGoodbye");
            Console.ReadKey();
        }
    }
}
