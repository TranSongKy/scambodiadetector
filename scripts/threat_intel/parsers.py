import re
import xml.etree.ElementTree as ElementTree
from dataclasses import dataclass
from html.parser import HTMLParser
from urllib.parse import urljoin

CONTENT_TAGS = {"p", "blockquote", "li", "h1", "h2", "h3", "figcaption", "td"}
SKIPPED_TAGS = {"script", "style", "nav", "footer", "aside", "header", "form", "noscript", "svg"}
WHITESPACE = re.compile(r"\s+")
MIN_PARAGRAPH_CHARACTERS = 20


@dataclass(frozen=True)
class FeedItem:
    title: str
    link: str
    published: str
    summary: str


class _ArticleParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.paragraphs: list[str] = []
        self._skip_depth = 0
        self._content_depth = 0
        self._buffer: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in SKIPPED_TAGS:
            self._skip_depth += 1
        elif tag in CONTENT_TAGS:
            self._content_depth += 1
        elif tag == "br" and self._content_depth:
            self._buffer.append(" ")

    def handle_endtag(self, tag: str) -> None:
        if tag in SKIPPED_TAGS and self._skip_depth:
            self._skip_depth -= 1
        elif tag in CONTENT_TAGS and self._content_depth:
            self._content_depth -= 1
            if not self._content_depth:
                self._flush()

    def handle_data(self, data: str) -> None:
        if self._content_depth and not self._skip_depth:
            self._buffer.append(data)

    def _flush(self) -> None:
        paragraph = WHITESPACE.sub(" ", "".join(self._buffer)).strip()
        self._buffer.clear()
        if len(paragraph) >= MIN_PARAGRAPH_CHARACTERS:
            self.paragraphs.append(paragraph)


class _LinkParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.links: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        href = dict(attrs).get("href")
        if tag == "a" and href:
            self.links.append(href)


def extract_paragraphs(html: str) -> list[str]:
    parser = _ArticleParser()
    parser.feed(html)
    parser.close()
    return parser.paragraphs


def extract_links(html: str, base_url: str, pattern: str) -> list[str]:
    parser = _LinkParser()
    parser.feed(html)
    compiled = re.compile(pattern)
    links = (urljoin(base_url, link) for link in parser.links)
    return list(dict.fromkeys(link for link in links if compiled.search(link)))


def _child_text(element: ElementTree.Element, *names: str) -> str:
    for name in names:
        child = element.find(name)
        if child is not None:
            return (child.text or child.get("href") or "").strip()
    return ""


def parse_feed(xml_text: str) -> list[FeedItem]:
    root = ElementTree.fromstring(xml_text)
    atom = "{http://www.w3.org/2005/Atom}"
    entries = root.findall(".//item") or root.findall(f".//{atom}entry")
    return [
        FeedItem(
            title=_child_text(entry, "title", f"{atom}title"),
            link=_child_text(entry, "link", f"{atom}link"),
            published=_child_text(entry, "pubDate", f"{atom}published", f"{atom}updated"),
            summary=re.sub(r"<[^>]+>", " ", _child_text(entry, "description", f"{atom}summary")),
        )
        for entry in entries
    ]
