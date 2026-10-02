import argparse
import csv
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

REPORTS_PATH = "/api/v1/reports"
API_KEY_HEADER = "X-Api-Key"
API_KEY_ENVIRONMENT_VARIABLE = "REPORTS_API_KEY"
REQUEST_TIMEOUT_SECONDS = 30
CONTRIBUTED_SOURCE = "contributed"
DATASET_CHANNELS = {"telegram": "telegram"}
FALLBACK_CHANNEL = "other"
OUTPUT_COLUMNS = ["report_id", "text", "label", "scam_type", "channel", "source", "collected_at", "annotator"]


class ExportError(Exception):
    pass


def build_url(api_base_url: str, since: str | None, limit: int) -> str:
    query = {"limit": str(limit)} | ({"since": since} if since else {})
    return f"{api_base_url.rstrip('/')}{REPORTS_PATH}?{urllib.parse.urlencode(query)}"


def fetch_reports(url: str, api_key: str) -> list[dict[str, str]]:
    request = urllib.request.Request(url, headers={API_KEY_HEADER: api_key, "Accept": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        raise ExportError(f"API trả lỗi HTTP {error.code}") from error
    except urllib.error.URLError as error:
        raise ExportError(f"Không kết nối được API: {error.reason}") from error


def to_review_row(report: dict[str, str]) -> dict[str, str]:
    return {
        "report_id": report["id"],
        "text": report["text"],
        "label": report["label"],
        "scam_type": "",
        "channel": DATASET_CHANNELS.get(report["channel"], FALLBACK_CHANNEL),
        "source": CONTRIBUTED_SOURCE,
        "collected_at": report["createdAt"][:10],
        "annotator": "",
    }


def write_review_file(output_path: Path, rows: list[dict[str, str]]) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8", newline="") as output_file:
        writer = csv.DictWriter(output_file, fieldnames=OUTPUT_COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Xuất báo cáo người dùng (đã ẩn danh) ra CSV để duyệt tay.")
    parser.add_argument("output", type=Path, help="File CSV để người gán nhãn duyệt")
    parser.add_argument("--api", default="http://localhost:8080", help="Địa chỉ ScamDetector.Api")
    parser.add_argument("--since", help="Chỉ lấy báo cáo từ thời điểm này (ISO 8601)")
    parser.add_argument("--limit", type=int, default=500, help="Số báo cáo tối đa (API giới hạn 500)")
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    api_key = os.environ.get(API_KEY_ENVIRONMENT_VARIABLE, "")
    if not api_key:
        print(f"Lỗi: đặt biến môi trường {API_KEY_ENVIRONMENT_VARIABLE} (giá trị ReportAdmin:ApiKey của API)")
        return 1

    try:
        reports = fetch_reports(build_url(options.api, options.since, options.limit), api_key)
    except ExportError as error:
        print(f"Lỗi: {error}")
        return 1

    write_review_file(options.output, [to_review_row(report) for report in reports])
    print(f"Đã xuất {len(reports)} báo cáo vào {options.output}")
    print("Bước tiếp: duyệt từng dòng, sửa label/scam_type, điền annotator, rồi chạy scripts/anonymize.py")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
