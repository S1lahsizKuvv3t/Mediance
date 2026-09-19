<p align="center">
  <img src="assets/brand/Mediance-mark-master.png" width="128" alt="Mediance logosu">
</p>

<h1 align="center">Mediance</h1>

<p align="center">
  Windows 11 için kompakt bir masaüstü medya yardımcısı.<br>
  Oynatmayı kontrol edin, senkronize şarkı sözlerini takip edin ve uygulamanın ses çıkışını masaüstünden ayrılmadan değiştirin.
</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="docs/INSTALLATION.md">Kurulum</a> ·
  <a href="docs/WALKTHROUGH.md">Kullanım rehberi</a> ·
  <a href="docs/FAQ.md">SSS</a> ·
  <a href="docs/BETA_QUALITY.md">Beta kalitesi</a> ·
  <a href="docs/MARKETING.md">Tanıtım paketi</a>
</p>

> En yeni genel beta sürümünü [GitHub Releases](https://github.com/S1lahsizKuvv3t/Mediance/releases/tag/v0.9.0-beta.3) sayfasından indirebilirsiniz. İmzalı kurulum paketi ve otomatik güncelleme henüz yayın kontrol listesindedir.

<p align="center">
  <img src="assets/marketing/Mediance-feature-tour.gif" width="760" alt="Mediance özellik turu">
</p>

## Özellik özeti

| Alan | Mediance ne sunar? |
|---|---|
| Medya | Tercihli oturum seçimi, oynat/duraklat, önceki/sonraki, seek ve donmuş oturum kurtarma |
| Lyrics | Kaynak senkronu, cihaz üzerinde otomatik eşleştirme, yerel öğrenme, timing offset ve iki/üç satır görünümü |
| Ses | Uygulamaya özel çıkış cihazı ve fare tekerleğiyle uygulama sesi |
| Görünümler | Standart, Micro, kapak + kontroller, dikey lyrics ve sadeleştirilebilir düzen |
| Görünüm | Acrylic cam, ortalanmış Album teması, blur, zoom, karartma, genişlik ve yoğunluk ayarları |
| Masaüstü | Çoklu monitör hizalama, konum kilidi, üstte tutma, sistem tepsisi ve değiştirilebilir global kısayol |
| Gizlilik | Hesap, telemetri, dinleme geçmişi, kayıtlı ses veya bulut konuşma çözümleme yok |

## Mediance ne yapar?

Mediance masaüstünde küçük bir Acrylic widget olarak durur. Windows'un mevcut medya oturumlarını kullandığı için Spotify, YouTube Music, tarayıcılar ve diğer uyumlu oynatıcılarla hesap bağlantısı veya tarayıcı eklentisi istemeden çalışabilir.

- Oynat, duraklat, önceki, sonraki ve zaman çizelgesinde ilerleme kontrolleri.
- Alakasız bir tarayıcı videosu yerine Spotify ve YouTube Music'e öncelik verme.
- Yumuşak geçişli senkronize şarkı sözleri ve ayarlanabilir zamanlama farkı.
- Yalnızca doğrulanmış düz söz bulunan şarkılarda cihaz üzerinde otomatik zamanlama; gerektiğinde manuel zamanlama seçeneği.
- Bilgisayarın genel çıkışını değiştirmeden seçili uygulamayı başka bir ses cihazına yönlendirme.
- Widget öğelerini ayrı ayrı gizleme; genişlik, kapak, yazı, kontrol, cam yoğunluğu ve tema ayarları.
- Album temasıyla mevcut kapağı yumuşak geçişli ve okunaklı bir arka plana dönüştürme.
- Üstte tutma, konum kilidi, iki monitörde kenara hizalama ve sistem tepsisine küçültme.
- Oyunu ön planda tutma; widget görev çubuğu ve Alt+Tab'da görünmez, tıklanınca oyundan odağı almaz.
- Kullanıcının belirleyebildiği global göster/gizle kısayolu.

Mediance dinleme geçmişi tutmaz ve telemetri göndermez. Şarkı sözleri yalnızca lyrics paneli açıldığında aranır. Ayrıntılı veri akışı [Gizlilik](docs/PRIVACY.md) sayfasında açıklanmıştır.

## Gereksinimler

- Windows 11 24H2 veya daha yeni bir sürüm
- x64 işlemci
- Windows medya oturumu yayınlayan bir medya uygulaması

Yayın paketi gerekli çalışma zamanlarını beraberinde taşır. Kullanıcının ayrıca .NET SDK veya Windows App SDK kurması gerekmez.

## Beta sürümünü kurma

1. GitHub Releases sayfasından `Mediance-<sürüm>-win-x64.zip` dosyasını indirin.
2. Arşivin tamamını normal bir klasöre çıkarın.
3. Klasördeki `Mediance.exe` dosyasını çalıştırın.

Paketin en üstünde yalnızca başlatıcı bulunduğu için `Mediance.exe` kolayca görünür. Self-contained uygulama dosyalarını içeren `App` klasörünü başlatıcının yanında tutun. Sürümler dijital olarak imzalanana kadar Windows SmartScreen bilinmeyen yayıncı uyarısı gösterebilir.

Güncelleme, kaldırma ve temiz kurulum adımları [Kurulum](docs/INSTALLATION.md) belgesinde bulunur.

## Kısa kullanım

Uyumlu bir uygulamada müzik başlatın ve Mediance'ı açın. Widget tercih edilen medya oturumunu otomatik izler. Cam yüzeydeki boş bir alandan sürükleyebilir, yerine yerleştirdikten sonra alt kısımdaki kilidi açabilir ve `settings` üzerinden görünümü değiştirebilirsiniz.

Ses çıkışı seçicisi yalnızca seçilen medya uygulamasını etkiler. **Varsayılan** seçimi uygulamayı yeniden Windows'un genel çıkışına bağlar. Tarayıcı yönlendirmesi tek bir sekmeye değil tarayıcı işlemine uygulanır.

`lyrics` düğmesi mevcut şarkı için söz aramasını başlatır. Kaynağında zaman kodu bulunan sözler her zaman önceliklidir. Yalnızca doğrulanmış düz söz bulunursa isteğe bağlı yerel senkron sistemi seçili medya uygulamasının sesini en fazla 75 saniyelik bölümlerde cihaz üzerinde çözümler, sözlerin muhtemel bölümünü eşleştirir ve yalnızca güvenilir sonucu saklar. Şarkının ortasından başlayabilir ve düşük güvenli bir bölümden sonra ileride yeniden deneyebilir. Manuel zamanlama seçeneği de korunur. Çok dilli model ilk kullanımda bir kez indirilir; yakalanan ses arşivlenmez.

Bütün kontroller [Kullanım rehberinde](docs/WALKTHROUGH.md), sık karşılaşılan sorular ise [SSS](docs/FAQ.md) sayfasında anlatılmıştır.

## Kaynak koddan derleme

Windows 11 ve .NET 10 SDK gerekir.

```powershell
git clone <fork-adresiniz>
cd Mediance
dotnet restore Mediance.slnx
dotnet build Mediance.slnx --configuration Release
dotnet test tests/Mediance.Core.Tests --configuration Release
```

Projede geliştirme için kısa komutlar da bulunur:

```powershell
.\scripts\dev.ps1 build
.\scripts\dev.ps1 test
.\scripts\dev.ps1 glass-test
.\scripts\beta-check.ps1
```

## Güncel durum

Mevcut sürüm 97 otomatik testi ve native pencere smoke testini geçmektedir. Canlı lyrics kabul setinde 12/12 kaynak senkronlu ve 4/4 kontrollü otomatik senkron örneği başarıyla tamamlanmıştır. Spotify kontrolleri ve uygulamaya özel ses yönlendirmesi gerçek cihazlarda da doğrulanmıştır. Tekrarlanabilir beta kapısı; canlı lyrics örneklerini, kaynak matrisini, çoklu monitör durumunu, uyku dönüşünü ve isteğe bağlı uzun çalışma testini raporlar. Donanım veya canlı medya gerektiren bir adım çalıştırılmadığında başarılı sayılmaz; raporda bekliyor olarak kalır. İmzalı 1.0 sürümünden önce kalan işler [Yol haritasında](docs/ROADMAP.md) tutulur.

Hata ve özellik talepleri için GitHub issue şablonlarını kullanabilirsiniz. Güvenlik sorunları herkese açık issue olarak paylaşılmamalıdır; bunun için [SECURITY.md](SECURITY.md) belgesini izleyin.

İlk push öncesindeki repo ayarları için [GitHub kurulum listesini](docs/GITHUB_SETUP.md) kullanabilirsiniz.

## Lisans

Copyright © 2026 Mediance. Tüm hakları saklıdır. Mevcut lisans kaynak kodun yeniden kullanılmasına veya değiştirilmiş derlemelerin dağıtılmasına izin vermez. Proje açık kaynak lisansa geçirilecekse bu metin genel yayından önce değiştirilebilir.

Üçüncü taraf bileşenleri [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) içinde listelenmiştir.
