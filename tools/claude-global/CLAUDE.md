# Genel çalışma kuralları (tüm projeler)

Bu dosya `~/.claude/CLAUDE.md` olarak kurulur ve her projede otomatik yüklenir.

## Özerklik
- Sormadan ilerle. Makul bir varsayılan varsa onu seç, varsayımını sonuç notunda tek cümleyle belirt.
- Yalnızca geri alınamaz veya dışarıya dönük işlerde (silme, force-push, prod deploy, ödeme, e-posta/mesaj gönderme) onay iste.
- İş bitmeden durma: derle, test et, doğrula; sonra raporla.

## İş akışı (superpowers)
- Yeni özellik veya davranış değişikliği: `brainstorming` → `writing-plans` → `executing-plans`. Brainstorming'in sorularını kullanıcıya sorma; kod tabanından ve bu dosyadan kendin cevapla.
- Uygulama sırasında `test-driven-development`: önce başarısız test, sonra kod.
- Hata ayıklarken `systematic-debugging`: kök nedeni bulmadan yama yapma.
- "Bitti" demeden önce `verification-before-completion`: komutu çalıştır, çıktıyı gör.
- Büyük değişiklikten sonra `requesting-code-review` ile kendi diff'ini gözden geçir.

## Dokümantasyon (context7)
- Kütüphane/framework API'si, kurulum veya yapılandırma gerektiğinde eğitim verisine güvenme: önce context7 `resolve-library-id`, sonra `query-docs`. Kullanıcı istemese de yap.
- Sürüm projedeki lockfile/manifest ile eşleşmeli.

## Arayüz doğrulama (playwright)
- Web arayüzünü veya web'e export edilebilen mobil arayüzü değiştirdiysen Playwright ile aç, ekran görüntüsü al, konsol hatalarına bak. Tip kontrolü tek başına "çalışıyor" demek değildir.

## Skills
- Göreve uyan bir skill varsa (frontend-design, webapp-testing, docx/pdf/xlsx/pptx, skill-creator, mcp-builder) önce onu yükle.
- Tekrarlayan bir iş akışı fark edersen `.claude/skills/` altında skill olarak kaydet.

## Kod kalitesi
- Mevcut desenleri izle; yeni bağımlılık eklemeden önce projede olanı kullan.
- Girdi doğrulama, yetki kontrolü ve gizli anahtarlar: her değişiklikte kontrol et. Sırları koda/commit'e yazma.
- Küçük, anlamlı commit'ler. Commit mesajı "ne" değil "neden".
- Kullanıcıya dönük metinler projenin dilinde (Nereye: Türkçe).
