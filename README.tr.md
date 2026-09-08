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
  <a href="docs/FAQ.md">SSS</a>
</p>

> İlk genel beta sürümünü [GitHub Releases](https://github.com/S1lahsizKuvv3t/Mediance/releases/tag/v0.9.0-beta.1) sayfasından indirebilirsiniz. İmzalı kurulum paketi ve otomatik güncelleme henüz yayın kontrol listesindedir.

## Mediance ne yapar?

Mediance masaüstünde küçük bir Acrylic widget olarak durur. Windows'un mevcut medya oturumlarını kullandığı için Spotify, YouTube Music, tarayıcılar ve diğer uyumlu oynatıcılarla hesap bağlantısı veya tarayıcı eklentisi istemeden çalışabilir.

- Oynat, duraklat, önceki, sonraki ve zaman çizelgesinde ilerleme kontrolleri.
- Alakasız bir tarayıcı videosu yerine Spotify ve YouTube Music'e öncelik verme.
- Yumuşak geçişli senkronize şarkı sözleri ve ayarlanabilir zamanlama farkı.
- Yalnızca düz söz bulunan şarkılar için isteğe bağlı manuel zamanlama.
- Bilgisayarın genel çıkışını değiştirmeden seçili uygulamayı başka bir ses cihazına yönlendirme.
- Widget öğelerini ayrı ayrı gizleme; genişlik, kapak, yazı, kontrol, cam yoğunluğu ve tema ayarları.
- Üstte tutma, konum kilidi, iki monitörde kenara hizalama ve sistem tepsisine küçültme.
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

Yalnızca EXE dosyasını başka yere taşımayın; yanındaki çalışma zamanı dosyaları uygulamanın bir parçasıdır. Sürümler dijital olarak imzalanana kadar Windows SmartScreen bilinmeyen yayıncı uyarısı gösterebilir.

Güncelleme, kaldırma ve temiz kurulum adımları [Kurulum](docs/INSTALLATION.md) belgesinde bulunur.

## Kısa kullanım

Uyumlu bir uygulamada müzik başlatın ve Mediance'ı açın. Widget tercih edilen medya oturumunu otomatik izler. Cam yüzeydeki boş bir alandan sürükleyebilir, yerine yerleştirdikten sonra alt kısımdaki kilidi açabilir ve `settings` üzerinden görünümü değiştirebilirsiniz.

Ses çıkışı seçicisi yalnızca seçilen medya uygulamasını etkiler. **Varsayılan** seçimi uygulamayı yeniden Windows'un genel çıkışına bağlar. Tarayıcı yönlendirmesi tek bir sekmeye değil tarayıcı işlemine uygulanır.

`lyrics` düğmesi mevcut şarkı için söz aramasını başlatır. Kaynağında zaman kodu bulunan sözler her zaman önceliklidir. Yalnızca doğrulanmış düz söz bulunursa kullanıcı isterse satırların zamanını kendisi işaretleyebilir; tamamlanan zamanlar yerel olarak saklanır.

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
```

## Güncel durum

Mevcut sürüm 85 otomatik testi ve native pencere smoke testini geçmektedir. Spotify kontrolleri ve uygulamaya özel ses yönlendirmesi gerçek cihazlarda da doğrulanmıştır. İmzalı 1.0 sürümünden önce kalan işler [Yol haritasında](docs/ROADMAP.md) tutulur.

Hata ve özellik talepleri için GitHub issue şablonlarını kullanabilirsiniz. Güvenlik sorunları herkese açık issue olarak paylaşılmamalıdır; bunun için [SECURITY.md](SECURITY.md) belgesini izleyin.

İlk push öncesindeki repo ayarları için [GitHub kurulum listesini](docs/GITHUB_SETUP.md) kullanabilirsiniz.

## Lisans

Copyright © 2026 Mediance. Tüm hakları saklıdır. Mevcut lisans kaynak kodun yeniden kullanılmasına veya değiştirilmiş derlemelerin dağıtılmasına izin vermez. Proje açık kaynak lisansa geçirilecekse bu metin genel yayından önce değiştirilebilir.

Üçüncü taraf bileşenleri [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) içinde listelenmiştir.
