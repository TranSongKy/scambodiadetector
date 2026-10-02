import argparse
import csv
import json
import random
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from masking import mask, normalize_text

SEED = 42
BASE_MODEL_NAME = "vinai/phobert-base-v2"
LABELS = ("normal", "spam", "scam")
SCAM_LABEL = "scam"
MAX_SEQUENCE_LENGTH = 256
ONNX_FILE_NAME = "scam-detector.onnx"
TOKENIZER_GOLDEN_FILE_NAME = "tokenizer-golden.json"
ONNX_OPSET_VERSION = 17
ONNX_PARITY_TOLERANCE = 1e-3
PARITY_SAMPLE_COUNT = 32
SPLIT_NAMES = ("train", "val", "test")
CHECKPOINT_DIRECTORY_SUFFIX = "-checkpoints"


@dataclass(frozen=True)
class Split:
    texts: list[str]
    label_ids: list[int]


@dataclass(frozen=True)
class Metrics:
    macro_f1: float
    scam_f1: float
    accuracy: float


def prepare_text(text: str) -> str:
    return mask(normalize_text(text))


def load_split(split_path: Path) -> Split:
    with split_path.open(encoding="utf-8", newline="") as split_file:
        rows = list(csv.DictReader(split_file))
    return Split(
        texts=[prepare_text(row["text"]) for row in rows],
        label_ids=[LABELS.index(row["label"]) for row in rows],
    )


def f1_for_label(predicted: list[int], expected: list[int], label_id: int) -> float:
    true_positive = sum(p == label_id and e == label_id for p, e in zip(predicted, expected, strict=True))
    predicted_positive = sum(p == label_id for p in predicted)
    expected_positive = sum(e == label_id for e in expected)
    if true_positive == 0:
        return 0.0
    precision = true_positive / predicted_positive
    recall = true_positive / expected_positive
    return 2 * precision * recall / (precision + recall)


def compute_metrics(predicted: list[int], expected: list[int]) -> Metrics:
    f1_scores = [f1_for_label(predicted, expected, label_id) for label_id in range(len(LABELS))]
    correct = sum(p == e for p, e in zip(predicted, expected, strict=True))
    return Metrics(
        macro_f1=sum(f1_scores) / len(f1_scores),
        scam_f1=f1_scores[LABELS.index(SCAM_LABEL)],
        accuracy=correct / len(expected) if expected else 0.0,
    )


def format_experiment_row(model_name: str, splits: dict[str, Split], hyperparameters: str, metrics: Metrics) -> str:
    sizes = "/".join(str(len(splits[name].texts)) for name in SPLIT_NAMES)
    return f"| <ngày> | {model_name} | {sizes} | {hyperparameters} | {metrics.macro_f1:.4f} | {metrics.scam_f1:.4f} | |"


def set_seed(seed: int) -> None:
    import numpy as np
    import torch

    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)


def tokenize_split(tokenizer: Any, split: Split) -> Any:
    from datasets import Dataset

    dataset = Dataset.from_dict({"text": split.texts, "labels": split.label_ids})
    return dataset.map(
        lambda batch: tokenizer(batch["text"], truncation=True, max_length=MAX_SEQUENCE_LENGTH),
        batched=True,
        remove_columns=["text"],
    )


def train(splits: dict[str, Split], output_dir: Path, options: argparse.Namespace) -> tuple[Any, Any]:
    from transformers import (
        AutoModelForSequenceClassification,
        AutoTokenizer,
        DataCollatorWithPadding,
        Trainer,
        TrainingArguments,
    )

    tokenizer = AutoTokenizer.from_pretrained(options.base_model, use_fast=False)
    model = AutoModelForSequenceClassification.from_pretrained(
        options.base_model,
        num_labels=len(LABELS),
        id2label=dict(enumerate(LABELS)),
        label2id={label: label_id for label_id, label in enumerate(LABELS)},
    )
    arguments = TrainingArguments(
        output_dir=str(output_dir.with_name(output_dir.name + CHECKPOINT_DIRECTORY_SUFFIX)),
        learning_rate=options.learning_rate,
        per_device_train_batch_size=options.batch_size,
        per_device_eval_batch_size=options.batch_size,
        num_train_epochs=options.epochs,
        weight_decay=options.weight_decay,
        eval_strategy="epoch",
        save_strategy="epoch",
        load_best_model_at_end=True,
        metric_for_best_model="macro_f1",
        save_total_limit=1,
        seed=SEED,
        report_to=[],
    )
    trainer = Trainer(
        model=model,
        args=arguments,
        train_dataset=tokenize_split(tokenizer, splits["train"]),
        eval_dataset=tokenize_split(tokenizer, splits["val"]),
        data_collator=DataCollatorWithPadding(tokenizer),
        compute_metrics=lambda evaluation: {
            "macro_f1": compute_metrics(
                evaluation.predictions.argmax(axis=-1).tolist(), evaluation.label_ids.tolist()
            ).macro_f1
        },
    )
    trainer.train()
    return trainer.model.eval(), tokenizer


