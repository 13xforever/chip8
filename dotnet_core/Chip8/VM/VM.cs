using System;
using System.Diagnostics;
using System.Threading;

namespace Chip8VM
{
    public class VM
    {
        public readonly Registers Registers = new Registers();
        public readonly ushort[] Stack = new ushort[32];
        public readonly ulong[] VideoBuffer = new ulong[32];
        public readonly byte[] Ram = new byte[4096];
        public readonly byte[] KeyCodes = new byte[16];

        private readonly Random rng = new Random();
        private readonly byte[] rngBuffer = new byte[1];

        public VM()
        {
            FontLoader.FromResource("hex_font.png", new Span<byte>(Ram, 0, 0x80));
        }

        public void Run()
        {
            var inputThread = new Thread(() =>
            {
                do
                {
                    var key = Console.ReadKey(false);
                    if (key.Key == ConsoleKey.Escape)
                        break;

                } while (true);
            });
            inputThread.Start();
            var tickrate = TimeSpan.FromSeconds(1.0 / 60.0);
            var time = new Stopwatch();
            do
            {
                var nextTick = time.Elapsed + tickrate;
                try
                {
                    Tick();
                    Draw();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw e;
                }
                Thread.Sleep((int)Math.Max(0, nextTick.TotalMilliseconds - time.Elapsed.TotalMilliseconds));
            } while (inputThread.IsAlive);
        }

