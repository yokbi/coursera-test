# Yapılacaklar — coursera-test (Alışkanlık Takibi)

Öncelik: 🔴 kritik · 🟡 orta · 🟢 düşük

**Kod tarafında yarım kalmış bir özellik tespit edilmedi.** Aşağıdaki maddeler
doğrulama, altyapı ve depo düzeni ile ilgilidir.

---

## C2 🔴 Backend'i bir kez derleyin ve testlerini koşturun

**Sorun:** Backend (.NET 8, 4 proje + 2 test projesi) bu denetimde **hiç
doğrulanmadı** — ortamda .NET SDK yoktu:

```
$ dotnet --version
bash: dotnet: command not found
```

Frontend'in derlendiği ve 47 testinin geçtiği doğrulandı; backend hakkında
söylenebilecek hiçbir şey yok.

**Yapılacak (yarın, Intel Mac'te, ~10 dakika):**

```bash
# .NET 8 SDK — macOS x64 yapısını indirin (Arm64 DEĞİL)
# https://dotnet.microsoft.com/download/dotnet/8.0
dotnet --version                 # 8.x görmelisiniz

cd backend
dotnet build                     # önce derlensin
dotnet test tests/HabitTracker.UnitTests    # birim testleri (Docker gerekmez)
```

Entegrasyon testleri **Testcontainers** kullanıyor, yani **Docker çalışıyor
olmalı**:

```bash
# Docker Desktop açıkken:
dotnet test tests/HabitTracker.IntegrationTests
```

**Sonucu bu dosyaya not edin.** C3 (CI) kararı buna bağlı — kırmızı testleri
CI'a bağlamanın anlamı yok.

---

## C1 🟡 Depoyu yeniden adlandırın

**Sorun:** Depo adı `coursera-test`. İçindeki proje bir kurs alıştırması değil:
Next.js 16 + .NET 8 Clean Architecture + PostgreSQL, kimlik doğrulama, arkadaşlık,
gruplar, yönetim paneli, e-posta hatırlatıcıları ve üç test katmanı olan tam bir
uygulama.

Depo listenizde bu proje "test" etiketiyle görünmez oluyor.

**Yapılacak:**
> GitHub → depo → **Settings** → *Repository name* → örn. `aliskanlik-takibi`
> veya `habit-tracker`

GitHub eski adres için otomatik yönlendirme kurar, ama yerel `git remote`
adresinizi güncellemeniz gerekir:

```bash
git remote set-url origin https://github.com/yokbi/habit-tracker
```

---

## C3 🟢 CI ekleyin (önce C2)

**Sorun:** `.github/workflows/` yok. Testler var ama kimse otomatik koşturmuyor.

**Yapılacak — kademeli, en ucuzdan başlayarak:**

**Adım 1 (bugün mantıklı):** Yalnızca frontend. Hızlı (yaklaşık 30 saniye) ve
bu denetimde geçtiği doğrulandı:

```yaml
# .github/workflows/frontend.yml
name: frontend
on: [pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    defaults: { run: { working-directory: frontend } }
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20', cache: 'npm', cache-dependency-path: frontend/package-lock.json }
      - run: npm ci
      - run: npm run lint
      - run: npm test
      - run: npm run build
```

**Adım 2 (C2 yeşilse):** Backend birim testleri (`actions/setup-dotnet@v4`).

**Adım 3 (isteğe bağlı):** Entegrasyon testleri — GitHub Actions runner'ında
Docker mevcut, yani Testcontainers çalışır; ama süre ve dakika tüketimi artar.

> **Not:** Actions dakikanız sınırlıysa Adım 1 ile başlayın; asıl kırılmaları
> yakalayan kısım orası olacaktır.

---

## C4 🟢 Tek komutla tam yığın (Docker Compose genişletmesi)

**Sorun:** `docker-compose.yml` yalnızca PostgreSQL'i kaldırıyor. API ve frontend
elle başlatılıyor — README'deki "quick start" beş komut.

Bu turda eklenen `run-mac-intel.sh` betiği bu beş adımı tek komuta indiriyor,
yani acil bir sorun değil. Ama gerçek "tek komut" için Compose'a `api` ve `web`
servisleri eklenebilir:

```yaml
  api:
    build: ./backend
    depends_on:
      db: { condition: service_healthy }
    ports: ["5000:8080"]
    environment:
      ConnectionStrings__Default: "Host=db;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
```

Bunun için `backend/Dockerfile` ve `frontend/Dockerfile` yazılması gerekir
(şu an ikisi de yok).

**Kazanç:** Yeni bir makinede kurulum "Docker kur + `docker compose up`"a iner.
**Maliyet:** İki Dockerfile + Compose bakımı.

---

## C5 🟢 Playwright uçtan uca testleri hiç koşturulmadı

`frontend/e2e/habit-tracker.spec.ts` yazılmış ama çalışan bir API + veritabanı
gerektirdiği için bu ortamda denenemedi.

**Yapılacak (C2'den sonra, tam yığın ayaktayken):**

```bash
cd frontend
npx playwright install chromium     # ilk seferde tarayıcıyı indirir
npm run test:e2e
```

Bu, uygulamanın uçtan uca gerçekten çalıştığının **tek gerçek kanıtı** olacak —
diğer test katmanları parçaları doğruluyor, bu bütünü doğruluyor.

---

## C6 🟢 Doğrulanmamış özellikler listesi

Kod okundu, ama aşağıdakilerin **çalıştığına dair kanıt yok**. Yarın elle
denenmesi önerilenler:

- [ ] E-posta hatırlatıcıları — gerçek bir SMTP/e-posta sağlayıcısı gerekiyor;
      hangisi yapılandırılmış, geliştirmede nereye düşüyor?
- [ ] Google ile giriş — OAuth istemci kimliği gerektirir; `.env.example`'da
      yeri var mı, yoksa bu akış yerelde hiç denenemez mi?
- [ ] Zaman dilimi duyarlı seri (streak) hesabı — sınır durumları (gece yarısı,
      yaz saati geçişi) test edilmiş mi?
- [ ] Hesap silme + anonimleştirme — verinin gerçekten gittiği doğrulanmalı
      (KVKK açısından önemli)
- [ ] Yönetim paneli yetki sınırı — README "yöneticiler içeriği değil hesapları
      yönetir, kimsenin alışkanlıklarını göremez" diyor. Bu **yetkilendirme
      iddiası** ve doğrulanması gerekir.

> Son madde en önemlisi: yetkilendirme iddiaları test edilmediği sürece
> iddiadır. `docs/SECURITY.md` bu konuda ne diyor, kontrol edin.

---

## Öncelik sırası önerisi

1. **C2** — backend'i derle ve testlerini koştur *(yarın ilk iş, ~10 dk)*
2. **C5** — tam yığın ayaktayken uçtan uca testi koştur
3. **C6** — yetkilendirme sınırını elle dene
4. **C1** — depoyu yeniden adlandır
5. **C3** — CI (önce sadece frontend)
6. **C4** — Compose genişletmesi
