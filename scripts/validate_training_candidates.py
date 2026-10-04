import argparse
import json
import sys
from dataclasses import fields
from pathlib import Path

from masking import find_maskable_pii
from training_data.candidates import (
    CANDIDATE_ID_PREFIX,
    CANDIDATES_FILE,
    ORIGINS,
    SOURCES_FILE,
    Candidate,
    read_rows,
)
from training_data.labels import LabelError, validate_suggestion
from training_data.sources import load_training_sources
from validate_dataset import SOURCES

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "training-candidates"


def validate_candidates(data_dir: Path) -> list[str]:
    errors: list[str] = []
    path = data_dir / CANDIDATES_FILE
    header = path.read_text(encoding="utf-8").splitlines()[0].split(",") if path.exists() else []
    expected = [field.name for field in fields(Candidate)]
    if header != expected:
        return [f"{CANDIDATES_FILE}: header phải là {','.join(expected)}"]
    seen: set[str] = set()
    for line, row in enumerate(read_rows(path), start=2):
        prefix = f"{CANDIDATES_FILE} dòng {line}"
        candidate_id = row["id"]
        if not candidate_id.startswith(CANDIDATE_ID_PREFIX) or candidate_id in seen:
            errors.append(f"{prefix}: id '{candidate_id}' sai dạng hoặc bị trùng")
        seen.add(candidate_id)
        if row["origin"] not in ORIGINS:
            errors.append(f"{prefix}: origin '{row['origin']}' không hợp lệ")
        if row["source"] not in SOURCES:
            errors.append(f"{prefix}: source '{row['source']}' không hợp lệ")
        pii = find_maskable_pii(row["text"])
        if pii:
            errors.append(f"{prefix}: text còn thông tin cá nhân chưa che ({', '.join(pii)})")
        if row["suggested_label"]:
            try:
                validate_suggestion(
                    row["suggested_label"],
                    row["suggested_scam_type"],
                    row["suggested_channel"],
                    row["confidence"],
                    row["note"],
                )
            except LabelError as error:
                errors.append(f"{prefix}: {error}")
    return errors


def validate_sources(data_dir: Path) -> list[str]:
    try:
        sources = load_training_sources(data_dir / SOURCES_FILE)
    except (ValueError, TypeError, json.JSONDecodeError) as error:
        return [f"{SOURCES_FILE}: {error}"]
    return [
        f"{SOURCES_FILE}: '{source.name}' phải dùng https"
        for source in sources
        if source.kind != "manual" and not source.url.startswith("https://")
    ]


def main(arguments: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Kiểm tra data/training-candidates/.")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    options = parser.parse_args(arguments)
    errors = validate_sources(options.data_dir) + validate_candidates(options.data_dir)
    for error in errors:
        print(f"Lỗi: {error}")
    if errors:
        return 1
    print(f"Ứng viên hợp lệ: {len(read_rows(options.data_dir / CANDIDATES_FILE))} mẫu.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
