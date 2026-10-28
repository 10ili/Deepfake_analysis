"""Обучение ResNet50 + CatBoost для распознавания сгенерированных изображений.

ResNet50 с весами ImageNet здесь не обучается: она только превращает каждую картинку
в 2048 чисел (признаки — выход сети перед последним слоем). На этих признаках
учится CatBoost — градиентный бустинг на решающих деревьях.

Метки те же, что в train.py: CatBoost выдаёт вероятность класса «Real»,
prob <= 0.5 означает, что изображение сгенерировано.

Признаки считаются один раз и сохраняются в <out>/features; повторный запуск
берёт их оттуда и сразу переходит к CatBoost. Защита от перекоса PNG/JPEG та же,
что в train.py: обучающие картинки перед расчётом признаков пересохраняются в JPEG.
"""
import argparse
import json
import os
import random
import sys
import time
import zipfile
from pathlib import Path

import catboost
import numpy as np
import torch
from catboost import CatBoostClassifier, Pool
from torch import nn
from torch.utils.data import DataLoader
from torchvision import models, transforms

from train import (JPEG_QUALITY, MEAN, STD, ROOT, TEST_JPEG_QUALITY, RandomJpeg, ZipImages,
                   compute_metrics, format_metrics, list_samples, split_train_val)

IMAGE_SIZE = 224


def build_extractor():
    """ResNet50 без последнего слоя: на выходе 2048 признаков вместо 1000 классов ImageNet."""
    model = models.resnet50(weights=models.ResNet50_Weights.IMAGENET1K_V1)
    model.fc = nn.Identity()
    return model.eval()


@torch.inference_mode()
def extract_features(model, samples, transform, variant, args, device, log):
    """Признаки и метки для списка картинок; берёт готовые из кэша, если он от тех же картинок.

    variant — как картинки преобразованы перед сетью (например, «jpeg1.0»); кэш с другим
    преобразованием не подходит.
    """
    path = args.out / "features" / f"{variant}_{len(samples)}.npz"
    names = np.array([name for name, _ in samples])
    if path.exists():
        cached = np.load(path)
        if np.array_equal(cached["names"], names):
            log(f"  признаки взяты из {path}")
            return cached["features"], cached["labels"]

    loader = DataLoader(
        ZipImages(args.data, samples, transform),
        batch_size=args.batch_size,
        num_workers=args.workers,
        pin_memory=device.type == "cuda")

    features, labels = [], []
    started, seen = time.time(), 0
    for step, (images, batch_labels) in enumerate(loader, 1):
        # Без autocast: признаки в float32, такие же, какие потом посчитает C#.
        features.append(model(images.to(device, non_blocking=True)).cpu().numpy())
        labels.append(batch_labels.numpy())
        seen += len(batch_labels)
        if step % 50 == 0 or step == len(loader):
            speed = seen / (time.time() - started)
            left = (len(loader) - step) * args.batch_size / speed / 60
            log(f"  шаг {step}/{len(loader)}  {speed:.0f} изобр/с  осталось ~{left:.0f} мин")

    features = np.concatenate(features).astype(np.float32)
    labels = np.concatenate(labels).astype(np.float32)
    path.parent.mkdir(parents=True, exist_ok=True)
    np.savez(path, features=features, labels=labels, names=names)
    return features, labels


class LogStream:
    """Принимает вывод CatBoost и пишет его и на экран, и в train.log."""

    def __init__(self, log_file):
        self.log_file = log_file

    def write(self, text):
        sys.stdout.write(text)
        self.log_file.write(text)

    def flush(self):
        sys.stdout.flush()
        self.log_file.flush()


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--data", type=Path, default=ROOT / "data" / "dataset.zip", help="архив с датасетом")
    parser.add_argument("--out", type=Path, default=ROOT / "checkpoints" / "resnet50_catboost",
                        help="куда сохранять модель и признаки")
    parser.add_argument("--iterations", type=int, default=2000, help="сколько деревьев строит CatBoost")
    parser.add_argument("--depth", type=int, default=6, help="глубина каждого дерева")
    parser.add_argument("--lr", type=float, default=0.02, help="скорость обучения CatBoost")
    parser.add_argument("--batch-size", type=int, default=64, help="картинок за раз при расчёте признаков")
    parser.add_argument("--jpeg-prob", type=float, default=1.0,
                        help="доля обучающих картинок, пересохраняемых в JPEG (0 — без защиты)")
    parser.add_argument("--val-share", type=float, default=0.1, help="доля train, отложенная для проверки")
    parser.add_argument("--workers", type=int, default=min(8, max(2, (os.cpu_count() or 4) - 2)),
                        help="процессов, читающих картинки")
    parser.add_argument("--threads", type=int, default=-1, help="потоков CatBoost (-1 — все ядра)")
    parser.add_argument("--limit", type=int, help="взять не больше стольких картинок каждого класса")
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--smoke", action="store_true",
                        help="быстрая проверка, что всё работает: мало картинок и деревьев")
    args = parser.parse_args()

    if args.smoke:
        args.limit = args.limit or 64
        args.iterations = min(args.iterations, 50)
        args.batch_size = min(args.batch_size, 8)
        args.out = args.out / "smoke"
    return args


