import argparse
import json
import re
import sys
from datetime import date
from pathlib import Path

from masking import find_maskable_pii, normalize_text
from threat_intel.domains import SHARED_PLATFORM_ROOTS, is_allowed, normalize_domain, refang
from threat_intel.similarity import jaccard, matches_fingerprint
from threat_intel.sources import SOURCE_KINDS
from threat_intel.store import (
    ALLOWED_DOMAINS_FILE,
    BLOCKED_DOMAINS_FILE,
    REJECTED_DOMAINS_FILE,
    REJECTED_TEMPLATES_FILE,
    SCAM_TEMPLATES_FILE,
    BlockedDomain,
    RejectedDomain,
    RejectedTemplate,
    ScamTemplate,
    load_allowed_domains,
    read_rows,
)
from threat_intel.templates import DUPLICATE_JACCARD, is_template_candidate

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "threat-intel"
SOURCES_FILE = "sources.json"
TEMPLATE_ID_PATTERN = re.compile(r"^tpl_\d{6}$")
PUBLIC_SUFFIXES = {"vn", "com", "net", "org", "com.vn", "net.vn", "org.vn", "edu.vn", "gov.vn", "info.vn", "biz.vn"}
HTTPS_PREFIX = "https://"


def _header_errors(path: Path, record_type: type) -> list[str]:
    if not path.exists():
        return [f"{path.name}: không tồn tại"]
    expected = list(record_type.__dataclass_fields__)
    header = path.read_text(encoding="utf-8").splitlines()[0].split(",")
    return [] if header == expected else [f"{path.name}: header phải là {','.join(expected)}"]


def _date_error(prefix: str, value: str) -> list[str]:
    try:
        date.fromisoformat(value)
    except ValueError:
        return [f"{prefix}: first_seen '{value}' không đúng YYYY-MM-DD"]
    return []


def validate_domain_row(row: dict[str, str], line: int, allowlist: set[str]) -> list[str]:
    prefix = f"{BLOCKED_DOMAINS_FILE} dòng {line}"
    domain = row.get("domain", "")
    errors = _date_error(prefix, row.get("first_seen", ""))
    if normalize_domain(domain) != domain:
        errors.append(f"{prefix}: '{domain}' chưa chuẩn hóa (chữ thường, không www, không đường dẫn, punycode)")
    if domain in PUBLIC_SUFFIXES or domain.count(".") == 0:
        errors.append(f"{prefix}: '{domain}' là hậu tố công cộng, không được chặn")
    if domain in SHARED_PLATFORM_ROOTS:
        errors.append(f"{prefix}: '{domain}' là nền tảng dùng chung, chỉ được chặn subdomain cụ thể")
    if is_allowed(domain, allowlist):
        errors.append(f"{prefix}: '{domain}' nằm trong danh sách trắng")
    if not row.get("source"):
        errors.append(f"{prefix}: thiếu source")
    if find_maskable_pii(refang(row.get("evidence", ""))):
        errors.append(f"{prefix}: evidence còn thông tin cá nhân")
    return errors


def validate_template_row(row: dict[str, str], line: int) -> list[str]:
    prefix = f"{SCAM_TEMPLATES_FILE} dòng {line}"
    text = row.get("text", "")
    errors = _date_error(prefix, row.get("first_seen", ""))
    if not TEMPLATE_ID_PATTERN.match(row.get("id", "")):
        errors.append(f"{prefix}: id '{row.get('id')}' sai định dạng tpl_000001")
    if normalize_text(text) != text:
        errors.append(f"{prefix}: text chưa chuẩn hóa (NFC, khoảng trắng)")
    pii = find_maskable_pii(refang(text))
    if pii:
        errors.append(f"{prefix}: text còn thông tin cá nhân ({', '.join(pii)})")
    if not is_template_candidate(text):
        errors.append(f"{prefix}: text không đạt điều kiện văn mẫu (độ dài, dấu hiệu lừa đảo, tiếng Việt)")
    if not row.get("source"):
        errors.append(f"{prefix}: thiếu source")
    return errors


