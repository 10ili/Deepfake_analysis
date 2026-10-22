"""Эталонный расчёт анализа картинки на Python — «правильные ответы» для C#.

Для каждой картинки считает вероятность «Real», тепловую карту Integrated Gradients и все метрики
и сохраняет в reference/<имя>/:
  result.json        — вероятность, метрики, сетка 3x3, зона, тип аномалии и промежуточные числа;
  input.bin          — тензор после предобработки (3x300x300), чтобы сверять сеть отдельно от декодера;
  baseline.bin       — базовая линия IG (размытый вход, 3x300x300);
  attr.bin           — сырые атрибуции IG (3x300x300);
  attr_norm.bin      — карта после сглаживания и нормировки (300x300), до растяжения до размера картинки;
  heatmap.png, overlay.png — тепловая карта и наложение на исходную картинку.
Файлы .bin — float32 little-endian, порядок C x H x W (как в PyTorch).
В reference/ig_steps.json — точки и веса метода Гаусса–Лежандра, которым Captum считает интеграл.

Запуск: .venv\\Scripts\\python reference.py --weights checkpoints\\efficientnet_b3_best.pth картинки...
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


def load_model(weights):
    # EfficientNet-B3 с одним выходом — логит вероятности «Real».
    model = models.efficientnet_b3(weights=None)
    model.classifier[1] = torch.nn.Linear(model.classifier[1].in_features, 1)
    model.load_state_dict(torch.load(weights, map_location="cpu"))
    return model.eval()


TRANSFORM = transforms.Compose([
    transforms.ToPILImage(), transforms.Resize((300, 300)), transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])


def read_image(path):
    # cv2 сам поворачивает картинку по EXIF.
    img = cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_COLOR)
    if img is None:
        raise ValueError(f"Не удалось декодировать {path}")
    return cv2.cvtColor(img, cv2.COLOR_BGR2RGB)


def analyze(model, img_rgb):
    """Полный расчёт анализа; дополнительно возвращает промежуточные данные."""
    h, w, _ = img_rgb.shape
    input_tensor = TRANSFORM(img_rgb).unsqueeze(0)
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
    parser.add_argument("--weights", type=Path, default=ROOT / "checkpoints" / "efficientnet_b3_best.pth")
    parser.add_argument("--out", type=Path, default=ROOT / "reference")
    args = parser.parse_args()

    args.out.mkdir(parents=True, exist_ok=True)
    save_ig_steps(args.out)
    model = load_model(args.weights)
    print(f"PyTorch {torch.__version__}, потоков: {torch.get_num_threads()}")

    summary = {}
    for path in args.images:
        started = time.time()
        img_rgb = read_image(path)
        result, tensors, attr_res = analyze(model, img_rgb)

        folder = args.out / path.stem
        folder.mkdir(exist_ok=True)
        result["file"] = path.name
        with open(folder / "result.json", "w", encoding="utf-8") as file:
            json.dump(result, file, ensure_ascii=False, indent=1)
        for name, array in tensors.items():
            np.ascontiguousarray(array, dtype="<f4").tofile(folder / f"{name}.bin")
        save_pictures(folder, img_rgb, attr_res)

        summary[path.name] = {key: result[key] for key in
                              ("prob", "is_generated", "entropy", "concentration", "std", "best_region", "anomaly_type")}
        print(f"{path.name}: prob {result['prob']:.4f} ({'СГЕНЕРИРОВАНО' if result['is_generated'] else 'НАСТОЯЩЕЕ'}), "
              f"энтропия {result['entropy']:.4f}, концентрация {result['concentration']:.2%}, СКО {result['std']:.4f}, "
              f"зона «{result['best_region']}», «{result['anomaly_type']}»  ({time.time() - started:.0f} с)", flush=True)

    with open(args.out / "summary.json", "w", encoding="utf-8") as file:
        json.dump(summary, file, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
