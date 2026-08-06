# Mini Sipariş Uygulaması

.NET 8 Web API + EF Core + SQLite, React + TypeScript, xUnit.

## Çalıştırma

.NET 8 SDK ve Node 18+ yeterli, başka kurulum yok.

```bash
cd backend/src/OrderApp.Api
dotnet run
```

API `http://localhost:5116`, Swagger `/swagger` altında. Migration'lar açılışta
uygulanıyor, veritabanı boşsa 6 örnek ürün ekleniyor. SQLite dosyası kendiliğinden
oluşuyor.

```bash
cd frontend
npm install
npm run dev
```

Uygulama `http://localhost:5173`. API adresini değiştirmek gerekirse `frontend/.env`.

Testler için `cd backend && dotnet test`.

## Endpoint'ler

```
GET  /api/products
GET  /api/products?search=klavye     isim veya stok kodunda arar
GET  /api/products/{id}
POST /api/orders
GET  /api/orders
GET  /api/orders/{id}
```

Hatalar ProblemDetails formatında. Stok yetmediğinde hangi üründe ne kadar eksik
olduğunu `reasons` alanında satır satır dönüyorum, React tarafı bunu liste olarak
gösteriyor:

```json
{
  "title": "Islem gerceklestirilemedi",
  "status": 400,
  "detail": "Yetersiz stok nedeniyle siparis olusturulamadi.",
  "reasons": ["1080p Webcam (WC-5001) icin yeterli stok yok. Talep: 2, mevcut: 0."]
}
```

201 / 400 / 404 dönüyor, bir de eşzamanlı stok çakışmasında 409.

Örnek istekteki `pricingType` için dokümanda bir hesaplama kuralı yok. Kendim bir
indirim kuralı uydurmak istemedim, alanı opsiyonel yapıp `Standard`/`Bulk` diye
doğruladım ve siparişle beraber kaydettim. Fiyata dokunmuyor.

## Nasıl böldüm

Üç parça var: ürün okuma (liste/arama/detay, hiç yazma yok), sipariş yazma (asıl iş
kuralları burada) ve sipariş okuma (sadece kayıtlı veriyi döner, bir şey hesaplamaz).
Hata yönetimi ve cache temizleme bunların üstünde ortak duruyor.

## Veritabanı

`Products`, `Orders`, `OrderItems`. Klasik master-detail.

`OrderItem` ürünün fiyatını, ismini ve stok kodunu sipariş anında kendi içine
kopyalıyor. Fiyat değişse bile eski siparişin tutarı değişmemeli dendiği için
siparişin `Product` tablosuna okuma bağımlılığı olmaması gerekiyordu. `ProductId`
FK'si duruyor ama tutar hesabında kullanılmıyor. Aynı sebeple `LineTotal` ve
`TotalAmount` da kaydediliyor, her okumada yeniden hesaplamıyorum.

Para alanları decimal(18,2). Stok kodunda unique index var, arama da ondan
faydalanıyor. `Product.Version` optimistic concurrency token, aşağıda anlattım.
Negatif fiyat/stok ve sıfır miktar için check constraint koydum.

SQLite seçtim çünkü serbest bırakılmış ve dosya tabanlı olduğu için karşı taraf
hiçbir şey kurmadan `dotnet run` diyebiliyor. Transaction ve constraint desteklediği
için testler de gerçek veritabanı üzerinde çalışıyor.

## Kod organizasyonu

Feature bazlı: `Features/Products` ve `Features/Orders`, yanlarında `Common` (hata
tipleri + ProblemDetails middleware) ve `Data`. Bir işi değiştirirken tek klasörde
kalıyorum, bu boyutta `Models/` `Services/` diye yatay kesmenin faydası yok.

Controller'lar ince, isteği alıp servisi çağırıyorlar. Stok kontrolü veya try/catch
controller'da yok.

Servislere interface ve repository katmanı eklemedim. Tek implementasyon var,
DbContext zaten Unit of Work görüyor ve testleri gerçek SQLite üzerinde yazdığım için
mock'a ihtiyacım olmadı. DI'a yine kayıtlılar.

Frontend'i klasörlere bölmedim, dört ekran için `src/` altında düz duruyor.

## Sipariş ve stok

`OrderService.CreateOrderAsync` içinde her şey tek transaction ve tek SaveChanges.
Önce bütün satırları kontrol ediyorum, sonra yazıyorum: bir üründe stok yetmezse
exception fırlıyor, SaveChanges hiç çağrılmadığı için hiçbir stok değişikliği
veritabanına gitmiyor. Kısmi sipariş oluşmuyor.

İki kişi aynı anda son ürünü sipariş etmesin diye `Product.Version` concurrency token
koydum. Stok her düştüğünde artıyor, EF de `UPDATE ... WHERE Id = @id AND Version =
@version` üretiyor, eşleşmezse 409 dönüyorum.

