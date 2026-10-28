"""Переводит модель CatBoost в ONNX — формат, который в C# считает ONNX Runtime.

На вход модели подаются 2048 признаков картинки от ResNet50 (их считает train_catboost.py),
на выходе — метка (label) и вероятности двух классов (probability_tensor): [сгенерировано, настоящее].

Запуск: .venv\\Scripts\\python export_catboost_onnx.py
"""
import argparse
import json
import tempfile
from pathlib import Path

import onnx
from catboost import CatBoostClassifier

ROOT = Path(__file__).resolve().parent


def remove_zipmap(path):
    """Убирает из графа узел ZipMap: выход probabilities становится обычной таблицей N×2.

    CatBoost отдаёт вероятности списком словарей {класс: вероятность}; читать такой выход
    в C# неудобно, а таблица float[N, 2] читается как обычный массив.
    """
    graph_model = onnx.load(str(path))
    graph = graph_model.graph
    zipmap = next((node for node in graph.node if node.op_type == "ZipMap"), None)
    if zipmap is None:
        return
    graph.node.remove(zipmap)
    output = next(item for item in graph.output if item.name == zipmap.output[0])
    graph.output.remove(output)
    graph.output.append(onnx.helper.make_tensor_value_info(zipmap.input[0], onnx.TensorProto.FLOAT, ["N", 2]))
    onnx.checker.check_model(graph_model)
    onnx.save(graph_model, str(path))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--model", type=Path, default=ROOT / "checkpoints" / "resnet50_catboost" / "resnet50_catboost.cbm")
    parser.add_argument("--out", type=Path, help="куда сохранить (по умолчанию рядом с моделью, .onnx)")
    args = parser.parse_args()
    out = args.out or args.model.with_suffix(".onnx")

    model = CatBoostClassifier()
    model.load_model(str(args.model))
    onnx_model = model
    if model.classes_.dtype.kind == "f":
        # Модель, обученная на метках 0.0/1.0, хранит классы дробными числами, а ONNX требует
        # целые. Деревья от этого не зависят: меняем только тип меток через JSON-выгрузку.
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "model.json"
            model.save_model(str(path), format="json")
            exported = json.loads(path.read_text(encoding="utf-8"))
            exported["model_info"]["class_params"]["class_label_type"] = "Integer"
            path.write_text(json.dumps(exported), encoding="utf-8")
            onnx_model = CatBoostClassifier()
            onnx_model.load_model(str(path), format="json")
    onnx_model.save_model(str(out), format="onnx", export_parameters={"onnx_graph_name": "resnet50_catboost"})
    remove_zipmap(out)
    print(f"Сохранено: {out} ({out.stat().st_size / 2**20:.1f} МБ)")


if __name__ == "__main__":
    main()
