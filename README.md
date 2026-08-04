# Mini Sipariş Uygulaması

Ürün listeleme/arama ve sipariş oluşturma yapan küçük bir web uygulaması.

- **Backend:** .NET 8 Web API, Entity Framework Core, SQLite, IMemoryCache
- **Frontend:** React 19 + TypeScript (Vite), react-router-dom
- **Test:** xUnit + FluentAssertions (in-memory SQLite üzerinde)

---

## Uygulama nasıl çalıştırılır?

Gereksinimler: .NET 8 SDK, Node.js 18+.

### 1) Backend

```bash
cd backend/src/OrderApp.Api
dotnet run
```

- API: `http://localhost:5116`
- Swagger UI: `http://localhost:5116/swagger`

Uygulama açılışta migration'ları otomatik uygular (`Database.MigrateAsync`) ve veritabanı boşsa
6 örnek ürün ekler. Ayrıca ayrı bir kurulum adımı gerekmez; SQLite dosyası (`orderapp.db`)
kendiliğinden oluşur.

> Örnek verilerden biri (`WC-5001` 1080p Webcam) bilerek **0 stokla** eklenir; yetersiz stok
> senaryosunu elle denemek için kullanılabilir.

### 2) Frontend

```bash
cd frontend
npm install
npm run dev
```

- Uygulama: `http://localhost:5173`

API adresi `frontend/.env` içindeki `VITE_API_BASE_URL` ile ayarlanır (varsayılan `http://localhost:5116`).
Backend'in CORS ayarı `appsettings.json` → `Cors:AllowedOrigins` içinde `http://localhost:5173`e izin verir.

### 3) Testler

```bash
cd backend
dotnet test
```

### 4) Migration'ları elle yönetmek (opsiyonel)

```bash
dotnet tool install --global dotnet-ef
cd backend
dotnet ef database update --project src/OrderApp.Api --startup-project src/OrderApp.Api
```

---

## API

| Metot | Endpoint | Açıklama |
|---|---|---|
| GET | `/api/products` | Ürünleri listeler |
| GET | `/api/products?search=klavye` | İsim **veya** stok kodunda arar |
| GET | `/api/products/{id}` | Ürün detayı |
| POST | `/api/orders` | Sipariş oluşturur (stokları düşer) |
| GET | `/api/orders` | Siparişleri listeler (en yeniden eskiye) |
| GET | `/api/orders/{id}` | Sipariş detayı (satırlarıyla) |

Örnek istek:

```json
POST /api/orders
{
  "customerName": "Example Customer",
  "pricingType": "Bulk",
  "items": [
    { "productId": 1, "quantity": 6 },
    { "productId": 2, "quantity": 4 }
  ]
}
```

Status kodları: `201` (oluşturuldu), `400` (validation / iş kuralı), `404` (kayıt yok),
`409` (eşzamanlı stok çakışması), `500` (beklenmeyen).

Hatalar `ProblemDetails` formatında döner. İş kuralı hatalarında satır bazlı sebepler
`reasons` alanında taşınır ve React tarafında liste olarak gösterilir:

```json
{
  "title": "Islem gerceklestirilemedi",
  "status": 400,
  "detail": "Yetersiz stok nedeniyle siparis olusturulamadi.",
  "instance": "/api/orders",
  "traceId": "0HNNI3ED6UUB8:00000002",
  "reasons": ["1080p Webcam (WC-5001) icin yeterli stok yok. Talep: 2, mevcut: 0."]
}
```

### `pricingType` hakkında

Case'deki örnek istekte `pricingType` alanı var fakat dokümanda bu alan için bir hesaplama
kuralı tanımlanmamış. Kendi kuralımı uydurmak yerine alanı **opsiyonel** kabul ettim,
`Standard`/`Bulk` değerlerine karşı doğruladım ve siparişle birlikte sakladım; fiyatı etkilemiyor.
Bir indirim kuralı tanımlandığında `OrderService` içinde tek bir noktada uygulanabilir.

---

## Problemi hangi parçalara ayırdım?

1. **Ürün okuma** – listeleme, arama, detay. Yazma yok, bu yüzden tamamen cache'lenebilir.
2. **Sipariş yazma** – asıl iş kurallarının olduğu yer: doğrulama, stok kontrolü, stok düşümü,
   fiyat snapshot'ı, transaction.
3. **Sipariş okuma** – liste ve detay; sadece kayıtlı veriyi okur, yeniden hesaplama yapmaz.
4. **Çapraz kesen konular** – hata → `ProblemDetails` çevirisi (middleware), cache invalidation,
   migration + seed.
5. **Frontend** – üç ekran (ürünler / yeni sipariş / siparişler) + ortak API istemcisi ve
   loading-error-empty durumlarını gösteren küçük bileşenler.

