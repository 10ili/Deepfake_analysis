"""Переводит обученные веса в TorchScript — формат, который загружает TorchSharp в C#.

Файл .pth хранит только числа (веса), а устройство сети описано Python-кодом torchvision.
TorchScript сохраняет и веса, и сам граф вычислений, поэтому C# может загрузить сеть без Python.

Сеть выбирается ключом --arch:
  efficientnet_b3, resnet50 — обученные сети, файл <сеть>.torchscript.pt рядом с весами;
  resnet50_catboost         — ResNet50 с весами ImageNet, которая считает 2048 признаков для CatBoost,
                              файл checkpoints/resnet50_catboost/resnet50_features.torchscript.pt.

Версия torch должна совпадать с libtorch, на которой собран TorchSharp (TorchSharp 0.107.0 — libtorch 2.10.0).

Запуск: .venv\\Scripts\\python export_torchscript.py --arch resnet50
"""
import argparse
from pathlib import Path

import torch
from torchvision import models

ROOT = Path(__file__).parent
CHECKPOINTS = ROOT / "checkpoints"
WEIGHTS = {
    "efficientnet_b3": CHECKPOINTS / "efficientnet_b3" / "efficientnet_b3_best.pth",
    "resnet50": CHECKPOINTS / "resnet50" / "resnet50_best.pth",
}


def load_model(arch, weights):
    """Обученная сеть с одним выходом — логит вероятности «Real»."""
    if arch == "efficientnet_b3":
        model = models.efficientnet_b3(weights=None)
        model.classifier[1] = torch.nn.Linear(model.classifier[1].in_features, 1)
    else:
        model = models.resnet50(weights=None)
        model.fc = torch.nn.Linear(model.fc.in_features, 1)
    model.load_state_dict(torch.load(weights, map_location="cpu"))
    return model.eval()


def load_feature_extractor():
    """ResNet50 с весами ImageNet без последнего слоя: на выходе 2048 признаков для CatBoost."""
    model = models.resnet50(weights=models.ResNet50_Weights.IMAGENET1K_V1)
    model.fc = torch.nn.Identity()
    return model.eval()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--arch", choices=["efficientnet_b3", "resnet50", "resnet50_catboost"],
                        default="efficientnet_b3", help="какую сеть переводить")
    parser.add_argument("--weights", type=Path, help="файл весов (по умолчанию из checkpoints/<сеть>)")
    parser.add_argument("--out", type=Path, help="куда сохранить")
    args = parser.parse_args()

    if args.arch == "resnet50_catboost":
        model = load_feature_extractor()
        out = args.out or CHECKPOINTS / "resnet50_catboost" / "resnet50_features.torchscript.pt"
    else:
        weights = args.weights or WEIGHTS[args.arch]
        model = load_model(args.arch, weights)
        out = args.out or weights.with_name(f"{args.arch}.torchscript.pt")
    torch.jit.script(model).save(str(out))
    print(f"PyTorch {torch.__version__}. Сохранено: {out} ({out.stat().st_size / 2**20:.1f} МБ)")


if __name__ == "__main__":
    main()
