import csv
import tempfile
import unittest
from pathlib import Path

from anonymize import AnonymizeError, anonymize, may_contain_name

RAW_COLUMNS = ["text", "label", "scam_type", "channel", "source", "collected_at", "annotator"]


class AnonymizeTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.root = Path(self._directory.name)

    def tearDown(self) -> None:
        self._directory.cleanup()

    def write_input(self, rows: list[dict[str, str]]) -> Path:
        input_path = self.root / "input.csv"
        with input_path.open("w", encoding="utf-8", newline="") as input_file:
            writer = csv.DictWriter(input_file, fieldnames=RAW_COLUMNS)
            writer.writeheader()
            writer.writerows(rows)
        return input_path

    def make_raw_row(self, text: str) -> dict[str, str]:
        return {
            "text": text,
            "label": "scam",
            "scam_type": "bank_impersonation",
            "channel": "sms",
            "source": "collected",
            "collected_at": "2026-10-02",
            "annotator": "ky",
        }

    def test_text_is_masked_normalized_and_has_url_computed(self) -> None:
        input_path = self.write_input([self.make_raw_row("  Goi 090 123 4567   hoac vao bit.ly/x  ")])

        rows, counts = anonymize(input_path, self.root / "out.csv", start_id=1)

        self.assertEqual("Goi <PHONE> hoac vao <URL>", rows[0]["text"])
        self.assertEqual("true", rows[0]["has_url"])
        self.assertEqual({"phone": 1, "url": 1}, dict(counts))

    def test_ids_continue_from_start_id(self) -> None:
        input_path = self.write_input([self.make_raw_row("Tin thu nhat"), self.make_raw_row("Tin thu hai")])

        rows, _ = anonymize(input_path, self.root / "out.csv", start_id=41)

        self.assertEqual(["msg_000041", "msg_000042"], [row["id"] for row in rows])

    def test_output_file_has_dataset_columns(self) -> None:
        output_path = self.root / "out.csv"
        anonymize(self.write_input([self.make_raw_row("Mai hop nhom nhe")]), output_path, start_id=1)

        with output_path.open(encoding="utf-8", newline="") as output_file:
            header = next(csv.reader(output_file))

        self.assertEqual(
            ["id", "text", "label", "scam_type", "channel", "source", "has_url", "collected_at", "annotator"], header
        )

    def test_decomposed_unicode_is_composed(self) -> None:
        input_path = self.write_input([self.make_raw_row("Xin chào bàn")])

        rows, _ = anonymize(input_path, self.root / "out.csv", start_id=1)

        self.assertEqual("Xin chào bàn", rows[0]["text"])

    def test_missing_required_column_raises(self) -> None:
        input_path = self.root / "input.csv"
        input_path.write_text("text\nxin chao\n", encoding="utf-8")

        with self.assertRaises(AnonymizeError):
            anonymize(input_path, self.root / "out.csv", start_id=1)

    def test_writing_into_raw_directory_raises(self) -> None:
        input_path = self.write_input([self.make_raw_row("Mai hop nhom nhe")])

        with self.assertRaises(AnonymizeError):
            anonymize(input_path, self.root / "raw" / "out.csv", start_id=1)


class MayContainNameTests(unittest.TestCase):
    def test_honorific_followed_by_capitalized_word_is_flagged(self) -> None:
        self.assertTrue(may_contain_name("Anh Nam oi cho em muon tien"))

    def test_honorific_followed_by_lowercase_word_is_not_flagged(self) -> None:
        self.assertFalse(may_contain_name("em oi mai di hoc khong"))


if __name__ == "__main__":
    unittest.main()