## Database modelini neden bu şekilde oluşturdum?

Üç tablo: `Products`, `Orders`, `OrderItems` (klasik master-detail).

- **`OrderItem` ürünün kopyasını tutar.** `UnitPrice`, `ProductName`, `ProductStockCode` sipariş
  anında satıra kopyalanır. En kritik gereksinim "ürün fiyatı değişse bile geçmiş siparişin tutarı
  değişmemeli" olduğu için sipariş, `Product` tablosuna *okuma bağımlılığı* taşımamalı.
  `ProductId` FK'si yine duruyor (raporlama/izlenebilirlik için) ama tutar hesabında kullanılmıyor.
- **`LineTotal` ve `Order.TotalAmount` kaydediliyor (denormalize).** Sipariş tutarı bir kez
  hesaplanan ve bir daha değişmemesi gereken bir değer; her okumada yeniden hesaplamak hem
  gereksiz hem de "tarihsel doğruluk" açısından riskli.
- **Para alanları `decimal(18,2)`** (`HasPrecision(18,2)`). `double`/`float` yuvarlama hatası
  ürettiği için para değerlerinde kullanılmadı.
- **`Product.StockCode` unique index**; arama bu index'ten faydalanıyor. `Name` üzerinde de index var.
- **`Product.Version`** optimistic concurrency token — aşağıdaki veri bütünlüğü bölümünde açıklandı.
- **Check constraint'ler**: fiyat ve stok negatif olamaz, sipariş miktarı > 0.
  (Not: SQLite decimal'i TEXT olarak sakladığından fiyat kontrolü `CAST("Price" AS REAL) >= 0`
  şeklinde yazıldı; düz `"Price" >= 0` yazılırsa metin/sayı karşılaştırması nedeniyle constraint
  sessizce hep `true` döner.)
- **`OrderItem` → `Product` ilişkisi `Restrict`**, `Order` → `OrderItem` ilişkisi `Cascade`.
  Sipariş silinirse satırları da silinir; ürün silinmesi geçmiş siparişi bozamaz.

**SQLite tercihi:** case'de serbest bırakılmış ve dosya tabanlı olduğu için değerlendiren kişinin
hiçbir kurulum yapmadan `dotnet run` diyebilmesini sağlıyor. Transaction ve check constraint
desteklediği için iş kurallarını gerçekten test edebiliyoruz.

## Kod organizasyonunu neden bu şekilde tercih ettim?

**Feature-based klasörleme + controller/service ayrımı:**

```
src/OrderApp.Api/
├── Common/          ExceptionHandlingMiddleware, BusinessRuleException, NotFoundException
├── Data/            AppDbContext, DatabaseSeeder, Migrations/
└── Features/
    ├── Products/    Product, ProductService, ProductCache, ProductsController, ProductDtos
    └── Orders/      Order, OrderItem, PricingType, OrderService, OrdersController, OrderDtos
```

- Bir işi değiştirirken tek klasörde çalışıyorum; `Models/`, `Services/`, `Repositories/` diye
  yatay kesmek bu boyutta sadece dosya arasında gezinme maliyeti yaratırdı.
- **Controller'lar ince**: istek alır, servisi çağırır, HTTP sonucu döner. Hiçbir iş kuralı,
  `try/catch` veya stok kontrolü controller'da değil.
- **Gereksiz abstraction yok**: servisler için `IProductService`/`IOrderService` arayüzü ve repository
  katmanı eklemedim. Tek implementasyon var, `DbContext` zaten Unit of Work + Repository görevi
  görüyor ve testler gerçek SQLite üzerinde çalıştığı için mock'a ihtiyaç duymuyorum. Servisler yine
  DI ile (`AddScoped`) kayıtlı; ileride arayüz gerekirse tek satırlık değişiklik.
- Case CQRS/MediatR/Clean Architecture beklemediğini açıkça belirttiği için bilinçli olarak
  eklemedim.
- Tüm I/O metotları `async` ve `CancellationToken` alıyor; token controller'dan servise, oradan
  EF Core sorgusuna kadar taşınıyor.

React tarafı da benzer şekilde: `api/` (istemci + tipler), `hooks/` (`useProducts`),
`pages/` (ekranlar), `components/` (Loading / Error / Empty), `lib/` (para ve tarih formatlama).

## Sipariş ve stok işlemlerinde veri bütünlüğünü nasıl sağladım?

`OrderService.CreateOrderAsync` içinde:

1. **Tek transaction** – `BeginTransactionAsync` ile başlar; sipariş kaydı ve tüm stok düşümleri
   tek `SaveChangesAsync` çağrısında yazılır, sonra `CommitAsync`.
