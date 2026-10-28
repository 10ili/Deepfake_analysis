namespace Deepfake_analysis.Detection
{
    /// <summary>
    /// Изменение размера картинки так же, как Image.resize(..., BILINEAR) в библиотеке PIL (Pillow).
    /// Сети обучались на картинках, уменьшенных именно так, и при анализе должны видеть то же самое.
    /// OpenCV уменьшает иначе: PIL усредняет все исходные пиксели под окном фильтра (сглаживание),
    /// а не берёт четыре соседних. Здесь повторён алгоритм Pillow (Resample.c) с той же
    /// целочисленной арифметикой.
    /// </summary>
    internal static class PilResize
    {
        // Число двоичных знаков после запятой у коэффициентов фильтра (как в Pillow для 8-битных картинок).
        private const int PrecisionBits = 32 - 8 - 2;

        /// <summary>Картинка RGB, 3 байта на пиксель построчно; возвращает картинку нового размера в том же виде.</summary>
        public static byte[] Bilinear(byte[] source, int width, int height, int newWidth, int newHeight)
        {
            var image = source;
            if (newWidth != width)
                image = ResampleHorizontal(image, width, height, newWidth);
            if (newHeight != height)
                image = ResampleVertical(image, newWidth, height, newHeight);
            return image;
        }

        // Треугольный фильтр билинейной интерполяции: вес падает от 1 в центре до 0 на расстоянии 1.
        private static double Triangle(double x)
        {
            x = Math.Abs(x);
            return x < 1.0 ? 1.0 - x : 0.0;
        }

        /// <summary>
        /// Для каждого выходного пикселя: первый исходный пиксель окна, число пикселей в окне
        /// и их веса (целые, в долях 2^PrecisionBits).
        /// </summary>
        private static (int[] Start, int[] Count, int[] Weights, int WindowSize) Coefficients(int inSize, int outSize)
        {
            var scale = (double)inSize / outSize;
            var filterScale = Math.Max(scale, 1.0);   // при уменьшении окно фильтра растягивается
            var support = 1.0 * filterScale;
            var windowSize = (int)Math.Ceiling(support) * 2 + 1;
            var invScale = 1.0 / filterScale;   // Pillow умножает на обратное, а не делит — так же и здесь

            var start = new int[outSize];
            var count = new int[outSize];
            var weights = new int[outSize * windowSize];
            var window = new double[windowSize];
            for (var i = 0; i < outSize; i++)
            {
                var center = (i + 0.5) * scale;
                var min = Math.Max((int)(center - support + 0.5), 0);
                var max = Math.Min((int)(center + support + 0.5), inSize) - min;

                var sum = 0.0;
                for (var x = 0; x < max; x++)
                {
                    window[x] = Triangle((x + min - center + 0.5) * invScale);
                    sum += window[x];
                }
                for (var x = 0; x < max; x++)
                {
                    var w = sum != 0.0 ? window[x] / sum : window[x];
                    weights[i * windowSize + x] = w < 0
                        ? (int)(-0.5 + w * (1 << PrecisionBits))
                        : (int)(0.5 + w * (1 << PrecisionBits));
                }
                start[i] = min;
                count[i] = max;
            }
            return (start, count, weights, windowSize);
        }

        private static byte Clip(int value) => (byte)Math.Clamp(value >> PrecisionBits, 0, 255);

        private static byte[] ResampleHorizontal(byte[] image, int width, int height, int newWidth)
        {
            var (start, count, weights, windowSize) = Coefficients(width, newWidth);
            var result = new byte[newWidth * height * 3];
            for (var y = 0; y < height; y++)
            {
                var row = y * width * 3;
                for (var x = 0; x < newWidth; x++)
                {
                    var k = x * windowSize;
                    for (var c = 0; c < 3; c++)
                    {
                        var sum = 1 << (PrecisionBits - 1);   // +0.5 для округления
                        for (var i = 0; i < count[x]; i++)
                            sum += image[row + (start[x] + i) * 3 + c] * weights[k + i];
                        result[(y * newWidth + x) * 3 + c] = Clip(sum);
                    }
                }
            }
            return result;
        }

        private static byte[] ResampleVertical(byte[] image, int width, int height, int newHeight)
        {
            var (start, count, weights, windowSize) = Coefficients(height, newHeight);
            var result = new byte[width * newHeight * 3];
            for (var y = 0; y < newHeight; y++)
            {
                var k = y * windowSize;
                for (var x = 0; x < width; x++)
                {
                    for (var c = 0; c < 3; c++)
                    {
                        var sum = 1 << (PrecisionBits - 1);
                        for (var i = 0; i < count[y]; i++)
                            sum += image[((start[y] + i) * width + x) * 3 + c] * weights[k + i];
                        result[(y * width + x) * 3 + c] = Clip(sum);
                    }
                }
            }
            return result;
        }
    }
}
