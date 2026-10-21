"""Обучение EfficientNet-B3 для распознавания сгенерированных изображений.

Выход модели после сигмоиды — вероятность класса «Real»:
prob <= 0.5 означает, что изображение сгенерировано.

Обучение идёт в два этапа:
  1. head     — свёрточная часть заморожена, учится только новый выходной слой;
  2. finetune — разморожена вся сеть, дообучение с низкой скоростью.

Защита от перекоса датасета: все настоящие фото в нём — JPEG, а около трети
сгенерированных — PNG. Чтобы сеть не выучила «нет следов JPEG-сжатия → подделка»,
обучающие картинки пересохраняются в JPEG (см. --jpeg-prob). Тест считается дважды:
на картинках как есть и на пересжатых, плюс точность отдельно по PNG и прочим подделкам.
"""
import argparse
import io
import json
import os
import random
import shutil
import time
import zipfile
from pathlib import Path

import torch
from PIL import Image, ImageFile, ImageOps
from sklearn.metrics import roc_auc_score
from torch import nn
from torch.utils.data import DataLoader, Dataset
from torchvision import models, transforms

ROOT = Path(__file__).parent
LABELS = {"fake": 0.0, "real": 1.0}
IMAGE_SIZE = 300
THRESHOLD = 0.5
MEAN, STD = [0.485, 0.456, 0.406], [0.229, 0.224, 0.225]
JPEG_QUALITY = (65, 95)  # диапазон качества при пересжатии обучающих картинок
TEST_JPEG_QUALITY = 90   # качество при пересжатии тестовых картинок

# В датасете есть очень большие и слегка повреждённые файлы — читаем их без ошибок.
Image.MAX_IMAGE_PIXELS = None
ImageFile.LOAD_TRUNCATED_IMAGES = True


class ZipImages(Dataset):
    """Картинки из zip-архива без распаковки; каждый рабочий процесс открывает архив сам."""

    def __init__(self, zip_path, samples, transform):
        self.zip_path = str(zip_path)
        self.samples = samples
        self.transform = transform
        self._archive = None

    def __len__(self):
        return len(self.samples)

    def __getstate__(self):
        state = self.__dict__.copy()
        state["_archive"] = None
        return state

    def __getitem__(self, index):
        if self._archive is None:
            self._archive = zipfile.ZipFile(self.zip_path)

        name, label = self.samples[index]
        try:
            image = Image.open(io.BytesIO(self._archive.read(name)))
            image = ImageOps.exif_transpose(image).convert("RGB")
        except Exception as error:
            print(f"Файл {name} не прочитан ({error}), взят следующий.")
            return self[(index + 1) % len(self)]

        return self.transform(image), label


class RandomJpeg:
    """С заданной вероятностью пересохраняет картинку в JPEG со случайным качеством.

    После этого следы JPEG-сжатия есть у картинок обоих классов, и формат исходного
    файла перестаёт подсказывать ответ. Делается до сжатия до 300x300, пока следы
    исходного сжатия ещё не размыты.
    """

    def __init__(self, probability, quality):
        self.probability = probability
        self.quality = quality

    def __call__(self, image):
        if torch.rand(1).item() >= self.probability:
            return image
        quality = int(torch.randint(self.quality[0], self.quality[1] + 1, (1,)))
        buffer = io.BytesIO()
        image.save(buffer, "JPEG", quality=quality)
        buffer.seek(0)
        return Image.open(buffer).convert("RGB")


def list_samples(names, split, limit):
    """Отбирает файлы вида split/класс/имя и ставит им метку класса."""
    samples = []
    for folder, label in LABELS.items():
        prefix = f"{split}/{folder}/"
        found = sorted(name for name in names if name.startswith(prefix))
        samples += [(name, label) for name in found[:limit]]
    return samples


def split_train_val(samples, val_share, seed):
    """Откладывает долю обучающих картинок для проверки, поровну из каждого класса."""
    rng = random.Random(seed)
    train, val = [], []
    for label in LABELS.values():
        group = [sample for sample in samples if sample[1] == label]
        rng.shuffle(group)
        cut = int(len(group) * val_share)
        val += group[:cut]
        train += group[cut:]
    return train, val


