import argparse
import sys
from pathlib import Path

from train_phobert import BASE_MODEL_NAME, prepare_text, write_tokenizer_golden

SAMPLE_TEXTS = (
    "Tài khoản của quý khách bị tạm khóa, vui lòng xác minh tại bit.ly/abc",
    "Tai khoan cua quy khach bi tam khoa, vui long xac minh ngay",
    "Mai 7h họp nhóm đồ án ở thư viện nha",
    "Chúc mừng bạn trúng thưởng iPhone 17, gọi 0901234567 để nhận quà 🎁",
    "Mã OTP của bạn là 123456, không chia sẻ cho bất kỳ ai",
    "Việc nhẹ lương cao, cộng tác viên làm nhiệm vụ nhận 500.000đ/ngày",
    "CÔNG AN THÔNG BÁO: bạn có liên quan đến vụ án, chuyển tiền vào STK 1234567890123",
    "Shopee: đơn hàng của bạn đang được giao, phí ship 15000d",
    "ok e, tối nay a về muộn nhé!!!",
    "Giảm giá 50% toàn bộ cửa hàng dịp cuối tuần",
    "Xin chào",
    " ".join(["Đây là một tin nhắn rất dài để kiểm tra việc cắt token"] * 40),
)


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Sinh tokenizer-golden.json từ PhobertTokenizer của HuggingFace.")
    parser.add_argument("output_dir", type=Path, help="Nơi ghi vocab.txt, bpe.codes, tokenizer-golden.json")
    parser.add_argument("--model", default=BASE_MODEL_NAME, help="Tên model trên HuggingFace Hub")
    parser.add_argument("--vocab-dir", type=Path, help="Dùng vocab.txt và bpe.codes có sẵn thay vì tải từ Hub")
    return parser.parse_args(arguments)


def load_tokenizer(options: argparse.Namespace) -> object:
    from transformers import AutoTokenizer, PhobertTokenizer

    if options.vocab_dir is not None:
        return PhobertTokenizer(
            vocab_file=str(options.vocab_dir / "vocab.txt"),
            merges_file=str(options.vocab_dir / "bpe.codes"),
        )
    return AutoTokenizer.from_pretrained(options.model, use_fast=False)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    options.output_dir.mkdir(parents=True, exist_ok=True)
    tokenizer = load_tokenizer(options)
    tokenizer.save_pretrained(str(options.output_dir))
    golden_path = write_tokenizer_golden(tokenizer, [prepare_text(text) for text in SAMPLE_TEXTS], options.output_dir)
    print(f"Đã ghi {golden_path} ({len(SAMPLE_TEXTS)} mẫu)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
