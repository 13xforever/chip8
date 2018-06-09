using System;
using System.Threading.Tasks;
using Chip8VM;

namespace Chip8
{

    internal static class Program
    {
        internal static async Task Main(string[] args)
        {
            try
            {
                Console.Title = "Chip-8 emulator";
                Console.SetWindowSize(64*2, 32);
                Console.SetBufferSize(64*2, 32);
                if (args.Length != 1)
                {
                    Console.WriteLine("No rom path was specified, exiting");
                    return;
                }

                Console.CursorVisible = false;
                var vm = new VM();
                await vm.Run();
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
