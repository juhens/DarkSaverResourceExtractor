using System.Runtime.InteropServices;

namespace GrsExtractor
{
    internal class Program
    {
        private static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory + "extract\\";
        private static string _fileName = string.Empty;
        private static int _fileSize;
        private static int _exportIndex;
        private const int TextMargin = -16;

        private static void CheckSignature(ref Span<byte> bSpan)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"Signature",TextMargin}{_exportIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 8).ToArray()).Replace("-", " ")}");

            var signature = MemoryMarshal.Read<ulong>(bSpan.Slice(0, 8));
            if (signature != 137438953504) //0x20 0x00 0x00 0x00 0x20 0x00 0x00 0x00
                throw new Exception("파일 헤더 오류");

            bSpan = bSpan.Slice(8, bSpan.Length - 8);
        }

        private static void CheckFirstArgs(ref Span<byte> bSpan)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"FirstArgs",TextMargin}{_exportIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");
            int args1 = MemoryMarshal.Read<int>(bSpan.Slice(0, 4));
            int args2 = MemoryMarshal.Read<int>(bSpan.Slice(4, 4));
            int args3 = MemoryMarshal.Read<int>(bSpan.Slice(8, 4));
            int args4 = MemoryMarshal.Read<int>(bSpan.Slice(12, 4));

            bSpan = bSpan.Slice(16, bSpan.Length - 16);
        }

        private static void CheckArgs(ref Span<byte> bSpan)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"Args",TextMargin}{_exportIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");

            int args1 = MemoryMarshal.Read<int>(bSpan.Slice(0, 4));
            int args2 = MemoryMarshal.Read<int>(bSpan.Slice(4, 4));
            int args3 = MemoryMarshal.Read<int>(bSpan.Slice(8, 4));
            int args4 = MemoryMarshal.Read<int>(bSpan.Slice(12, 4));

            if (args1 == 0x20 && args2 == 0x20)
            {
                bSpan = bSpan.Slice(8, bSpan.Length - 8);
            }
            else if (args1 % 0x20 == 0 && args2 % 0x20 == 0)
            {
                bSpan = bSpan.Slice(8, bSpan.Length - 8);
            }
            else
            {
                bSpan = bSpan.Slice(12, bSpan.Length - 12);
            }
        }

        private static Span<byte> GetColorTable(ref Span<byte> bSpan)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"ColorTable",TextMargin}{_exportIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");

            var colorTable = bSpan.Slice(0, 1024);
            //swap red blue
            for (int i = 0; i < colorTable.Length; i += 4)
            {
                (colorTable[i], colorTable[i + 2]) = (colorTable[i + 2], colorTable[i]);
            }

            bSpan = bSpan.Slice(1024, bSpan.Length - 1024);
            return colorTable;
        }

        private static SpriteHeader GetSpriteHeader(ref Span<byte> bSpan, bool isFirst = true)
        {
            SpriteHeader spriteHeader;
            while (true)
            {
                var spriteCount = isFirst ? bSpan[0] : (byte)0;
                var width = isFirst
                    ? MemoryMarshal.Read<int>(bSpan.Slice(1, 4))
                    : MemoryMarshal.Read<int>(bSpan.Slice(0, 4));
                var height = isFirst
                    ? MemoryMarshal.Read<int>(bSpan.Slice(5, 4))
                    : MemoryMarshal.Read<int>(bSpan.Slice(4, 4));
                var dataLength = isFirst
                    ? MemoryMarshal.Read<int>(bSpan.Slice(9, 4))
                    : MemoryMarshal.Read<int>(bSpan.Slice(8, 4));

                //mdrs21 예외, 첫번째일때 스프라이트 갯수 검사
                if (!isFirst || spriteCount != 0)
                {
                    //Mdsr129 예외
                    //이미지 크기가 1 이상인지 
                    if ((width > 0x1 && height > 0x1))
                    {
                        //오버플로우 검사
                        long pixelCount = (long)width * (long)height;
                        if (pixelCount <= int.MaxValue)
                        {
                            //음수 검사
                            if (pixelCount > 0 && dataLength > 0)
                            {
                                //픽셀갯수와 데이터길이가 일치하는지
                                if (pixelCount == dataLength || pixelCount == dataLength - 2)
                                {
                                    Console.WriteLine(isFirst
                                        ? $"0x{_fileSize - bSpan.Length:X8} {"SpriteHeader[0]",TextMargin}{_exportIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 15).ToArray()).Replace("-", " ")}"
                                        : $"0x{_fileSize - bSpan.Length:X8} {"SpriteHeader[1]",TextMargin}{_exportIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 14).ToArray()).Replace("-", " ")}");
                                    spriteHeader = new(spriteCount, width, height, dataLength);
                                    break;
                                }
                            }
                        }

                    }
                }

                bSpan = bSpan.Slice(1, bSpan.Length - 1);
            }

            bSpan = isFirst ? bSpan.Slice(15, bSpan.Length - 15) : bSpan.Slice(14, bSpan.Length - 14);
            return spriteHeader;
        }

        private static Span<byte> GetPixelData(ref Span<byte> bSpan, SpriteHeader spriteHeader)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"PixelData",TextMargin}{_exportIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");
            var pixelData = bSpan.Slice(0, spriteHeader.Width * spriteHeader.Height);
            bSpan = bSpan.Slice(spriteHeader.DataLength, bSpan.Length - spriteHeader.DataLength);
            return pixelData;
        }

        private static void CreateBmpFile(string path, Span<byte> widthArr, Span<byte> heightArr, Span<byte> colorTable,
            Span<byte> pixelData)
        {

            byte[] bmpHeader1 =
            {
                0x42, 0x4D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x04, 0x00, 0x00, 0x28, 0x00,
                0x00, 0x00
            };


            byte[] bmpHeader2 =
            {
                0x01, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            MemoryStream ms = new();
            ms.Write(bmpHeader1);
            ms.Write(widthArr);
            ms.Write(heightArr);
            ms.Write(bmpHeader2);
            ms.Write(colorTable);
            ms.Write(pixelData);
            byte[] combined = ms.ToArray();
            FileStream fs = new FileStream($"{path}{_exportIndex:D}.bmp", FileMode.Create);
            fs.Write(combined, 0, combined.Length);
            fs.Close();
        }


        struct SpriteHeader
        {
            public readonly byte SpriteCount;
            public readonly int Width;
            public readonly int Height;
            public readonly int DataLength;

            public SpriteHeader(byte spriteCount, int width, int height, int dataLength)
            {
                SpriteCount = spriteCount;
                Width = width;
                Height = height;
                DataLength = dataLength;
            }
        }

        static void Main(string[] args)
        {
            //args = new string[1];
            //args[0] = "E:\\비주얼스튜디오\\DarkSaverServer\\GrsReader\\MAP\\Mdsr129.grs";
            if (args.Length == 0)
            {
                Console.WriteLine("Argument가 없습니다.");
                Thread.Sleep(2000);
                return;
            }


            //extract 폴더 생성
            DirectoryInfo directoryInfo = new(BasePath);
            if (!directoryInfo.Exists)
                directoryInfo.Create();


            foreach (string fileLocate in args)
            {
                if (Path.GetExtension(fileLocate).ToLower() != ".grs")
                {
                    Console.WriteLine(fileLocate + ".grs 파일이 아닙니다.");
                    continue;
                }

                _fileName = Path.GetFileNameWithoutExtension(fileLocate);
                _exportIndex = 0;

                //_fileName 폴더 생성
                string extractPath = BasePath + _fileName + "\\";
                directoryInfo = new(extractPath);
                if (!directoryInfo.Exists)
                    directoryInfo.Create();


                var bSpan = File.ReadAllBytes(fileLocate).AsSpan();
                _fileSize = bSpan.Length;

                CheckSignature(ref bSpan);
                CheckFirstArgs(ref bSpan);

                //예외 mdsr15는 마지막에 00 00 00 00 있음
                while (bSpan.Length > 4)
                {
                    if (_exportIndex > 0)
                        CheckArgs(ref bSpan);

                    var colorTable = GetColorTable(ref bSpan);
                    var spriteHeader = GetSpriteHeader(ref bSpan);
                    var loopCount = spriteHeader.SpriteCount;

                    for (int i = 0; i < loopCount; i++)
                    {
                        //스프라이트 갯수 여러개일때
                        if (i > 0)
                        {
                            spriteHeader = GetSpriteHeader(ref bSpan, false);
                        }

                        var pixelData = GetPixelData(ref bSpan, spriteHeader);

                        Console.WriteLine($"{"",27}{_exportIndex,3}.bmp");
                        CreateBmpFile(extractPath, BitConverter.GetBytes(spriteHeader.Width),
                            BitConverter.GetBytes(spriteHeader.Height),
                            colorTable, pixelData);

                        _exportIndex++;
                    }
                }
            }
        }
    }
}