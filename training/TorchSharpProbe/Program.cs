// Этап 3: проверяем, что нейросеть работает в C# так же, как в Python.
//
// Для каждой папки reference/<имя>/ (их создал training/reference.py):
//   1) берём готовый вход сети input.bin (3x300x300 после предобработки) и считаем logit —
//      он должен совпасть с "logit" из result.json;
//   2) считаем Integrated Gradients так же, как Captum (40 точек Гаусса–Лежандра из ig_steps.json,
//      базовая линия baseline.bin) — атрибуции должны совпасть с attr.bin.
// Второй пункт проверяет главное: что TorchSharp умеет считать градиенты по входу (ONNX Runtime этого не умеет).
// Декодирование картинок и метрики тепловой карты сюда не входят — это этап 4.
//
// Запуск из папки проекта: dotnet run -c Release [-- --model <файл.pt>] [--reference <папка>] [--threads N]

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TorchSharp;
using static TorchSharp.torch;

const double LogitTolerance = 1e-3;     // допустимая разница logit (абсолютная)
const double AttrTolerance = 1e-3;      // допустимая разница атрибуций относительно их максимума

var options = ParseArgs(args);
var modelPath = options.GetValueOrDefault("model", Path.Combine("..", "checkpoints", "checkpoints", "efficientnet_b3.torchscript.pt"));
var referenceDir = options.GetValueOrDefault("reference", Path.Combine("..", "reference"));
var threads = int.Parse(options.GetValueOrDefault("threads", "4"));

torch.set_num_threads(threads);
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"TorchSharp: libtorch {torch.__version__}, потоков: {threads}");

var model = torch.jit.load<Tensor, Tensor>(modelPath);
model.eval();
Console.WriteLine($"Модель загружена: {Path.GetFullPath(modelPath)}");

// Точки (alphas) и веса (step_sizes) интеграла — те же, что использовал Captum.
using var stepsJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(referenceDir, "ig_steps.json")));
var alphas = stepsJson.RootElement.GetProperty("alphas").EnumerateArray().Select(e => e.GetDouble()).ToArray();
var stepSizes = stepsJson.RootElement.GetProperty("step_sizes").EnumerateArray().Select(e => e.GetDouble()).ToArray();

var allPassed = true;
foreach (var folder in Directory.GetDirectories(referenceDir).Order())
{
    var resultPath = Path.Combine(folder, "result.json");
    if (!File.Exists(resultPath))
        continue;

    using var scope = torch.NewDisposeScope();
    using var expected = JsonDocument.Parse(File.ReadAllText(resultPath));
    var name = expected.RootElement.GetProperty("file").GetString();
    var expectedLogit = expected.RootElement.GetProperty("logit").GetDouble();

    var input = ReadTensor(Path.Combine(folder, "input.bin"), 1, 3, 300, 300);
    var baseline = ReadTensor(Path.Combine(folder, "baseline.bin"), 1, 3, 300, 300);
    var expectedAttr = ReadTensor(Path.Combine(folder, "attr.bin"), 1, 3, 300, 300);

    // 1) Прямой проход: logit и вероятность «Real».
    double logit;
    using (torch.no_grad())
        logit = model.call(input).item<float>();
    var prob = 1 / (1 + Math.Exp(-logit));
    var logitDiff = Math.Abs(logit - expectedLogit);

    // 2) Integrated Gradients — градиенты сети по входу.
    var stopwatch = Stopwatch.StartNew();
    var attr = IntegratedGradients(model, input, baseline, alphas, stepSizes);
    stopwatch.Stop();
    var attrScale = expectedAttr.abs().max().item<float>();
    var attrDiff = (attr - expectedAttr).abs().max().item<float>() / attrScale;

    var passed = logitDiff <= LogitTolerance && attrDiff <= AttrTolerance;
    allPassed &= passed;
    Console.WriteLine($"{name}: logit {logit:F6} (Python {expectedLogit:F6}, |Δ| {logitDiff:E1}), " +
                      $"prob {prob:F4} → {(prob <= 0.5 ? "СГЕНЕРИРОВАНО" : "НАСТОЯЩЕЕ")}; " +
                      $"IG: |Δ| {attrDiff:E1} от максимума, сумма {attr.sum().item<float>():F4} " +
                      $"(Python {expectedAttr.sum().item<float>():F4}), {stopwatch.Elapsed.TotalSeconds:F0} с " +
                      $"— {(passed ? "OK" : "РАСХОЖДЕНИЕ")}");
}

Console.WriteLine(allPassed ? "Итог: всё совпало с Python." : "Итог: есть расхождения!");
return allPassed ? 0 : 1;

// Повторяет Captum IntegratedGradients.attribute(..., method="gausslegendre", internal_batch_size=1):
// attr = (x - baseline) * Σ step_i * ∂model/∂x в точке baseline + alpha_i * (x - baseline).
static Tensor IntegratedGradients(jit.ScriptModule<Tensor, Tensor> model, Tensor input, Tensor baseline,
                                  double[] alphas, double[] stepSizes)
{
    var delta = input - baseline;
    var totalGrad = torch.zeros_like(input);
    for (var i = 0; i < alphas.Length; i++)
    {
        using var stepScope = torch.NewDisposeScope();   // освобождаем память каждого шага сразу
        var point = (baseline + alphas[i] * delta).detach().requires_grad_(true);
        model.call(point).sum().backward();
        totalGrad.add_(point.grad!, alpha: stepSizes[i]);
    }
    return totalGrad * delta;
}

// Файлы .bin из reference.py: float32 little-endian, порядок C x H x W.
static Tensor ReadTensor(string path, params long[] shape)
{
    var floats = MemoryMarshal.Cast<byte, float>(File.ReadAllBytes(path)).ToArray();
    return torch.tensor(floats, shape);
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>();
    for (var i = 0; i + 1 < args.Length; i += 2)
        result[args[i].TrimStart('-')] = args[i + 1];
    return result;
}
