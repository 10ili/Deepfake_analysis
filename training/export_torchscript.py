"""Переводит обученные веса в TorchScript — формат, который загружает TorchSharp в C#.

Файл .pth хранит только числа (веса), а устройство сети описано Python-кодом torchvision.
TorchScript сохраняет и веса, и сам граф вычислений, поэтому C# может загрузить сеть без Python.

После сохранения скрипт загружает файл обратно и сверяет с исходной моделью:
выход сети и градиент по входу (он нужен для Integrated Gradients) должны совпасть.

Версия torch должна совпадать с libtorch, на которой собран TorchSharp (TorchSharp 0.107.0 — libtorch 2.10.0).

Запуск: .venv\\Scripts\\python export_torchscript.py --weights checkpoints\\efficientnet_b3\\efficientnet_b3_best.pth
"""
import argparse
from pathlib import Path

import numpy as np
import torch

from reference import ROOT, load_model


def output_and_gradient(model, x):
    x = x.clone().requires_grad_(True)
    y = model(x)
    y.sum().backward()
    return y.detach(), x.grad


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--weights", type=Path, default=ROOT / "checkpoints" / "efficientnet_b3" / "efficientnet_b3_best.pth")
    parser.add_argument("--out", type=Path, help="куда сохранить (по умолчанию рядом с весами)")
    parser.add_argument("--reference", type=Path, default=ROOT / "reference", help="папка с input.bin для сверки")
    args = parser.parse_args()
    out = args.out or args.weights.with_name("efficientnet_b3.torchscript.pt")

    model = load_model(args.weights)
    torch.jit.script(model).save(str(out))
    print(f"PyTorch {torch.__version__}. Сохранено: {out} ({out.stat().st_size / 2**20:.1f} МБ)")

    loaded = torch.jit.load(str(out), map_location="cpu").eval()

    inputs = [torch.from_numpy(np.fromfile(path, dtype="<f4").reshape(1, 3, 300, 300))
              for path in sorted(args.reference.glob("*/input.bin"))]
    inputs.append(torch.randn(2, 3, 300, 300))  # пакет из двух — проверка, что размер пакета не «зашит»
    for x in inputs:
        y_eager, g_eager = output_and_gradient(model, x)
        y_script, g_script = output_and_gradient(loaded, x)
        print(f"  вход {tuple(x.shape)}: |Δ выхода| {(y_eager - y_script).abs().max():.2e}, "
              f"|Δ градиента| {(g_eager - g_script).abs().max():.2e} (градиент до {g_eager.abs().max():.2e})")


if __name__ == "__main__":
    main()
