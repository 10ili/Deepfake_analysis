"""Скачивает датасет «AI-generated images vs real images» с Kaggle (около 52 ГБ) и проверяет архив.

Архив не распаковывается: train.py читает картинки прямо из него.
Загрузку можно прервать (Ctrl+C) и запустить заново — она продолжится с места остановки.
"""
import argparse
import collections
import http.client
import shutil
import sys
import time
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

DATASET = "tristanzhang32/ai-generated-images-vs-real-images"
API_URL = "https://www.kaggle.com/api/v1/datasets/download/"
DEFAULT_OUT = Path(__file__).parent / "data" / "dataset.zip"
CHUNK = 1024 * 1024
MAX_RETRIES = 20
GB = 1024 ** 3


def open_from(url, start):
    """Открывает загрузку начиная с байта start. Kaggle перенаправляет на временную ссылку хранилища."""
    request = urllib.request.Request(url, headers={"Range": f"bytes={start}-"})
    response = urllib.request.urlopen(request, timeout=60)
    if response.status != 206:
        response.close()
        raise RuntimeError(f"Сервер не поддержал докачку (код {response.status}).")
    total = int(response.headers["Content-Range"].rsplit("/", 1)[1])
    return response, total


def show_progress(done, total, speed):
    left = (total - done) / speed / 60 if speed > 0 else 0
    sys.stdout.write(f"\r{done / GB:6.2f} / {total / GB:.2f} ГБ ({done / total:5.1%})  "
                     f"{speed / 1024 ** 2:5.1f} МБ/с  осталось ~{left:4.0f} мин ")
    sys.stdout.flush()


def download(url, part):
    retries = 0
    space_checked = False
    while True:
        done = part.stat().st_size if part.exists() else 0
        started_at = done
        try:
            response, total = open_from(url, done)
            with response, open(part, "ab") as file:
                if not space_checked:
                    free = shutil.disk_usage(part.parent).free
                    if free < total - done:
                        raise SystemExit(f"Не хватает места: нужно ещё {(total - done) / GB:.1f} ГБ, "
                                         f"свободно {free / GB:.1f} ГБ.")
                    space_checked = True
                mark_time, mark_done = time.time(), done
                speed = 0.0
                while chunk := response.read(CHUNK):
                    file.write(chunk)
                    done += len(chunk)
                    now = time.time()
                    if now - mark_time >= 1:
                        speed = (done - mark_done) / (now - mark_time)
                        mark_time, mark_done = now, done
                        show_progress(done, total, speed)
            if done >= total:
                show_progress(done, total, speed)
                print()
                return total
        except urllib.error.HTTPError as error:
            if error.code == 416:  # запрошено начало за концом файла: всё уже скачано
                return done
            if error.code in (401, 403, 404):
                raise SystemExit(f"\nKaggle ответил кодом {error.code}. Скачайте архив вручную "
                                 f"(см. README.md, раздел «Если что-то пошло не так»).")
            problem = error
        except (urllib.error.URLError, http.client.HTTPException, OSError) as error:
            problem = error

        # счётчик попыток сбрасывается, если за попытку удалось что-то скачать
        retries = 1 if done > started_at else retries + 1
        if retries > MAX_RETRIES:
            raise SystemExit(f"\nЗагрузка не удалась: {problem}. Запустите скрипт ещё раз — он продолжит.")
        print(f"\nОбрыв связи ({problem}). Повтор через 10 с...")
        time.sleep(10)


def verify(path):
    """Проверяет, что архив открывается, и печатает, сколько картинок в каждой папке."""
    with zipfile.ZipFile(path) as archive:
        folders = collections.Counter(
            info.filename.rsplit("/", 1)[0] for info in archive.infolist() if not info.is_dir())
    print("Архив в порядке. Картинок по папкам:")
    for folder, count in sorted(folders.items()):
        print(f"  {folder:12} {count}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="куда сохранить архив")
    parser.add_argument("--dataset", default=DATASET, help="датасет Kaggle в виде автор/название")
    args = parser.parse_args()

    if args.out.exists():
        print(f"Архив уже скачан: {args.out}")
    else:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        part = args.out.with_name(args.out.name + ".part")
        print(f"Скачивание {args.dataset} в {args.out}")
        total = download(API_URL + args.dataset, part)
        if part.stat().st_size != total:
            raise SystemExit(f"Размер файла ({part.stat().st_size}) не совпал с ожидаемым ({total}). "
                             f"Удалите {part} и запустите скрипт заново.")
        part.rename(args.out)

    verify(args.out)


if __name__ == "__main__":
    main()
