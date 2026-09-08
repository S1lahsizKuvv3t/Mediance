# Kurulum ve güncelleme

## Sistem gereksinimleri

- Windows 11 24H2 veya daha yeni bir sürüm
- x64 işlemci
- Yaklaşık 400 MB boş alan; kesin boyut sürüme göre değişebilir
- Lyrics özelliği için internet bağlantısı

Mediance yayın paketi .NET ve Windows App SDK çalışma zamanlarını içerir. Son kullanıcıların ayrıca geliştirme aracı kurması gerekmez.

## ZIP paketini kurma

1. Resmi GitHub Releases sayfasını açın.
2. Son sürümün altındaki `Mediance-<sürüm>-win-x64.zip` dosyasını indirin.
3. Dosyaya sağ tıklayıp **Tümünü ayıkla** seçeneğini kullanın.
4. Çıkan klasörü kalıcı olarak kullanmak istediğiniz bir yere taşıyın. Örneğin `%LOCALAPPDATA%\Programs\Mediance` kullanılabilir.
5. Klasördeki `Mediance.exe` dosyasını çalıştırın.

EXE'nin yanındaki DLL ve çalışma zamanı dosyaları gereklidir. Yalnızca `Mediance.exe` dosyasını masaüstüne taşımak uygulamayı bozabilir. Masaüstünde erişim istiyorsanız EXE'nin kendisini değil, kısayolunu oluşturun.

## SmartScreen uyarısı

İlk beta sürümleri dijital olarak imzalanmadıysa Windows **Bilinmeyen yayıncı** uyarısı gösterebilir. Paketi yalnızca resmi Releases sayfasından indirin. Yayın notlarında verilen SHA-256 değeri indirdiğiniz dosyayla eşleşmiyorsa paketi çalıştırmayın.

PowerShell ile SHA-256 kontrolü:

```powershell
Get-FileHash .\Mediance-0.9.0-win-x64.zip -Algorithm SHA256
```

## İlk çalıştırma

Uyumlu bir uygulamada medya başlatın ve Mediance'ı açın. Widget birkaç saniye içinde mevcut oturumu bulur. Hiçbir şey çalmıyorsa boş durum göstermesi normaldir.

Settings içinden isteğe bağlı olarak:

- Windows ile sessizce başlatmayı;
- kapatıldığında tepsiye küçültmeyi;
- global göster/gizle kısayolunu

ayarlayabilirsiniz.

## Güncelleme

İlk beta sürümünde otomatik güncelleme bulunmaz.

1. Sistem tepsisi menüsünden Mediance'ı tamamen kapatın.
2. Yeni ZIP paketini indirin ve ayrı bir klasöre çıkarın.
3. Eski uygulama klasörünü yeni dosyalarla değiştirin.
4. `Mediance.exe` dosyasını yeniden açın.

Ayarlar ve manuel lyrics zamanlamaları uygulama klasöründe değil `%LOCALAPPDATA%\Mediance` altında saklandığı için normal güncellemede korunur.

## Kaldırma

1. Settings içinden **Windows ile başlat** seçeneğini kapatın.
2. Sistem tepsisi menüsünden **Çıkış** seçeneğini kullanın.
3. Mediance uygulama klasörünü silin.

Kişisel ayarları ve manuel zamanlamaları da kaldırmak için `%LOCALAPPDATA%\Mediance` klasörünü silebilirsiniz. Bu son adım geri alınamaz; yerel manuel zamanlamalar da silinir.

## Kaynak koddan çalıştırma

Geliştiriciler Windows 11 üzerinde .NET 10 SDK ile aşağıdaki komutları kullanabilir:

```powershell
dotnet restore Mediance.slnx
dotnet build Mediance.slnx --configuration Release
dotnet test tests/Mediance.Core.Tests --configuration Release
```

Native pencere smoke testi:

```powershell
.\scripts\dev.ps1 glass-test
```
