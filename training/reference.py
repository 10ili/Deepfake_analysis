"""Эталонный расчёт анализа картинки на Python — «правильные ответы» для C#.

Модель выбирается ключом --arch:
  efficientnet_b3, resnet50 — нейросети: вероятность «Real», тепловая карта Integrated Gradients и метрики;
  resnet50_catboost         — признаки ResNet50 (ImageNet) и вероятность от CatBoost (ONNX); тепловой карты нет.

Для каждой картинки результаты сохраняются в reference/<модель>/<имя>/:
  result.json        — вероятность, метрики, сетка 3x3, зона, тип аномалии и промежуточные числа;
  input.bin          — тензор после предобработки (3xNxN), чтобы сверять сеть отдельно от декодера;
  baseline.bin       — базовая линия IG (размытый вход, 3xNxN);
  attr.bin           — сырые атрибуции IG (3xNxN);
  attr_norm.bin      — карта после сглаживания и нормировки (NxN), до растяжения до размера картинки;
  features.bin       — только у resnet50_catboost: 2048 признаков, которые подаются в CatBoost;
  heatmap.png, overlay.png — тепловая карта и наложение на исходную картинку.
N — сторона входа сети: 300 у EfficientNet-B3, 224 у ResNet50.
Файлы .bin — float32 little-endian, порядок C x H x W (как в PyTorch).
В reference/ig_steps.json — точки и веса метода Гаусса–Лежандра, которым Captum считает интеграл.

Запуск: .venv\\Scripts\\python reference.py --arch resnet50 reference\\images\\*
"""
import argparse
import json
import time
from pathlib import Path

import cv2
import numpy as np
import scipy.ndimage as ndimage
import torch
from captum.attr import IntegratedGradients
from captum.attr._utils.approximation_methods import approximation_parameters
from scipy.stats import entropy
from torchvision import models, transforms
from torchvision.transforms import GaussianBlur

ROOT = Path(__file__).parent
N_STEPS = 40

# Сторона входа сети и файл с обученной моделью по умолчанию.
ARCHS = {
    "efficientnet_b3": {"image_size": 300, "weights": ROOT / "checkpoints" / "efficientnet_b3" / "efficientnet_b3_best.pth"},
    "resnet50": {"image_size": 224, "weights": ROOT / "checkpoints" / "resnet50" / "resnet50_best.pth"},
    "resnet50_catboost": {"image_size": 224, "weights": ROOT / "checkpoints" / "resnet50_catboost" / "resnet50_catboost.onnx"},
}


def load_model(arch, weights):
    """Нейросеть с одним выходом — логит вероятности «Real»."""
    if arch == "efficientnet_b3":
        model = models.efficientnet_b3(weights=None)
        model.classifier[1] = torch.nn.Linear(model.classifier[1].in_features, 1)
    elif arch == "resnet50":
        model = models.resnet50(weights=None)
        model.fc = torch.nn.Linear(model.fc.in_features, 1)
    else:
        raise ValueError(f"{arch} — не нейросеть")
    model.load_state_dict(torch.load(weights, map_location="cpu"))
    return model.eval()


def load_feature_extractor():
    """ResNet50 с весами ImageNet без последнего слоя: на выходе 2048 признаков для CatBoost."""
    model = models.resnet50(weights=models.ResNet50_Weights.IMAGENET1K_V1)
    model.fc = torch.nn.Identity()
    return model.eval()


def make_transform(image_size):
    return transforms.Compose([
        transforms.ToPILImage(), transforms.Resize((image_size, image_size)), transforms.ToTensor(),
        transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
    ])


def read_image(path):
    # cv2 сам поворачивает картинку по EXIF.
    img = cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_COLOR)
    if img is None:
        raise ValueError(f"Не удалось декодировать {path}")
    return cv2.cvtColor(img, cv2.COLOR_BGR2RGB)


