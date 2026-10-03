import json
from dataclasses import dataclass, field
from pathlib import Path

DEFAULT_MAX_ITEMS = 15
RSS_KIND = "rss"
HTML_LIST_KIND = "html_list"
DOMAIN_LIST_KIND = "domain_list"
MANUAL_KIND = "manual"
SOURCE_KINDS = {RSS_KIND, HTML_LIST_KIND, DOMAIN_LIST_KIND, MANUAL_KIND}


@dataclass(frozen=True)
class Source:
    name: str
    kind: str
    url: str = ""
    category: str = ""
    enabled: bool = True
    max_items: int = DEFAULT_MAX_ITEMS
    link_pattern: str = ""
    keywords: list[str] = field(default_factory=list)


def load_sources(path: Path) -> list[Source]:
    sources = [Source(**entry) for entry in json.loads(path.read_text(encoding="utf-8"))]
    unknown = sorted({source.kind for source in sources} - SOURCE_KINDS)
    if unknown:
        raise ValueError(f"Loại nguồn không hợp lệ: {', '.join(unknown)}")
    return [source for source in sources if source.enabled]
