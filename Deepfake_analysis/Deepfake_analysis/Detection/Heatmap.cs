using Deepfake_analysis.Models;
using OpenCvSharp;
using static TorchSharp.torch;

namespace Deepfake_analysis.Detection
{
    /// <summary>Тепловая карта Integrated Gradients и её метрики.</summary>
    public sealed class HeatmapAnalysis
    {
        /// <summary>Энтропия распределения значений карты: чем меньше, тем «сосредоточеннее» внимание сети.</summary>
        public double Entropy { get; init; }

        /// <summary>Доля суммы карты, приходящаяся на 1% самых ярких пикселей.</summary>
        public double Concentration { get; init; }

        public double StdDeviation { get; init; }
        public ImageRegion CriticalRegion { get; init; }
        public AnomalyType AnomalyType { get; init; }

        /// <summary>Карта в цветах JET размером с исходную картинку, PNG.</summary>
        public byte[] HeatmapPng { get; init; } = [];

        /// <summary>Картинка с наложенной картой (60% картинки + 40% карты), PNG.</summary>
        public byte[] OverlayPng { get; init; } = [];
    }

    public static class Heatmap
    {
        private const double SmoothSigma = 2.5;
        private const int HistogramBins = 256;

        /// <summary>Строит карту и метрики по атрибуциям (1 x 3 x N x N) и исходной картинке RGB.</summary>
        public static HeatmapAnalysis Analyze(Tensor attributions, Mat rgb)
        {
            var size = (int)attributions.shape[2];
            var normalized = Normalize(Smooth(PositiveMean(attributions, size), size));

            // Карта растягивается до размера исходной картинки, все метрики считаются по ней.
            using var small = Mat.FromPixelData(size, size, MatType.CV_32FC1, normalized);
            using var full = new Mat();
            Cv2.Resize(small, full, new OpenCvSharp.Size(rgb.Width, rgb.Height), 0, 0, InterpolationFlags.Linear);
            full.GetArray(out float[] map);
            int width = rgb.Width, height = rgb.Height;

            // Зоны: три горизонтальные полосы и левая/правая трети; побеждает первая с наибольшим средним.
            int hs = height / 3, ws = width / 3;
            var regionMeans = new Dictionary<ImageRegion, double>
            {
                [ImageRegion.Top] = Mean(map, width, 0, hs, 0, width),
                [ImageRegion.Center] = Mean(map, width, hs, 2 * hs, 0, width),
                [ImageRegion.Bottom] = Mean(map, width, 2 * hs, height, 0, width),
                [ImageRegion.Left] = Mean(map, width, 0, height, 0, ws),
                [ImageRegion.Right] = Mean(map, width, 0, height, 2 * ws, width),
            };
            var bestRegion = regionMeans.First(r => r.Value == regionMeans.Values.Max()).Key;

            // Тип аномалии: где сосредоточена карта — на контурах (Canny + расширение 5x5) или на ровных участках.
            var edgeMask = EdgeMask(rgb);
            double edgeScore = 0, flatScore = 0;
            for (var i = 0; i < map.Length; i++)
            {
                if (edgeMask[i])
                    edgeScore += map[i];
                else
                    flatScore += map[i];
            }
            var anomalyType = edgeScore > flatScore ? AnomalyType.GeometryAndContours : AnomalyType.TextureAndNoise;

            var total = map.Sum(v => (double)v);
            var topCount = (int)(0.01 * map.Length);
            var topSum = map.OrderDescending().Take(topCount).Sum(v => (double)v);
            var mean = total / map.Length;
            var std = Math.Sqrt(map.Sum(v => (v - mean) * (v - mean)) / map.Length);

            var (heatmapPng, overlayPng) = Pictures(map, rgb);
            return new HeatmapAnalysis
            {
                Entropy = Entropy(Histogram(map), map.Length),
                Concentration = topSum / (total + 1e-8),
                StdDeviation = std,
                CriticalRegion = bestRegion,
                AnomalyType = anomalyType,
                HeatmapPng = heatmapPng,
                OverlayPng = overlayPng,
            };
        }

        /// <summary>
        /// Среднее по каналам от положительной части атрибуций. Сеть выдаёт одно число — уверенность в том,
        /// что картинка настоящая, — поэтому положительные атрибуции отмечают пиксели, которые эту уверенность
        /// повышают. Карта показывает именно их, при любом вердикте.
        /// </summary>
        private static float[] PositiveMean(Tensor attributions, int size)
        {
            var attr = attributions.data<float>().ToArray();
            var pixels = size * size;
            var gray = new float[pixels];
            for (var i = 0; i < pixels; i++)
                gray[i] = (Math.Max(attr[i], 0f) + Math.Max(attr[pixels + i], 0f) + Math.Max(attr[2 * pixels + i], 0f)) / 3f;
            return gray;
        }

