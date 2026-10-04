import re
from html.parser import HTMLParser

WHITESPACE = re.compile(r"[ \t\r\f\v]+")
BLANK_LINES = re.compile(r"\n\s*\n+")
SKIPPED_TAGS = {"script", "style", "noscript", "svg"}
VOID_TAGS = {"br", "img", "hr", "input", "meta", "link", "source", "wbr"}
BLOCK_TAGS = {"p", "div", "li", "blockquote", "tr"}


class _ClassTextParser(HTMLParser):
    def __init__(self, class_name: str) -> None:
        super().__init__(convert_charrefs=True)
        self._class_name = class_name
        self._depth = 0
        self._skip_depth = 0
        self._buffer: list[str] = []
        self.texts: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if self._depth:
            if tag in SKIPPED_TAGS:
                self._skip_depth += 1
            if tag == "br" or tag in BLOCK_TAGS:
                self._buffer.append("\n")
            if tag not in VOID_TAGS:
                self._depth += 1
            return
        classes = (dict(attrs).get("class") or "").split()
        if self._class_name in classes and tag not in VOID_TAGS:
            self._depth = 1

    def handle_endtag(self, tag: str) -> None:
        if not self._depth or tag in VOID_TAGS:
            return
        if tag in SKIPPED_TAGS and self._skip_depth:
            self._skip_depth -= 1
        if tag in BLOCK_TAGS:
            self._buffer.append("\n")
        self._depth -= 1
        if not self._depth:
            self._flush()

    def handle_data(self, data: str) -> None:
        if self._depth and not self._skip_depth:
            self._buffer.append(data)

    def _flush(self) -> None:
        text = BLANK_LINES.sub("\n", WHITESPACE.sub(" ", "".join(self._buffer))).strip()
        self._buffer.clear()
        if text:
            self.texts.append("\n".join(line.strip() for line in text.splitlines()))


def extract_texts_by_class(html: str, class_name: str) -> list[str]:
    parser = _ClassTextParser(class_name)
    parser.feed(html)
    parser.close()
    return parser.texts