def main():
    args = parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    log_file = open(args.out / "train.log", "a", encoding="utf-8")

    def log(message=""):
        print(message, flush=True)
        log_file.write(message + "\n")
        log_file.flush()

    random.seed(args.seed)
    torch.manual_seed(args.seed)

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    log(f"PyTorch {torch.__version__}, CatBoost {catboost.__version__}, устройство для признаков: "
        f"{torch.cuda.get_device_name(0) if device.type == 'cuda' else 'процессор'}")

    if not args.data.exists():
        raise SystemExit(f"Архив {args.data} не найден. Сначала запустите download_dataset.py.")
    with zipfile.ZipFile(args.data) as archive:
        names = archive.namelist()

    # Та же раскладка на обучение, проверку и тест, что в train.py с тем же --seed.
    train_samples, val_samples = split_train_val(list_samples(names, "train", args.limit), args.val_share, args.seed)
    test_samples = list_samples(names, "test", args.limit)
    if not train_samples or not val_samples or not test_samples:
        raise SystemExit("В архиве не найдены папки train/fake, train/real, test/fake, test/real.")
    log(f"Картинок: обучение {len(train_samples)}, проверка {len(val_samples)}, тест {len(test_samples)}")

    # Сжатие до 224x224 и нормализация ImageNet — как при обучении ResNet50 в train.py.
    eval_transform = transforms.Compose([
        transforms.Resize((IMAGE_SIZE, IMAGE_SIZE)),
        transforms.ToTensor(),
        transforms.Normalize(MEAN, STD),
    ])
    train_transform = transforms.Compose([RandomJpeg(args.jpeg_prob, JPEG_QUALITY), eval_transform])
    jpeg_test_transform = transforms.Compose([
        RandomJpeg(1.0, (TEST_JPEG_QUALITY, TEST_JPEG_QUALITY)),
        eval_transform,
    ])

    extractor = build_extractor().to(device)
    parts = {}
    for part, samples, transform, variant in [
        ("train", train_samples, train_transform, f"train_jpeg{args.jpeg_prob}"),
        ("val", val_samples, eval_transform, "val"),
        ("test", test_samples, eval_transform, "test"),
        ("test_jpeg", test_samples, jpeg_test_transform, f"test_jpeg{TEST_JPEG_QUALITY}"),
    ]:
        log(f"\nПризнаки: {part}")
        parts[part] = extract_features(extractor, samples, transform, variant, args, device, log)
    del extractor

    # use_best_model: из всех построенных деревьев оставить столько, сколько дало
    # наименьший Logloss на проверочных картинках.
    log(f"\nCatBoost: {args.iterations} деревьев глубины {args.depth}, скорость обучения {args.lr}")
    model = CatBoostClassifier(
        iterations=args.iterations,
        depth=args.depth,
        learning_rate=args.lr,
        loss_function="Logloss",
        random_seed=args.seed,
        thread_count=args.threads,
        use_best_model=True,
        train_dir=str(args.out / "catboost_info"))
    started = time.time()
    model.fit(Pool(*parts["train"]), eval_set=Pool(*parts["val"]), metric_period=100,
              log_cout=LogStream(log_file), log_cerr=LogStream(log_file))
    log(f"CatBoost обучен за {(time.time() - started) / 60:.1f} мин, "
        f"лучшее число деревьев: {model.get_best_iteration() + 1}")

    # RawFormulaVal — ответ до сигмоиды, как logit у нейросети; так метрики считаются общей функцией.
    def metrics_for(part, samples):
        features, labels = parts[part]
        logits = model.predict(features, prediction_type="RawFormulaVal")
        return compute_metrics(torch.from_numpy(logits).float(), torch.from_numpy(labels),
                               [name for name, _ in samples])

    val_metrics = metrics_for("val", val_samples)
    test_metrics = metrics_for("test", test_samples)
    jpeg_test_metrics = metrics_for("test_jpeg", test_samples)

    # .cbm — родной формат CatBoost, .json — деревья в виде текста, чтобы считать их без CatBoost.
    model.save_model(str(args.out / "resnet50_catboost.cbm"))
    model.save_model(str(args.out / "resnet50_catboost.json"), format="json")

    log(f"\nПроверка: {format_metrics(val_metrics)}")
    log(f"Тест (картинки, которых модель не видела): {format_metrics(test_metrics)}")
    log(f"Тот же тест, все пересохранены в JPEG {TEST_JPEG_QUALITY}: {format_metrics(jpeg_test_metrics)}")
    log(f"Итоговая модель: {args.out / 'resnet50_catboost.cbm'} (и .json)")

    with open(args.out / "metrics.json", "w", encoding="utf-8") as file:
        json.dump({
            "best_iteration": model.get_best_iteration(),
            "val": val_metrics,
            "test": test_metrics,
            "test_jpeg": jpeg_test_metrics,
            "settings": {key: str(value) for key, value in vars(args).items()},
            "torch": torch.__version__,
            "catboost": catboost.__version__,
        }, file, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
