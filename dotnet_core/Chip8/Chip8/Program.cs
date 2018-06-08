using System;
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
                Console.SetWindowSize(64, 32);
                Console.SetBufferSize(64, 32);
                if (args.Length != 1)
                {
                    Console.WriteLine("No rom path was specified, exiting");
                    return;
                }

                Console.CursorVisible = false;
                var vm = new VM();
                Console.WriteLine(vm.Ram.Length);
            }
            finally
            {
                Console.CursorVisible = true;
            }
            Console.WriteLine("Goodbye");
            Console.WriteLine();
        }
    }
}
