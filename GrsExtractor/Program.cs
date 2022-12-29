using System.Drawing;
using System.Runtime.InteropServices;

namespace GrsExtractor
{
    internal class Program
    {
        private static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory + "extract\\";
        
        private static int _fileSize;
        private static int _extractIndex;
        private const int TextMargin = -16;

        private static void CheckSignature(ref Span<byte> bSpan)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"Signature",TextMargin}{_extractIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 8).ToArray()).Replace("-", " ")}");

            var signature = MemoryMarshal.Read<ulong>(bSpan.Slice(0, 8));
            if (signature != 137438953504) //0x20 0x00 0x00 0x00 0x20 0x00 0x00 0x00
                throw new Exception("파일 헤더 오류");

            bSpan = bSpan.Slice(8, bSpan.Length - 8);
        }

        private static void CheckFirstArgs(ref Span<byte> bSpan)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"FirstArgs",TextMargin}{_extractIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");
            //int args1 = MemoryMarshal.Read<int>(bSpan.Slice(0, 4));
            //int args2 = MemoryMarshal.Read<int>(bSpan.Slice(4, 4));
            //int args3 = MemoryMarshal.Read<int>(bSpan.Slice(8, 4));
            //int args4 = MemoryMarshal.Read<int>(bSpan.Slice(12, 4));

            bSpan = bSpan.Slice(16, bSpan.Length - 16);
        }

        private static void CheckArgs(ref Span<byte> bSpan)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"Args",TextMargin}{_extractIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");

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
                $"0x{_fileSize - bSpan.Length:X8} {"ColorTable",TextMargin}{_extractIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");

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
            int addOffset = isFirst ? 1 : 0;
            string spriteHeaderText = isFirst ? "SpriteHeader1" : "SpriteHeader2";
            while (true)
            {
                var spriteCount = isFirst ? bSpan[0] : (byte)0;
                var width = MemoryMarshal.Read<int>(bSpan.Slice(0 + addOffset, 4));
                var height = MemoryMarshal.Read<int>(bSpan.Slice(4 + addOffset, 4));
                var dataLength = MemoryMarshal.Read<int>(bSpan.Slice(8 + addOffset, 4));
                var mask = MemoryMarshal.Read<ushort>(bSpan.Slice(10 + addOffset, 2));

                //mdrs21 예외 첫번째일때 스프라이트 갯수 0인지 검사
                if (!isFirst || spriteCount > 0)
                {
                    //Mdsr129 예외
                    //이미지 최소 크기가 2*2 이상인지 
                    if ((width > 0x1 && height > 0x1))
                    {
                        //오버플로우 검사
                        long pixelCount = width * (long)height;
                        if (pixelCount <= int.MaxValue)
                        {
                            //음수 검사
                            if (pixelCount > 0 && dataLength > 0)
                            {
                                //픽셀갯수와 데이터길이가 일치하는지
                                if (pixelCount == dataLength || pixelCount == dataLength - 2)
                                {
                                    Console.WriteLine($"0x{_fileSize - bSpan.Length:X8} {spriteHeaderText,TextMargin}{_extractIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 14 + addOffset).ToArray()).Replace("-", " ")}");
                                    spriteHeader = new(spriteCount, width, height, dataLength, mask);
                                    break;
                                }
                            }
                        }

                    }
                }

                bSpan = bSpan.Slice(1, bSpan.Length - 1);
            }

            bSpan = bSpan.Slice(14 + addOffset, bSpan.Length - (14 + addOffset));
            return spriteHeader;
        }

        private static Span<byte> GetPixelData(ref Span<byte> bSpan, SpriteHeader spriteHeader)
        {
            Console.WriteLine(
                $"0x{_fileSize - bSpan.Length:X8} {"PixelData",TextMargin}{_extractIndex,3}: {BitConverter.ToString(bSpan.Slice(0, 16).ToArray()).Replace("-", " ")}");
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
            FileStream fs = new FileStream($"{path}{_extractIndex:D}.bmp", FileMode.Create);
            fs.Write(combined, 0, combined.Length);
            fs.Close();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        private static void CreatePngFile(string path, SpriteHeader spriteHeader, Span<byte> colorTable, Span<byte> pixelData)
        {
            using var bmp = new Bitmap(spriteHeader.Width, spriteHeader.Height);

            for (int y = 0; y < spriteHeader.Height; y++)
            {
                for (int x = 0; x < spriteHeader.Width; x++)
                {
                    int pos = pixelData[y * spriteHeader.Width + x];

                    int r = colorTable[pos * 4 + 2];
                    int g = colorTable[pos * 4 + 1];
                    int b = colorTable[pos * 4 + 0];

                    //var mask = BitConverter.GetBytes(spriteHeader.Mask);
                    if (r == 0 && g == 0 && b == 0)//(mask[0] == pos)
                    {
                        bmp.SetPixel(x, (spriteHeader.Height - 1) - y, Color.FromArgb(0, r, g, b));
                    }
                    else
                    {
                        bmp.SetPixel(x, (spriteHeader.Height - 1) - y, Color.FromArgb(255, r, g, b));
                    }
                }
            }
            bmp.Save($"{path}{_extractIndex:D}.png");
        }


        struct SpriteHeader
        {
            public readonly byte SpriteCount;
            public readonly int Width;
            public readonly int Height;
            public readonly int DataLength;
            public readonly ushort Mask;

            public SpriteHeader(byte spriteCount, int width, int height, int dataLength, ushort mask)
            {
                SpriteCount = spriteCount;
                Width = width;
                Height = height;
                DataLength = dataLength;
                Mask = mask;
            }
        }

        static void Main(string[] args)
        {
            //args = new string[1];
            //args[0] = "E:\\비주얼스튜디오\\DarkSaverServer\\GrsReader\\MAP\\Mdsr5.grs";
            if (args.Length == 0)
            {
                Console.WriteLine("Argument가 없습니다.");
                Thread.Sleep(2000);
                return;
            }

            //추출 옵션 선택
            bool isBmp;
            while (true)
            {
                Console.WriteLine("파일출력 옵션을 입력하세요. 1:BMP 2:PNG");
                var str = Console.ReadLine();

                if (str == "1")
                {
                    isBmp = true;
                    break;
                }
                else if (str == "2")
                {
                    isBmp = false;
                    break;
                }
                else
                {
                    Console.WriteLine("1 또는 2를 입력 하세요.");
                }
            }


            //extract 폴더 생성
            DirectoryInfo directoryInfo = new(BasePath);
            if (!directoryInfo.Exists)
                directoryInfo.Create();


            foreach (string filePath in args)
            {
                if (Path.GetExtension(filePath).ToLower() != ".grs")
                {
                    Console.WriteLine(filePath + ".grs 파일이 아닙니다.");
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                _extractIndex = 0;

                //_fileName 폴더 생성
                string extractPath = BasePath + fileName + "\\";
                directoryInfo = new(extractPath);
                if (!directoryInfo.Exists)
                    directoryInfo.Create();

                //파일 로드
                var bSpan = File.ReadAllBytes(filePath).AsSpan();
                _fileSize = bSpan.Length;
                CheckSignature(ref bSpan);
                CheckFirstArgs(ref bSpan);


                //예외 mdsr15는 마지막에 00 00 00 00 있음
                while (bSpan.Length > 4)
                {
                    if (_extractIndex > 0)
                        CheckArgs(ref bSpan);

                    var colorTable = GetColorTable(ref bSpan);
                    var spriteHeader = GetSpriteHeader(ref bSpan);
                    var spriteCount = spriteHeader.SpriteCount;

                    for (int i = 0; i < spriteCount; i++)
                    {
                        if (i > 0)
                            spriteHeader = GetSpriteHeader(ref bSpan, false);

                        var pixelData = GetPixelData(ref bSpan, spriteHeader);

                        if (isBmp)
                        {
                            CreateBmpFile(extractPath, BitConverter.GetBytes(spriteHeader.Width),
                                BitConverter.GetBytes(spriteHeader.Height),
                                colorTable, pixelData);
                            Console.WriteLine($"{"",TextMargin - 11}{_extractIndex,3}.bmp");
                        }
                        else
                        {
                            CreatePngFile(extractPath, spriteHeader,colorTable, pixelData);
                            Console.WriteLine($"{"",TextMargin - 11}{_extractIndex,3}.png");
                        }
                        _extractIndex++;
                    }
                }
            }

            Console.WriteLine("\n완료. 아무키나 누르면 종료됩니다.");
            Console.ReadKey();
        }
    }
}