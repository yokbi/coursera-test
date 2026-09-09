# Yapılacaklar — coursera-test (Alışkanlık Takibi)

Öncelik: 🔴 kritik · 🟡 orta · 🟢 düşük

**Kod tarafında yarım kalmış bir özellik tespit edilmedi.** Aşağıdaki maddeler
doğrulama, altyapı ve depo düzeni ile ilgilidir.

---

## Kapananlar (2026-09-09)

Üç madde kapandı ve **hiçbiri elle koşturmayı gerektirmedi** — `main` üzerindeki
CI koşusu üçünü de zaten kanıtlıyordu. Denetim turu bu koşuyu görmemiş.

Kanıt: [CI koşusu #13](https://github.com/yokbi/coursera-test/actions/runs/34157063139),
`main` @ `246f9f3`, 2026-09-07 19:49–19:51 UTC, **üç işin üçü de yeşil**.

| Madde | Ne oldu |
|---|---|
| **C2** Backend'i derle ve testlerini koştur | ✅ CI'daki `Backend` işi: `dotnet restore` → `dotnet build --configuration Release` → **birim testleri** → **entegrasyon testleri** (Testcontainers ile gerçek PostgreSQL) → **kapsam kapısı** (Domain + Application ≥ %80). Hepsi geçti. Backend'in derlendiği ve testlerinin geçtiği artık ölçülü bir gerçek. |
| **C3** CI ekleyin | ✅ **Madde hatalıydı.** `.github/workflows/ci.yml` 2026-08-08'den beri mevcut ve önerilenden çok daha kapsamlı: üç iş (backend, frontend, e2e), her push ve PR'da. Denetim raporu dosyanın yokluğunu yanlış tespit etmiş. |
| **C5** Playwright uçtan uca testleri | ✅ CI'daki `E2E` işi Playwright paketini gerçek yığına karşı koşuyor: PostgreSQL servis konteyneri, API kendi kendine başlıyor ve Development'ta migration uyguluyor, ardından Chromium. Geçti. |

Bu ortamda (uzak oturum) **.NET SDK kurulamıyor** — `dot.net` indirme adresi
ağ politikası tarafından engelli — ve Docker daemon yok. Yani backend burada
elle derlenemez; doğrulama CI üzerinden yapılır. Kendi makinende koşturmak
istersen komutlar aşağıdaki C4 bölümünün üstündeki eski C2 metnindeydi; özü:
`cd backend && dotnet build && dotnet test tests/HabitTracker.UnitTests`,
entegrasyon testleri için Docker açık olmalı.

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

## C3 ❌ "CI ekleyin" — geri çekildi

Bu madde yanlıştı; yukarıdaki "Kapananlar" bölümüne bakın. `.github/workflows/ci.yml`
zaten var ve üç işi de koşuyor. Yanlış bir maddeyi sessizce silmek yerine burada
bırakıyorum: raporun neyi kaçırdığı, raporu okuyan için maddenin kendisinden
daha bilgilendirici.

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

## C5 ✅ Playwright uçtan uca testleri — CI'da koşuyor

Yukarıdaki "Kapananlar"a bakın: `E2E` işi her push ve PR'da gerçek yığına karşı
koşuyor ve `main`'de yeşil. Elle koşturmak isteyen için:

```bash
cd frontend
npx playwright install chromium
npm run test:e2e
```

---

## C6 🟡 Doğrulanmamış özellikler — üçü artık testli, ikisi hâlâ açık

Denetimin "kanıt yok" dediği beş maddeyi koda ve testlere karşı yeniden
okudum. Üçünün kanıtı zaten depoda duruyordu:

| Madde | Durum | Nerede |
|---|---|---|
| **Yönetim paneli yetki sınırı** | ✅ Testli — ve iddia doğru | `backend/tests/HabitTracker.IntegrationTests/AdminTests.cs`: anonim çağrı reddediliyor (`AdminEndpoints_RejectAnonymousCallers`), sıradan kullanıcı **403** alıyor (`…RejectOrdinaryUsersWith403`), yetki yükseltmesi yeniden girişten önce geçerli olmuyor, ve en önemlisi `Admin_GetsNoWindowIntoAnotherUsersHabits` — yönetici başkasının alışkanlıklarını göremiyor. README'nin iddiası testle sabitlenmiş. |
| **Zaman dilimi duyarlı seri hesabı** | ✅ Testli | `tests/HabitTracker.UnitTests/Domain/TimezoneHelperTests.cs` ve `StreakCalculatorTests.cs` |
| **Hesap silme + anonimleştirme** | ✅ Testli, ve bu turda derinleştirildi (aşağıya bak) | `AuthFlowTests.DeleteAccount_SoftDeletesAndBlocksLoginAndTokens`; uygulama `AuthService.DeleteAccountAsync` |
| **E-posta hatırlatıcıları** | ⏳ Açık — gerçek SMTP gerekiyor | Kod `ReminderTests` ile test ediliyor ama gerçek bir sağlayıcıya tek posta gönderilmedi |
| **Google ile giriş** | ⏳ Açık — OAuth istemci kimliği gerekiyor | `GoogleAuthTests` akışı taklit ediyor; gerçek Google'a hiç gidilmedi |

### Silme derinleştirildi (karar verildi, 2026-09-09)

`DeleteAccountAsync` e-postayı `deleted-<id>@anonymized.invalid` yapıyor, parola
hash'ini geçersizleştiriyor, Google bağını koparıyor, arkadaşlıkları ve grup
üyeliklerini siliyor, refresh token'ları iptal ediyordu. **Ama alışkanlıklar ve
kayıtları duruyordu.**

Alışkanlık başlığı kullanıcının kendi hayatı hakkında yazdığı serbest metindir —
yani kişisel veri. `docs/SECURITY.md:37` "soft delete everywhere" diyordu; bu
satır ile KVKK'nın silme hakkı çakışıyordu. İki seçenek vardı: olduğu gibi
bırakıp gerekçeyi yazmak, ya da silmeyi derinleştirmek.

**Derinleştirildi.** Hesap silinince alışkanlıklar da siliniyor, check-in'ler
veritabanı seviyesinde onlardan cascade ediyor. Kullanıcı satırı hâlâ duruyor
(anonimleştirilmiş), böylece yabancı anahtarlar ve denetim geçmişi bozulmuyor.
`docs/SECURITY.md` bu tek istisnayı açıkça yazıyor artık, ve
`AuthFlowTests.DeleteAccount_TakesTheHabitsAndCheckInsWithIt` testi tutuyor.

---

## Öncelik sırası önerisi

1. **C1** — depoyu yeniden adlandır *(Settings'ten, 1 dakika)*
2. **C4** — Compose genişletmesi *(Docker gerektirir; bu ortamda daemon yok)*
3. Kalan iki doğrulama: gerçek SMTP ve gerçek Google OAuth — ikisi de senin
   anahtarını istiyor