        /// <summary>
        /// Сглаживание по Гауссу (σ = 2.5, радиус 10, зеркальные края): сначала по столбцам, потом по строкам.
        /// </summary>
        private static float[] Smooth(float[] image, int size)
        {
            var radius = (int)(4.0 * SmoothSigma + 0.5);
            var weights = new double[radius + 1];   // вес для сдвига 0..radius (ядро симметрично)
            var sum = 0.0;
            for (var x = -radius; x <= radius; x++)
                sum += Math.Exp(-0.5 / (SmoothSigma * SmoothSigma) * (x * x));
            for (var x = 0; x <= radius; x++)
                weights[x] = Math.Exp(-0.5 / (SmoothSigma * SmoothSigma) * (x * x)) / sum;

            var columns = Pass(image, size, weights, vertical: true);
            return Pass(columns, size, weights, vertical: false);
        }

        private static float[] Pass(float[] image, int size, double[] weights, bool vertical)
        {
            var radius = weights.Length - 1;
            var result = new float[image.Length];
            var line = new double[size];
            for (var a = 0; a < size; a++)
            {
                for (var b = 0; b < size; b++)
                    line[b] = image[vertical ? b * size + a : a * size + b];
                for (var b = 0; b < size; b++)
                {
                    var value = line[b] * weights[0];
                    for (var j = radius; j >= 1; j--)
                        value += (line[Reflect(b - j, size)] + line[Reflect(b + j, size)]) * weights[j];
                    result[vertical ? b * size + a : a * size + b] = (float)value;
                }
            }
            return result;
        }

        // Зеркальное продолжение за край с повтором крайнего пикселя: … c b a | a b c …
        private static int Reflect(int i, int size) => i < 0 ? -i - 1 : i >= size ? 2 * size - i - 1 : i;

        /// <summary>Приведение к 0..1: (x - min) / (max - min).</summary>
        private static float[] Normalize(float[] map)
        {
            float min = map.Min(), max = map.Max();
            var range = max - min + 1e-8f;
            return map.Select(v => (v - min) / range).ToArray();
        }

        private static double Mean(float[] map, int width, int rowFrom, int rowTo, int colFrom, int colTo)
        {
            var sum = 0.0;
            for (var y = rowFrom; y < rowTo; y++)
                for (var x = colFrom; x < colTo; x++)
                    sum += map[y * width + x];
            return sum / ((rowTo - rowFrom) * (colTo - colFrom));
        }

        private static bool[] EdgeMask(Mat rgb)
        {
            using var gray = new Mat();
            using var edges = new Mat();
            using var dilated = new Mat();
            using var kernel = Mat.Ones(5, 5, MatType.CV_8UC1).ToMat();
            Cv2.CvtColor(rgb, gray, ColorConversionCodes.RGB2GRAY);
            Cv2.Canny(gray, edges, 50, 150);
            Cv2.Dilate(edges, dilated, kernel);
            dilated.GetArray(out byte[] pixels);
            return pixels.Select(p => p > 0).ToArray();
        }

        /// <summary>Гистограмма значений на 256 равных корзин отрезка [0; 1].</summary>
        private static int[] Histogram(float[] map)
        {
            var counts = new int[HistogramBins];
            foreach (var value in map)
            {
                double v = value;
                if (v < 0 || v > 1)
                    continue;
                var index = (int)(v * HistogramBins);
                if (index == HistogramBins)
                    index--;
                if (v < (double)index / HistogramBins)
                    index--;
                else if (index != HistogramBins - 1 && v >= (double)(index + 1) / HistogramBins)
                    index++;
                counts[index]++;
            }
            return counts;
        }

        /// <summary>
        /// Энтропия гистограммы: H = -Σ p·ln p. К плотности каждой корзины прибавляется 1e-8,
        /// чтобы пустые корзины не давали ln 0.
        /// </summary>
        private static double Entropy(int[] counts, int total)
        {
            const double binWidth = 1.0 / HistogramBins;
            var density = counts.Select(c => c / binWidth / total + 1e-8).ToArray();
            var sum = density.Sum();
            return -density.Select(d => d / sum).Sum(p => p * Math.Log(p));
        }

        private static (byte[] Heatmap, byte[] Overlay) Pictures(float[] map, Mat rgb)
        {
            var levels = map.Select(v => (byte)(255f * v)).ToArray();
            using var gray = Mat.FromPixelData(rgb.Height, rgb.Width, MatType.CV_8UC1, levels);
            using var heatmapBgr = new Mat();
            Cv2.ApplyColorMap(gray, heatmapBgr, ColormapTypes.Jet);

            using var heatmapRgb = new Mat();
            using var overlayRgb = new Mat();
            using var overlayBgr = new Mat();
            Cv2.CvtColor(heatmapBgr, heatmapRgb, ColorConversionCodes.BGR2RGB);
            Cv2.AddWeighted(rgb, 0.6, heatmapRgb, 0.4, 0, overlayRgb);
            Cv2.CvtColor(overlayRgb, overlayBgr, ColorConversionCodes.RGB2BGR);
            return (heatmapBgr.ImEncode(".png"), overlayBgr.ImEncode(".png"));
        }
    }
}
