using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        private readonly string name;

        public VM(string name)
        {
            FontLoader.FromResource("hex_font.png", new Span<byte>(Ram, 0, 0x80));
            this.name = name;
            Console.Title += ": " + name;
        }

        public void LoadRom(string filename, ushort baseAddress = 0x0200)
        {
            using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var memStream = new MemoryStream(Ram, baseAddress, Ram.Length - baseAddress, true))
                stream.CopyTo(memStream);
            Registers.PC = baseAddress;
        }

        public void Run()
        {
            var time = new Stopwatch();
            var tickrate = TimeSpan.FromTicks((long)(1.0 / 60.0 * Stopwatch.Frequency));
            var fpsUpdateThreshold = 0.5 * Stopwatch.Frequency;
            var tps = 0.0;
            var inputThread = new Thread(() =>
            {
                var @continue = true;
                do
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D0:
                            lock (KeyCodes) KeyCodes[0x0] = 1;
                            break;
                        case ConsoleKey.D1:
                            lock (KeyCodes) KeyCodes[0x1] = 1;
                            break;
                        case ConsoleKey.D2:
                            lock (KeyCodes) KeyCodes[0x2] = 1;
                            break;
                        case ConsoleKey.D3:
                            lock (KeyCodes) KeyCodes[0x3] = 1;
                            break;
                        case ConsoleKey.D4:
                            lock (KeyCodes) KeyCodes[0x4] = 1;
                            break;
                        case ConsoleKey.D5:
                            lock (KeyCodes) KeyCodes[0x5] = 1;
                            break;
                        case ConsoleKey.D6:
                            lock (KeyCodes) KeyCodes[0x6] = 1;
                            break;
                        case ConsoleKey.D7:
                            lock (KeyCodes) KeyCodes[0x7] = 1;
                            break;
                        case ConsoleKey.D8:
                            lock (KeyCodes) KeyCodes[0x8] = 1;
                            break;
                        case ConsoleKey.D9:
                            lock (KeyCodes) KeyCodes[0x9] = 1;
                            break;
                        case ConsoleKey.A:
                            lock (KeyCodes) KeyCodes[0xa] = 1;
                            break;
                        case ConsoleKey.B:
                            lock (KeyCodes) KeyCodes[0xb] = 1;
                            break;
                        case ConsoleKey.C:
                            lock (KeyCodes) KeyCodes[0xc] = 1;
                            break;
                        case ConsoleKey.D:
                            lock (KeyCodes) KeyCodes[0xd] = 1;
                            break;
                        case ConsoleKey.E:
                            lock (KeyCodes) KeyCodes[0xe] = 1;
                            break;
                        case ConsoleKey.F:
                            lock (KeyCodes) KeyCodes[0xf] = 1;
                            break;
                        case ConsoleKey.Q:
                        case ConsoleKey.Escape:
                            @continue = false;
                            break;
                    }
                } while (@continue);
            });
            var drawThread = new Thread(() =>
            {
                var videoBufferCopy = new ulong[VideoBuffer.Length];
                var bufferLengthInBytes = videoBufferCopy.Length * sizeof(ulong);
                var lastFpsTimestamp = 0.0;
                var frames = 0;
                var fps = 0.0;
                var outBuf = new StringBuilder(Console.WindowWidth * Console.WindowHeight);
                do
                {
                    lock (VideoBuffer)
                        Buffer.BlockCopy(VideoBuffer, 0, videoBufferCopy, 0, bufferLengthInBytes);
                    Console.SetCursorPosition(0, 0);
                    outBuf.Clear();
                    for (var y = 0; y < videoBufferCopy.Length; y++)
                    {
                        var line = videoBufferCopy[y];
                        for (var x = 0; x < sizeof(ulong) * 8; x++)
                        {
                            outBuf.Append((line & 0x8000000000000000) == 0 ? "  " : "██");
                            line <<= 1;
                        }
                    }
                    outBuf.Append($"{name}: {tps:#0.00} tps / {fps:#0.00} fps ");
                    outBuf.Append(Registers.DT > 0 ? '!' : ' ');
                    if (Registers.ST > 0)
                    {
                        Console.Beep(440, tickrate.Milliseconds);
                        outBuf.Append('☼');
                    }
                    else
                        outBuf.Append(' ');
                    outBuf.Append(' ');
                    for (var i = 0; i < KeyCodes.Length; i++)
                        outBuf.Append(KeyCodes[i] == 0 ? " " : i.ToString("X1"));
                    Console.Write(outBuf.ToString());
                    frames++;
                    var timeDelta = time.Elapsed.Ticks - lastFpsTimestamp;
                    if (timeDelta > fpsUpdateThreshold)
                    {
                        fps = frames / timeDelta * Stopwatch.Frequency;
                        frames = 0;
                        lastFpsTimestamp = time.Elapsed.Ticks;
                    }
                } while (inputThread.IsAlive);
            });
            inputThread.Start();
            drawThread.Start();
            var ticks = 0;
            var lastTpsTimestamp = 0.0;
            time.Restart();
            do
            {
                var nextTick = time.Elapsed + tickrate;
                try
                {
                    Tick();
                    lock (KeyCodes)
                    {
                        Array.Clear(KeyCodes, 0, KeyCodes.Length);
                    }
                    if (Registers.DT > 0)
                        Registers.DT--;
                    if (Registers.ST > 0)
                        Registers.ST--;
                    ticks++;
                    var timeDelta = time.Elapsed.Ticks - lastTpsTimestamp;
                    if (timeDelta > fpsUpdateThreshold)
                    {
                        tps = ticks / timeDelta * Stopwatch.Frequency;
                        ticks = 0;
                        lastTpsTimestamp = time.Elapsed.Ticks;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw e;
                }

                var sleepTime = (nextTick.Ticks - time.Elapsed.Ticks) * 1000 / Stopwatch.Frequency;
                Thread.Sleep((int) Math.Max(0, sleepTime - 1));
            } while (inputThread.IsAlive);
        }

        private void Tick()
        {
            if (//(Registers.PC & 1) == 1 || 
                Registers.PC > Ram.Length-2)
                throw new InvalidOperationException($"PC was 0x{Registers.PC:x4}");

            ref var i1 = ref Ram[Registers.PC];
            ref var i2 = ref Ram[Registers.PC + 1];
            switch (i1 & 0xf0)
            {
                case 0x00:
                {
                    if (i1 == 0x00)
                    {
                        // CLS
                        if (i2 == 0xe0)
                        {
                            lock(VideoBuffer)
                                Array.Clear(VideoBuffer, 0, VideoBuffer.Length);
                            break;
                        }
                        // RET
                        if (i2 == 0xee)
                        {
                            Registers.PC = Stack[Registers.SP];
                            if (Registers.SP > 0) //todo: should it halt?
                                Registers.SP--;
                            Inc(ref Registers.PC);
                            break;
                        }
                    }
                    // SYS addr
                    goto case 0x20;
                }
                // JP addr
                case 0x10:
                {
                    Registers.PC = GetAddr(ref i1, ref i2);
                    break;
                }
                // CALL addr
                case 0x20:
                {
                    Registers.SP++;
                    if (Registers.SP >= Stack.Length)
                        throw new StackOverflowException();

                    Stack[Registers.SP] = Registers.PC;
                    Registers.PC = GetAddr(ref i1, ref i2);
                    break;
                }
                // SE Vx, byte
                case 0x30:
                {
                    if (Registers.VR[GetX(ref i1)] == i2)
                        Inc(ref Registers.PC);
                    Inc(ref Registers.PC);
                    break;
                }
                // SNE Vx, byte
                case 0x40:
                {
                    if (Registers.VR[GetX(ref i1)] != i2)
                        Inc(ref Registers.PC);
                    Inc(ref Registers.PC);
                    break;
                }
                // SE Vx, Vy
                case 0x50:
                {
                    CheckY0(ref i1, ref i2);
                    if (Registers.VR[GetX(ref i1)] == Registers.VR[GetY(ref i2)])
                        Inc(ref Registers.PC);
                    Inc(ref Registers.PC);
                    break;
                }
                // LD Vx, byte
                case 0x60:
                {
                    Registers.VR[GetX(ref i1)] = i2;
                    Inc(ref Registers.PC);
                    break;
                }
                // ADD Vx, byte
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
                        // LD Vx, Vy
                        case 0x00:
                        {
                            Registers.VR[x] = Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // OR Vx, Vy
                        case 0x01:
                        {
                            Registers.VR[x] |= Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // AND Vx, Vy
                        case 0x02:
                        {
                            Registers.VR[x] &= Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // XOR Vx, Vy
                        case 0x03:
                        {
                            Registers.VR[x] ^= Registers.VR[y];
                            Inc(ref Registers.PC);
                            break;
                        }
                        // ADD Vx, Vy
                        case 0x04:
                        {
                            var result = Registers.VR[x] + Registers.VR[y];
                            Registers.VF = result > 0xff ? (byte)0x01 : (byte)0x00;
                            Registers.VR[x] = (byte)result;
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SUB Vx, Vy
                        case 0x05:
                        {
                            Registers.VF = Registers.VR[x] > Registers.VR[y] ? (byte)0x01 : (byte)0x00;
                            Registers.VR[x] = (byte)(0x0100 + Registers.VR[x] - Registers.VR[y]);
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SHR Vx {, Vy}
                        case 0x06:
                        {
                            Registers.VF = (byte)(Registers.VR[x] & 0x01);
                            Registers.VR[x] >>= 1;
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SUBN Vx, Vy
                        case 0x07:
                        {
                            Registers.VF = Registers.VR[y] > Registers.VR[x] ? (byte)0x01 : (byte)0x00;
                            Registers.VR[x] = (byte)(0x0100 + Registers.VR[y] - Registers.VR[x]);
                            Inc(ref Registers.PC);
                            break;
                        }
                        // SHL Vx {, Vy}
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
                //SNE Vx, Vy
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
                    var x = Registers.VR[GetX(ref i1)];
                    var y = Registers.VR[GetY(ref i2)];

                    ulong simple(byte spriteLine, byte offset) => (ulong)spriteLine << (56 - offset);
                    ulong trivial(byte spriteLine, byte offset) => spriteLine;
                    ulong wrap(byte spriteLine, byte offset) => ((ulong)spriteLine >> (offset - 56)) | ((ulong)spriteLine << offset);

                    Func<byte, byte, ulong> transform;
                    if (x < 56)
                        transform = simple;
                    else if (x == 56)
                        transform = trivial;
                    else
                        transform = wrap;
                    var yBoundary = y + GetX(ref i2);
                    lock(VideoBuffer)
                        for (ushort yi = y, i = Registers.IR; yi < yBoundary; yi++, i++)
                        {
                            ref var line = ref VideoBuffer[yi % VideoBuffer.Length];
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
                            lock (KeyCodes)
                                if (KeyCodes[Registers.VR[GetX(ref i1)]] == 1)
                                    Inc(ref Registers.PC);
                            break;
                        }
                        // SKNP Vx
                        case 0xa1:
                        {
                            Inc(ref Registers.PC);
                            lock (KeyCodes)
                                if (KeyCodes[Registers.VR[GetX(ref i1)]] == 0)
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
                            lock (KeyCodes)
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
