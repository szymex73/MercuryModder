using MercuryModder.Helpers;
using SkiaSharp;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

namespace MercuryModder.Assets;

public class Texture2DAsset
{
    public UAsset Asset;
    public SKBitmap Bitmap;

    private NormalExport Export;

    public Texture2DAsset(string path)
    {
        Asset = new UAsset(path, true, EngineVersion.VER_UE4_19);
        Export = Asset.Exports[0] as NormalExport;
        Bitmap = GetBitmapFromAsset();
    }

    private SKBitmap GetBitmapFromAsset()
    {
        // TODO: at some point it would be nice to
        // also extract textures from existing assets
        return new SKBitmap();
    }

    public void SetBitmap(SKBitmap bitmap)
    {
        if (bitmap.ColorType != SKColorType.Bgra8888)
        {
            Bitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            bitmap.CopyTo(Bitmap);
        }
        else
        {
            Bitmap = bitmap;
        }
    }

    public void SetName(string name)
    {
        Export.ObjectName = FName.FromString(Asset, name);
    }

    public void Save(string path)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);

        var texture = new FTexture2D(Bitmap);

        texture.Write(writer);
        Export.Extras = ms.ToArray();

        Asset.Write(path);
        ApplyUexpFixup(path, Path.ChangeExtension(path, "uexp"));
    }

    private class FTexture2D
    {
        private SKBitmap Bitmap;

        public FTexture2D(SKBitmap bitmap)
        {
            Bitmap = bitmap;
        }

        public void Write(BinaryWriter writer)
        {
            // Copy bitmap into a buffer
            var pixels = new byte[4 * Bitmap.Width * Bitmap.Height];
            int i = 0;
            unsafe
            {
                var ptr = (byte*)Bitmap.GetPixels();
                for (int y = 0; y < Bitmap.Height; y++)
                {
                    for (int x = 0; x < Bitmap.Width; x++)
                    {
                        pixels[i++] = *(ptr + 0);
                        pixels[i++] = *(ptr + 1);
                        pixels[i++] = *(ptr + 2);
                        pixels[i++] = *(ptr + 3);
                        ptr += 4;
                    }
                }
            }

            var platformData = new FTexturePlatformData(Bitmap.Width, Bitmap.Height, pixels);

            writer.Write(new byte[] {
                // 0x00, 0x00, 0x00, 0x00,
                0x01, 0x00,
                0x01, 0x00,
                0x01, 0x00, 0x00, 0x00,
	            0x0E, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            });
            writer.Write((uint)platformData.CalculatePlatformDataSize());
            platformData.Write(writer);
        }
    }

    private class FTexturePlatformData
    {
        int SizeX { get; set; } = 256;
        int SizeY { get; set; } = 256;
        int NumSlices { get; set; } = 1;
        string PixelFormat { get; set; } = "PF_B8G8R8A8";
        int FirstMip { get; set; } = 0;
        uint BulkByteFlags { get; set; } = 72;
        ulong ContainerOffset { get; set; } = 1178; // ?
        byte[] Pixels { get; set; }
        ulong Unknown { get; set; } = 12; // ?

        public FTexturePlatformData(int sizeX, int sizeY, byte[] pixels)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            Pixels = pixels;
        }

        public int CalculatePlatformDataSize()
        {
            return 60 + Pixels.Length + 16;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(SizeX);
            writer.Write(SizeY);
            writer.Write(NumSlices);
            FStringHelper.Write(writer, PixelFormat);
            writer.Write(FirstMip);
            writer.Write(1);

            writer.Write(1);
            writer.Write(BulkByteFlags);

            writer.Write(Pixels.Length);
            writer.Write(Pixels.Length);

            writer.Write(ContainerOffset);
            writer.Write(Pixels);

            writer.Write(SizeX);
            writer.Write(SizeY);
            writer.Write(Unknown);
        }
    }

    private static void ApplyUexpFixup(string assetPath, string expPath)
    {
        if (!BitConverter.IsLittleEndian)
            // must double check the result of BitConveter.GetBytes() for the new size
            throw new Exception("Big-endian not handled yet");

        // Get the header size from the uasset file
        int headerSize = -1;
        using (var file = File.OpenRead(assetPath))
        {
            using (var reader = new BinaryReader(file))
            {
                file.Seek(0x18, SeekOrigin.Begin);
                headerSize = reader.ReadInt32();
            }
        }
        if (headerSize < 0 || headerSize > 2048)
            throw new Exception("expected header size is out of range");

        // Get the size of the uexp file so we can subtract 12 from it
        // Since we're not changing the size of the export, it should be stable even after another save
        int uexpSize = (int)new FileInfo(expPath).Length;

        // Now, read the asset back in with UAssetAPI to modify the field in Extras
        var asset = new UAsset(assetPath, EngineVersion.VER_UE4_19);
        var jacket = asset.Exports[0] as NormalExport;

        // ...hack lol
        var newSize = BitConverter.GetBytes(uexpSize - 12 + headerSize);
        for (var i = 0; i < newSize.Length; i++)
        {
            jacket.Extras[20 + i] = newSize[i];
        }
        asset.Write(assetPath);
    }
}