def build_model():
    model = models.efficientnet_b3(weights=models.EfficientNet_B3_Weights.IMAGENET1K_V1)
    model.classifier[1] = nn.Linear(model.classifier[1].in_features, 1)
    return model


def configure_stage(model, stage, args):
    """Замораживает или размораживает свёрточную часть и создаёт оптимизатор этапа."""
    frozen = stage == "head"
    for parameter in model.features.parameters():
        parameter.requires_grad = not frozen

    if frozen:
        return torch.optim.Adam(model.classifier.parameters(), lr=args.head_lr), None

    optimizer = torch.optim.Adam(model.parameters(), lr=args.lr)
    scheduler = torch.optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=args.epochs)
    return optimizer, scheduler


def to_device(images, device):
    return images.to(device, non_blocking=True).contiguous(memory_format=torch.channels_last)


def train_epoch(model, loader, optimizer, scaler, device, stage, log):
    model.train()
    if stage == "head":
        model.features.eval()  # замороженная часть не должна менять статистику BatchNorm

    criterion = nn.BCEWithLogitsLoss()
    use_amp = device.type == "cuda"
    started = time.time()
    loss_sum, seen = 0.0, 0

    for step, (images, labels) in enumerate(loader, 1):
        images = to_device(images, device)
        labels = labels.float().to(device, non_blocking=True)

        with torch.autocast(device.type, enabled=use_amp):
            loss = criterion(model(images).squeeze(1), labels)

        optimizer.zero_grad(set_to_none=True)
        scaler.scale(loss).backward()
        scaler.step(optimizer)
        scaler.update()

        loss_sum += loss.item() * len(labels)
        seen += len(labels)
        if step % 50 == 0 or step == len(loader):
            speed = seen / (time.time() - started)
            left = (len(loader) - step) * loader.batch_size / speed / 60
            log(f"  шаг {step}/{len(loader)}  loss {loss_sum / seen:.4f}  "
                f"{speed:.0f} изобр/с  до конца эпохи ~{left:.0f} мин")

    return loss_sum / seen


@torch.no_grad()
def evaluate(model, loader, device):
    """Считает метрики; «положительным» считается класс «сгенерировано»."""
    model.eval()
    logits, labels = [], []
    for images, batch_labels in loader:
        with torch.autocast(device.type, enabled=device.type == "cuda"):
            output = model(to_device(images, device))
        logits.append(output.squeeze(1).float().cpu())
        labels.append(batch_labels.float())

    logits, labels = torch.cat(logits), torch.cat(labels)
    real_probability = torch.sigmoid(logits)
    said_generated = real_probability <= THRESHOLD
    is_generated = labels == LABELS["fake"]

    tp = (said_generated & is_generated).sum().item()
    fp = (said_generated & ~is_generated).sum().item()
    fn = (~said_generated & is_generated).sum().item()
    tn = (~said_generated & ~is_generated).sum().item()
    precision = tp / (tp + fp) if tp + fp else 0.0
    recall = tp / (tp + fn) if tp + fn else 0.0

    # Если сеть смотрит на формат файла, подделки-PNG она узнаёт заметно лучше остальных.
    is_png = torch.tensor([Path(name).suffix.lower() == ".png" for name, _ in loader.dataset.samples])
    correct = said_generated == is_generated

    def group_accuracy(mask):
        return correct[mask].float().mean().item() if mask.any() else None

    return {
        "loss": nn.functional.binary_cross_entropy_with_logits(logits, labels).item(),
        "accuracy": (tp + tn) / len(labels),
        "precision": precision,
        "recall": recall,
        "f1": 2 * precision * recall / (precision + recall) if precision + recall else 0.0,
        "roc_auc": float(roc_auc_score(labels.numpy(), real_probability.numpy())),
        "generated_as_generated": tp,
        "generated_as_real": fn,
        "real_as_generated": fp,
        "real_as_real": tn,
        "accuracy_generated_png": group_accuracy(is_generated & is_png),
        "accuracy_generated_other": group_accuracy(is_generated & ~is_png),
        "accuracy_real": group_accuracy(~is_generated),
    }


