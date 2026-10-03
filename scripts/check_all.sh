#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

step() {
    printf '\n==> %s\n' "$1"
}

step "dotnet build"
dotnet build --configuration Release

step "dotnet test"
dotnet test --configuration Release --no-build

step "ruff"
ruff check .
ruff format --check .

step "Python unit tests"
python3 -m unittest discover -s scripts/tests -t .

step "Threat intel"
python3 scripts/validate_threat_intel.py

step "Dataset"
if [ "$(wc -l < data/processed/dataset.csv)" -gt 1 ]; then
    python3 scripts/validate_dataset.py data/processed/dataset.csv
else
    echo "dataset.csv chưa có mẫu, bỏ qua."
fi

step "Chrome extension"
(cd extension && npm ci --silent && npm test)

printf '\nTất cả kiểm tra đều đạt.\n'
