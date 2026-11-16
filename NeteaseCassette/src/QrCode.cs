// QrCode.cs
// 二维码编码器（Model 2），只做字节模式、版本 1~15、L/M/Q/H 四档纠错。
// 参数表照 ISO 18004 抄的，矩阵跟 segno、ZXing 逐块对过。
// 不依赖 UnityEngine 和外部库，游戏里那个登录二维码就是它画的。

using System;
using System.Text;

namespace NeteaseCassette
{
    // 纯 C# 的 QR 编码器，画登录二维码用
    public static class QrCode
    {
        public const int EccLow = 0;
        public const int EccMedium = 1;
        public const int EccQuartile = 2;
        public const int EccHigh = 3;

        private const int MinVersion = 1;
        private const int MaxVersion = 15;

        private const int PenaltyN1 = 3;
        private const int PenaltyN2 = 3;
        private const int PenaltyN3 = 40;
        private const int PenaltyN4 = 10;

        // 每块的纠错码字数，下标 1 起是版本，0 空着
        private static readonly int[][] EccCodewordsPerBlock =
        {
            new int[] { -1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22 },  // L
            new int[] { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24 }, // M
            new int[] { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30 }, // Q
            new int[] { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24 }, // H
        };

        // 纠错块数，下标同上
        private static readonly int[][] EccBlockCount =
        {
            new int[] { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6 },        // L
            new int[] { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10 },       // M
            new int[] { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12 },    // Q
            new int[] { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18 },  // H
        };

        // 每档纠错在格式信息里对应的 5 位值（L=1 M=0 Q=3 H=2）
        private static readonly int[] EccFormatBits = { 1, 0, 3, 2 };

        // GF(256) 的指数/对数表，本原多项式 x^8+x^4+x^3+x^2+1（0x11D）
        private static readonly byte[] GfExp;
        private static readonly byte[] GfLog;

        static QrCode()
        {
            GfExp = new byte[256];
            GfLog = new byte[256];
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = (byte)x;
                GfLog[x] = (byte)i;
                x <<= 1;
                if ((x & 0x100) != 0)
                {
                    x ^= 0x11D;
                }
            }
            GfExp[255] = GfExp[0];
        }

        // 生成矩阵，[y, x]，true 是黑块，不含静区。放不下返回 null。
        public static bool[,] Encode(string text, int eccLevel = EccMedium)
        {
            return Encode(text, eccLevel, -1, 0);
        }

