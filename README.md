# Satan

Altıgen adalarda köy kurup ticaret yaptığın, 2-8 kişilik online bir strateji oyunu. Bütün kurallar lobide ayarlanabiliyor.

## 1. Projeyi aç

1. Unity Hub → **Add → Add project from disk** → proje klasörü (`Takimada`)
2. Editör sürümü: **6000.6.6f1**. İlk açılışta paketler indirilir (internet gerekir).
3. Açılınca `Assets/Takimada/Scenes/Main.unity` otomatik oluşturulur ve build listesine eklenir.
4. Play'e bas. Sahnede bir şey olmasına gerek yok, oyun kendini kurar.

## Kamera

- **Sol tık + sürükle**: haritayı kaydır
- **Sağ tık + sürükle**: döndür ve eğ
- **Tekerlek**: yakınlaş / uzaklaş
- **Q / E**: döndür, **F**: görünümü sıfırla

Tahta tamamen kodla üretilen 3B modellerden oluşur (ağaçlar, koyunlar, değirmen, dağlar, volkan, iskeleler). Gölgeler ve deniz dalgaları `Assets/Takimada/Resources` içindeki iki shader ile çizilir.

## 2. Online oynamak için (Unity Relay, port açmaya gerek yok)

1. **Edit → Project Settings → Services**: projeyi bir Unity Cloud projesine bağla (yoksa "Create project" de). Ücretsiz.
2. Unity Cloud panelinde Multiplayer → **Relay**'in açık olduğundan emin ol.
3. **File → Build Profiles → Windows → Build**. Çıkan klasörü zip'leyip arkadaşlarına gönder.
4. Biri menüde **Oda kur** der, ekranda çıkan kodu paylaşır. Diğerleri kodu yazıp **Kodla katıl** der.

Herkes aynı build'i kullanmalı. Farklı sürümler birbirine bağlanamaz.

### Relay olmadan

- **Doğrudan IP**: kurucu "Sunucu kur" der, diğerleri kurucunun IP'sini yazıp "Bağlan" der. Aynı ağda (LAN) veya Hamachi / Radmin VPN / ZeroTier ile çalışır. İnternet üzerinden çalışması için kurucunun modeminde UDP 7777 portunu yönlendirmesi gerekir.
- **Sırayla oyna**: tek bilgisayarda herkes sırayla oynar. Kuralları denemek için iyi.

## Ayarlanabilen kurallar

Lobide sağdaki panelden, kurucu değiştirir, herkes canlı görür. Hazır ayarlar: Standart (3-4), Büyük (5-6), Hızlı, Dev Ada. "Kaydet" ile kendi kural setini saklayabilir, JSON olarak panoya kopyalayıp paylaşabilirsin.

| Sekme | Neler var |
|---|---|
| Genel | Zafer puanı, oyuncu sayısı, rastgele sıra, gerçek zar / dengeli zar destesi, başlangıç tur sayısı, yılan sırası, son köyden kaynak, başlangıç kaynakları, ek inşa aşaması |
| Harita | Ada şekli (satır uzunlukları), arazi sayıları, sayı pulları, 6-8 ayrımı, sayıları başlangıç yerleşiminden sonra dağıtma, limanlar, harita tohumu (0 = her oyunda yeni harita) |
| Taş & Maliyet | Yol/köy/kasaba adetleri, puanları ve üretimleri, dört maliyet, banka boyutu, sınırsız banka |
| Korsan | Açık/kapalı, hangi zarla geldiği, kart atma sınırı, volkanda başlama, üretimi engelleme, dostane korsan, korsansız ilk turlar |
| Ticaret & Kart | Oyuncu/banka ticareti, takasta sırası gelen oyuncunun taraf olma şartı, hediye, açık teklif sınırı, banka ve liman oranları, deste içeriği, kart etkileri, tur başı kart sınırı, alındığı tur oynama |
| Ödüller | En Uzun Yol ve En Güçlü Ordu: açık/kapalı, eşik, puan |

## Ticaret nasıl işler

- **Ticaret** penceresinde verdiğin ve istediğin kartları seçersin. Aynı seçimle ya bankayla takas yaparsın ya da oyunculara teklif gönderirsin.
- **Banka / liman**: her kaynağın oranı pencerede yazar (4:1, liman varsa 3:1 veya 2:1). Birden fazla kaynağı tek seferde verebilirsin. Örneğin 4 Odun ve 2 Koyun (2:1 limanın varsa) karşılığında 2 kart alırsın.
- **Tek kişiye teklif**: o kişi kabul ederse takas hemen yapılır.
- **Herkese teklif**: kabul edenler listelenir, teklif sahibi "X ile takas" diyerek kiminle yapacağını seçer.
- **Karşı teklif**: gelen teklifi ters çevirip pencereye doldurur, değiştirip geri gönderirsin.
- Kartları artık yetmeyen teklifler kendiliğinden düşer. Tur bitince bütün teklifler silinir.

## Kod yapısı

```
Assets/Takimada/Scripts/
  Core/      Kural motoru (Unity'den bağımsız): GameConfig, BoardGen, GameState, Rules, Engine
  Net/       NetSession: Netcode for GameObjects üzerinde sıkıştırılmış JSON mesajları
  Online/    UgsRooms: Unity Relay oda kurma/katılma (paket yoksa derlenmez)
  View/      BoardView (prosedürel mesh), Hud (menü, lobi, kural editörü, oyun arayüzü)
  App.cs     Akış, sunucu tarafı oda yönetimi
  Editor/    İlk açılışta sahne ve oyuncu ayarlarını kurar
```

Sunucu yetkilidir: her hamle `Engine.Apply` içinde doğrulanır. İstemciye giden durumda rakiplerin gelişim kartları gizlenir. Kopan oyuncu aynı isimle tekrar katılırsa yerine geri oturur.
