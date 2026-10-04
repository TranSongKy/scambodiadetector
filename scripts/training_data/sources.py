import json
from dataclasses import dataclass, field
from pathlib import Path

RSS_KIND = "rss"
HTML_LIST_KIND = "html_list"
CSV_DATASET_KIND = "csv_dataset"
MANUAL_KIND = "manual"
SOURCE_KINDS = {RSS_KIND, HTML_LIST_KIND, CSV_DATASET_KIND, MANUAL_KIND}
NEWS_ORIGIN = "news"
FORUM_ORIGIN = "forum"
WEB_ORIGINS = {NEWS_ORIGIN, FORUM_ORIGIN}
DEFAULT_MAX_ITEMS = 10


@dataclass(frozen=True)
class TrainingSource:
    name: str
    kind: str
    url: str = ""
    origin: str = NEWS_ORIGIN
    enabled: bool = True
    max_items: int = DEFAULT_MAX_ITEMS
    link_pattern: str = ""
    keywords: list[str] = field(default_factory=list)
    content_class: str = ""
    license: str = ""
    text_column: str = "text"
    label_column: str = ""
    label_map: dict[str, str] = field(default_factory=dict)
    channel: str = ""


def load_training_sources(path: Path) -> list[TrainingSource]:
    sources = [TrainingSource(**entry) for entry in json.loads(path.read_text(encoding="utf-8"))]
    unknown = sorted({source.kind for source in sources} - SOURCE_KINDS)
    if unknown:
        raise ValueError(f"Loại nguồn không hợp lệ: {', '.join(unknown)}")
    unlicensed = [source.name for source in sources if source.kind == CSV_DATASET_KIND and not source.license]
    if unlicensed:
        raise ValueError(f"Bộ dữ liệu công khai phải ghi rõ giấy phép: {', '.join(unlicensed)}")
    return [source for source in sources if source.enabled]
