using System;
using System.IO;
using System.Reflection;
using SixLabors.ImageSharp;

namespace Chip8VM
{
    public static class FontLoader
    {
        public static void FromResource(string name, Span<byte> destination)
        {
            using (var stream = GetResourceStream(name))
                LoadFont(stream, destination);
        }

        public static void FromFile(string filename, Span<byte> destination)
        {
            using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                LoadFont(stream, destination);
        }

        private static void LoadFont(Stream stream, Span<byte> destination)
        {
            if (destination.Length < 0x80)
                throw new ArgumentException("Destination is too small", nameof(destination));

            using (var image = Image.Load(stream))
            {
                /*
                if (image.PixelType.BitsPerPixel != 2)
                    throw new InvalidOperationException("Only monochrome images are supported");
                */

                if (image.Width != 128 || image.Height != 5)
                    throw new InvalidOperationException("Image size must be 128x5");

                var background = image[0, 0];
                var dstIdx = 0;
                for (var idx = 0; idx < 16; idx++)
                for (var spriteLine = 0; spriteLine < 5; spriteLine++)
                {
                    byte packedLine = 0;
                    for (var x = idx * 8; x < idx * 8 + 8; x++)
                        packedLine = (byte)((packedLine << 1) | (image[x, spriteLine] == background ? 0 : 1));
                    destination[dstIdx] = packedLine;
                    dstIdx++;
                }
            }
        }

        private static Stream GetResourceStream(string name)
        {
            var assembly = Assembly.GetCallingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();
            foreach (var resourceName in resourceNames)
            {
                if (resourceName.EndsWith(name, StringComparison.InvariantCultureIgnoreCase))
                    return assembly.GetManifestResourceStream(resourceName);
            }

            throw new ArgumentException($"Resource '{name}' wasn't found", nameof(name));
        }
    }
}