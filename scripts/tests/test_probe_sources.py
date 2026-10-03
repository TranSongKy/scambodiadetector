import unittest

from probe_sources import probe
from threat_intel.fetcher import FetchError

FEED = """<rss><channel><item><title>Cảnh báo lừa đảo</title><link>https://a.vn/1</link></item></channel></rss>"""
PAGE = """<html><head><title>Tin tức</title><link rel="alternate" href="/rss/phap-luat.rss"></head>
<body><p>Đoạn một</p><a href="/canh-bao-lua-dao-1.html">Bài</a><a href="/the-thao.html">Khác</a></body></html>"""


def fake_fetch(url: str) -> str:
    pages = {"https://a.vn/rss": FEED, "https://a.vn/": PAGE}
    if url not in pages:
        raise FetchError(f"404 {url}")
    return pages[url]


class ProbeTests(unittest.TestCase):
    def test_feed_is_reported_with_items(self) -> None:
        report = probe(["https://a.vn/rss"], "lua-dao", fake_fetch)

        self.assertIn("Feed hợp lệ: 1 mục", report)
        self.assertIn("Cảnh báo lừa đảo → https://a.vn/1", report)

    def test_page_lists_feed_links_and_matching_article_links(self) -> None:
        report = probe(["https://a.vn/"], "lua-dao", fake_fetch)

        self.assertIn("/rss/phap-luat.rss", report)
        self.assertIn("https://a.vn/canh-bao-lua-dao-1.html", report)
        self.assertNotIn("the-thao", report)

    def test_fetch_error_is_reported(self) -> None:
        report = probe(["https://a.vn/missing"], "lua-dao", fake_fetch)

        self.assertIn("Lỗi: 404 https://a.vn/missing", report)
