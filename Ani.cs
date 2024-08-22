using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

using Vector2 = (int x, int y);

// Reference：https://www.cnblogs.com/youlin/p/3282263.html
public static class Ani
{
    public static void Convert()
    {
        #region 0 Input
        List<Bitmap> images = [];
        int width = -1, height = -1;
        while (true)
        {
            Console.Write($"图片文件位置 (索引：{images.Count}，留空继续): ");
            string s = Console.ReadLine();
            if (string.IsNullOrEmpty(s))
                break;
            Bitmap bm = (Bitmap)Image.FromFile(s);
            images.Add(bm);
            if (width == -1 && height == -1)
            {
                width = bm.Width;
                height = bm.Height;
            }
            if (width != bm.Width || height != bm.Height)
                return;
        }



        Console.Write("播放速率 (单位为1/60秒): ");
        int displayRate = int.Parse(Console.ReadLine());



        List<int> rates = [];
        for (int g = 0; g < images.Count; g++)
        {
            Console.Write($"第 {rates.Count} 帧播放速率 (单位为1/60秒，留空为 {displayRate}): ");
            string s = Console.ReadLine();
            if (string.IsNullOrEmpty(s))
            {
                rates.Add(displayRate);
                continue;
            }
            int i = int.Parse(s);
            rates.Add(i);
        }



        Console.Write("热点X坐标: ");
        int hotspotX = int.Parse(Console.ReadLine());
        Console.Write("热点Y坐标: ");
        int hotspotY = int.Parse(Console.ReadLine());



        List<Vector2> hotspots = [];
        for (int g = 0; g < images.Count; g++)
        {
            Console.Write($"第 {hotspots.Count} 帧热点X坐标（留空为 {hotspotX}）: ");
            string s = Console.ReadLine();
            int x;
            if (string.IsNullOrEmpty(s))
                x = hotspotX;
            else
                x = int.Parse(s);

            Console.Write($"第 {hotspots.Count} 帧热点Y坐标（留空为 {hotspotY}）: ");
            s = Console.ReadLine();
            int y;
            if (string.IsNullOrEmpty(s))
                y = hotspotY;
            else
                y = int.Parse(s);

            hotspots.Add(new(x, y));
        }



        Console.Write("输出文件位置: ");
        string outputPath = Console.ReadLine();

        int len = GetData(out byte[] buffer, width, height, images, displayRate, rates, hotspots);
        byte[] val = buffer[..len];
        Console.WriteLine(len);
        File.WriteAllBytes(outputPath, val);
        #endregion
    }

    // 这个函数来获取ANI的数据！！！！
    public static int GetData(out byte[] buffer, int width, int height, List<Bitmap> images, int rate, List<int> rates, List<Vector2> hotspots)
    {
        BinaryWriter bw = new(new MemoryStream());

        int datalistoffset = 0;
        int fullfilesize = 0;

        #region 1 Header
        // Header
        // RIFF (52 49 46 46)
        bw.Write([0x52, 0x49, 0x46, 0x46]);

        // File Length
        int fileLengthPos = 0x0004;
        bw.Write([0, 0, 0, 0]);

        // ACON
        // ACON (41 43 4F 4E)
        bw.Write([0x41, 0x43, 0x4F, 0x4E]);
        datalistoffset += 8 + sizeof(int);
        #endregion

        #region 2 ANIHeader
        // ANIH Sign
        // anih (61 6E 69 68)
        bw.Write([0x61, 0x6E, 0x69, 0x68]);

        // Anih struct
        AniHeader anih = new()
        {
            dwNumFrames = images.Count,
            dwNumSteps = images.Count,
            dwWidth = width,
            dwHeight = height,
            dwDisplayRate = rate,
            dwFlags = AniHeader.Flags.IconOrCursor | AniHeader.Flags.HasSeqSegment,
        };

        bw.Write(anih.dwHeaderSize); // 我不知道为啥，但是cursorworkshop导出的这里就多一个这个
        datalistoffset += 4 + sizeof(int);

        int size = Marshal.SizeOf(anih);
        nint b = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(anih, b, false);
        byte[] bytes = new byte[size];
        Marshal.Copy(b, bytes, 0, size);
        bw.Write(bytes);
        datalistoffset += size;
        #endregion

        #region 3 SEQ
        // SEQ sign
        // "seq " (73 65 71 20)
        bw.Write([0x73, 0x65, 0x71, 0x20]);

        // Data length
        bw.Write(images.Count * sizeof(int));

        for (int i = 0; i < images.Count; i++)
            bw.Write(i);
        datalistoffset += 4 + sizeof(int) + images.Count * sizeof(int);
        #endregion

        #region 4 Rate
        // Rate sign
        // rate (72 61 74 65)
        bw.Write([0x72, 0x61, 0x74, 0x65]);

        // Data length
        bw.Write(rates.Count * sizeof(int));
        datalistoffset += 4 + rates.Count * sizeof(int) + sizeof(int);

        foreach (int i in rates)
            bw.Write(i);
        #endregion

        #region 5 List (Frame data)
        // List sign
        // LIST (4C 49 53 54)
        bw.Write([0x4C, 0x49, 0x53, 0x54]);
        datalistoffset += 4;
        fullfilesize += datalistoffset;

        // Data Length
        bw.Write([0, 0, 0, 0]);
        int datalenpos = datalistoffset;

        // Fram sign
        // fram (66 72 61 6D)
        bw.Write([0x66, 0x72, 0x61, 0x6D]);
        fullfilesize += sizeof(int);

        int datalength = 4;
        for (int i = 0; i < images.Count; i++)
        {
            // Icon sign
            // icon (69 63 6F 6E)
            bw.Write([0x69, 0x63, 0x6F, 0x6E]);

            int len = Cur.GetData(out byte[] buf, images[i], hotspots[i].x, hotspots[i].y);
            bw.Write(len);
            byte[] trimedbuf = buf[..len];
            bw.Write(trimedbuf);
            datalength += len + 4 + sizeof(int);
        }
        fullfilesize += datalength;

        buffer = ((MemoryStream)bw.BaseStream).GetBuffer();

        byte[] ffsb = BitConverter.GetBytes(fullfilesize);
        buffer[fileLengthPos] = ffsb[0];
        buffer[fileLengthPos + 1] = ffsb[1];
        buffer[fileLengthPos + 2] = ffsb[2];
        buffer[fileLengthPos + 3] = ffsb[3];
        ffsb = BitConverter.GetBytes(datalength);
        buffer[datalenpos] = ffsb[0];
        buffer[datalenpos + 1] = ffsb[1];
        buffer[datalenpos + 2] = ffsb[2];
        buffer[datalenpos + 3] = ffsb[3];
        return fullfilesize;
        #endregion
    }

    // _anih
    [StructLayout(LayoutKind.Sequential)]
    private struct AniHeader
    {
        public int dwHeaderSize = 36;
        public int dwNumFrames;      // Frame count
        public int dwNumSteps;       // Begin frame
        public int dwWidth;          // Image width
        public int dwHeight;         // Image height
        public int dwBitCount = 32;  // Color pixel bit count
        public int dwNumPlanes = 1;  // Planes number
        public int dwDisplayRate;    // Frame delay, unit: 1/60 second
        public Flags dwFlags;        // Flags

        public AniHeader()
        { }

        [Flags]
        public enum Flags : int
        {
            IconOrCursor = 0b00000001,
            HasSeqSegment = 0b00000010,
        }
    }
}