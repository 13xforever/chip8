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
                Console.SetWindowSize(64 * 2, 32);
                Console.SetBufferSize(64 * 2, 32);
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

                Console.Title += ": " + Path.GetFileNameWithoutExtension(args[0]);
                Console.CursorVisible = false;
                var vm = new VM();
                using (var stream = File.Open(args[0], FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var memStream = new MemoryStream(vm.Ram, 0x0200, vm.Ram.Length - 0x0200, true))
                    stream.CopyTo(memStream);

                vm.Run();
            }
            finally
            {
                Console.CursorVisible = true;
            }
            Console.WriteLine("Goodbye");
            Console.ReadKey();
        }
    }
}
