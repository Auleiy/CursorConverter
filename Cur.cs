using System.Drawing;

public static class Cur
{
    public static void Convert()
    {
        Console.Write("输入位置: ");
        string inputPath = Console.ReadLine();
        Console.Write("输出位置: ");
        string outputPath = Console.ReadLine();
        Console.Write("热点X: ");
        ushort hotspotX = ushort.Parse(Console.ReadLine());
        Console.Write("热点Y: ");
        ushort hotspotY = ushort.Parse(Console.ReadLine());

        int len = GetData(out byte[] b, (Bitmap)Image.FromFile(inputPath), hotspotX, hotspotY);
        byte[] val = b[..len];
        Console.WriteLine(len);
        File.WriteAllBytes(outputPath, val);
    }

    public static int GetData(out byte[] buffer, Bitmap input, int hotspotX = 0, int hotspotY = 0)
    {
        BinaryWriter bw = new(new MemoryStream());



        int fullLength = 0;
        #region 1 Header

        // Reserved data
        // 2 bytes
        // 00 00
        bw.Write([0x00, 0x00]);

        // Resource type
        // 2 bytes
        // 01 00 (1): .ico
        // 02 00 (2): .cur
        bw.Write([0x02, 0x00]);

        // Bitmap count
        // 2 bytes, ushort
        // usually 1
        bw.Write([0x01, 0x00]);

        fullLength += 6;

        #endregion

        #region 2 ICO/CUR information

        // Image scale
        // 2 bytes
        // width: byte, height: byte
        // Set to 0 if greater than 255 (i don't know why)
        uint width = (uint)input.Width;
        uint height = (uint)input.Height;

        byte bi_width;
        byte bi_height;

        if (width > byte.MaxValue)
            bi_width = 0;
        else
            bi_width = (byte)width;

        if (height > byte.MaxValue)
            bi_height = 0;
        else
            bi_height = (byte)height;

        bw.Write([bi_width, bi_height]);

        // Color count
        // 1 byte
        // >= 256 = 0
        // RGBA32 = 00
        // RGB24  = 00
        // 256    = 00
        // 16     = 10
        // mono   = 02
        // I think we only need RGBA32
        bw.Write((byte)0x00);

        // Unused
        // 1 byte
        bw.Write((byte)0x00);

        // Hot spot
        // 4 bytes
        // X: ushort (2 bytes), Y: ushort (2 bytes)
        bw.Write((ushort)hotspotX);
        bw.Write((ushort)hotspotY);

        // Bitmap data chunk (in code: region 3 ~ 6) length
        // 4 bytes, int
        // Will be invoked when finish
        void BitmapDataChunkLength(uint length)
        {
            byte[] b = BitConverter.GetBytes(length);
            bw.BaseStream.Position = 14;
            bw.BaseStream.Insert(b);
        }

        // Bitmap data chunk offset
        // 4 bytes, int
        // Always 22 (0x16)
        bw.Write(0x00000016);
        fullLength += 16;

        #endregion

        uint bdclength = 0;

        #region 3 BMP information header

        // For more information of this region, see BITMAPINFOHEADER (wingdi.h) (https://learn.microsoft.com/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader)

        // BMP information header length
        // 4 bytes, uint
        // Always 40 (0x28)
        bw.Write((uint)0x00000028);
        bdclength += 0x00000028;
        fullLength += 0x00000028;

        // Bitmap width
        // 4 bytes, uint
        bw.Write(width);

        // Bitmap height
        // 4 bytes, uint
        // XOR height + AND height
        bw.Write(height * 2);

        // Bitmap planes
        // 2 bytes, ushort
        bw.Write((ushort)0x0001);

        // Bitmap pixel length
        // 2 bytes, ushort
        // RGBA32 used 32 bits, so it is 32 (0x20)
        bw.Write((ushort)0x0020);

        // Bitmap compression
        // 4 bytes, uint
        // ico/cur file always not compressed, so it is 0 (0x0)
        bw.Write((uint)0x00000000);

        // Bitmap size
        // 4 bytes, uint
        // Will be invoked when finish
        void BitmapSize(uint length)
        {
            byte[] b = BitConverter.GetBytes(length);
            bw.BaseStream.Position = 42;
            bw.BaseStream.Insert(b);
        }

        // X pixels per meter
        // 4 bytes, uint
        // Not used in ico/cur, so it is 0 (0x0)
        bw.Write((uint)0x00000000);

        // Y pixels per meter
        // 4 bytes, uint
        // Not used in ico/cur, so it is 0 (0x0)
        bw.Write((uint)0x00000000);

        // Color Palette used indexes count
        // 4 bytes, uint
        // Not used in ico/cur, so it is 0 (0x0)
        bw.Write((uint)0x00000000);

        // Color Palette important indexes count
        // 4 bytes, uint
        // Not used in ico/cur, so it is 0 (0x0)
        bw.Write((uint)0x00000000);

        #endregion

        #region 4 XOR Palette

        // Not used in RGBA32 ico file.

        #endregion

        uint imagelength = 0;

        #region 5 XOR Bitmap

        // RGBA32 used 32 bit per pixel, equals 4 bytes per pixel
        imagelength += width * height * 4;
        fullLength += (int)width * (int)height * 4;

        // Data
        for (int i = (int)height - 1; i >= 0; i--)
        {
            for (int j = 0; j < width; j++)
            {
                Color c = input.GetPixel(j, i);
                //         00 01 02 03
                // format: BB GG RR AA
                bw.Write([c.B, c.G, c.R, c.A]);
            }
        }

        #endregion

        #region 6 AND Bitmap

        if (width % 8 != 0 || height % 8 != 0)
            throw new Exception("Width or height should be divisible by 8");

        imagelength += width * height / 8;
        fullLength += (int)width * (int)height / 8;

        // From bottom to top, from left to right.
        // If is transparent (background color), the binary will be 1
        // Else the binary will be 0

        for (int i = (int)height - 1; i >= 0; i--)
        {
            bool[] s = new bool[width];
            Array.Fill(s, false);
            for (int j = 0; j < width; j++)
            {
                Color c = input.GetPixel(j, i);
                if (!c.Equals(Color.Transparent))
                    s[j] = true;
            }
            // BinaryWriter can't write binary array T_T
            byte[] buf = new byte[width / 8];
            for (int ib = 0; ib < s.Length; ib++)
            {
                if (s[ib])
                    buf[ib / 8] |= (byte)(1 << (ib & 8));
            }
            bw.Write(buf);
        }

        #endregion

        #region Writing

        bdclength += imagelength;

        BitmapDataChunkLength(bdclength);
        BitmapSize(imagelength);

        buffer = ((MemoryStream)bw.BaseStream).GetBuffer();

        bw.Flush();
        bw.Close();
        bw.Dispose();

        return fullLength;

        #endregion

        // Congratulations! You won!

    }
}