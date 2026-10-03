import argparse
import re
import sys
from collections.abc import Callable
from xml.etree.ElementTree import ParseError

from threat_intel.domains import extract_domains
from threat_intel.fetcher import FetchError, PoliteFetcher
from threat_intel.parsers import extract_links, extract_paragraphs, extract_visible_text, parse_feed

FEED_HREF_PATTERN = re.compile(r"""href=["']([^"']*(?:rss|feed|atom)[^"']*)["']""", re.IGNORECASE)
TITLE_PATTERN = re.compile(r"<title[^>]*>(.*?)</title>", re.IGNORECASE | re.DOTALL)
MAX_LISTED = 15
MAX_PLAIN_TEXT_LINES = 60


def describe_page(url: str, content: str, link_pattern: str) -> list[str]:
    lines = [f"- Tải được: {len(content)} ký tự"]
    try:
        items = parse_feed(content)
    except ParseError:
        items = []
    if items:
        lines.append(f"- Feed hợp lệ: {len(items)} mục")
        lines += [f"  - {item.title} → {item.link}" for item in items[:5]]
        return lines
    if "<" not in content[:500]:
        lines.append("- Nội dung văn bản:")
        lines += [f"  {line}" for line in content.splitlines()[:MAX_PLAIN_TEXT_LINES]]
        return lines
    title = TITLE_PATTERN.search(content)
    lines.append(f"- Tiêu đề: {title.group(1).strip() if title else '(không có)'}")
    lines.append(f"- Số đoạn văn: {len(extract_paragraphs(content))}")
    domains = sorted(extract_domains(extract_visible_text(content)))
    lines.append(f"- Tên miền trong chữ của trang: {len(domains)}: {', '.join(domains[: MAX_LISTED * 2])}")
    feeds = list(dict.fromkeys(FEED_HREF_PATTERN.findall(content)))
    if feeds:
        lines.append("- Link feed tìm thấy:")
        lines += [f"  - {feed}" for feed in feeds[:MAX_LISTED]]
    links = extract_links(content, url, link_pattern)
    lines.append(f"- Link khớp `{link_pattern}`: {len(links)}")
    lines += [f"  - {link}" for link in links[:MAX_LISTED]]
    return lines


def probe(urls: list[str], link_pattern: str, fetch: Callable[[str], str]) -> str:
    lines = ["## Kiểm tra nguồn", ""]
    for url in urls:
        lines.append(f"### {url}")
        try:
            lines += describe_page(url, fetch(url), link_pattern)
        except FetchError as error:
            lines.append(f"- Lỗi: {error}")
        lines.append("")
    return "\n".join(lines) + "\n"


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Kiểm tra URL nguồn trước khi thêm vào sources.json.")
    parser.add_argument("urls", nargs="+")
    parser.add_argument("--link-pattern", default=r"lua-dao|gia-mao|canh-bao|mao-danh")
    parser.add_argument("--delay", type=float, default=2.0)
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    print(probe(options.urls, options.link_pattern, PoliteFetcher(delay_seconds=options.delay).fetch))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
