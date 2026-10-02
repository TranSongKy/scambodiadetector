import csv
import random
import sys
from collections import defaultdict
from pathlib import Path

SEED = 42
TRAIN_RATIO = 0.70
VAL_RATIO = 0.15
TEST_RATIO = 0.15
SYNTHETIC_SOURCE = "synthetic"
SPLIT_NAMES = ("train", "val", "test")
BOM = "\ufeff"


class SplitError(Exception):
    pass


def read_rows(csv_path: Path) -> tuple[list[str], list[dict[str, str]]]:
    with csv_path.open(encoding="utf-8", newline="") as csv_file:
        if csv_file.read(1) == BOM:
            raise SplitError(f"{csv_path} có BOM, chạy scripts/validate_dataset.py trước")
        csv_file.seek(0)
        reader = csv.DictReader(csv_file)
        return list(reader.fieldnames or []), list(reader)


def write_rows(csv_path: Path, fieldnames: list[str], rows: list[dict[str, str]]) -> None:
    csv_path.parent.mkdir(parents=True, exist_ok=True)
    with csv_path.open("w", encoding="utf-8", newline="") as csv_file:
        writer = csv.DictWriter(csv_file, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def group_by_label(rows: list[dict[str, str]]) -> dict[str, list[dict[str, str]]]:
    groups: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        groups[row["label"]].append(row)
    return groups


def split_new_test(rows: list[dict[str, str]], rng: random.Random) -> list[dict[str, str]]:
    test_rows: list[dict[str, str]] = []
    total_by_label = {label: len(group) for label, group in group_by_label(rows).items()}
    candidates = [row for row in rows if row["source"] != SYNTHETIC_SOURCE]

    for label, group in sorted(group_by_label(candidates).items()):
        rng.shuffle(group)
        test_count = min(len(group), round(total_by_label[label] * TEST_RATIO))
        test_rows.extend(group[:test_count])

    return test_rows


def split_train_val(
    rows: list[dict[str, str]], rng: random.Random
) -> tuple[list[dict[str, str]], list[dict[str, str]]]:
    train_rows: list[dict[str, str]] = []
    val_rows: list[dict[str, str]] = []
    val_share = VAL_RATIO / (TRAIN_RATIO + VAL_RATIO)

    for _, group in sorted(group_by_label(rows).items()):
        rng.shuffle(group)
        val_count = round(len(group) * val_share)
        val_rows.extend(group[:val_count])
        train_rows.extend(group[val_count:])

    return train_rows, val_rows


def verify_existing_test(test_rows: list[dict[str, str]], rows: list[dict[str, str]]) -> None:
    rows_by_id = {row["id"]: row for row in rows}
    for test_row in test_rows:
        dataset_row = rows_by_id.get(test_row["id"])
        if dataset_row is None:
            raise SplitError(f"Mẫu test '{test_row['id']}' không còn trong dataset")
        if dataset_row != test_row:
            raise SplitError(f"Mẫu test '{test_row['id']}' khác với dataset, không tự sửa tập test")
        if test_row["source"] == SYNTHETIC_SOURCE:
            raise SplitError(f"Mẫu test '{test_row['id']}' là synthetic")


def find_empty_test_labels(rows: list[dict[str, str]], test_rows: list[dict[str, str]]) -> list[str]:
    test_labels = {row["label"] for row in test_rows}
    return sorted({row["label"] for row in rows} - test_labels)


def split_dataset(
    fieldnames: list[str], rows: list[dict[str, str]], output_dir: Path
) -> dict[str, list[dict[str, str]]]:
    rows = sorted(rows, key=lambda row: row["id"])
    test_path = output_dir / "test.csv"

    if test_path.exists():
        _, test_rows = read_rows(test_path)
        verify_existing_test(test_rows, rows)
    else:
        test_rows = split_new_test(rows, random.Random(f"{SEED}-test"))

    test_ids = {row["id"] for row in test_rows}
    remaining_rows = [row for row in rows if row["id"] not in test_ids]
    train_rows, val_rows = split_train_val(remaining_rows, random.Random(f"{SEED}-train-val"))
    splits = {"train": train_rows, "val": val_rows, "test": test_rows}

    for split_name in SPLIT_NAMES:
        if split_name == "test" and test_path.exists():
            continue
        write_rows(output_dir / f"{split_name}.csv", fieldnames, splits[split_name])

    return splits


def main() -> int:
    if len(sys.argv) != 3:
        print("Cách dùng: python scripts/split_dataset.py <dataset.csv> <thư mục splits>")
        return 2

    dataset_path = Path(sys.argv[1])
    output_dir = Path(sys.argv[2])
    try:
        fieldnames, rows = read_rows(dataset_path)
        if not rows:
            raise SplitError("Dataset rỗng, không chia được.")
        splits = split_dataset(fieldnames, rows, output_dir)
    except SplitError as error:
        print(f"Lỗi: {error}")
        return 1

    for split_name in SPLIT_NAMES:
        print(f"{split_name}: {len(splits[split_name])} mẫu")

    empty_test_labels = find_empty_test_labels(rows, splits["test"])
    if empty_test_labels:
        print(f"Cảnh báo: tập test không có mẫu nhãn {', '.join(empty_test_labels)}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