def validate_uniqueness(domains: list[dict[str, str]], templates: list[dict[str, str]]) -> list[str]:
    errors: list[str] = []
    seen_domains: set[str] = set()
    for row in domains:
        if row["domain"] in seen_domains:
            errors.append(f"{BLOCKED_DOMAINS_FILE}: '{row['domain']}' bị trùng")
        seen_domains.add(row["domain"])
    ids = [row["id"] for row in templates]
    errors += [f"{SCAM_TEMPLATES_FILE}: id '{dup}' bị trùng" for dup in sorted({i for i in ids if ids.count(i) > 1})]
    for index, row in enumerate(templates):
        for other in templates[index + 1 :]:
            if jaccard(row["text"], other["text"]) >= DUPLICATE_JACCARD:
                errors.append(f"{SCAM_TEMPLATES_FILE}: '{row['id']}' gần trùng '{other['id']}'")
    return errors


def validate_sources(path: Path) -> list[str]:
    try:
        sources = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        return [f"{SOURCES_FILE}: không đọc được ({error})"]
    errors: list[str] = []
    names = [source.get("name", "") for source in sources]
    duplicates = sorted({name for name in names if names.count(name) > 1})
    errors += [f"{SOURCES_FILE}: tên nguồn '{name}' bị trùng" for name in duplicates]
    for source in sources:
        if source.get("kind") not in SOURCE_KINDS:
            errors.append(f"{SOURCES_FILE}: '{source.get('name')}' có kind không hợp lệ")
        if source.get("kind") != "manual" and not str(source.get("url", "")).startswith(HTTPS_PREFIX):
            errors.append(f"{SOURCES_FILE}: '{source.get('name')}' phải dùng https")
    return errors


def validate_allowlist(data_dir: Path) -> list[str]:
    errors: list[str] = []
    for row in read_rows(data_dir / ALLOWED_DOMAINS_FILE):
        if normalize_domain(row["domain"]) != row["domain"]:
            errors.append(f"{ALLOWED_DOMAINS_FILE}: '{row['domain']}' chưa chuẩn hóa")
    return errors


def validate_not_rejected(data_dir: Path, domains: list[dict[str, str]], templates: list[dict[str, str]]) -> list[str]:
    rejected_domains = {row["domain"] for row in read_rows(data_dir / REJECTED_DOMAINS_FILE)}
    rejected_fingerprints = [row["fingerprint"] for row in read_rows(data_dir / REJECTED_TEMPLATES_FILE)]
    errors = [
        f"{BLOCKED_DOMAINS_FILE}: '{row['domain']}' đã bị loại trước đây"
        for row in domains
        if row["domain"] in rejected_domains
    ]
    errors += [
        f"{SCAM_TEMPLATES_FILE}: '{row['id']}' giống văn mẫu đã bị loại"
        for row in templates
        if matches_fingerprint(row["text"], rejected_fingerprints, DUPLICATE_JACCARD)
    ]
    return errors


def validate_threat_intel(data_dir: Path) -> list[str]:
    errors = _header_errors(data_dir / BLOCKED_DOMAINS_FILE, BlockedDomain)
    errors += _header_errors(data_dir / SCAM_TEMPLATES_FILE, ScamTemplate)
    errors += _header_errors(data_dir / REJECTED_DOMAINS_FILE, RejectedDomain)
    errors += _header_errors(data_dir / REJECTED_TEMPLATES_FILE, RejectedTemplate)
    if errors:
        return errors
    allowlist = load_allowed_domains(data_dir)
    domains = read_rows(data_dir / BLOCKED_DOMAINS_FILE)
    templates = read_rows(data_dir / SCAM_TEMPLATES_FILE)
    for line, row in enumerate(domains, start=2):
        errors += validate_domain_row(row, line, allowlist)
    for line, row in enumerate(templates, start=2):
        errors += validate_template_row(row, line)
    errors += validate_uniqueness(domains, templates)
    errors += validate_not_rejected(data_dir, domains, templates)
    errors += validate_sources(data_dir / SOURCES_FILE)
    errors += validate_allowlist(data_dir)
    return errors


def main(arguments: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Kiểm tra luật cứng cho dữ liệu data/threat-intel/.")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    options = parser.parse_args(arguments)
    errors = validate_threat_intel(options.data_dir)
    if errors:
        print(f"Phát hiện {len(errors)} lỗi:")
        for error in errors:
            print(f"  - {error}")
        return 1
    domains = len(read_rows(options.data_dir / BLOCKED_DOMAINS_FILE))
    templates = len(read_rows(options.data_dir / SCAM_TEMPLATES_FILE))
    print(f"Dữ liệu threat-intel hợp lệ: {domains} tên miền, {templates} văn mẫu.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
