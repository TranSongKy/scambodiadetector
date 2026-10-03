import json
import re
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path
from urllib.parse import urlsplit

from threat_intel.domains import extract_domains, extract_warned_domains, normalize_domain
from threat_intel.fetcher import FetchError
from threat_intel.parsers import extract_links, extract_paragraphs, parse_feed
from threat_intel.sources import DOMAIN_LIST_KIND, HTML_LIST_KIND, MANUAL_KIND, RSS_KIND, Source
from threat_intel.store import MANUAL_SUBMISSIONS_FILE, read_rows
from threat_intel.templates import evidence_excerpt, extract_quotes

DEFAULT_KEYWORDS = ("lừa đảo", "giả danh", "mạo danh", "chiếm đoạt", "giả mạo", "cảnh báo", "lừa")
JSON_DOMAIN_KEYS = ("domain", "url", "website", "link")
WHITESPACE = re.compile(r"\s+")


@dataclass(frozen=True)
class DomainSighting:
    source_url: str
    evidence: str


@dataclass
class SourceResult:
    source: str
    domains: dict[str, DomainSighting] = field(default_factory=dict)
    texts: list[tuple[str, str]] = field(default_factory=list)
    articles: int = 0
    errors: list[str] = field(default_factory=list)


def _matches_keywords(text: str, keywords: tuple[str, ...]) -> bool:
    lowered = text.lower()
    return any(keyword in lowered for keyword in keywords)


def _process_article(fetch: Callable[[str], str], url: str, result: SourceResult) -> None:
    paragraphs = extract_paragraphs(fetch(url))
    own_host = normalize_domain(urlsplit(url).netloc)
    for domain, paragraph in extract_warned_domains(paragraphs).items():
        if domain != own_host:
            result.domains.setdefault(domain, DomainSighting(url, evidence_excerpt(paragraph)))
    result.texts.extend((quote, url) for quote in extract_quotes(paragraphs))
    result.articles += 1


def _process_articles(fetch: Callable[[str], str], urls: list[str], result: SourceResult) -> None:
    for url in urls:
        try:
            _process_article(fetch, url, result)
        except FetchError as error:
            result.errors.append(str(error))


def crawl_rss(source: Source, fetch: Callable[[str], str], result: SourceResult) -> None:
    keywords = tuple(keyword.lower() for keyword in source.keywords) or DEFAULT_KEYWORDS
    items = [
        item for item in parse_feed(fetch(source.url)) if _matches_keywords(f"{item.title} {item.summary}", keywords)
    ]
    _process_articles(fetch, [item.link for item in items[: source.max_items] if item.link], result)


def crawl_html_list(source: Source, fetch: Callable[[str], str], result: SourceResult) -> None:
    links = extract_links(fetch(source.url), source.url, source.link_pattern)
    _process_articles(fetch, links[: source.max_items], result)


def _domains_from_json(value: object) -> list[str]:
    if isinstance(value, str):
        return [value]
    if isinstance(value, list):
        return [domain for item in value for domain in _domains_from_json(item)]
    if isinstance(value, dict):
        return [str(value[key]) for key in JSON_DOMAIN_KEYS if key in value] or _domains_from_json(list(value.values()))
    return []


def crawl_domain_list(source: Source, fetch: Callable[[str], str], result: SourceResult) -> None:
    content = fetch(source.url)
    try:
        candidates = _domains_from_json(json.loads(content))
    except json.JSONDecodeError:
        candidates = [line for line in content.splitlines() if line.strip() and not line.lstrip().startswith("#")]
    for candidate in candidates:
        domain = normalize_domain(candidate)
        if domain:
            result.domains.setdefault(domain, DomainSighting(source.url, source.name))


def crawl_manual(data_dir: Path, result: SourceResult) -> None:
    for row in read_rows(data_dir / MANUAL_SUBMISSIONS_FILE):
        text = row.get("text", "")
        source_url = row.get("source_url", "")
        for domain in extract_domains(text):
            result.domains.setdefault(domain, DomainSighting(source_url, evidence_excerpt(text)))
        result.texts.append((text, source_url))
        result.texts.extend((quote, source_url) for quote in extract_quotes([text]))


def crawl_source(source: Source, fetch: Callable[[str], str], data_dir: Path) -> SourceResult:
    result = SourceResult(source=source.name)
    handlers = {
        RSS_KIND: lambda: crawl_rss(source, fetch, result),
        HTML_LIST_KIND: lambda: crawl_html_list(source, fetch, result),
        DOMAIN_LIST_KIND: lambda: crawl_domain_list(source, fetch, result),
        MANUAL_KIND: lambda: crawl_manual(data_dir, result),
    }
    try:
        handlers[source.kind]()
    except (FetchError, ValueError, SyntaxError) as error:
        message = WHITESPACE.sub(" ", str(error))
        result.errors.append(f"{source.name}: {message}")
    return result
