import csv
import tempfile
import unittest
from pathlib import Path

from add_synthetic_candidates import add_synthetic
from review_candidates import run_review
from threat_intel.fetcher import FetchError
from training_data.candidates import (
    Candidate,
    load_candidates,
    load_reviewed,
    save_candidates,
    text_key,
)
from training_data.collect import build_collector, collect_source, prepare_text
from training_data.dataset_file import DATASET_COLUMNS
from training_data.forum import extract_texts_by_class
from training_data.labels import LabelError, apply_suggestions
from training_data.sources import TrainingSource

FIXTURES = Path(__file__).parent / "fixtures" / "threat_intel"
TODAY = "2026-10-03"
FEED_URL = "https://news.example.vn/rss/phap-luat.rss"
ARTICLE_URL = "https://news.example.vn/canh-bao-gia-danh-ngan-hang.html"
FORUM_FEED_URL = "https://forum.example.vn/forums/-/index.rss"
THREAD_URL = "https://forum.example.vn/threads/canh-giac-tin-nhan-lua-dao.1/"
FORUM_FEED = (
    f"<rss><channel><item><title>Cảnh giác tin nhắn lừa đảo</title><link>{THREAD_URL}</link></item></channel></rss>"
)
SCAM_TEXT = "Tài khoản của quý khách bị khóa, vui lòng truy cập vcb-xacminh.com để xác minh ngay"


def fake_fetch(pages: dict[str, str]):
    def fetch(url: str) -> str:
        if url not in pages:
            raise FetchError(f"404 {url}")
        return pages[url]

    return fetch


def candidate(candidate_id: str, text: str, **values: str) -> Candidate:
    return Candidate(candidate_id, text, "news", "public_post", "https://a.vn", TODAY, **values)


class CollectTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.data_dir = Path(self._directory.name)
        self.pages = {
            FEED_URL: (FIXTURES / "feed.xml").read_text(encoding="utf-8"),
            ARTICLE_URL: (FIXTURES / "article.html").read_text(encoding="utf-8"),
            FORUM_FEED_URL: FORUM_FEED,
            THREAD_URL: (FIXTURES / "forum_thread.html").read_text(encoding="utf-8"),
        }

    def tearDown(self) -> None:
        self._directory.cleanup()

    def test_prepare_text_masks_and_rejects_short_or_foreign_text(self) -> None:
        self.assertEqual("Gọi <PHONE> để nhận quà ngay", prepare_text("Gọi  0901234567 để nhận quà ngay"))
        self.assertIsNone(prepare_text("ok"))
        self.assertIsNone(prepare_text("Please call this number to claim your prize now"))

    def test_news_articles_give_masked_quote_candidates(self) -> None:
        collector = build_collector([], [], set(), TODAY)
        source = TrainingSource(name="news", kind="rss", url=FEED_URL)

        report = collect_source(source, fake_fetch(self.pages), collector, self.data_dir)

        self.assertEqual([], report.errors)
        self.assertGreater(report.added, 0)
        self.assertTrue(all(c.origin == "news" and c.source == "public_post" for c in collector.added))
        self.assertTrue(all("0" not in c.text or "<" in c.text for c in collector.added))

    def test_forum_posts_are_read_by_content_class(self) -> None:
        collector = build_collector([], [], set(), TODAY)
        source = TrainingSource(name="forum", kind="rss", url=FORUM_FEED_URL, origin="forum", content_class="bbWrapper")

        report = collect_source(source, fake_fetch(self.pages), collector, self.data_dir)

        self.assertEqual(2, report.added)
        self.assertIn("<URL>", collector.added[0].text)
        self.assertIn("<PHONE>", collector.added[1].text)
        self.assertEqual(THREAD_URL, collector.added[0].source_url)

    def test_site_forbidding_ai_training_is_skipped(self) -> None:
        self.pages["https://forum.example.vn/robots.txt"] = "User-agent: *\nContent-Signal: ai-train=no\nAllow: /\n"
        collector = build_collector([], [], set(), TODAY)
        source = TrainingSource(name="forum", kind="rss", url=FORUM_FEED_URL, origin="forum", content_class="bbWrapper")

        report = collect_source(source, fake_fetch(self.pages), collector, self.data_dir)

        self.assertEqual(0, report.added)
        self.assertIn("ai-train=no", report.errors[0])

    def test_duplicates_of_dataset_candidates_and_reviewed_are_skipped(self) -> None:
        reviewed = {text_key(prepare_text("Chúc mừng bạn trúng thưởng xe SH, bấm link nhận quà"))}
        collector = build_collector(
            [candidate("cand_000007", prepare_text(SCAM_TEXT))], ["Mai 7h họp nhóm ở thư viện nha"], reviewed, TODAY
        )
        source = TrainingSource(name="tay", kind="manual")
        with (self.data_dir / "manual_messages.csv").open("w", encoding="utf-8") as manual:
            manual.write("text,label,channel,source_url,note\n")
            manual.write(f'"{SCAM_TEXT}",scam,sms,,\n')
            manual.write("Mai 7h  họp nhóm ở thư viện nha,normal,zalo,,\n")
            manual.write('"Chúc mừng bạn trúng thưởng xe SH, bấm link nhận quà",scam,sms,,\n')
            manual.write("Chị Lan ơi chiều nay em qua lấy đồ nhé,normal,zalo,,\n")
            manual.write("Ê tối nay đi ăn lẩu không mày,normal,messenger,,\n")

        report = collect_source(source, fake_fetch(self.pages), collector, self.data_dir)

        self.assertEqual(3, report.skipped_duplicate)
        self.assertEqual(1, report.skipped_name)
        self.assertEqual(["cand_000008"], [c.id for c in collector.added])
        self.assertEqual(
            ("contributed", "normal", "messenger"),
            (collector.added[0].source, collector.added[0].hint_label, collector.added[0].hint_channel),
        )

    def test_csv_dataset_maps_labels(self) -> None:
        url = "https://data.example.org/sms.csv"
        self.pages[url] = (
            "content,kind\nKhuyến mãi 50% toàn bộ cửa hàng đến hết tuần,ads\nMai họp lúc 8h nhé cả nhà,ham\n"
        )
        collector = build_collector([], [], set(), TODAY)
        source = TrainingSource(
            name="sms",
            kind="csv_dataset",
            url=url,
            license="CC BY 4.0",
            text_column="content",
            label_column="kind",
            label_map={"ads": "spam", "ham": "normal"},
            channel="sms",
        )

        collect_source(source, fake_fetch(self.pages), collector, self.data_dir)

        self.assertEqual(["spam", "normal"], [c.hint_label for c in collector.added])
        self.assertEqual({"public_dataset"}, {c.origin for c in collector.added})


