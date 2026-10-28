using TorchSharp;
using static TorchSharp.torch;

namespace Deepfake_analysis.Detection
{
    /// <summary>
    /// Integrated Gradients — насколько каждый пиксель входа повлиял на ответ сети.
    /// Ответ сети на размытой картинке (базовой линии) и на настоящей различается; метод делит эту
    /// разницу между пикселями, интегрируя градиент вдоль прямой от базовой линии к картинке:
    ///   attr = (x - baseline) * ∫₀¹ ∂model/∂x (baseline + α (x - baseline)) dα.
    /// Интеграл считается по 40 точкам Гаусса–Лежандра.
    /// </summary>
    public static class IntegratedGradients
    {
        public const int Steps = 40;

        // Базовая линия — сильно размытый вход: детали, по которым сеть отличает подделку, пропадают.
        private const int BlurKernelSize = 51;
        private const double BlurSigma = 10;

        private static readonly (double[] Alphas, double[] Weights) Nodes = GaussLegendre(Steps);

        /// <summary>Атрибуции того же размера, что вход (1 x 3 x N x N).</summary>
        public static Tensor Attribute(jit.ScriptModule<Tensor, Tensor> model, Tensor input)
        {
            using var baseline = Baseline(input);
            var delta = input - baseline;
            var totalGrad = torch.zeros_like(input);
            for (var i = 0; i < Steps; i++)
            {
                using var stepScope = torch.NewDisposeScope();   // память каждого шага освобождается сразу
                var point = (baseline + Nodes.Alphas[i] * delta).detach().requires_grad_(true);
                model.call(point).sum().backward();
                totalGrad.add_(point.grad!, alpha: Nodes.Weights[i]);
            }
            return totalGrad * delta;
        }

        /// <summary>Размытие по Гауссу (ядро 51, σ = 10) с зеркальными краями — как GaussianBlur в torchvision.</summary>
        private static Tensor Baseline(Tensor input)
        {
            using var scope = torch.NewDisposeScope();
            var half = (BlurKernelSize - 1) * 0.5;
            var x = torch.linspace(-half, half, BlurKernelSize, dtype: ScalarType.Float32);
            var pdf = torch.exp(-0.5 * (x / BlurSigma).pow(2));
            var kernel1d = pdf / pdf.sum();
            var channels = input.shape[1];
            var kernel = torch.mm(kernel1d.unsqueeze(1), kernel1d.unsqueeze(0))
                .expand(channels, 1, BlurKernelSize, BlurKernelSize);

            var pad = BlurKernelSize / 2;
            var padded = nn.functional.pad(input, [pad, pad, pad, pad], PaddingModes.Reflect);
            return nn.functional.conv2d(padded, kernel, groups: channels).MoveToOuterDisposeScope();
        }

        /// <summary>
        /// Точки и веса квадратуры Гаусса–Лежандра, перенесённые с отрезка [-1; 1] на [0; 1]:
        /// корни многочлена Лежандра степени n (метод Ньютона) и веса 2 / ((1 - x²) P'(x)²).
        /// </summary>
        private static (double[] Alphas, double[] Weights) GaussLegendre(int n)
        {
            var alphas = new double[n];
            var weights = new double[n];
            for (var i = 0; i < n; i++)
            {
                var x = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5));   // начальное приближение i-го корня
                double derivative;
                while (true)
                {
                    var (p, previous) = (x, 1.0);
                    for (var k = 2; k <= n; k++)
                        (p, previous) = (((2 * k - 1) * x * p - (k - 1) * previous) / k, p);
                    derivative = n * (x * p - previous) / (x * x - 1);
                    var step = p / derivative;
                    x -= step;
                    if (Math.Abs(step) < 1e-15)
                        break;
                }
                // Корни идут по убыванию; храним по возрастанию.
                alphas[n - 1 - i] = 0.5 * (1 + x);
                weights[n - 1 - i] = 0.5 * 2 / ((1 - x * x) * derivative * derivative);
            }
            return (alphas, weights);
        }
    }
}
