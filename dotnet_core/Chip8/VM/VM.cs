using System;

namespace Chip8VM
{
    public class VM
    {
        public Registers Registers = new Registers();
        public byte[] Stack = new byte[64];
        public ulong[] VideoBuffer = new ulong[32];
        public byte[] Ram = new byte[4096];

        public VM()
        {
            FontLoader.FromResource("hex_font.png", new Span<byte>(Ram, 0, 0x80));
        }
    }
}