def format_metrics(metrics):
    def share(value):
        return "—" if value is None else f"{value:.4f}"

    return (f"accuracy {metrics['accuracy']:.4f}  f1 {metrics['f1']:.4f}  "
            f"roc-auc {metrics['roc_auc']:.4f}  loss {metrics['loss']:.4f}\n"
            f"    по группам: подделки PNG {share(metrics['accuracy_generated_png'])}  "
            f"прочие подделки {share(metrics['accuracy_generated_other'])}  "
            f"настоящие {share(metrics['accuracy_real'])}")


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--data", type=Path, default=ROOT / "data" / "dataset.zip", help="архив с датасетом")
    parser.add_argument("--out", type=Path, default=ROOT / "checkpoints", help="куда сохранять веса")
    parser.add_argument("--head-epochs", type=int, default=1, help="эпох на этапе с замороженной сетью")
    parser.add_argument("--epochs", type=int, default=5, help="эпох на этапе полного дообучения")
    parser.add_argument("--batch-size", type=int, default=32)
    parser.add_argument("--head-lr", type=float, default=1e-3)
    parser.add_argument("--lr", type=float, default=1e-4)
    parser.add_argument("--jpeg-prob", type=float, default=1.0,
                        help="доля обучающих картинок, пересохраняемых в JPEG (0 — без защиты)")
    parser.add_argument("--val-share", type=float, default=0.1, help="доля train, отложенная для проверки")
    parser.add_argument("--workers", type=int, default=min(8, max(2, (os.cpu_count() or 4) - 2)),
                        help="процессов, читающих картинки")
    parser.add_argument("--limit", type=int, help="взять не больше стольких картинок каждого класса")
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--resume", action="store_true", help="продолжить прерванное обучение")
    parser.add_argument("--smoke", action="store_true",
                        help="быстрая проверка, что всё работает: мало картинок, по одной эпохе")
    args = parser.parse_args()

    if args.smoke:
        args.limit = args.limit or 64
        args.head_epochs, args.epochs = 1, 1
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
    torch.backends.cudnn.benchmark = True

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    log(f"PyTorch {torch.__version__}, устройство: "
        f"{torch.cuda.get_device_name(0) if device.type == 'cuda' else 'процессор'}")
    if device.type != "cuda" and not args.smoke:
        log("ВНИМАНИЕ: видеокарта не найдена, полное обучение на процессоре займёт дни.")

    if not args.data.exists():
        raise SystemExit(f"Архив {args.data} не найден. Сначала запустите download_dataset.py.")
    with zipfile.ZipFile(args.data) as archive:
        names = archive.namelist()

    train_samples, val_samples = split_train_val(list_samples(names, "train", args.limit), args.val_share, args.seed)
    test_samples = list_samples(names, "test", args.limit)
    if not train_samples or not val_samples or not test_samples:
        raise SystemExit("В архиве не найдены папки train/fake, train/real, test/fake, test/real.")
    log(f"Картинок: обучение {len(train_samples)}, проверка {len(val_samples)}, тест {len(test_samples)}")

    # То же преобразование, что при анализе картинки: сжатие до 300x300 и нормализация ImageNet.
    eval_transform = transforms.Compose([
        transforms.Resize((IMAGE_SIZE, IMAGE_SIZE)),
        transforms.ToTensor(),
        transforms.Normalize(MEAN, STD),
    ])
    train_transform = transforms.Compose([
        transforms.RandomHorizontalFlip(),
        RandomJpeg(args.jpeg_prob, JPEG_QUALITY),
        eval_transform,
    ])
    jpeg_test_transform = transforms.Compose([
        RandomJpeg(1.0, (TEST_JPEG_QUALITY, TEST_JPEG_QUALITY)),
        eval_transform,
    ])

    def make_loader(samples, transform, training):
        return DataLoader(
            ZipImages(args.data, samples, transform),
            batch_size=args.batch_size,
            shuffle=training,
            drop_last=training,
            num_workers=args.workers,
            pin_memory=device.type == "cuda")

    train_loader = make_loader(train_samples, train_transform, training=True)
    val_loader = make_loader(val_samples, eval_transform, training=False)
    test_loader = make_loader(test_samples, eval_transform, training=False)
    jpeg_test_loader = make_loader(test_samples, jpeg_test_transform, training=False)

    model = build_model().to(device, memory_format=torch.channels_last)
    scaler = torch.amp.GradScaler(device.type, enabled=device.type == "cuda")

    plan = [("head", n) for n in range(1, args.head_epochs + 1)] + \
           [("finetune", n) for n in range(1, args.epochs + 1)]
    history, best, first, saved = [], None, 0, None

    last_path = args.out / "last.pt"
    if args.resume:
        if not last_path.exists():
            raise SystemExit(f"Нечего продолжать: {last_path} не найден.")
        saved = torch.load(last_path, map_location=device)
        model.load_state_dict(saved["model"])
        scaler.load_state_dict(saved["scaler"])
        history, best, first = saved["history"], saved["best"], saved["done"]
        log(f"Продолжение: уже пройдено эпох {first} из {len(plan)}")

    stage, optimizer, scheduler = None, None, None
    for index in range(first, len(plan)):
        if plan[index][0] != stage:
            stage = plan[index][0]
            optimizer, scheduler = configure_stage(model, stage, args)
            if saved and saved["stage"] == stage:
                optimizer.load_state_dict(saved["optimizer"])
                if scheduler:
                    scheduler.load_state_dict(saved["scheduler"])
            saved = None

        epoch = plan[index][1]
        total = args.head_epochs if stage == "head" else args.epochs
        log(f"\n[{stage}] эпоха {epoch}/{total}, скорость обучения {optimizer.param_groups[0]['lr']:.2e}")
        started = time.time()
        train_loss = train_epoch(model, train_loader, optimizer, scaler, device, stage, log)
        metrics = evaluate(model, val_loader, device)
        if scheduler:
            scheduler.step()
        log(f"  проверка: {format_metrics(metrics)}  ({(time.time() - started) / 60:.1f} мин)")

        record = {"stage": stage, "epoch": epoch, "train_loss": train_loss, **metrics}
        history.append(record)

        if stage == "finetune":
            path = args.out / f"efficientnet_b3_finetuned_epoch{epoch}.pth"
            torch.save(model.state_dict(), path)
            if best is None or metrics["accuracy"] > best["accuracy"]:
                best = {**record, "file": path.name}

        torch.save({
            "model": model.state_dict(),
            "optimizer": optimizer.state_dict(),
            "scheduler": scheduler.state_dict() if scheduler else None,
            "scaler": scaler.state_dict(),
            "stage": stage,
            "done": index + 1,
            "history": history,
            "best": best,
        }, last_path)

    if best is None:
        raise SystemExit("Этап finetune не выполнялся (--epochs 0), итоговых весов нет.")

    best_path = args.out / "efficientnet_b3_best.pth"
    shutil.copyfile(args.out / best["file"], best_path)
    model.load_state_dict(torch.load(best_path, map_location=device))
    test_metrics = evaluate(model, test_loader, device)
    jpeg_test_metrics = evaluate(model, jpeg_test_loader, device)

    log(f"\nЛучшая эпоха: {best['epoch']} ({best['file']})")
    log(f"Тест (картинки, которых сеть не видела): {format_metrics(test_metrics)}")
    log(f"Тот же тест, все пересохранены в JPEG {TEST_JPEG_QUALITY}: {format_metrics(jpeg_test_metrics)}")
    log(f"Итоговые веса: {best_path}")

    with open(args.out / "metrics.json", "w", encoding="utf-8") as file:
        json.dump({
            "best": best,
            "test": test_metrics,
            "test_jpeg": jpeg_test_metrics,
            "history": history,
            "settings": {key: str(value) for key, value in vars(args).items()},
            "torch": torch.__version__,
        }, file, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
