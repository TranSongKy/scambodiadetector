import csv
import io
import re
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path
from urllib.parse import urlsplit

from anonymize import may_contain_name
from masking import mask, normalize_text
from threat_intel.crawler import DEFAULT_KEYWORDS
from threat_intel.fetcher import FetchError
from threat_intel.parsers import extract_links, extract_paragraphs, parse_feed
from threat_intel.templates import extract_quotes
from training_data.candidates import (
    MANUAL_MESSAGES_FILE,
    Candidate,
    format_candidate_id,
    next_candidate_number,
    read_rows,
    text_key,
)
from training_data.forum import extract_texts_by_class
from training_data.policy import decide_training_use
from training_data.sources import (
    CSV_DATASET_KIND,
    HTML_LIST_KIND,
    MANUAL_KIND,
    RSS_KIND,
    TrainingSource,
)

MIN_TEXT_LENGTH = 5
MAX_TEXT_LENGTH = 2000
PUBLIC_POST_SOURCE = "public_post"
COLLECTED_SOURCE = "collected"
CONTRIBUTED_SOURCE = "contributed"
VIETNAMESE_LETTER_PATTERN = re.compile(r"[ăâđêôơưàảãạáằẳẵặắầẩẫậấèẻẽẹéềểễệếìỉĩịíòỏõọóồổỗộốờởỡợớùủũụúừửữựứỳỷỹỵý]", re.I)
LABELS = {"scam", "spam", "normal"}


@dataclass
class CollectionReport:
    source: str
    added: int = 0
    skipped_duplicate: int = 0
    skipped_name: int = 0
    skipped_invalid: int = 0
    articles: int = 0
    errors: list[str] = field(default_factory=list)


@dataclass
class CandidateCollector:
    known_keys: set[str]
    collected_at: str
    next_number: int
    added: list[Candidate] = field(default_factory=list)

    def offer(self, raw_text: str, origin: str, source: str, source_url: str, report: CollectionReport, **hints: str):
        text = prepare_text(raw_text)
        if text is None:
            report.skipped_invalid += 1
            return
        if may_contain_name(text):
            report.skipped_name += 1
            return
        key = text_key(text)
        if key in self.known_keys:
            report.skipped_duplicate += 1
            return
        self.known_keys.add(key)
        self.added.append(
            Candidate(
                id=format_candidate_id(self.next_number),
                text=text,
                origin=origin,
                source=source,
                source_url=source_url,
                collected_at=self.collected_at,
                hint_label=hints.get("hint_label", ""),
                hint_channel=hints.get("hint_channel", ""),
            )
        )
        self.next_number += 1
        report.added += 1


def prepare_text(raw_text: str) -> str | None:
    text = mask(normalize_text(raw_text))
    if not MIN_TEXT_LENGTH <= len(text) <= MAX_TEXT_LENGTH:
        return None
    if VIETNAMESE_LETTER_PATTERN.search(text) is None:
        return None
    return text


def build_collector(existing: list[Candidate], dataset_texts: list[str], reviewed_keys: set[str], today: str):
    known = {text_key(candidate.text) for candidate in existing}
    known |= {text_key(text) for text in dataset_texts}
    known |= reviewed_keys
    return CandidateCollector(known, today, next_candidate_number(existing))


def _training_allowed(source: TrainingSource, fetch: Callable[[str], str], report: CollectionReport) -> bool:
    parts = urlsplit(source.url)
    try:
        robots_text = fetch(f"{parts.scheme}://{parts.netloc}/robots.txt")
    except FetchError:
        robots_text = ""
    decision = decide_training_use(robots_text)
    if not decision.allowed:
        report.errors.append(f"{source.name}: bỏ qua vì {decision.reason}")
    return decision.allowed


def _article_texts(source: TrainingSource, html: str) -> list[str]:
    if source.content_class:
        return extract_texts_by_class(html, source.content_class)
    return extract_quotes(extract_paragraphs(html))


def _collect_articles(source, urls, fetch, collector: CandidateCollector, report: CollectionReport) -> None:
    for url in urls[: source.max_items]:
        try:
            texts = _article_texts(source, fetch(url))
        except FetchError as error:
            report.errors.append(str(error))
            continue
        report.articles += 1
        for text in texts:
            collector.offer(text, source.origin, PUBLIC_POST_SOURCE, url, report)


def _matches_keywords(text: str, keywords: tuple[str, ...]) -> bool:
    lowered = text.lower()
    return any(keyword in lowered for keyword in keywords)


def _collect_rss(source, fetch, collector, report) -> None:
    keywords = tuple(keyword.lower() for keyword in source.keywords) or DEFAULT_KEYWORDS
    items = parse_feed(fetch(source.url))
    links = [item.link for item in items if item.link and _matches_keywords(f"{item.title} {item.summary}", keywords)]
    _collect_articles(source, links, fetch, collector, report)


def _collect_html_list(source, fetch, collector, report) -> None:
    links = extract_links(fetch(source.url), source.url, source.link_pattern)
    _collect_articles(source, links, fetch, collector, report)


def _collect_csv_dataset(source, fetch, collector, report) -> None:
    for row in csv.DictReader(io.StringIO(fetch(source.url))):
        label = source.label_map.get(row.get(source.label_column, ""), "") if source.label_column else ""
        collector.offer(
            row.get(source.text_column, ""),
            "public_dataset",
            COLLECTED_SOURCE,
            source.url,
            report,
            hint_label=label if label in LABELS else "",
            hint_channel=source.channel,
        )


def _collect_manual(data_dir: Path, collector, report) -> None:
    for row in read_rows(data_dir / MANUAL_MESSAGES_FILE):
        collector.offer(
            row.get("text", ""),
            "manual",
            CONTRIBUTED_SOURCE,
            row.get("source_url", ""),
            report,
            hint_label=row.get("label", "") if row.get("label", "") in LABELS else "",
            hint_channel=row.get("channel", ""),
        )


def collect_source(source: TrainingSource, fetch: Callable[[str], str], collector, data_dir: Path) -> CollectionReport:
    report = CollectionReport(source=source.name)
    handlers = {
        RSS_KIND: lambda: _collect_rss(source, fetch, collector, report),
        HTML_LIST_KIND: lambda: _collect_html_list(source, fetch, collector, report),
        CSV_DATASET_KIND: lambda: _collect_csv_dataset(source, fetch, collector, report),
        MANUAL_KIND: lambda: _collect_manual(data_dir, collector, report),
    }
    try:
        if source.kind in (RSS_KIND, HTML_LIST_KIND) and not _training_allowed(source, fetch, report):
            return report
        handlers[source.kind]()
    except (FetchError, ValueError, SyntaxError) as error:
        report.errors.append(f"{source.name}: {error}")
    return report
