using System;

namespace Chip8VM
{
    public class VM
    {
        public readonly Registers Registers = new Registers();
        public readonly byte[] Stack = new byte[64];
        public readonly ulong[] VideoBuffer = new ulong[32];
        public readonly byte[] Ram = new byte[4096];

        public VM()
        {
            FontLoader.FromResource("hex_font.png", new Span<byte>(Ram, 0, 0x80));
        }
    }
}
