# Durum Raporu — coursera-test (Alışkanlık Takibi / Habit Tracker)

**Denetim tarihi:** 2026-09-07 · **Depo:** https://github.com/yokbi/coursera-test
**Varsayılan dal:** `main`

> **Depo adı yanıltıcı.** `coursera-test` bir kurs alıştırması gibi duruyor;
> içindeki proje aslında tam teşekküllü, çok katmanlı bir **alışkanlık takip
> uygulaması**. → `YAPILACAKLAR.md` C1

---

## 1. Özet

| | |
|---|---|
| Proje | Alışkanlık Takibi — Türkçe arayüz, İngilizce kod tabanı |
| Frontend | **Next.js 16.2.10** (App Router) · React 19.2.4 · TypeScript strict · Tailwind CSS 4 |
| Backend | **.NET 8 Web API** · Clean Architecture · EF Core + Npgsql · FluentValidation · Serilog |
| Veritabanı | PostgreSQL 16 (Docker) |
| Test | xUnit (birim + Testcontainers entegrasyon) · Vitest + RTL · Playwright |
| Olgunluk | **Yüksek.** Kimlik doğrulama, arkadaşlık, gruplar, yönetim paneli, e-posta hatırlatıcı — hepsi yazılmış |

---

## 2. Doğrulanan çalışma kanıtı

Bu denetimde **gerçekten çalıştırılanlar** (Node v22.22.2, Linux):

### ✅ Frontend birim testleri — 47/47 geçti

```
$ cd frontend && npm install && npm test
 RUN  v4.1.10
 Test Files  8 passed (8)
      Tests  47 passed (47)
   Duration  5.95s
```

### ✅ Frontend üretim derlemesi — başarılı

```
$ npm run build
Route (app)
┌ ○ /                      ├ ○ /arkadaslar     ├ ○ /giris/google
├ ○ /_not-found            ├ ○ /ayarlar        ├ ○ /gruplar
├ ○ /aliskanliklar         ├ ○ /giris          ├ ƒ /gruplar/[id]
├ ƒ /aliskanliklar/[id]    ├ ○ /kayit          └ ○ /yonetim
ƒ Proxy (Middleware)
```

12 rota derlendi. TypeScript strict tip kontrolü ve ESLint bu derlemenin
parçası olarak geçti.

### ❌ Doğrulanamayanlar — bu ortamda .NET SDK ve Docker yok

```
$ dotnet --version
bash: dotnet: command not found
```

Bu yüzden **yapılamayanlar**:
- Backend derlenmedi (`dotnet build`)
- xUnit birim testleri (13 test dosyası) çalıştırılmadı
- Testcontainers entegrasyon testleri çalıştırılmadı (Docker gerekir)
- PostgreSQL başlatılmadı, migration uygulanmadı, seed çalıştırılmadı
- Uçtan uca (Playwright) testler çalıştırılmadı — çalışan API gerektirir

**Dolayısıyla kurulabilecek cümle:** *"Frontend derleniyor ve testleri geçiyor;
backend'in durumu bu ortamda ölçülmedi."* Yarın Intel Mac'te .NET 8 SDK ve
Docker kurulu olacağı için asıl doğrulama orada yapılabilir.

---

## 3. README doğruluk kontrolü

Mevcut `README.md` iyi yazılmış. İddiaları tek tek kontrol edildi:

| README iddiası | Kontrol | Sonuç |
|---|---|:---:|
| Next.js 16 | `package.json` → `16.2.10` | ✅ |
| React 19 · Tailwind 4 · TS strict | `19.2.4`, `^4`, `tsconfig` | ✅ |
| .NET 8 Clean Architecture (Domain/Application/Infrastructure/Api) | 4 `.csproj` mevcut, tam bu adlarla | ✅ |
| xUnit birim + entegrasyon testleri | `HabitTracker.UnitTests` + `HabitTracker.IntegrationTests` | ✅ |
| Vitest + RTL | 8 test dosyası, 47 test, hepsi geçiyor | ✅ |
| Playwright | `frontend/e2e/habit-tracker.spec.ts` | ✅ |
| `docs/API.md · SECURITY.md · DECISIONS.md` | Üçü de mevcut | ✅ |
| `.env.example` var, depoda sır yok | Mevcut (1207 bayt) | ✅ |
| PostgreSQL 16 Docker | `docker-compose.yml` → `postgres:16-alpine` | ✅ |

