import tempfile
import unittest
from pathlib import Path

from split_dataset import SplitError, read_rows, split_dataset

from scripts.tests.dataset_rows import FIELDNAMES, make_balanced_rows, make_row

SPLIT_FILES = ("train.csv", "val.csv", "test.csv")


class SplitDatasetTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.output = Path(self._directory.name) / "splits"

    def tearDown(self) -> None:
        self._directory.cleanup()

    def test_ratios_are_70_15_15_per_label(self) -> None:
        splits = split_dataset(FIELDNAMES, make_balanced_rows(per_label=20), self.output)

        self.assertEqual((42, 9, 9), (len(splits["train"]), len(splits["val"]), len(splits["test"])))
        self.assertEqual({"scam", "spam", "normal"}, {row["label"] for row in splits["test"]})

    def test_splits_do_not_overlap_and_cover_dataset(self) -> None:
        rows = make_balanced_rows(per_label=20)

        splits = split_dataset(FIELDNAMES, rows, self.output)

        split_ids = [row["id"] for split in splits.values() for row in split]
        self.assertEqual(len(split_ids), len(set(split_ids)))
        self.assertEqual({row["id"] for row in rows}, set(split_ids))

    def test_synthetic_rows_never_enter_test(self) -> None:
        rows = make_balanced_rows(per_label=20)
        rows = [{**row, "source": "synthetic"} if index % 4 == 0 else row for index, row in enumerate(rows)]

        splits = split_dataset(FIELDNAMES, rows, self.output)

        self.assertFalse(any(row["source"] == "synthetic" for row in splits["test"]))

    def test_rerun_produces_identical_files(self) -> None:
        rows = make_balanced_rows(per_label=20)
        split_dataset(FIELDNAMES, rows, self.output)
        first_run = [(self.output / name).read_bytes() for name in SPLIT_FILES]

        split_dataset(FIELDNAMES, rows, self.output)

        self.assertEqual(first_run, [(self.output / name).read_bytes() for name in SPLIT_FILES])

    def test_new_rows_keep_test_set_fixed(self) -> None:
        rows = make_balanced_rows(per_label=20)
        split_dataset(FIELDNAMES, rows, self.output)
        test_before = (self.output / "test.csv").read_bytes()

        new_rows = [make_row(index, "spam") for index in range(500, 510)]
        splits = split_dataset(FIELDNAMES, rows + new_rows, self.output)

        self.assertEqual(test_before, (self.output / "test.csv").read_bytes())
        self.assertTrue({row["id"] for row in new_rows} <= {row["id"] for row in splits["train"] + splits["val"]})

    def test_test_row_removed_from_dataset_raises(self) -> None:
        rows = make_balanced_rows(per_label=20)
        splits = split_dataset(FIELDNAMES, rows, self.output)
        removed_id = splits["test"][0]["id"]

        with self.assertRaises(SplitError):
            split_dataset(FIELDNAMES, [row for row in rows if row["id"] != removed_id], self.output)

    def test_bom_file_raises(self) -> None:
        dataset = Path(self._directory.name) / "bom.csv"
        dataset.write_text("﻿id,text\n", encoding="utf-8")

        with self.assertRaises(SplitError):
            read_rows(dataset)


if __name__ == "__main__":
    unittest.main()
