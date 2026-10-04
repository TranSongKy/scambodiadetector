import argparse
import sys
from pathlib import Path

from training_data.candidates import load_candidates, read_rows, save_candidates
from training_data.labels import LabelError, apply_suggestions

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "training-candidates"
SUGGESTION_COLUMNS = "id,label,scam_type,channel,confidence,note"


def list_unlabeled(data_dir: Path, limit: int) -> str:
    unlabeled = [candidate for candidate in load_candidates(data_dir) if not candidate.is_labeled][:limit]
    lines = [f"Chưa gán: {len(unlabeled)} (hiển thị tối đa {limit})", ""]
    for candidate in unlabeled:
        hints = ", ".join(
            part
            for part in (
                f"gợi ý nguồn: {candidate.hint_label}" if candidate.hint_label else "",
                f"kênh: {candidate.hint_channel}" if candidate.hint_channel else "",
            )
            if part
        )
        lines.append(f"### {candidate.id} · {candidate.origin}" + (f" · {hints}" if hints else ""))
        lines.append(candidate.text)
        lines.append("")
    return "\n".join(lines)


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Công cụ cho agent gán nhãn gợi ý: liệt kê và ghi gợi ý nhãn.")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--list", action="store_true", help="In các ứng viên chưa gán nhãn")
    group.add_argument("--apply", type=Path, help=f"CSV gợi ý với cột {SUGGESTION_COLUMNS}")
    parser.add_argument("--limit", type=int, default=40)
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    if options.list:
        print(list_unlabeled(options.data_dir, options.limit))
        return 0
    try:
        updated = apply_suggestions(load_candidates(options.data_dir), read_rows(options.apply))
    except LabelError as error:
        print(f"Lỗi: {error}")
        return 1
    save_candidates(options.data_dir, updated)
    print(
        f"Đã ghi gợi ý cho {len(read_rows(options.apply))} ứng viên. Còn chưa gán: "
        f"{sum(not candidate.is_labeled for candidate in updated)}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
