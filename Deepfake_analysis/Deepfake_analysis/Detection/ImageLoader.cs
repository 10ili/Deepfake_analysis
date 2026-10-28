using OpenCvSharp;

namespace Deepfake_analysis.Detection
{
    /// <summary>Файл картинки не удалось прочитать как изображение.</summary>
    public class InvalidImageException(string message) : Exception(message);

    public static class ImageLoader
    {
        /// <summary>
        /// Декодирует JPEG или PNG в цветную картинку RGB (3 канала по байту).
        /// OpenCV сам поворачивает картинку по EXIF, серые и палитровые картинки становятся цветными,
        /// прозрачность отбрасывается.
        /// </summary>
        public static Mat Decode(byte[] bytes)
        {
            Mat bgr;
            try
            {
                bgr = Cv2.ImDecode(bytes, ImreadModes.Color);
            }
            catch (OpenCVException)
            {
                throw new InvalidImageException("Файл не является изображением JPEG или PNG.");
            }
            if (bgr.Empty())
            {
                bgr.Dispose();
                throw new InvalidImageException("Файл не является изображением JPEG или PNG.");
            }

            var rgb = new Mat();
            Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);
            bgr.Dispose();
            return rgb;
        }
    }
}