        public void Tick()
        {
            if ((Registers.PC & 1) == 1 || Registers.PC > Ram.Length-2)
                throw new InvalidOperationException($"PC was 0x{Registers.PC:x4}");

            ref var i1 = ref Ram[Registers.PC];
            ref var i2 = ref Ram[Registers.PC + 1];


            switch (i1 & 0xf0)
            {
                // SYS
                case 0x00:
                {
                    if (i1 == 0x00)
                    {
                        // CLS
                        if (i2 == 0xe0)
                        {
                            Array.Clear(VideoBuffer, 0, VideoBuffer.Length);
                            break;
                        }
                        // RET
                        if (i2 == 0xee)
                        {
                            Registers.PC = Stack[Registers.SP];
                            if (Registers.SP > 0) //todo: should it halt?
                                Registers.SP--;
                            break;
                        }
                    }
                    Registers.PC = GetAddr(ref i1, ref i2);
                    break;
                }
                // JP
                case 0x10:
                {
                    Registers.PC = GetAddr(ref i1, ref i2);
                    break;
                }
                // JP
                case 0x20:
                {
                    Registers.SP++;
                    if (Registers.SP >= Stack.Length)
                        throw new StackOverflowException();

                    Stack[Registers.SP] = Registers.PC;
                    Registers.PC = GetAddr(ref i1, ref i2);
                    break;
                }
                // SE
                case 0x30:
                {
                    if (Registers.VR[GetX(ref i1)] == i2)
                        Inc(ref Registers.PC);
                    Inc(ref Registers.PC);
                    break;
                }
                // SNE
                case 0x40:
                {
                    if (Registers.VR[GetX(ref i1)] != i2)
                        Inc(ref Registers.PC);
                    Inc(ref Registers.PC);
                    break;
                }
                // SE
                case 0x50:
                {
                    CheckY0(ref i1, ref i2);
                    if (Registers.VR[GetX(ref i1)] == Registers.VR[GetY(ref i2)])
                        Inc(ref Registers.PC);
                    Inc(ref Registers.PC);
                    break;
                }
                // LD
                case 0x60:
                {
                    Registers.VR[GetX(ref i1)] = i2;
                    Inc(ref Registers.PC);
                    break;
                }
                // ADD
                case 0x70:
                {
                    Registers.VR[GetX(ref i1)] += i2;
                    Inc(ref Registers.PC);
                    break;
                }
                case 0x80:
                {
                    var x = GetX(ref i1);
                    var y = GetY(ref i2);
                    switch (i2 & 0x0f)
                    {
                        // LD
                        case 0x00:
                        {
                            Registers.VR[x] = Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // OR
                        case 0x01:
                        {
                            Registers.VR[x] |= Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // AND
                        case 0x02:
                        {
                            Registers.VR[x] &= Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // XOR
                        case 0x03:
                        {
                            Registers.VR[x] ^= Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // ADD
                        case 0x04:
                        {
                            var result = Registers.VR[x] + Registers.VR[y];
                            Registers.VF = result > 0xff ? (byte)0x01 : (byte)0x00;
                            Registers.VR[x] = (byte)result;
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SUB
                        case 0x05:
                        {
                            var result = Registers.VR[x] - Registers.VR[y];
                            Registers.VF = Registers.VR[x] > Registers.VR[y] ? (byte)0x01 : (byte)0x00;
                            Registers.VR[x] = (byte)result;
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SHR
                        case 0x06:
                        {
                            Registers.VF = (byte)(Registers.VR[x] & 0x01);
                            Registers.VR[x] >>= 1;
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SUBN
                        case 0x07:
                        {
                            var result = Registers.VR[y] - Registers.VR[x];
                            Registers.VF = Registers.VR[y] > Registers.VR[x] ? (byte)0x01 : (byte)0x00;
                            Registers.VR[x] = (byte)result;
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SHL
                        case 0x0e:
                        {
                            Registers.VF = (Registers.VR[x] & 0x08) == 0x08 ? (byte)0x01 : (byte)0x00;
                            Registers.VR[x] <<= 1;
                            Inc(ref Registers.PC);
                            break;
                        }

                        default:
                        {
                            CheckY0(ref i1, ref i2);
                            Inc(ref Registers.PC);
                            break;
                        }
                    }
                    break;
                }
                //SNE
                case 0x90:
                {
                    CheckY0(ref i1, ref i2);
                    if (Registers.VR[GetX(ref i1)] != Registers.VR[GetY(ref i2)])
                        Inc(ref Registers.PC);
                    Inc(ref Registers.PC);
                    break;
                }
                // LD I
                case 0xa0:
                {
                    Registers.IR = GetAddr(ref i1, ref i2);
                    Inc(ref Registers.PC);
                    break;
                }
                // JP V0, addr
                case 0xb0:
                {
                    Registers.PC = (ushort)(GetAddr(ref i1, ref i2) + Registers.VR[0]);
                    break;
                }
                // RND Vx, byte
                case 0xc0:
                {
                    Registers.VR[GetX(ref i1)] = (byte)(GetRng() & i2);
                    Inc(ref Registers.PC);
                    break;
                }
                //DRW Vx, Vy, nibble
                case 0xd0:
                {
                    Registers.VF = 0x00;
                    var x = GetX(ref i1);

                    ulong simple(byte spriteLine, byte offset) => ((ulong)spriteLine) << (56 - offset);
                    ulong trivial(byte spriteLine, byte offset) => (ulong)spriteLine;
                    ulong wrap(byte spriteLine, byte offset) => (((ulong)spriteLine) >> (offset - 56)) | (((ulong)spriteLine) << (64 - offset));

                    Func<byte, byte, ulong> transform;
                    if (x < 56)
                        transform = simple;
                    else if (x == 56)
                        transform = trivial;
                    else
                        transform = wrap;
                    for (ushort y = 0, i = Registers.IR; y < GetY(ref i2); y++, i++)
                    {
                        ref var line = ref VideoBuffer[y % VideoBuffer.Length];
                        var oldLine = line;
                        var spriteLine = transform(Ram[i], x);
                        line ^= spriteLine;
                        if ((line & oldLine) != oldLine)
                            Registers.VF = 0x01;
                    }
                    Inc(ref Registers.PC);
                    break;
                }
                case 0xe0:
                {
                    switch (i2)
                    {
                        // SKP Vx
                        case 0x9e:
                        {
                            Inc(ref Registers.PC);
                            if (KeyCodes[GetX(ref i1)] != 0)
                                Inc(ref Registers.PC);
                            break;
                        }
                        // SKNP Vx
                        case 0xa1:
                        {
                            Inc(ref Registers.PC);
                            if (KeyCodes[GetX(ref i1)] == 0)
                                Inc(ref Registers.PC);
                            break;
                        }
                        default:
                        {
                            throw new InvalidOperationException($"Invalid instruction 0x{i1:x2}{i2:x2}");
                        }
                    }
                    break;
                }
                case 0xf0:
                {
                    switch (i2)
                    {
                        // LD Vx, DT
                        case 0x07:
                        {
                            Registers.VR[GetX(ref i1)] = Registers.DT;
                            Inc(ref Registers.PC);
                            break;
                        }
                        // LD Vx, K
                        case 0x0a:
                        {
                            for (byte i = 0; i < KeyCodes.Length; i++)
                                if (KeyCodes[i] != 0)
                                {
                                    Registers.VR[GetX(ref i1)] = i;
                                    Inc(ref Registers.PC);
                                }
                            break;
                        }
                        // LD DT, Vx
                        case 0x15:
                        {
                            Registers.DT = Registers.VR[GetX(ref i1)];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // LD ST, Vx
                        case 0x18:
                        {
                            Registers.ST = Registers.VR[GetX(ref i1)];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // ADD I, Vx
                        case 0x1e:
                        {
                            Registers.IR += Registers.VR[GetX(ref i1)];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // LD F, Vx
                        case 0x29:
                        {
                            Registers.IR = (ushort)(GetX(ref i1) * 5);
                            Inc(ref Registers.PC);
                            break;
                        }
                        // LD B, Vx
                        case 0x33:
                        {
                            if (Registers.IR < Ram.Length)
                            {
                                var val = GetX(ref i1);
                                Ram[Registers.IR] = (byte)((val % 1000) / 100);
                                if (Registers.IR + 1 < Ram.Length)
                                {
                                    Ram[Registers.IR + 1] = (byte)((val % 100) / 10);
                                    if (Registers.IR + 2 < Ram.Length)
                                    {
                                        Ram[Registers.IR + 2] = (byte)(val % 10);
                                    }
                                }
                            }
                            Inc(ref Registers.PC);
                            break;
                        }
                        // LD [I], Vx
                        case 0x55:
                        {
                            for (ushort i = Registers.IR, ri = 0; i < Math.Min(Registers.IR + Registers.VR.Length, Ram.Length); i++, ri++)
                                Ram[i] = Registers.VR[ri];
                            Inc(ref Registers.PC);
                            break;
                        }
                        //  LD Vx, [I]
                        case 0x65:
                        {
                            for (ushort i = Registers.IR, ri = 0; i < Math.Min(Registers.IR + Registers.VR.Length, Ram.Length); i++, ri++)
                                Registers.VR[ri] = Ram[i];
                            Inc(ref Registers.PC);
                            break;
                        }
                    }
                    break;
                }
            }
        }

        public void Draw()
        {
            Console.SetCursorPosition(0, 0);
            for (var y = 0; y < VideoBuffer.Length; y++)
            {
                var line = VideoBuffer[y];
                for (var x = 0; x < sizeof(ulong); x++)
                {
                    Console.Write((line & 0x8000000000000000) == 0 ? "  " : "██");
                    line <<= 1;
                }
            }
        }

        private ref byte GetRng()
        {
            rng.NextBytes(rngBuffer);
            return ref rngBuffer[0];
        }

        private static ushort GetAddr(ref byte i1, ref byte i2) => (ushort)(((i1 & 0x0f) << 8) | i2);
        private static byte GetX(ref byte i1) => (byte)(i1 & 0x0f);
        private static byte GetY(ref byte i2) => (byte)((i2 >> 4) & 0x0f);
        private static void Inc(ref ushort pc) => pc += 2;

        private static void CheckY0(ref byte i1, ref byte i2)
        {
            if ((i2 & 0x0f) != 0)
                throw new InvalidOperationException($"Invalid instruction 0x{i1:x2}{i2:x2}");
        }
    }
}
