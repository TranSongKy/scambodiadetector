import csv
import tempfile
import unittest
from pathlib import Path

from threat_intel.crawler import DomainSighting, SourceResult, crawl_source
from threat_intel.domains import extract_domains, extract_warned_domains, is_allowed, normalize_domain, refang
from threat_intel.fetcher import FetchError, PoliteFetcher
from threat_intel.parsers import extract_links, extract_paragraphs, parse_feed
from threat_intel.similarity import fingerprint, jaccard, matches_fingerprint, template_coverage
from threat_intel.sources import Source
from threat_intel.store import BlockedDomain, ScamTemplate
from threat_intel.templates import candidate_templates, extract_quotes, is_template_candidate
from threat_intel.update import build_update

FIXTURES = Path(__file__).parent / "fixtures" / "threat_intel"
ARTICLE_URL = "https://news.example.vn/canh-bao-gia-danh-ngan-hang.html"
FEED_URL = "https://news.example.vn/rss/phap-luat.rss"
LIST_URL = "https://official.example.vn/canh-bao"
BLACKLIST_URL = "https://data.example.org/blacklist.json"
TODAY = "2026-10-03"


def fixture(name: str) -> str:
    return (FIXTURES / name).read_text(encoding="utf-8")


def fake_fetch(pages: dict[str, str]):
    def fetch(url: str) -> str:
        if url not in pages:
            raise FetchError(f"404 {url}")
        return pages[url]

    return fetch


class DomainTests(unittest.TestCase):
    def test_refang_restores_defanged_urls(self) -> None:
        self.assertEqual("https://bidv.top vcb.com", refang("hxxps://bidv[.]top vcb(.)com"))

    def test_normalize_domain_lowercases_and_strips(self) -> None:
        self.assertEqual("lua-dao.vip", normalize_domain("https://www.Lua-Dao.VIP/nhan-qua?x=1"))
        self.assertEqual("xn--vitcombank-n7a.com", normalize_domain("viêtcombank.com"))
        self.assertIsNone(normalize_domain("anh.jpg"))
        self.assertIsNone(normalize_domain("localhost"))
        self.assertIsNone(normalize_domain("TP.HCM"))
        self.assertIsNone(normalize_domain("bao-cao.docx"))

    def test_extract_domains_finds_defanged_and_plain(self) -> None:
        self.assertEqual(
            {"vcb-xacminh.com", "bidv-hotro.top"}, extract_domains("vcb-xacminh[.]com và hxxps://bidv-hotro.top/x")
        )

    def test_warned_domains_only_come_from_warning_paragraphs(self) -> None:
        warned = extract_warned_domains(["Trang giả mạo vcb-xacminh.com", "Thời tiết hôm nay tại weather.example.com"])

        self.assertEqual(["vcb-xacminh.com"], list(warned))

    def test_city_abbreviations_are_not_domains(self) -> None:
        warned = extract_warned_domains(["Công an TP.HCM phát hiện kho hàng giả mạo nhãn hiệu tại Q.1"])

        self.assertEqual({}, warned)

    def test_allowlist_matches_parent_domains(self) -> None:
        allowlist = {"vietcombank.com.vn", "gov.vn"}

        self.assertTrue(is_allowed("vcbdigibank.vietcombank.com.vn", allowlist))
        self.assertTrue(is_allowed("dichvucong.gov.vn", allowlist))
        self.assertFalse(is_allowed("vietcombank-com.vn", allowlist))


