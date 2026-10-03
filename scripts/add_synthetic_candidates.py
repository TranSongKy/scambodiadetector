import argparse
import sys
from datetime import UTC, datetime
from pathlib import Path

from training_data.candidates import load_candidates, load_reviewed, read_rows, save_candidates
from training_data.collect import CollectionReport, build_collector
from training_data.dataset_file import dataset_texts
from training_data.labels import LabelError, apply_suggestions

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "training-candidates"
DEFAULT_DATASET = REPOSITORY_ROOT / "data" / "processed" / "dataset.csv"
SYNTHETIC = "synthetic"
SYNTHETIC_NOTE = "Tin sinh tổng hợp theo yêu cầu, cần người duyệt"


def add_synthetic(
    data_dir: Path, dataset: Path, rows: list[dict[str, str]], today: str
) -> tuple[int, CollectionReport]:
    existing = load_candidates(data_dir)
    reviewed_keys = {record.key for record in load_reviewed(data_dir)}
    collector = build_collector(existing, dataset_texts(dataset), reviewed_keys, today)
    report = CollectionReport(source=SYNTHETIC)
    suggestions = []
    for row in rows:
        before = len(collector.added)
        collector.offer(row.get("text", ""), SYNTHETIC, SYNTHETIC, "", report, hint_label=row.get("label", ""))
        if len(collector.added) > before:
            suggestions.append(
                {
                    "id": collector.added[-1].id,
                    "label": row.get("label", ""),
                    "scam_type": row.get("scam_type", ""),
                    "channel": row.get("channel", ""),
                    "confidence": "high",
                    "note": SYNTHETIC_NOTE,
                }
            )
    labeled = apply_suggestions(existing + collector.added, suggestions)
    save_candidates(data_dir, labeled)
    return len(suggestions), report


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Thêm tin nhắn sinh tổng hợp (source=synthetic) vào hàng chờ duyệt.")
    parser.add_argument("input", type=Path, help="CSV với cột text,label,scam_type,channel")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET)
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    today = datetime.now(UTC).date().isoformat()
    try:
        added, report = add_synthetic(options.data_dir, options.dataset, read_rows(options.input), today)
    except LabelError as error:
        print(f"Lỗi: {error}")
        return 1
    print(
        f"Đã thêm {added} tin synthetic. Bỏ qua: trùng {report.skipped_duplicate}, "
        f"có thể chứa tên người {report.skipped_name}, không hợp lệ {report.skipped_invalid}."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
