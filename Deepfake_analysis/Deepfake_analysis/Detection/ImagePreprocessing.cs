using System.Runtime.InteropServices;
using OpenCvSharp;
using TorchSharp;
using static TorchSharp.torch;

namespace Deepfake_analysis.Detection
{
    public static class ImagePreprocessing
    {
        // Средние и разбросы каналов RGB по ImageNet — на них обучались все сети.
        private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
        private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

        /// <summary>Байты картинки RGB построчно (3 байта на пиксель).</summary>
        public static byte[] ToBytes(Mat rgb)
        {
            using var continuous = rgb.IsContinuous() ? null : rgb.Clone();
            var source = continuous ?? rgb;
            var bytes = new byte[source.Width * source.Height * 3];
            Marshal.Copy(source.Data, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// Вход сети: картинка сжата до size x size (как PIL), пиксели переведены в 0..1
        /// и нормализованы по ImageNet. Тензор 1 x 3 x size x size, каналы отдельно (как в PyTorch).
        /// </summary>
        public static Tensor ToInputTensor(Mat rgb, int size)
        {
            var resized = PilResize.Bilinear(ToBytes(rgb), rgb.Width, rgb.Height, size, size);
            var pixels = size * size;
            var data = new float[3 * pixels];
            for (var i = 0; i < pixels; i++)
                for (var c = 0; c < 3; c++)
                    data[c * pixels + i] = (resized[i * 3 + c] / 255f - Mean[c]) / Std[c];
            return torch.tensor(data, [1, 3, size, size]);
        }
    }
}