**README doğrudur.** Yeniden yazılmadı; yalnızca yanına bu rapor, yapılacaklar
listesi ve çalıştırma betikleri eklendi.

---

## 4. Dal envanteri

| Dal | `main`'in önünde | Durum |
|---|---:|---|
| `main` | — | Varsayılan, tüm iş burada |
| `claude/habit-tracker-autonomous-build-6nbfnl` | 0 | PR #7 ile birleştirildi, `main` ile aynı |
| `feature/lesson` | 0 | `main` ile aynı — saklı iş yok |
| `claude/repo-audit-docs-e1dail` | — | Bu doküman turu |

Ölçüm: her dal için `git rev-list --count origin/main..<dal>` → **0**.
**Hiçbir dalda birleştirilmemiş iş yoktur.**

---

## 5. Bulgular

### 🟡 C1 — Depo adı içeriğiyle uyuşmuyor
`coursera-test` adı bir kurs alıştırması izlenimi veriyor; içeride tam bir
üretim düzeyi uygulama var. Depo listenizde bu proje gözden kaçıyor.
→ `YAPILACAKLAR.md` C1

### 🟡 C2 — Backend bu ortamda hiç doğrulanmadı
Yazılmış ama bu turda derlenmedi/çalıştırılmadı (.NET SDK yok). Yarın ilk iş
bu olmalı. → C2

### 🟢 C3 — CI yok
`.github/workflows/` bulunmuyor. Frontend testleri hızlı (6 sn) ve değerli;
en azından onlar PR'larda koşabilir. → C3

### 🟢 C4 — Docker Compose yalnızca veritabanını kaldırıyor
`docker-compose.yml` sadece PostgreSQL içeriyor; API ve frontend elle
başlatılıyor. Tek komutla tam yığın ayağa kalkmıyor. → C4

**Güvenlik:** `.env.example` mevcut, `docker-compose.yml` parolayı ortam
değişkeninden alıyor (`dev_only_password` yalnızca varsayılan) ve `docs/SECURITY.md`
yazılmış. Depoda düz metin sır tespit edilmedi. Ayrıntılı güvenlik denetimi
bu turun kapsamı dışındaydı.

---

## 6. Yarınki test için (Intel Mac) — sıralı plan

### Ön gereksinimler
```bash
# Docker Desktop (Intel Mac sürümü)  -> https://docker.com/products/docker-desktop
# .NET 8 SDK (macOS x64!)            -> https://dotnet.microsoft.com/download/dotnet/8.0
# Node 20+                           -> brew install node@20
```
> **Intel Mac uyarısı:** .NET SDK'nın **x64** yapısını indirin, Arm64'ü değil.

### En hızlı doğrulama (Docker/.NET beklemeden, ~2 dakika)
```bash
git clone https://github.com/yokbi/coursera-test
cd coursera-test/frontend
npm install && npm test        # 47 test — burada geçtiği doğrulandı
npm run build                  # 12 rota — burada geçtiği doğrulandı
```

### Tam yığın
```bash
./run-mac-intel.sh
```
Betik sırayla: gereksinimleri kontrol eder → `.env` oluşturur → PostgreSQL'i
başlatır → migration + seed çalıştırır → API'yi (`:5000`) arka planda kaldırır →
frontend'i (`:3000`) ön planda başlatır.

Demo giriş (seed sonrası): `demo@habittracker.local` / `Demo1234!`

**Asıl bakılacak:** Backend derleniyor mu, `dotnet test` geçiyor mu (C2).

---

Kalan işler: [`YAPILACAKLAR.md`](YAPILACAKLAR.md) · Genel bakış: [`README.md`](README.md)
Mimari kararlar: [`docs/DECISIONS.md`](docs/DECISIONS.md) · API: [`docs/API.md`](docs/API.md)