2. **Önce tüm satırlar kontrol edilir, sonra yazılır** – herhangi bir üründe stok yetmiyorsa
   `BusinessRuleException` fırlatılır; `SaveChangesAsync` hiç çağrılmadığı için hiçbir stok
   değişikliği veritabanına gitmez ve transaction commit edilmeden `using` ile rollback olur.
   Hata mesajı *hangi üründe ne kadar eksik olduğunu* satır satır döner.
3. **Kısmi sipariş yok** – ürünlerden biri bulunamazsa veya stok yetmezse sipariş hiç oluşmaz.
4. **Optimistic concurrency** – `Product.Version` bir concurrency token. Stok her düştüğünde
   `Product.TryReduceStock` içinde artırılır, EF `UPDATE ... WHERE Id=@id AND Version=@version`
   üretir. İki eşzamanlı istek aynı stoğu okuyup ikisi birden düşemez; kaybeden istek
   `DbUpdateConcurrencyException` alır, middleware bunu `409 Conflict` + anlaşılır mesaja çevirir.
   Böylece "iki kullanıcı aynı anda son ürünü sipariş etti" senaryosunda stok eksiye düşmez.
5. **Domain kuralı entity içinde** – stok düşürme `Product.TryReduceStock(quantity)` metodunda;
   "yeterli mi?" kontrolü ile "düş" işlemi ayrı yerlere dağılmıyor.
6. **Aynı ürün birden fazla satırda gelirse** miktarlar tek satırda toplanır (`NormalizeItems`);
   aksi halde stok kontrolü satır satır geçip toplamda stoğu aşabilirdi.

Ayrıca DB seviyesinde `StockQuantity >= 0` ve `Quantity > 0` check constraint'leri son savunma hattı.

## Cache'i nerede ve neden kullandım?

Cache sadece **ürün okumalarında** kullanılıyor (`ProductService` → `ProductCache` → `IMemoryCache`).
Ürün listesi sipariş ekranında sürekli okunuyor, buna karşılık ancak sipariş verildiğinde değişiyor —
yani okuma/yazma oranı yüksek, cache'in en anlamlı olduğu yer burası. Siparişler cache'lenmiyor;
her istekte değişebilirler ve tutarsız veri göstermenin maliyeti okuma kazancından yüksek.

- **Cache key yapısı**
  - `products:all` – filtresiz liste
  - `products:search:{terim}` – aramaya göre liste (terim küçük harfe normalize edilir)
  - `products:id:{id}` – ürün detayı
- **Cache süresi:** 1 dakika absolute expiration. Stok değiştiğinde zaten anında temizlendiği için
  TTL'in görevi sadece "bir şekilde kaçırılan" invalidation'lara karşı güvenlik ağı olmak.
- **Cache'e ne yazılıyor:** entity değil, API'nin döndüğü `ProductResponse` DTO'su. Böylece cache'te
  EF change tracker'a bağlı nesneler durmuyor ve cache'lenen değer doğrudan response ile aynı.
- **Bulunamayan kayıt cache'lenmiyor** – aksi halde 404 sonucu TTL boyunca sabitlenirdi.

## Stok değiştiğinde cache'i nasıl yönettim?

Bütün ürün cache girdileri ortak bir `CancellationChangeToken` ile işaretleniyor. Sipariş başarıyla
commit edildikten sonra `OrderService`, `ProductCache.InvalidateAll()` çağırıyor; bu da token'ı iptal
edip yenisiyle değiştiriyor ve **ürünle ilgili tüm key'ler tek hamlede düşüyor**.

Alternatif olarak sadece etkilenen ürünün key'ini silebilirdim; ancak `products:all` ve
`products:search:*` girdilerinde de o ürünün stoğu görünüyor. "Hangi arama terimleri bu ürünü
içeriyordu?" sorusunu takip etmek, ürün verisinin nadiren değiştiği bu senaryoda kazancından
fazla karmaşıklık getirirdi. Invalidation **commit sonrasında** yapılıyor; sipariş başarısız olursa
cache'e dokunulmuyor.

Frontend tarafında da sipariş sonrası ürün listesi yeniden çekiliyor, böylece ekrandaki stok
bilgisi güncel kalıyor.

## Testler

`backend/tests/OrderApp.Tests` – gerçek SQLite (`Data Source=:memory:`) üzerinde, InMemory provider
yerine, çünkü transaction ve constraint davranışını da doğrulamak istedim. Her test kendi izole
veritabanını kuruyor.

