#!/usr/bin/env bash
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$DEPLOY_DIR/../.." && pwd)"
readonly DEPLOY_DIR REPO_DIR
readonly ENV_FILE="${SCAMBODIA_ENV_FILE:-$DEPLOY_DIR/.env}"
readonly ENV_TEMPLATE="$DEPLOY_DIR/.env.example"
readonly COMPOSE_FILE="$DEPLOY_DIR/docker-compose.yml"
readonly BRANCH="${SCAMBODIA_BRANCH:-main}"
readonly DOMAIN_PREFIX="scambodia"
readonly DOMAIN_SUFFIX="sslip.io"
readonly SWAP_FILE="/swapfile"
readonly SWAP_SIZE="2G"
readonly CRON_FILE="/etc/cron.d/scambodia-update"
readonly UPDATE_LOG="/var/log/scambodia-update.log"
readonly UPDATE_SCHEDULE="*/15 * * * *"
readonly PUBLIC_IP_URL="https://api.ipify.org"
readonly REBUILD_PATHS='^(src/|Dockerfile$|Directory\.Build\.props$|\.editorconfig$|deploy/oracle/(docker-compose\.yml|Caddyfile)$)'
readonly HTTP_PORTS=(80 443)
readonly DEFAULT_LLM_MODEL="qwen2.5:3b"

log() {
    printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"
}

fail() {
    log "Lỗi: $*" >&2
    exit 1
}

require_root() {
    [[ "$(id -u)" -eq 0 ]] || fail "hãy chạy bằng sudo: sudo $0 $*"
}

repo_owner() {
    stat -c %U "$REPO_DIR"
}

as_owner() {
    sudo -u "$(repo_owner)" git -C "$REPO_DIR" "$@"
}

env_value() {
    grep -E "^$1=" "$ENV_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2- || true
}

set_env_value() {
    local remaining
    remaining="$(grep -vE "^$1=" "$ENV_FILE" || true)"
    printf '%s\n%s=%s\n' "$remaining" "$1" "$2" | sed '/^$/d' > "$ENV_FILE"
}

compose() {
    local profiles=""
    [[ -n "$(env_value TELEGRAM_BOT_TOKEN)" ]] && profiles="bot"
    COMPOSE_PROFILES="$profiles" docker compose --project-directory "$DEPLOY_DIR" -f "$COMPOSE_FILE" "$@"
}

install_docker() {
    if command -v docker >/dev/null 2>&1; then
        log "Docker đã có: $(docker --version)"
        return
    fi
    log "Cài Docker"
    apt-get update -qq
    apt-get install -y -qq ca-certificates curl
    curl -fsSL https://get.docker.com | sh
    usermod -aG docker "$(repo_owner)"
}

open_firewall() {
    local port
    for port in "${HTTP_PORTS[@]}"; do
        iptables -C INPUT -p tcp --dport "$port" -m state --state NEW -j ACCEPT 2>/dev/null \
            || iptables -I INPUT 1 -p tcp --dport "$port" -m state --state NEW -j ACCEPT
    done
    if command -v netfilter-persistent >/dev/null 2>&1; then
        netfilter-persistent save >/dev/null
    fi
    log "Đã mở cổng ${HTTP_PORTS[*]} trên máy (vẫn phải mở trong Security List của Oracle)"
}

create_swap() {
    if swapon --show=NAME --noheadings | grep -qx "$SWAP_FILE"; then
        return
    fi
    log "Tạo swap $SWAP_SIZE để build .NET không thiếu bộ nhớ"
    fallocate -l "$SWAP_SIZE" "$SWAP_FILE"
    chmod 600 "$SWAP_FILE"
    mkswap "$SWAP_FILE" >/dev/null
    swapon "$SWAP_FILE"
    grep -q "^$SWAP_FILE " /etc/fstab || echo "$SWAP_FILE none swap sw 0 0" >> /etc/fstab
}

prepare_env_file() {
    [[ -f "$ENV_FILE" ]] || cp "$ENV_TEMPLATE" "$ENV_FILE"
    chown "$(repo_owner)" "$ENV_FILE"
    chmod 600 "$ENV_FILE"
    if [[ -z "$(env_value DOMAIN)" ]]; then
        local public_ip
        public_ip="$(curl -fsS "$PUBLIC_IP_URL")" || fail "không lấy được IP công khai"
        set_env_value DOMAIN "$DOMAIN_PREFIX-${public_ip//./-}.$DOMAIN_SUFFIX"
    fi
    if [[ -n "$(env_value SCAMDETECTOR_CONNECTION_STRING)" ]]; then
        set_env_value APPLY_MIGRATIONS true
    fi
}

