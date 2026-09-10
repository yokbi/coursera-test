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

> **Düzeltme (2026-09-10).** Bu paragraf ".NET SDK bu ortamda kurulamıyor"
> diyordu. Doğru değil: `dot.net` indirme adresi gerçekten ağ politikasıyla
> engelli, ama dağıtımın kendi paket deposu değil —
> `apt-get update && apt-get install dotnet-sdk-8.0` 8.0.131'i kuruyor. Backend
> burada derlenebilir ve koşturulabilir; bu turda `/health` ucu tam da böyle
> doğrulandı. Engelli olan tek bir indirme adresinden "SDK kurulamıyor"
> sonucunu çıkarmak, denenmemiş bir varsayımdı.

Docker daemon ise gerçekten yok, yani imajlar ve compose burada koşturulamaz;
o taraf CI üzerinden doğrulanır. Kendi makinende koşturmak istersen özü:
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

## C4 ✅ Tek komutla tam yığın — yazıldı, CI derliyor

```bash
docker compose up --build
```

- `backend/Dockerfile` — SDK ile derleyip çalışma zamanı imajına yalnızca
  çıktıyı taşıyan iki aşama; kök olmayan `app` kullanıcısı.
- `frontend/Dockerfile` — `output: 'standalone'` sayesinde imaj bütün
  `node_modules` ağacını taşımıyor.
- `docker-compose.yml` — `db` (sağlıklı) → `api` → `web`.
- `.dockerignore` — ana makinenin `node_modules`/`bin`/`obj` çıktıları ve
  `.env` imaja girmiyor.

**Migration ayrı bir servis DEĞİL.** API başlangıçta uyguluyor (Development'ta
zaten öyleydi) ve bu yığında tek bir API kopyası var; ayrı bir servis kurulum
adımını ikiye bölmekten başka bir işe yaramazdı. Çok kopyalı bir dağıtımda bu
karar değişir.

**`NEXT_PUBLIC_API_URL` build argümanı**, çünkü Next onu derleme anında
gömüyor: bu bir dağıtım kararı, çalışma anı ayarı değil. Değer tarayıcıdan
görülebilen adres olmalı — `http://api:8080` konteyner ağında geçerli ama
tarayıcıda çözülmez.

**Doğrulama CI'da.** Bir Dockerfile okunarak doğrulanamaz; eksik bir `COPY` ya
da yanlış bir yol ancak derlemede ortaya çıkar. Yeni `docker` işi iki imajı da
derliyor **ve** yığını ayağa kaldırıp API'nin yanıt verdiğini görüyor — yanlış
bir `ENTRYPOINT` derlemede değil, ilk çalıştırmada patlar.

> Bu ortamda Docker daemon yok; imajlar burada derlenmedi. Derlemenin can alıcı
> iki adımı ayrı ayrı koşuldu: `dotnet publish` çıktı üretti ve
> `npm run build` `.next/standalone/server.js`'i gerçekten yazdı — Dockerfile'daki
> `COPY` yolları o yerleşime karşı kontrol edildi.

**İlk CI koşusu bir şey öğretti.** İki imaj da derlendi, ama yığın açılmadı:
compose `Jwt__SigningKey`'i boş bir değerle geçiyordu ve boş bir ortam
değişkeni `appsettings.Development.json`'daki geliştirme anahtarını **eziyor**.
API "anahtar yapılandırılmamış" diyerek çıktı — ki bu doğru davranış:
anahtarsız açılan bir API, herkesin imzalayabildiği bir API demek. Değişken
compose'dan kaldırıldı; geliştirmede anahtar appsettings'ten geliyor, üretimde
ortamdan verilmek zorunda. Bir Dockerfile'ın okunarak doğrulanamayacağının
canlı örneği.

**İkinci koşu bir tane daha öğretti.** Bu sefer API gerçekten açıldı, migration'ları
uyguladı ve 8080'i dinledi — ama iş yine düştü, çünkü hazır-mı yoklaması
`/swagger/index.html` adresini çağırıyordu ve **bu API'de Swagger hiç kurulu
değil**. Uç 404 döndü, döngü 90 saniye bekledi, iş "API yanıt vermedi" dedi;
oysa API sapasağlam ayaktaydı. Yoklama, var olduğu doğrulanmış bir uca
(`/health`, `Program.cs:172`) çevrildi. `/health` üstelik Npgsql üzerinden
veritabanına da bakıyor, yani yeşil dönmesi "API açıldı **ve** Compose ağında
db'ye ulaştı" demek.

Ders, ilkinin aynısının başka bir kılığı: yoklamayı yazarken uç var sayıldı,
kontrol edilmedi. İkisi de aynı sınıftan hata — kodu okumadan varsaymak — ve
ikisini de yalnızca gerçekten çalıştırmak yakaladı.

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
2. Kalan iki doğrulama: gerçek SMTP ve gerçek Google OAuth — ikisi de senin
   anahtarını istiyor
