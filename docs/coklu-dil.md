# Arayüz dili (Türkçe / İngilizce)

## Nasıl çalışıyor

- Anahtar Türkçe metnin kendisidir: `t('Keşfet')`.
  - Türkçede metin olduğu gibi döner.
  - İngilizcede `apps/nereye-go/lib/locales/en.ts` içindeki karşılığı döner.
- Yer tutucular `{ad}` biçimindedir: `t('{n} dk yürüme', { n })`.
- Aynı Türkçe sözcüğün farklı anlamları için bağlam soneki kullanılır: `t('Ara|telefon')`. Türkçede "Ara" görünür,
  İngilizcede "Call".
- Dil açılışta seçilir:
  - Profil'deki tercih (Sistem / Türkçe / English) önce gelir.
  - Tercih yoksa cihaz dili kullanılır; Türkçe değilse İngilizce.
  - `lib/locale.ts` uygulamanın ilk içe aktarılan modülüdür.
  - Değişiklik web'de hemen (yenileme), yerel uygulamada bir sonraki açılışta uygulanır.
- Türkçe ekler (23:00'e, 09:00'da) yalnızca Türkçede kurulur. İngilizcenin kendi kalıpları var: "Open until 23:00",
  "Opens at 09:00".
- Ondalık: Türkçede 4,6, İngilizcede 4.6 (`decimal()`).

## Testler

| Test | Ne denetler |
| --- | --- |
| `lib/i18n.test.ts` | Kaynaktaki her `t('…')` metninin İngilizce karşılığı olduğunu ve yer tutucuların aynı kaldığını. Yeni metin eklenip sözlük unutulursa test kırılır. |
| `e2e/tests/english.spec.ts` | İngilizce cihazda arayüzün İngilizce açıldığını ve Profil'den Türkçeye geçişi. |

## Kapsam

**Çevrildi:**

- Sekme çubuğu
- Ana sayfa: başlık, kategoriler, "Benim için seç", hava notu, bu akşam bandı, izin kartı
- Keşfet, harita ve arama
- Mekan kartı ve mekan sayfası
- "Benim için seç" sayfası
- Profil
- Filtreler ve yarıçap
- Konum seçimi
- Listeler ve paylaşılan liste
- Birlikte karar (oylama)
- Kaydedilenler
- Hata ve boş durum metinleri
- Açık/kapalı satırı
- Süre ve "… önce" metinleri
- Yaygın kategori ve olanak adları

**Henüz Türkçe:**

- **Ekranlar:**
  - giriş/kayıt (`auth`)
  - hesap (`account`)
  - yorum yazma (`review`)
  - yorumlarım
  - karşılama (`onboarding`)
  - işletme paneli (`owner`)
  - gizlilik metni (`legal`)
- **Sunucudan gelen metinler:**
  - iş kuralı hata mesajları (ör. "Liste adı 1–40 karakter olmalı.")
  - mekan açıklama notları
  - Google'dan gelen nadir kategori adları
  - gizlilik ve koşullar metni
  - bildirim metinleri

  Bunlar için API'nin `Accept-Language` başlığına göre yanıt vermesi gerekir; sonraki iş.
- **İçerik:** kullanıcı ve mekan sahibi içerikleri (yorum, etkinlik adı) çevrilmez.