        // 同上，但能强制掩码和版本。forceMask 传 -1 就自动挑，forceVersion 传 0 用最小的。
        public static bool[,] Encode(string text, int eccLevel, int forceMask, int forceVersion)
        {
            if (text == null)
            {
                return null;
            }
            if (eccLevel < EccLow || eccLevel > EccHigh)
            {
                return null;
            }
            if (forceMask < -1 || forceMask > 7)
            {
                return null;
            }
            if (forceVersion < 0 || forceVersion > MaxVersion)
            {
                return null;
            }

            byte[] payload;
            try
            {
                payload = Encoding.UTF8.GetBytes(text);
            }
            catch (Exception)
            {
                return null;
            }

            int version;
            if (forceVersion > 0)
            {
                version = forceVersion;
                if (!Fits(payload.Length, version, eccLevel))
                {
                    return null;
                }
            }
            else
            {
                version = -1;
                for (int v = MinVersion; v <= MaxVersion; v++)
                {
                    if (Fits(payload.Length, v, eccLevel))
                    {
                        version = v;
                        break;
                    }
                }
                if (version < 0)
                {
                    return null;
                }
            }

            byte[] codewords = BuildCodewords(payload, version, eccLevel);
            if (codewords == null)
            {
                return null;
            }

            bool[,] best = null;
            int bestPenalty = int.MaxValue;
            int firstMask = forceMask >= 0 ? forceMask : 0;
            int lastMask = forceMask >= 0 ? forceMask : 7;
            for (int mask = firstMask; mask <= lastMask; mask++)
            {
                bool[,] matrix = BuildMatrix(codewords, version, eccLevel, mask);
                int penalty = PenaltyScore(matrix, version * 4 + 17);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    best = matrix;
                }
            }
            return best;
        }

        // 容量和码字

        private static bool Fits(int byteLength, int version, int eccLevel)
        {
            int ccBits = version <= 9 ? 8 : 16;
            if (byteLength >= (1 << ccBits))
            {
                return false;
            }
            return 4 + ccBits + byteLength * 8 <= GetNumDataCodewords(version, eccLevel) * 8;
        }

        private static int GetNumDataCodewords(int version, int eccLevel)
        {
            return GetNumRawDataModules(version) / 8
                - EccCodewordsPerBlock[eccLevel][version] * EccBlockCount[eccLevel][version];
        }

        private static int GetNumRawDataModules(int version)
        {
            int result = (16 * version + 128) * version + 64;
            if (version >= 2)
            {
                int numAlign = version / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (version >= 7)
                {
                    result -= 36;
                }
            }
            return result;
        }

        private static void AppendBits(byte[] buffer, ref int bitLength, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                int index = bitLength >> 3;
                buffer[index] = (byte)((buffer[index] << 1) | ((value >> i) & 1));
                bitLength++;
            }
        }

        // 把数据码字和纠错码字交织成最终序列
        private static byte[] BuildCodewords(byte[] payload, int version, int eccLevel)
        {
            int dataCodewords = GetNumDataCodewords(version, eccLevel);
            int capacityBits = dataCodewords * 8;
            int ccBits = version <= 9 ? 8 : 16;
            if (payload.Length >= (1 << ccBits))
            {
                return null;
            }
            if (4 + ccBits + payload.Length * 8 > capacityBits)
            {
                return null;
            }

            // 模式 + 字符数 + 内容
            byte[] bits = new byte[dataCodewords];
            int bitLength = 0;
            AppendBits(bits, ref bitLength, 4, 4); // byte mode = 0100
            AppendBits(bits, ref bitLength, payload.Length, ccBits);
            for (int i = 0; i < payload.Length; i++)
            {
                AppendBits(bits, ref bitLength, payload[i], 8);
            }

            // 结束符，补到整字节
            AppendBits(bits, ref bitLength, 0, Math.Min(4, capacityBits - bitLength));
            AppendBits(bits, ref bitLength, 0, (8 - (bitLength & 7)) & 7);

            // 拿 0xEC / 0x11 填满数据区
            for (int pad = 0xEC; bitLength < capacityBits; pad ^= 0xEC ^ 0x11)
            {
                AppendBits(bits, ref bitLength, pad, 8);
            }

            // 分块、算纠错、再交织
            int numBlocks = EccBlockCount[eccLevel][version];
            int eccLength = EccCodewordsPerBlock[eccLevel][version];
            int rawCodewords = GetNumRawDataModules(version) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLength = rawCodewords / numBlocks;
            int shortDataLength = shortBlockLength - eccLength;

            byte[] divisor = ReedSolomonDivisor(eccLength);
            byte[][] dataBlocks = new byte[numBlocks][];
            byte[][] eccBlocks = new byte[numBlocks][];
            int offset = 0;
            int maxDataLength = 0;
            for (int b = 0; b < numBlocks; b++)
            {
                int length = shortDataLength + (b < numShortBlocks ? 0 : 1);
                byte[] block = new byte[length];
                Array.Copy(bits, offset, block, 0, length);
                offset += length;
                dataBlocks[b] = block;
                eccBlocks[b] = ReedSolomonRemainder(block, divisor);
                if (length > maxDataLength)
                {
                    maxDataLength = length;
                }
            }

            byte[] result = new byte[rawCodewords];
            int position = 0;
            for (int i = 0; i < maxDataLength; i++)
            {
                for (int b = 0; b < numBlocks; b++)
                {
                    if (i < dataBlocks[b].Length)
                    {
                        result[position++] = dataBlocks[b][i];
                    }
                }
            }
            for (int i = 0; i < eccLength; i++)
            {
                for (int b = 0; b < numBlocks; b++)
                {
                    result[position++] = eccBlocks[b][i];
                }
            }

            return position == rawCodewords ? result : null;
        }

        // Reed-Solomon，GF(256) 上算

        private static byte GfMul(byte a, byte b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }
            int sum = GfLog[a] + GfLog[b];
            if (sum >= 255)
            {
                sum -= 255;
            }
            return GfExp[sum];
        }

        // 生成多项式系数，最高次在前，首项那个 1 省了
        private static byte[] ReedSolomonDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < result.Length; j++)
                {
                    result[j] = GfMul(result[j], (byte)root);
                    if (j + 1 < result.Length)
                    {
                        result[j] ^= result[j + 1];
                    }
                }
                root = GfMul((byte)root, 0x02);
            }
            return result;
        }

        private static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            for (int i = 0; i < data.Length; i++)
            {
                int factor = data[i] ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                if (factor != 0)
                {
                    for (int j = 0; j < result.Length; j++)
                    {
                        result[j] ^= GfMul(divisor[j], (byte)factor);
                    }
                }
            }
            return result;
        }

        // 画矩阵

        private static bool[,] BuildMatrix(byte[] codewords, int version, int eccLevel, int mask)
        {
            int size = version * 4 + 17;
            bool[,] modules = new bool[size, size];
            bool[,] isFunction = new bool[size, size];

            DrawFunctionPatterns(modules, isFunction, version, eccLevel);
            DrawCodewords(modules, isFunction, codewords);
            ApplyMask(modules, isFunction, mask);
            DrawFormatBits(modules, isFunction, size, eccLevel, mask);

            return modules;
        }

        private static void SetFunction(bool[,] modules, bool[,] isFunction, int x, int y, bool dark)
        {
            modules[y, x] = dark;
            isFunction[y, x] = true;
        }

        private static void DrawFunctionPatterns(bool[,] modules, bool[,] isFunction, int version, int eccLevel)
        {
            int size = version * 4 + 17;

            // 定位图形，后面可能被校正图形盖掉
            for (int i = 0; i < size; i++)
            {
                SetFunction(modules, isFunction, 6, i, (i & 1) == 0);
                SetFunction(modules, isFunction, i, 6, (i & 1) == 0);
            }

            // 三个角上的定位图形和旁边的分隔带
            DrawFinderPattern(modules, isFunction, size, 3, 3);
            DrawFinderPattern(modules, isFunction, size, size - 4, 3);
            DrawFinderPattern(modules, isFunction, size, 3, size - 4);

            // 校正图形，三个角跳过
            int[] align = GetAlignmentPatternPositions(version);
            for (int i = 0; i < align.Length; i++)
            {
                for (int j = 0; j < align.Length; j++)
                {
                    bool isCorner = (i == 0 && j == 0)
                        || (i == 0 && j == align.Length - 1)
                        || (i == align.Length - 1 && j == 0);
                    if (!isCorner)
                    {
                        DrawAlignmentPattern(modules, isFunction, size, align[i], align[j]);
                    }
                }
            }

            DrawFormatBits(modules, isFunction, size, eccLevel, 0);
            DrawVersion(modules, isFunction, size, version);
        }

        private static void DrawFinderPattern(bool[,] modules, bool[,] isFunction, int size, int centerX, int centerY)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (x < 0 || x >= size || y < 0 || y >= size)
                    {
                        continue;
                    }
                    int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFunction(modules, isFunction, x, y, distance != 2 && distance != 4);
                }
            }
        }

        private static void DrawAlignmentPattern(bool[,] modules, bool[,] isFunction, int size, int centerX, int centerY)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFunction(modules, isFunction, centerX + dx, centerY + dy, distance != 1);
                }
            }
        }

        private static int[] GetAlignmentPatternPositions(int version)
        {
            if (version == 1)
            {
                return new int[0];
            }
            int numAlign = version / 7 + 2;
            int step = (version * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
            int[] result = new int[numAlign];
            result[0] = 6;
            for (int i = numAlign - 1, pos = version * 4 + 10; i >= 1; i--, pos -= step)
            {
                result[i] = pos;
            }
            return result;
        }

        private static void DrawFormatBits(bool[,] modules, bool[,] isFunction, int size, int eccLevel, int mask)
        {
            int data = (EccFormatBits[eccLevel] << 3) | mask;
            int remainder = data;
            for (int i = 0; i < 10; i++)
            {
                remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
            }
            int bits = ((data << 10) | remainder) ^ 0x5412;

            // 格式信息第一份，围着左上角
            for (int i = 0; i <= 5; i++)
            {
                SetFunction(modules, isFunction, 8, i, GetBit(bits, i));
            }
            SetFunction(modules, isFunction, 8, 7, GetBit(bits, 6));
            SetFunction(modules, isFunction, 8, 8, GetBit(bits, 7));
            SetFunction(modules, isFunction, 7, 8, GetBit(bits, 8));
            for (int i = 9; i < 15; i++)
            {
                SetFunction(modules, isFunction, 14 - i, 8, GetBit(bits, i));
            }

            // 第二份，右上和左下各一半
            for (int i = 0; i < 8; i++)
            {
                SetFunction(modules, isFunction, size - 1 - i, 8, GetBit(bits, i));
            }
            for (int i = 8; i < 15; i++)
            {
                SetFunction(modules, isFunction, 8, size - 15 + i, GetBit(bits, i));
            }

            // 那个固定黑块
            SetFunction(modules, isFunction, 8, size - 8, true);
        }

        private static void DrawVersion(bool[,] modules, bool[,] isFunction, int size, int version)
        {
            if (version < 7)
            {
                return;
            }
            int remainder = version;
            for (int i = 0; i < 12; i++)
            {
                remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
            }
            int bits = (version << 12) | remainder;
            for (int i = 0; i < 18; i++)
            {
                bool bit = GetBit(bits, i);
                int a = size - 11 + i % 3;
                int b = i / 3;
                SetFunction(modules, isFunction, a, b, bit);
                SetFunction(modules, isFunction, b, a, bit);
            }
        }

        private static bool GetBit(int value, int index)
        {
            return ((value >> index) & 1) != 0;
        }

        private static void DrawCodewords(bool[,] modules, bool[,] isFunction, byte[] codewords)
        {
            int size = modules.GetLength(0);
            int totalBits = codewords.Length * 8;
            int bitIndex = 0;

            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                {
                    right = 5;
                }
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!isFunction[y, x] && bitIndex < totalBits)
                        {
                            modules[y, x] = GetBit(codewords[bitIndex >> 3], 7 - (bitIndex & 7));
                            bitIndex++;
                        }
                    }
                }
            }
        }

        private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask)
        {
            int size = modules.GetLength(0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!isFunction[y, x] && MaskBit(mask, x, y))
                    {
                        modules[y, x] = !modules[y, x];
                    }
                }
            }
        }

        private static bool MaskBit(int mask, int x, int y)
        {
            switch (mask)
            {
                case 0: return ((x + y) & 1) == 0;
                case 1: return (y & 1) == 0;
                case 2: return x % 3 == 0;
                case 3: return (x + y) % 3 == 0;
                case 4: return (x / 3 + y / 2) % 2 == 0;
                case 5: return x * y % 2 + x * y % 3 == 0;
                case 6: return (x * y % 2 + x * y % 3) % 2 == 0;
                case 7: return ((x + y) % 2 + x * y % 3) % 2 == 0;
                default: return false;
            }
        }

        // 掩码打分，N1~N4

        private static int PenaltyScore(bool[,] modules, int size)
        {
            int result = 0;

            // N1 连续同色、N3 像定位图形：按行扫
            for (int y = 0; y < size; y++)
            {
                bool runColor = false;
                int runLength = 0;
                int[] runHistory = new int[7];
                for (int x = 0; x < size; x++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runLength++;
                        if (runLength == 5)
                        {
                            result += PenaltyN1;
                        }
                        else if (runLength > 5)
                        {
                            result++;
                        }
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runLength, runHistory, size);
                        if (!runColor)
                        {
                            result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
                        }
                        runColor = modules[y, x];
                        runLength = 1;
                    }
                }
                result += FinderPenaltyTerminateAndCount(runColor, runLength, runHistory, size) * PenaltyN3;
            }

            // N1、N3：按列扫
            for (int x = 0; x < size; x++)
            {
                bool runColor = false;
                int runLength = 0;
                int[] runHistory = new int[7];
                for (int y = 0; y < size; y++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runLength++;
                        if (runLength == 5)
                        {
                            result += PenaltyN1;
                        }
                        else if (runLength > 5)
                        {
                            result++;
                        }
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runLength, runHistory, size);
                        if (!runColor)
                        {
                            result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
                        }
                        runColor = modules[y, x];
                        runLength = 1;
                    }
                }
                result += FinderPenaltyTerminateAndCount(runColor, runLength, runHistory, size) * PenaltyN3;
            }

            // N2 同色 2×2
            for (int y = 0; y < size - 1; y++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    bool color = modules[y, x];
                    if (color == modules[y, x + 1] && color == modules[y + 1, x] && color == modules[y + 1, x + 1])
                    {
                        result += PenaltyN2;
                    }
                }
            }

            // N4 黑白比例
            int dark = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (modules[y, x])
                    {
                        dark++;
                    }
                }
            }
            int total = size * size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += k * PenaltyN4;

            return result;
        }

        private static void FinderPenaltyAddHistory(int currentRunLength, int[] runHistory, int size)
        {
            if (runHistory[0] == 0)
            {
                currentRunLength += size; // Treat the area outside the symbol as light.
            }
            Array.Copy(runHistory, 0, runHistory, 1, runHistory.Length - 1);
            runHistory[0] = currentRunLength;
        }

        private static int FinderPenaltyTerminateAndCount(bool currentRunColor, int currentRunLength, int[] runHistory, int size)
        {
            if (currentRunColor)
            {
                FinderPenaltyAddHistory(currentRunLength, runHistory, size);
                currentRunLength = 0;
            }
            currentRunLength += size;
            FinderPenaltyAddHistory(currentRunLength, runHistory, size);
            return FinderPenaltyCountPatterns(runHistory);
        }

        private static int FinderPenaltyCountPatterns(int[] runHistory)
        {
            int n = runHistory[1];
            bool core = n > 0
                && runHistory[2] == n
                && runHistory[3] == n * 3
                && runHistory[4] == n
                && runHistory[5] == n;
            return (core && runHistory[0] >= n * 4 && runHistory[6] >= n ? 1 : 0)
                + (core && runHistory[6] >= n * 4 && runHistory[0] >= n ? 1 : 0);
        }
    }
}