def analyze(model, img_rgb, image_size):
    """Полный расчёт анализа нейросетью; дополнительно возвращает промежуточные данные."""
    h, w, _ = img_rgb.shape
    input_tensor = make_transform(image_size)(img_rgb).unsqueeze(0)
    input_tensor.requires_grad = True

    ig = IntegratedGradients(model)
    baseline = GaussianBlur(51, 10)(input_tensor)

    attr_t = ig.attribute(input_tensor, baselines=baseline, target=0, n_steps=N_STEPS, internal_batch_size=1)
    attr = np.transpose(attr_t.squeeze().detach().cpu().numpy(), (1, 2, 0))

    attr_gray = np.mean(np.maximum(attr, 0), axis=2)
    attr_smooth = ndimage.gaussian_filter(attr_gray, sigma=2.5)
    attr_norm = (attr_smooth - attr_smooth.min()) / (attr_smooth.max() - attr_smooth.min() + 1e-8)
    attr_res = cv2.resize(attr_norm, (w, h))

    with torch.no_grad():
        logit = model(input_tensor).item()
    prob = float(torch.sigmoid(torch.tensor(logit)))

    grid = np.zeros((3, 3))
    hs, ws = attr_res.shape[0] // 3, attr_res.shape[1] // 3
    regions = {
        "в верхней части": attr_res[:hs, :],
        "в центральной части": attr_res[hs:2 * hs, :],
        "в нижней части": attr_res[2 * hs:, :],
        "слева": attr_res[:, :ws],
        "справа": attr_res[:, 2 * ws:],
    }
    best_region = max(regions, key=lambda k: np.mean(regions[k]))

    for i in range(3):
        for j in range(3):
            grid[i, j] = np.mean(attr_res[i * hs:(i + 1) * hs, j * ws:(j + 1) * ws])

    gray = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2GRAY)
    edges = cv2.Canny(gray, 50, 150)
    edges_dilated = cv2.dilate(edges, np.ones((5, 5))) > 0
    edge_score = np.sum(attr_res[edges_dilated])
    flat_score = np.sum(attr_res[~edges_dilated])
    anomaly_type = "нарушение геометрии и контуров" if edge_score > flat_score else "неестественная текстура и шум"

    hist, _ = np.histogram(attr_res, bins=256, range=(0, 1), density=True)
    top_count = int(0.01 * attr_res.size)
    top1 = np.sort(attr_res.flatten())[-top_count:]

    result = {
        "width": w,
        "height": h,
        "logit": logit,
        "prob": prob,
        "is_generated": prob <= 0.5,
        "risk": 1 - prob,
        "entropy": float(entropy(hist + 1e-8)),
        "concentration": float(np.sum(top1) / (np.sum(attr_res) + 1e-8)),
        "std": float(np.std(attr_res)),
        "best_region": best_region,
        "anomaly_type": anomaly_type,
        "grid": grid.tolist(),
        "debug": {
            "region_means": {name: float(np.mean(part)) for name, part in regions.items()},
            "edge_pixels": int(edges_dilated.sum()),
            "edge_score": float(edge_score),
            "flat_score": float(flat_score),
            "top_count": top_count,
            "attr_smooth_min": float(attr_smooth.min()),
            "attr_smooth_max": float(attr_smooth.max()),
            "attr_res_sum": float(np.sum(attr_res)),
            "attr_sum": float(attr.sum()),
            "histogram_counts": np.histogram(attr_res, bins=256, range=(0, 1))[0].tolist(),
        },
    }
    tensors = {
        "input": input_tensor.detach().squeeze(0).numpy(),
        "baseline": baseline.detach().squeeze(0).numpy(),
        "attr": attr_t.detach().squeeze(0).numpy(),
        "attr_norm": attr_norm.astype(np.float32),
    }
    return result, tensors, attr_res