prepare_models_dir() {
    mkdir -p "$REPO_DIR/models"
    chmod a+rx "$REPO_DIR/models"
    find "$REPO_DIR/models" -type f -exec chmod a+r {} +
    find "$REPO_DIR/data/threat-intel" -type f -exec chmod a+r {} +
}

llm_model() {
    local configured
    configured="$(env_value LLM_MODEL)"
    printf '%s' "${configured:-$DEFAULT_LLM_MODEL}"
}

pull_llm_model() {
    local model
    model="$(llm_model)"
    log "Tải model AI $model cho Ollama (lần đầu khoảng 2 GB)"
    compose exec -T ollama ollama pull "$model"
}

install_update_cron() {
    printf '%s root %s update >> %s 2>&1\n' "$UPDATE_SCHEDULE" "$DEPLOY_DIR/scambodia.sh" "$UPDATE_LOG" > "$CRON_FILE"
    chmod 644 "$CRON_FILE"
    log "Đã đặt lịch tự cập nhật mỗi 15 phút (log: $UPDATE_LOG)"
}

print_summary() {
    local domain bot_status model_status
    domain="$(env_value DOMAIN)"
    bot_status="chưa bật (điền TELEGRAM_BOT_TOKEN vào deploy/oracle/.env rồi chạy lại install)"
    if [[ -n "$(env_value TELEGRAM_BOT_TOKEN)" ]]; then
        bot_status="đang chạy"
    fi
    model_status="PhoBERT chưa có, API dùng LLM $(llm_model) qua Ollama"
    if [[ -f "$REPO_DIR/models/scam-detector.onnx" ]]; then
        model_status="PhoBERT đã có, LLM $(llm_model) làm dự phòng"
    fi
    cat <<SUMMARY

==> Xong. Địa chỉ API: https://$domain
    Kiểm tra:   curl -s https://$domain/health
    Extension:  Cài đặt → Địa chỉ API → https://$domain
    Bot Telegram: $bot_status
    Model: $model_status
SUMMARY
}

command_install() {
    require_root install
    install_docker
    open_firewall
    create_swap
    prepare_env_file
    prepare_models_dir
    log "Build và khởi động container (lần đầu mất 5–15 phút)"
    compose up -d --build --remove-orphans
    pull_llm_model
    install_update_cron
    print_summary
}

command_update() {
    require_root update
    as_owner fetch --quiet origin "$BRANCH"
    local current target changed
    current="$(as_owner rev-parse HEAD)"
    target="$(as_owner rev-parse "origin/$BRANCH")"
    if [[ "$current" == "$target" ]]; then
        return
    fi
    changed="$(as_owner diff --name-only "$current" "$target")"
    as_owner merge --ff-only --quiet "origin/$BRANCH" || fail "không fast-forward được, có sửa tay trên máy chủ?"
    prepare_models_dir
    if grep -qE "$REBUILD_PATHS" <<< "$changed"; then
        log "Code thay đổi (${current:0:7}..${target:0:7}), build lại"
        compose up -d --build --remove-orphans
        pull_llm_model
        docker image prune -f >/dev/null
    else
        log "Chỉ dữ liệu thay đổi (${current:0:7}..${target:0:7}), API tự nạp lại"
    fi
}

command_restart() {
    require_root restart
    prepare_models_dir
    compose restart
}

command_status() {
    compose ps
    local domain
    domain="$(env_value DOMAIN)"
    if [[ -n "$domain" ]]; then
        curl -s -o /dev/null -w "https://$domain/health → HTTP %{http_code}\n" "https://$domain/health" || true
    fi
}

command_logs() {
    compose logs --tail=100 "${@:-api}"
}

usage() {
    cat <<USAGE
Cách dùng: sudo deploy/oracle/scambodia.sh <lệnh>
  install   Cài Docker, mở cổng, tạo tên miền, build và chạy, tải model AI, đặt lịch tự cập nhật
  update    Kéo main mới; chỉ build lại khi code đổi (cron gọi mỗi 15 phút)
  restart   Khởi động lại container (sau khi chép model mới vào models/)
  status    Trạng thái container và /health
  logs [dịch vụ]  100 dòng log cuối (api, bot, caddy)
USAGE
}

main() {
    local command="${1:-}"
    shift || true
    case "$command" in
        install) command_install ;;
        update) command_update ;;
        restart) command_restart ;;
        status) command_status ;;
        logs) command_logs "$@" ;;
        *) usage; [[ -z "$command" ]] || exit 2 ;;
    esac
}

main "$@"