class ParserTests(unittest.TestCase):
    def test_parse_rss_items(self) -> None:
        items = parse_feed(fixture("feed.xml"))

        self.assertEqual(2, len(items))
        self.assertEqual(ARTICLE_URL, items[0].link)
        self.assertIn("giả mạo", items[0].summary)

    def test_feed_text_is_unescaped_and_stripped_of_tags(self) -> None:
        feed = (
            "<rss><channel><item><title>Kh&amp;ocirc;ng chuyển tiền &amp;amp;apos;lạ&amp;amp;apos;</title>"
            "<link>https://a.vn/1</link><description>&lt;p&gt;Cảnh b&amp;aacute;o &lt;b&gt;lừa đảo&lt;/b&gt;&lt;/p&gt;"
            "</description></item></channel></rss>"
        )

        item = parse_feed(feed)[0]

        self.assertEqual("Không chuyển tiền 'lạ'", item.title)
        self.assertEqual("Cảnh báo lừa đảo", item.summary)

    def test_parse_atom_entries(self) -> None:
        atom = (
            '<feed xmlns="http://www.w3.org/2005/Atom"><entry><title>Cảnh báo</title>'
            '<link href="https://a.example/x"/><updated>2026-10-02</updated></entry></feed>'
        )

        self.assertEqual("https://a.example/x", parse_feed(atom)[0].link)

    def test_extract_paragraphs_skips_navigation_scripts_and_footer(self) -> None:
        paragraphs = extract_paragraphs(fixture("article.html"))

        self.assertTrue(paragraphs[0].startswith("Cảnh báo thủ đoạn"))
        self.assertFalse(any("Trang chủ" in paragraph or "Bản quyền" in paragraph for paragraph in paragraphs))
        self.assertFalse(any("tracking" in paragraph for paragraph in paragraphs))

    def test_extract_links_resolves_and_deduplicates(self) -> None:
        links = extract_links(fixture("list.html"), LIST_URL, r"/bai-viet/")

        self.assertEqual(["https://official.example.vn/bai-viet/canh-bao-gia-danh-ngan-hang"], links)


class TemplateTests(unittest.TestCase):
    def test_quotes_are_extracted_from_paragraphs(self) -> None:
        quotes = extract_quotes(extract_paragraphs(fixture("article.html")))

        self.assertEqual(3, len(quotes))

    def test_scam_messages_are_masked_and_victim_quotes_rejected(self) -> None:
        quotes = extract_quotes(extract_paragraphs(fixture("article.html")))

        templates = candidate_templates(quotes, [])

        self.assertEqual(2, len(templates))
        self.assertIn("<URL>", templates[0])
        self.assertIn("<OTP>", templates[1])
        self.assertFalse(any("482913" in template or "vcb-xacminh" in template for template in templates))

    def test_official_domain_mentioned_without_warning_is_ignored(self) -> None:
        result = extract_warned_domains(["Trang chính thức của ngân hàng là vietcombank.com.vn"])

        self.assertEqual({}, result)

    def test_advice_and_reported_speech_are_not_candidates(self) -> None:
        self.assertFalse(
            is_template_candidate("Người dân tuyệt đối không truy cập các đường link lạ, không cung cấp mã OTP cho ai")
        )
        self.assertFalse(
            is_template_candidate("Chị H. cho biết đã chuyển khoản theo yêu cầu vì tin rằng mình trúng thưởng lớn")
        )
        self.assertTrue(
            is_template_candidate("Chúc mừng bạn được chọn làm cộng tác viên, kết bạn Zalo <PHONE> để nhận nhiệm vụ")
        )

    def test_fingerprint_hides_text_but_matches_near_duplicates(self) -> None:
        text = "Tài khoản của quý khách bị tạm khóa, vui lòng truy cập <URL> để xác minh trong 24h"

        self.assertNotIn("khóa", fingerprint(text))
        self.assertTrue(matches_fingerprint(text + "!", [fingerprint(text)], 0.8))
        self.assertFalse(
            matches_fingerprint("Việc nhẹ lương cao, nạp tiền làm nhiệm vụ nhận hoa hồng", [fingerprint(text)], 0.8)
        )

    def test_shared_platform_roots_are_never_added(self) -> None:
        result = SourceResult(
            source="news", domains={"blogspot.com": DomainSighting("", ""), "x.blogspot.com": DomainSighting("", "")}
        )

        update = build_update([], [], [result], set(), TODAY)

        self.assertEqual(["x.blogspot.com"], [record.domain for record in update.new_domains])

    def test_reporter_language_is_not_a_candidate(self) -> None:
        self.assertFalse(
            is_template_candidate(
                "Để đấu tranh, ngăn chặn đối với loại tội phạm này, cơ quan điều tra tiếp tục xác minh, làm rõ "
                "tất cả các hành vi vi phạm pháp luật trước đó của các đối tượng để xử lý triệt để"
            )
        )
        self.assertFalse(
            is_template_candidate("Với thủ đoạn này, nạn nhân được yêu cầu truy cập <URL> và chuyển tiền vào tài khoản")
        )

    def test_police_impersonation_scam_is_still_a_candidate(self) -> None:
        self.assertTrue(
            is_template_candidate(
                "Cơ quan điều tra thông báo bạn liên quan vụ án rửa tiền, chuyển toàn bộ tiền vào tài khoản "
                "<ACCOUNT> để xác minh"
            )
        )

    def test_victim_narration_without_call_to_action_is_not_a_candidate(self) -> None:
        self.assertFalse(is_template_candidate("Tôi rất bất ngờ khi tài khoản mất hết tiền chỉ sau vài phút"))

    def test_short_or_non_vietnamese_text_is_not_a_candidate(self) -> None:
        self.assertFalse(is_template_candidate("Chuyển tiền ngay"))
        self.assertFalse(is_template_candidate("Please transfer money to the account now to receive the prize"))

    def test_near_duplicates_are_skipped(self) -> None:
        text = "Tài khoản của quý khách bị tạm khóa, vui lòng truy cập <URL> để xác minh trong 24h"

        self.assertEqual([], candidate_templates([text + "!"], [text]))

    def test_similarity_measures(self) -> None:
        template = "Tài khoản của quý khách bị tạm khóa vui lòng truy cập <URL> để xác minh"
        message = "VCB: " + template + " ngay hôm nay"

        self.assertEqual(1.0, template_coverage(template, message))
        self.assertLess(jaccard(template, message), 1.0)
        self.assertEqual(0.0, template_coverage("", message))


class CrawlTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.data_dir = Path(self._directory.name)
        self.pages = {
            FEED_URL: fixture("feed.xml"),
            ARTICLE_URL: fixture("article.html"),
            LIST_URL: fixture("list.html"),
            "https://official.example.vn/bai-viet/canh-bao-gia-danh-ngan-hang": fixture("article.html"),
            BLACKLIST_URL: fixture("blacklist.json"),
        }

    def tearDown(self) -> None:
        self._directory.cleanup()

    def test_rss_source_reads_only_scam_articles(self) -> None:
        result = crawl_source(Source(name="news", kind="rss", url=FEED_URL), fake_fetch(self.pages), self.data_dir)

        self.assertEqual(1, result.articles)
        self.assertEqual({"vcb-xacminh.com", "bidv-hotro.top", "vietinbank-khoa.xyz"}, set(result.domains))
        self.assertIn("giả mạo", result.domains["bidv-hotro.top"].evidence)
        self.assertEqual([], result.errors)

    def test_html_list_source_follows_matching_links(self) -> None:
        source = Source(name="official", kind="html_list", url=LIST_URL, link_pattern=r"/bai-viet/")

        result = crawl_source(source, fake_fetch(self.pages), self.data_dir)

        self.assertEqual(1, result.articles)

    def test_domain_list_source_reads_json_entries(self) -> None:
        source = Source(name="blacklist", kind="domain_list", url=BLACKLIST_URL)

        result = crawl_source(source, fake_fetch(self.pages), self.data_dir)

        self.assertEqual({"lua-dao-qua-tang.vip", "shopee-trian.online", "momo-hoantien.click"}, set(result.domains))

    def test_manual_source_reads_submissions(self) -> None:
        with (self.data_dir / "manual_submissions.csv").open("w", encoding="utf-8", newline="") as file:
            writer = csv.DictWriter(file, fieldnames=["text", "source_url", "note"])
            writer.writeheader()
            writer.writerow(
                {
                    "text": "Bạn được nhận quà tri ân, truy cập qua-tang-shopee.vip và nhập mã OTP 1234 để nhận ngay",
                    "source_url": "",
                    "note": "",
                }
            )

        result = crawl_source(Source(name="manual", kind="manual"), fake_fetch(self.pages), self.data_dir)

        self.assertIn("qua-tang-shopee.vip", result.domains)
        self.assertEqual(1, len(candidate_templates([text for text, _ in result.texts], [])))

    def test_unreachable_source_is_reported_not_raised(self) -> None:
        result = crawl_source(
            Source(name="down", kind="rss", url="https://down.example/rss"), fake_fetch({}), self.data_dir
        )

        self.assertEqual(1, len(result.errors))

    def test_update_skips_allowlisted_and_existing_and_continues_ids(self) -> None:
        result = crawl_source(Source(name="news", kind="rss", url=FEED_URL), fake_fetch(self.pages), self.data_dir)
        existing_domains = [BlockedDomain("bidv-hotro.top", "old", "https://old.example", "2026-01-01")]
        existing_templates = [
            ScamTemplate(
                "tpl_000007",
                "Khác hẳn: việc nhẹ lương cao, nạp tiền làm nhiệm vụ nhận hoa hồng mỗi ngày",
                "old",
                "",
                "2026-01-01",
            )
        ]

        update = build_update(existing_domains, existing_templates, [result], {"vietinbank-khoa.xyz"}, TODAY)

        self.assertEqual(["vcb-xacminh.com"], [record.domain for record in update.new_domains])
        self.assertEqual(["vietinbank-khoa.xyz"], update.skipped_allowed)
        self.assertEqual(["tpl_000008", "tpl_000009"], [record.id for record in update.new_templates])
        self.assertTrue(all(record.first_seen == TODAY for record in update.new_domains))


