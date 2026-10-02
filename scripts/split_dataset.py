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


def read_rows(csv_path: Path) -> tuple[list[str], list[dict[str, str]]]:
    with csv_path.open(encoding="utf-8", newline="") as csv_file:
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


def split_dataset(dataset_path: Path, output_dir: Path) -> dict[str, list[dict[str, str]]]:
    fieldnames, rows = read_rows(dataset_path)
    rows.sort(key=lambda row: row["id"])
    rng = random.Random(SEED)
    test_path = output_dir / "test.csv"

    if test_path.exists():
        _, test_rows = read_rows(test_path)
    else:
        test_rows = split_new_test(rows, rng)

    test_ids = {row["id"] for row in test_rows}
    remaining_rows = [row for row in rows if row["id"] not in test_ids]
    train_rows, val_rows = split_train_val(remaining_rows, rng)
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
    _, rows = read_rows(dataset_path)
    if not rows:
        print("Dataset rỗng, không chia được.")
        return 1

    splits = split_dataset(dataset_path, output_dir)
    for split_name in SPLIT_NAMES:
        print(f"{split_name}: {len(splits[split_name])} mẫu")
    return 0


if __name__ == "__main__":
    sys.exit(main())
