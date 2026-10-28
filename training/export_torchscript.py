"""Переводит обученные веса в TorchScript — формат, который загружает TorchSharp в C#.

Файл .pth хранит только числа (веса), а устройство сети описано Python-кодом torchvision.
TorchScript сохраняет и веса, и сам граф вычислений, поэтому C# может загрузить сеть без Python.

Сеть выбирается ключом --arch:
  efficientnet_b3, resnet50 — обученные сети, файл <сеть>.torchscript.pt рядом с весами;
  resnet50_catboost         — ResNet50 с весами ImageNet, которая считает 2048 признаков для CatBoost,
                              файл checkpoints/resnet50_catboost/resnet50_features.torchscript.pt.

После сохранения скрипт загружает файл обратно и сверяет с исходной моделью:
выход сети и градиент по входу (он нужен для Integrated Gradients) должны совпасть.

Версия torch должна совпадать с libtorch, на которой собран TorchSharp (TorchSharp 0.107.0 — libtorch 2.10.0).

Запуск: .venv\\Scripts\\python export_torchscript.py --arch resnet50
"""
import argparse
from pathlib import Path

import numpy as np
import torch

from reference import ARCHS, ROOT, load_feature_extractor, load_model


def output_and_gradient(model, x):
    x = x.clone().requires_grad_(True)
    y = model(x)
    y.sum().backward()
    return y.detach(), x.grad


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--arch", choices=ARCHS, default="efficientnet_b3", help="какую сеть переводить")
    parser.add_argument("--weights", type=Path, help="файл весов (по умолчанию из checkpoints/<сеть>)")
    parser.add_argument("--out", type=Path, help="куда сохранить (по умолчанию рядом с весами)")
    parser.add_argument("--reference", type=Path, default=ROOT / "reference", help="папка с input.bin для сверки")
    args = parser.parse_args()
    weights = args.weights or ARCHS[args.arch]["weights"]
    size = ARCHS[args.arch]["image_size"]

    if args.arch == "resnet50_catboost":
        model = load_feature_extractor()
        out = args.out or weights.with_name("resnet50_features.torchscript.pt")
    else:
        model = load_model(args.arch, weights)
        out = args.out or weights.with_name(f"{args.arch}.torchscript.pt")
    torch.jit.script(model).save(str(out))
    print(f"PyTorch {torch.__version__}. Сохранено: {out} ({out.stat().st_size / 2**20:.1f} МБ)")

    loaded = torch.jit.load(str(out), map_location="cpu").eval()

    inputs = [torch.from_numpy(np.fromfile(path, dtype="<f4").reshape(1, 3, size, size))
              for path in sorted((args.reference / args.arch).glob("*/input.bin"))]
    inputs.append(torch.randn(2, 3, size, size))  # пакет из двух — проверка, что размер пакета не «зашит»
    for x in inputs:
        y_eager, g_eager = output_and_gradient(model, x)
        y_script, g_script = output_and_gradient(loaded, x)
        print(f"  вход {tuple(x.shape)}: |Δ выхода| {(y_eager - y_script).abs().max():.2e}, "
              f"|Δ градиента| {(g_eager - g_script).abs().max():.2e} (градиент до {g_eager.abs().max():.2e})")


if __name__ == "__main__":
    main()