class RejectedMemoryTests(unittest.TestCase):
    def test_rejected_domains_and_templates_are_not_added_again(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            pages = {FEED_URL: fixture("feed.xml"), ARTICLE_URL: fixture("article.html")}
            result = crawl_source(Source(name="news", kind="rss", url=FEED_URL), fake_fetch(pages), Path(directory))
        first = build_update([], [], [result], set(), TODAY)
        rejected_text = first.new_templates[0].text

        second = build_update([], [], [result], set(), TODAY, {"vcb-xacminh.com"}, [fingerprint(rejected_text)])

        self.assertNotIn("vcb-xacminh.com", [record.domain for record in second.new_domains])
        self.assertNotIn(rejected_text, [record.text for record in second.new_templates])
        self.assertEqual(2, second.skipped_rejected)


class FetcherTests(unittest.TestCase):
    def test_robots_disallow_blocks_fetch(self) -> None:
        def opener(request) -> bytes:
            if request.full_url.endswith("/robots.txt"):
                return b"User-agent: *\nDisallow: /private\n"
            return b"ok"

        fetcher = PoliteFetcher(delay_seconds=0, opener=opener, sleeper=lambda _: None)

        self.assertEqual("ok", fetcher.fetch("https://site.example/public"))
        with self.assertRaises(FetchError):
            fetcher.fetch("https://site.example/private/page")

    def test_requests_to_same_host_wait_for_delay(self) -> None:
        sleeps: list[float] = []
        fetcher = PoliteFetcher(delay_seconds=5, opener=lambda request: b"", sleeper=sleeps.append)

        fetcher.fetch("https://site.example/a")
        fetcher.fetch("https://site.example/b")

        self.assertEqual(1, len(sleeps))
        self.assertGreater(sleeps[0], 4)

    def test_missing_robots_allows_fetch(self) -> None:
        def opener(request) -> bytes:
            if request.full_url.endswith("/robots.txt"):
                raise FetchError("404")
            return b"page"

        fetcher = PoliteFetcher(delay_seconds=0, opener=opener, sleeper=lambda _: None)

        self.assertEqual("page", fetcher.fetch("https://site.example/x"))


if __name__ == "__main__":
    unittest.main()