Pratikte SQLite yazmaları zaten sıraya soktuğu için çoğu durumda sıra bekleyen istek
taze stoğu okuyup normal "yetersiz stok" hatasına düşüyor. Son 1 adet için 10 paralel
istek attığımda 1 tanesi 201, 9 tanesi 400 aldı ve stok tam 0'da kaldı. Token burada
asıl olarak SQL Server/PostgreSQL gibi paralel yazan bir veritabanına geçilirse ya da
okuma-yazma araya girerse devreye giren emniyet kemeri.

Stok düşürme kuralını `Product.TryReduceStock` içine koydum ki "yeterli mi" kontrolü
ile "düş" işlemi ayrı yerlere dağılmasın. Aynı ürün birden fazla satırda gelirse
miktarları topluyorum, yoksa satır satır kontrolü geçip toplamda stoğu aşabilirdi.

## Cache

Sadece ürün okumalarında. Ürün listesi sipariş ekranında sürekli okunuyor ama ancak
sipariş verilince değişiyor, cache'in işe yaradığı yer burası. Siparişleri
cache'lemedim.

Key'ler `products:all`, `products:search:{terim}`, `products:id:{id}`. Arama terimini
tek yerde trim + küçük harf yapıp hem key'de hem sorguda aynısını kullanıyorum; başta
sadece key'i küçültüyordum, o zaman aynı key altında farklı sonuçlar cache'lenebiliyor.

Süre 1 dakika. Stok değişince zaten anında temizlendiği için TTL sadece atladığım bir
durum olursa diye duruyor. Cache'e entity değil response DTO'su yazıyorum, bulunamayan
kaydı da hiç yazmıyorum (yoksa 404 de bir dakika sabitlenir).

Temizleme tarafında bütün ürün girdilerini ortak bir `CancellationChangeToken` ile
işaretliyorum. Sipariş commit olunca token iptal ediliyor ve ürünle ilgili bütün
key'ler bir anda düşüyor. Sadece o ürünün key'ini silmek yetmezdi, stoğu `products:all`
ve arama sonuçlarında da görünüyor; hangi aramaların o ürünü içerdiğini takip etmek de
bu senaryoda gereksiz karmaşıklık olurdu.

Bir detay: token'ı veriyi okumaya başlamadan önce alıyorum. Sonra alsam sorgu sürerken
gelen bir temizleme kaybolur ve eski stok bir dakika cache'te kalırdı, bunu yazarken
gözden kaçırıp sonra fark ettim.

## Testler

`backend/tests/OrderApp.Tests`, gerçek SQLite (`:memory:`) üzerinde. InMemory provider
transaction'ı gerçekten uygulamadığı için onu kullanmadım. 10 test var, önemlileri:

- Yetersiz stokta sipariş oluşmuyor ve diğer ürünlerin stoğu da değişmiyor
- Sipariş oluşunca stoklar doğru azalıyor, toplam doğru
- Ürün fiyatı sonradan değişince eski siparişin tutarı değişmiyor
- Sipariş sonrası cache temizleniyor

## Yapmadıklarım

Sayfalama yok, listeler tek seferde dönüyor. 409 durumunda otomatik retry koymadım,
kullanıcıya tekrar denemesini söylüyorum. Sipariş iptali/stok iadesi kapsam dışıydı.
WebApplicationFactory ile integration test yazmadım, iş kurallarını servis seviyesinde
test edip HTTP tarafını Swagger ve tarayıcıdan manuel doğruladım.

Arama sadece ASCII'de case-insensitive. Terimi ve kolonları `lower()` ile
karşılaştırıyorum ama SQLite'ın `lower()`'ı Ç/İ gibi karakterleri katlamıyor. Düzgünü
collation olurdu (PostgreSQL'de ILIKE), SQLite'ta kalmayı seçtiğim için bu sınırı kabul
ettim.

`DbUpdateException`'ı ayrıca maplemedim, sadece concurrency olanı 409'a çeviriyorum.
Check constraint ihlali zaten benim hatam demektir, 500 dönmesi doğru.

## AI kullanımı

Claude Code (CLI) kullandım. İskelet, tekrar eden DTO/CRUD kodu, CSS ve README taslağı için
işe yaradı. Mimari kararlar (feature bazlı yapı, OrderItem'da snapshot, concurrency
token, cache'i tek token'la temizleme) bana ait.

Kontrol için her endpoint'i hem başarılı hem başarısız senaryolarla çağırıp status
kodunu ve dönen gövdeyi kontrol ettim, iş kurallarını testle sabitledim, frontend'i
tarayıcıda baştan sona denedim. Bu sırada birkaç şey çıktı: aramada `%` ve `_` escape
edilmediği için `%` araması bütün katalogu döndürüyordu, `Enum.TryParse` tek başına
`"7"` gibi değerleri kabul ediyordu, cache token'ını yanlış sırada alıyordum. Yukarıda
anlattığım cache/arama detaylarının çoğu bu turdan çıktı.

## Süre

Yaklaşık 5 saat.
