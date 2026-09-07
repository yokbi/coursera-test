#!/usr/bin/env bash
#
# run-mac-intel.sh — Alışkanlık Takibi'ni Intel Mac'te (x86_64) tek komutla
# ayağa kaldırır. Apple Silicon için run-mac-apple-silicon.sh kullanın.
#
# Kullanım:
#   ./run-mac-intel.sh              # tam yığın: PostgreSQL + API + frontend
#   ./run-mac-intel.sh --frontend   # SADECE frontend testleri + derleme
#                                   # (Docker ve .NET gerekmez — en hızlı doğrulama)
#
# Ne yapar (tam yığın):
#   1) Docker, .NET 8 SDK ve Node 20+ var mı kontrol eder
#   2) .env yoksa .env.example'dan oluşturur
#   3) PostgreSQL 16'yı Docker ile başlatır ve hazır olmasını bekler
#   4) Migration + demo veri (seed) uygular
#   5) API'yi :5000'de arka planda, frontend'i :3000'de ön planda başlatır
#
# Ctrl+C ile durdurur; arka plandaki API süreci de kapanır.
# Veritabanı konteyneri AÇIK KALIR (veriyi kaybetmemek için) —
# kapatmak için: docker compose down

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

BOLD="$(tput bold 2>/dev/null || true)"; RESET="$(tput sgr0 2>/dev/null || true)"
GREEN="$(tput setaf 2 2>/dev/null || true)"; YELLOW="$(tput setaf 3 2>/dev/null || true)"
RED="$(tput setaf 1 2>/dev/null || true)"; DIM="$(tput dim 2>/dev/null || true)"

info() { echo "${BOLD}==>${RESET} $*"; }
ok()   { echo "${GREEN}✓${RESET} $*"; }
warn() { echo "${YELLOW}UYARI:${RESET} $*"; }
fail() { echo "${RED}${BOLD}HATA:${RESET} $*" >&2; exit 1; }

if [[ "$(uname -s)" == "Darwin" && "$(uname -m)" != "x86_64" ]]; then
  warn "Bu makine $(uname -m) görünüyor; ./run-mac-apple-silicon.sh önerilir."
  echo
fi

need_node() {
  command -v node >/dev/null 2>&1 || fail "Node.js bulunamadı.
  https://nodejs.org (Intel Mac için macOS x64 .pkg) veya: brew install node@20"
  local major; major="$(node -v | sed 's/^v//' | cut -d. -f1)"
  [ "$major" -ge 20 ] || fail "Node $(node -v) bulundu, 20+ gerekiyor.
  nvm varsa: nvm install 20 && nvm use 20"
  ok "Node.js $(node -v)"
}

# ---------------------------------------------------------------- frontend-only
if [[ "${1:-}" == "--frontend" ]]; then
  echo "${BOLD}Yalnızca frontend${RESET} — Docker ve .NET gerekmez."
  echo
  need_node
  cd frontend
  if [[ ! -d node_modules ]]; then
    info "Bağımlılıklar kuruluyor (npm install)..."
    npm install --no-audit --no-fund
  else
    ok "node_modules mevcut — kurulum atlandı."
  fi
  info "Birim testleri (npm test)..."
  npm test
  info "Üretim derlemesi (npm run build)..."
  npm run build
  echo
  ok "Frontend doğrulandı. Tam yığın için argümansız çalıştırın: ./run-mac-intel.sh"
  exit 0
fi

# ------------------------------------------------------------------- tam yığın
info "Gereksinimler kontrol ediliyor..."
need_node

command -v docker >/dev/null 2>&1 || fail "Docker bulunamadı.
  Docker Desktop kurun (Intel Mac sürümü): https://docker.com/products/docker-desktop
  Sadece frontend'i denemek isterseniz: ./run-mac-intel.sh --frontend"
docker info >/dev/null 2>&1 || fail "Docker kurulu ama çalışmıyor.
  Docker Desktop uygulamasını açın ve tekrar deneyin."
ok "Docker çalışıyor"

command -v dotnet >/dev/null 2>&1 || fail ".NET SDK bulunamadı.
  .NET 8 SDK kurun — Intel Mac için ${BOLD}x64${RESET} yapısını seçin (Arm64 DEĞİL):
  https://dotnet.microsoft.com/download/dotnet/8.0
  Sadece frontend'i denemek isterseniz: ./run-mac-intel.sh --frontend"
ok ".NET SDK $(dotnet --version)"

# .env
if [[ ! -f .env ]]; then
  [[ -f .env.example ]] || fail ".env.example bulunamadı."
  cp .env.example .env
  ok ".env, .env.example'dan oluşturuldu."
  warn "İçindeki POSTGRES_PASSWORD varsayılan değerdedir — yerel kullanım için yeterli."
else
  ok ".env mevcut, dokunulmadı."
fi

# PostgreSQL
info "PostgreSQL 16 başlatılıyor (docker compose up -d)..."
docker compose up -d
info "Veritabanının hazır olması bekleniyor..."
for i in $(seq 1 30); do
  if docker compose exec -T db pg_isready >/dev/null 2>&1; then ok "PostgreSQL hazır."; break; fi
  [ "$i" -eq 30 ] && fail "PostgreSQL 30 saniyede hazır olmadı. Bakın: docker compose logs db"
  sleep 1
done

# migrate + seed
info "Migration + demo veri uygulanıyor (seed)..."
dotnet run --project backend/src/HabitTracker.Api -- seed
ok "Veritabanı hazırlandı."
echo "  ${DIM}Demo giriş: demo@habittracker.local / Demo1234!${RESET}"

# frontend deps
if [[ ! -d frontend/node_modules ]]; then
  info "Frontend bağımlılıkları kuruluyor..."
  (cd frontend && npm install --no-audit --no-fund)
fi

# API arka planda
API_LOG="${TMPDIR:-/tmp}/habittracker-api.log"
API_PID=""
cleanup() {
  if [[ -n "$API_PID" ]] && kill -0 "$API_PID" 2>/dev/null; then
    info "API süreci durduruluyor (PID $API_PID)..."
    kill "$API_PID" 2>/dev/null || true
    wait "$API_PID" 2>/dev/null || true
  fi
  echo
  echo "  ${DIM}Veritabanı konteyneri açık bırakıldı. Kapatmak için: docker compose down${RESET}"
}
trap cleanup EXIT INT TERM

info "API başlatılıyor (http://localhost:5000)..."
dotnet run --project backend/src/HabitTracker.Api --urls http://localhost:5000 >"$API_LOG" 2>&1 &
API_PID=$!
sleep 4
kill -0 "$API_PID" 2>/dev/null || { echo "----- $API_LOG -----"; cat "$API_LOG"; fail "API başlatılamadı."; }
ok "API çalışıyor (PID $API_PID). Günlük: $API_LOG"

echo
ok "Frontend başlatılıyor: ${BOLD}http://localhost:3000${RESET}"
echo "  ${DIM}Demo giriş: demo@habittracker.local / Demo1234!${RESET}"
echo "  ${DIM}(Ctrl+C ile durdurun — API süreci de kapanır.)${RESET}"
echo
cd frontend
exec npm run dev