def predict(model: Any, tokenizer: Any, texts: list[str]) -> list[list[float]]:
    import torch

    logits: list[list[float]] = []
    with torch.no_grad():
        for text in texts:
            encoded = tokenizer(text, truncation=True, max_length=MAX_SEQUENCE_LENGTH, return_tensors="pt")
            encoded = {name: tensor.to(model.device) for name, tensor in encoded.items() if name != "token_type_ids"}
            logits.append(model(**encoded).logits[0].float().cpu().tolist())
    return logits


def export_onnx(model: Any, tokenizer: Any, output_dir: Path) -> Path:
    import torch

    class LogitsOnly(torch.nn.Module):
        def __init__(self, wrapped: Any) -> None:
            super().__init__()
            self.wrapped = wrapped

        def forward(self, input_ids: Any, attention_mask: Any) -> Any:
            return self.wrapped(input_ids=input_ids, attention_mask=attention_mask).logits

    cpu_model = LogitsOnly(model.to("cpu").eval())
    sample = tokenizer("xin chào", return_tensors="pt")
    onnx_path = output_dir / ONNX_FILE_NAME
    torch.onnx.export(
        cpu_model,
        (sample["input_ids"], sample["attention_mask"]),
        str(onnx_path),
        input_names=["input_ids", "attention_mask"],
        output_names=["logits"],
        dynamic_axes={"input_ids": {0: "batch", 1: "sequence"}, "attention_mask": {0: "batch", 1: "sequence"}},
        opset_version=ONNX_OPSET_VERSION,
        dynamo=False,
    )
    tokenizer.save_pretrained(str(output_dir))
    return onnx_path


def verify_onnx_parity(onnx_path: Path, tokenizer: Any, texts: list[str], expected_logits: list[list[float]]) -> float:
    import numpy as np
    import onnxruntime

    session = onnxruntime.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    max_difference = 0.0
    for text, expected in zip(texts, expected_logits, strict=True):
        encoded = tokenizer(text, truncation=True, max_length=MAX_SEQUENCE_LENGTH, return_tensors="np")
        inputs = {name: encoded[name].astype(np.int64) for name in ("input_ids", "attention_mask")}
        actual = session.run(["logits"], inputs)[0][0]
        max_difference = max(max_difference, float(np.abs(actual - np.array(expected)).max()))
    return max_difference


def write_tokenizer_golden(tokenizer: Any, texts: list[str], output_dir: Path) -> Path:
    golden_path = output_dir / TOKENIZER_GOLDEN_FILE_NAME
    cases = [
        {"text": text, "ids": tokenizer(text, truncation=True, max_length=MAX_SEQUENCE_LENGTH)["input_ids"]}
        for text in texts
    ]
    golden_path.write_text(json.dumps(cases, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return golden_path


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Fine-tune PhoBERT và export ONNX cho ScamDetector.Api.")
    parser.add_argument("splits_dir", type=Path, help="Thư mục có train.csv, val.csv, test.csv")
    parser.add_argument("output_dir", type=Path, help="Nơi ghi scam-detector.onnx, vocab.txt, bpe.codes")
    parser.add_argument("--base-model", default=BASE_MODEL_NAME)
    parser.add_argument("--epochs", type=float, default=4)
    parser.add_argument("--batch-size", type=int, default=16)
    parser.add_argument("--learning-rate", type=float, default=2e-5)
    parser.add_argument("--weight-decay", type=float, default=0.01)
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    set_seed(SEED)
    options.output_dir.mkdir(parents=True, exist_ok=True)
    splits = {name: load_split(options.splits_dir / f"{name}.csv") for name in SPLIT_NAMES}

    model, tokenizer = train(splits, options.output_dir, options)
    test_logits = predict(model, tokenizer, splits["test"].texts)
    predicted = [max(range(len(LABELS)), key=logits.__getitem__) for logits in test_logits]
    metrics = compute_metrics(predicted, splits["test"].label_ids)

    onnx_path = export_onnx(model, tokenizer, options.output_dir)
    parity_texts = splits["test"].texts[:PARITY_SAMPLE_COUNT]
    difference = verify_onnx_parity(onnx_path, tokenizer, parity_texts, test_logits[:PARITY_SAMPLE_COUNT])
    golden_path = write_tokenizer_golden(tokenizer, parity_texts, options.output_dir)

    hyperparameters = f"lr={options.learning_rate}, bs={options.batch_size}, epochs={options.epochs}"
    print(f"Test: macro F1 {metrics.macro_f1:.4f}, F1 scam {metrics.scam_f1:.4f}, accuracy {metrics.accuracy:.4f}")
    print(f"Chênh lệch logits PyTorch/ONNX tối đa: {difference:.6f}")
    print(f"Golden tokenizer: {golden_path} (kiểm tra bằng scripts/VerifyTokenizer.cs)")
    print("Dòng cho docs/experiments.md:")
    print(format_experiment_row(options.base_model, splits, hyperparameters, metrics))
    return 0 if difference <= ONNX_PARITY_TOLERANCE else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
