namespace Chip8VM
{
    public class Registers
    {
        public byte[] VR = new byte[16];
        public ref byte VF => ref VR[15];
        public ushort IR;
        public ushort PC = 0x0200;
        public byte SP;
        public byte DT;
        public byte ST;
    }
}