def analyze_catboost(extractor, session, img_rgb, image_size):
    """Расчёт ResNet50 + CatBoost: признаки картинки и вероятность «Real» от CatBoost."""
    h, w, _ = img_rgb.shape
    input_tensor = make_transform(image_size)(img_rgb).unsqueeze(0)
    with torch.no_grad():
        features = extractor(input_tensor).numpy()
    # probability_tensor — вероятности [сгенерировано, настоящее].
    probs = session.run(["probability_tensor"], {"features": features})[0]
    prob = float(probs[0, 1])

    result = {"width": w, "height": h, "prob": prob, "is_generated": prob <= 0.5, "risk": 1 - prob}
    tensors = {"input": input_tensor.squeeze(0).numpy(), "features": features.squeeze(0)}
    return result, tensors, None


def save_pictures(folder, img_rgb, attr_res):
    # JET-карта и наложение 0.6 картинки + 0.4 карты.
    hm = cv2.applyColorMap(np.uint8(255 * attr_res), cv2.COLORMAP_JET)
    ov = cv2.addWeighted(img_rgb, 0.6, cv2.cvtColor(hm, cv2.COLOR_BGR2RGB), 0.4, 0)
    cv2.imencode(".png", hm)[1].tofile(folder / "heatmap.png")
    cv2.imencode(".png", cv2.cvtColor(ov, cv2.COLOR_RGB2BGR))[1].tofile(folder / "overlay.png")


def save_ig_steps(out):
    step_sizes, alphas = approximation_parameters("gausslegendre")
    with open(out / "ig_steps.json", "w", encoding="utf-8") as file:
        json.dump({"method": "gausslegendre", "n_steps": N_STEPS,
                   "alphas": list(map(float, alphas(N_STEPS))),
                   "step_sizes": list(map(float, step_sizes(N_STEPS)))}, file, indent=1)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("images", type=Path, nargs="+")
    parser.add_argument("--arch", choices=ARCHS, default="efficientnet_b3", help="какую модель считать")
    parser.add_argument("--weights", type=Path, help="файл модели (по умолчанию из checkpoints/<модель>)")
    parser.add_argument("--out", type=Path, default=ROOT / "reference")
    args = parser.parse_args()
    image_size = ARCHS[args.arch]["image_size"]
    weights = args.weights or ARCHS[args.arch]["weights"]

    out = args.out / args.arch
    out.mkdir(parents=True, exist_ok=True)
    save_ig_steps(args.out)
    if args.arch == "resnet50_catboost":
        import onnxruntime
        extractor = load_feature_extractor()
        session = onnxruntime.InferenceSession(str(weights), providers=["CPUExecutionProvider"])
        run = lambda img: analyze_catboost(extractor, session, img, image_size)
    else:
        model = load_model(args.arch, weights)
        run = lambda img: analyze(model, img, image_size)
    print(f"PyTorch {torch.__version__}, потоков: {torch.get_num_threads()}, модель: {args.arch}")

    summary = {}
    for path in args.images:
        started = time.time()
        img_rgb = read_image(path)
        result, tensors, attr_res = run(img_rgb)

        folder = out / path.stem
        folder.mkdir(exist_ok=True)
        result["file"] = path.name
        with open(folder / "result.json", "w", encoding="utf-8") as file:
            json.dump(result, file, ensure_ascii=False, indent=1)
        for name, array in tensors.items():
            np.ascontiguousarray(array, dtype="<f4").tofile(folder / f"{name}.bin")
        verdict = f"{path.name}: prob {result['prob']:.4f} ({'СГЕНЕРИРОВАНО' if result['is_generated'] else 'НАСТОЯЩЕЕ'})"

        if attr_res is None:
            summary[path.name] = {key: result[key] for key in ("prob", "is_generated")}
        else:
            save_pictures(folder, img_rgb, attr_res)
            summary[path.name] = {key: result[key] for key in
                                  ("prob", "is_generated", "entropy", "concentration", "std", "best_region", "anomaly_type")}
            verdict += (f", энтропия {result['entropy']:.4f}, концентрация {result['concentration']:.2%}, "
                        f"СКО {result['std']:.4f}, зона «{result['best_region']}», «{result['anomaly_type']}»")
        print(f"{verdict}  ({time.time() - started:.0f} с)", flush=True)

    with open(out / "summary.json", "w", encoding="utf-8") as file:
        json.dump(summary, file, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