class ForumParserTests(unittest.TestCase):
    def test_extract_texts_by_class_keeps_line_breaks_and_skips_scripts(self) -> None:
        html = '<div class="post bbWrapper">Dòng một<br>Dòng hai<script>x()</script></div><p class="bbWrapper"></p>'

        self.assertEqual(["Dòng một\nDòng hai"], extract_texts_by_class(html, "bbWrapper"))


class LabelTests(unittest.TestCase):
    def test_valid_suggestions_are_applied(self) -> None:
        candidates = [candidate("cand_000001", SCAM_TEXT), candidate("cand_000002", "Thông báo họp lớp")]

        updated = apply_suggestions(
            candidates,
            [
                {
                    "id": "cand_000001",
                    "label": "scam",
                    "scam_type": "bank_impersonation",
                    "channel": "sms",
                    "confidence": "high",
                    "note": "Giả ngân hàng, link lạ",
                },
                {"id": "cand_000002", "label": "exclude", "note": "Quá ngắn, không phải tin nhắn"},
            ],
        )

        self.assertEqual("scam", updated[0].suggested_label)
        self.assertEqual("exclude", updated[1].suggested_label)

    def test_invalid_suggestions_are_refused(self) -> None:
        candidates = [candidate("cand_000001", SCAM_TEXT)]
        invalid = [
            {
                "id": "cand_000001",
                "label": "scam",
                "scam_type": "",
                "channel": "sms",
                "confidence": "high",
                "note": "abcdef",
            },
            {
                "id": "cand_000001",
                "label": "normal",
                "scam_type": "job_scam",
                "channel": "sms",
                "confidence": "high",
                "note": "abcdef",
            },
            {"id": "cand_000001", "label": "spam", "channel": "fax", "confidence": "high", "note": "abcdef"},
            {"id": "cand_000001", "label": "exclude", "note": ""},
            {"id": "cand_000099", "label": "normal", "channel": "sms", "confidence": "high", "note": "abcdef"},
        ]
        for suggestion in invalid:
            with self.subTest(suggestion=suggestion), self.assertRaises(LabelError):
                apply_suggestions(candidates, [suggestion])


class ReviewTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.data_dir = Path(self._directory.name)
        self.dataset = self.data_dir / "dataset.csv"
        self.dataset.write_text(",".join(DATASET_COLUMNS) + "\n", encoding="utf-8")
        save_candidates(
            self.data_dir,
            [
                candidate(
                    "cand_000001",
                    "Tài khoản bị khóa, vào <URL> xác minh",
                    suggested_label="scam",
                    suggested_scam_type="bank_impersonation",
                    suggested_channel="sms",
                    confidence="high",
                    note="Giả ngân hàng",
                ),
                candidate(
                    "cand_000002",
                    "Giảm 50% toàn bộ sản phẩm đến chủ nhật",
                    suggested_label="spam",
                    suggested_channel="sms",
                    confidence="high",
                    note="Quảng cáo",
                ),
                candidate(
                    "cand_000003", "Bài báo nói về vụ án lừa đảo", suggested_label="exclude", note="Không phải tin nhắn"
                ),
                candidate("cand_000004", "Tối nay đi ăn không"),
            ],
        )

    def tearDown(self) -> None:
        self._directory.cleanup()

    def test_review_accepts_overrides_and_excludes(self) -> None:
        answers = iter(["", "n", "x"])

        accepted = run_review(self.data_dir, self.dataset, "ky", lambda _: next(answers), lambda _: None)

        with self.dataset.open(encoding="utf-8") as dataset_file:
            rows = list(csv.DictReader(dataset_file))
        self.assertEqual(2, accepted)
        self.assertEqual(["msg_000001", "msg_000002"], [row["id"] for row in rows])
        self.assertEqual(["scam", "normal"], [row["label"] for row in rows])
        self.assertEqual(["bank_impersonation", ""], [row["scam_type"] for row in rows])
        self.assertEqual({"ky"}, {row["annotator"] for row in rows})
        self.assertEqual("true", rows[0]["has_url"])
        self.assertEqual(["cand_000004"], [c.id for c in load_candidates(self.data_dir)])
        self.assertEqual(["accepted", "accepted", "rejected"], [r.decision for r in load_reviewed(self.data_dir)])

    def test_quit_keeps_remaining_candidates(self) -> None:
        answers = iter(["b", "q"])

        accepted = run_review(self.data_dir, self.dataset, "ky", lambda _: next(answers), lambda _: None)

        self.assertEqual(0, accepted)
        self.assertEqual(4, len(load_candidates(self.data_dir)))


class SyntheticTests(unittest.TestCase):
    def test_synthetic_messages_are_queued_with_suggestions(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            data_dir = Path(directory)
            dataset = data_dir / "dataset.csv"
            dataset.write_text(",".join(DATASET_COLUMNS) + "\n", encoding="utf-8")
            rows = [
                {"text": "Mai mấy giờ họp nhóm vậy, mình quên mất rồi", "label": "normal", "channel": "zalo"},
                {"text": "Siêu sale 10.10 giảm đến 70% tại cửa hàng, mua ngay", "label": "spam", "channel": "sms"},
            ]

            added, _ = add_synthetic(data_dir, dataset, rows, TODAY)

            queued = load_candidates(data_dir)
            self.assertEqual(2, added)
            self.assertEqual({"synthetic"}, {c.source for c in queued})
            self.assertEqual(["normal", "spam"], [c.suggested_label for c in queued])

    def test_invalid_synthetic_label_is_refused(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            data_dir = Path(directory)
            dataset = data_dir / "dataset.csv"
            dataset.write_text(",".join(DATASET_COLUMNS) + "\n", encoding="utf-8")

            with self.assertRaises(LabelError):
                add_synthetic(data_dir, dataset, [{"text": "Tin nhắn mẫu hợp lệ nhé", "label": "ham"}], TODAY)
            self.assertEqual([], load_candidates(data_dir))


class ValidateCandidatesTests(unittest.TestCase):
    def test_repository_data_is_valid(self) -> None:
        from validate_training_candidates import validate_candidates, validate_sources

        data_dir = Path(__file__).resolve().parents[2] / "data" / "training-candidates"

        self.assertEqual([], validate_sources(data_dir) + validate_candidates(data_dir))

    def test_unmasked_pii_and_bad_suggestion_are_reported(self) -> None:
        from validate_training_candidates import validate_candidates

        with tempfile.TemporaryDirectory() as directory:
            data_dir = Path(directory)
            save_candidates(
                data_dir,
                [
                    candidate("cand_000001", "Gọi 0901234567 ngay để nhận quà"),
                    candidate("cand_000002", "Tin hợp lệ nhé bạn", suggested_label="scam", note="thiếu loại"),
                ],
            )

            errors = validate_candidates(data_dir)

        self.assertEqual(2, len(errors))
        self.assertIn("phone", errors[0])