| Test | Doğruladığı davranış |
|---|---|
| `CreateOrder_StoklariDusurur_VeToplamiDogruHesaplar` | Stoklar doğru azalır, toplam tutar doğru hesaplanır |
| `CreateOrder_YetersizStokta_SiparisOlusturmaz_VeHicbirStogu_Dusurmez` | Bir üründe stok yetmezse sipariş oluşmaz ve **diğer ürünlerin stoğu da değişmez** |
| `CreateOrder_UrunFiyatiSonradanDegisse_Bile_SiparisTutariDegismez` | Fiyat snapshot'ı |
| `CreateOrder_OlmayanUrunIcin_BusinessRuleException_Firlatir` | Ürün varlık kontrolü |
| `CreateOrder_MiktarSifirVeyaNegatifse_Reddedilir` | Miktar > 0 kuralı |
| `CreateOrder_AyniUrunBirdenFazlaSatirdaGelirse_MiktarlarBirlestirilir` | Satır birleştirme |
| `Arama_IsimVeyaStokKoduna_Gore_Filtreler` | İsim/stok kodu araması |
| `SiparisSonrasi_CacheTemizlenir_VeGuncelStokDoner` | Sipariş sonrası cache invalidation |

## Süre nedeniyle tamamlamadığım / sadeleştirdiğim noktalar

- **Sayfalama yok.** Ürün ve sipariş listeleri tek seferde dönüyor. Gerçek veri hacminde
  `skip/take` + toplam sayı gerekirdi.
- **Concurrency çakışmasında otomatik retry yok.** `409` dönüp kullanıcıdan tekrar denemesini
  istiyorum; küçük bir retry döngüsü eklenebilirdi ama davranışı görünür tutmayı tercih ettim.
- **Sipariş iptali / stok iadesi yok** — case kapsamında değil.
- **Integration test (WebApplicationFactory) yazmadım.** İş kuralları servis seviyesinde test edildi;
  HTTP katmanı Swagger üzerinden ve tarayıcıdan manuel doğrulandı. `Program.cs` içindeki
  `public partial class Program` bunu ileride eklemek için hazır bırakıldı.
- **Frontend'de global state yönetimi ve test yok.** Ekranlar birbirinden bağımsız veri çektiği için
  Redux/React Query gerekmedi; `useProducts` hook'u paylaşılan tek mantık.
- **Kimlik doğrulama, ürün ekleme/silme yok** — case'de gerekli değil denmişti.
- **Görsel tasarım minimum** tutuldu (ana kriter olmadığı belirtilmişti); loading / hata / boş
  durum geri bildirimlerine öncelik verildi.
- **Docker eklenmedi**; SQLite sayesinde zaten ek altyapı gerekmiyor.

## Hangi AI araçlarını kullandım?

Claude Code (Anthropic) kullandım; iskelet kurulumu, tekrar eden CRUD/DTO kodu, CSS ve README
taslağı için hızlandırıcı olarak. Mimari kararlar (feature-based yapı, snapshot'lı `OrderItem`,
concurrency token, `CancellationChangeToken` ile toplu invalidation) benim tercihlerim.

## AI tarafından üretilen kodları nasıl kontrol ettim?

- **Çalıştırarak doğruladım.** Backend'i ayağa kaldırıp tüm endpoint'leri başarılı ve başarısız
  senaryolarla denedim (yetersiz stok, olmayan ürün, boş sepet, miktar 0, geçersiz `pricingType`,
  olmayan sipariş id'si) ve dönen status code + `ProblemDetails` gövdelerini tek tek kontrol ettim.
- **İş kurallarını testle sabitledim** — özellikle "yetersiz stokta hiçbir stok düşmemeli" ve
  "fiyat sonradan değişince tutar değişmemeli" senaryolarını.
- **Frontend'i tarayıcıda uçtan uca denedim**: arama, ürün seçme, miktar girme, sipariş oluşturma,
  sipariş listesi ve detay ekranı. Bu sırada iki gerçek hata bulup düzelttim:
  1. Form üzerinde tarayıcının native constraint validation'ı (miktar > `max`) submit'i sessizce
     engelliyor ve ekranda eski hata mesajı kalıyordu → form `noValidate` yapıldı, doğrulama tek
     yerde toplandı.
  2. Ürün fiyatı için yazılan `"Price" >= 0` check constraint'i, SQLite decimal'i TEXT sakladığı
     için hiçbir zaman ihlal edilemiyordu (metin/sayı karşılaştırması) → `CAST(... AS REAL)` ile
     düzeltildi.
- **Üretilen kodu satır satır okudum**; EF sorgularının SQL'e çevrilebilir olduğunu (ör. `Select`
  içinde statik metot çağrısı yerine constructor projeksiyonu), `async` kullanımının doğru
  olduğunu ve cache'e null yazılmadığını kontrol ettim.

## Çalışmaya yaklaşık ne kadar zaman ayırdım?

Yaklaşık 4-5 saat: tasarım ve model kararları, backend, frontend, testler ve manuel doğrulama dahil.
