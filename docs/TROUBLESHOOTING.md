# Sorun giderme

Bir sorun yaşadığınızda önce Mediance'ı sistem tepsisi menüsündeki **Çıkış** komutuyla tamamen kapatıp yeniden açın. X düğmesi tepsiye küçültme ayarı açıkken uygulamadan çıkmaz.

## Medya bilgisi görünmüyor

1. Spotify veya tarayıcıda parçanın gerçekten oynatıldığını kontrol edin.
2. Medya uygulamasını bir kez duraklatıp yeniden oynatın.
3. Mediance'ı kapatıp açın.
4. Windows'un kendi medya panelinde parçanın görünüp görünmediğine bakın.

Kaynak uygulama Windows medya oturumu yayınlamıyorsa Mediance onu göremez. Uygulama oturumu geçici olarak kaybolduğunda Mediance arka planda yeniden bağlanmayı dener.

## Yanlış medya kaynağı seçiliyor

Spotify ve YouTube Music diğer tarayıcı videolarından önceliklidir. Öncelikli uygulama kapalı olduğu hâlde eski oturum görünüyorsa medya uygulamasını tamamen kapatın ve birkaç saniye bekleyin.

Hata raporuna aynı anda açık olan medya uygulamalarını yazın. Şarkı adını paylaşmanız şart değildir.

## Albüm kapağı gelmiyor

Kapak bilgisi kaynak uygulama tarafından geç yayınlanabilir. Mediance kısa aralıklarla yeniden dener. Birkaç saniye sonra hâlâ görünmüyorsa:

- şarkıyı duraklatıp oynatın;
- sonraki parçaya geçip geri dönün;
- kaynak uygulamayı yeniden açın.

## Medya düğmesi çalışmıyor

Mediance yalnızca kaynağın Windows'a desteklediğini bildirdiği komutları kullanır. Bazı web oynatıcıları previous, next veya seek komutlarını sunmayabilir. Spotify çalışıyor fakat düğmeler yanıt vermiyorsa Spotify'ı ve Mediance'ı yeniden başlatın.

## Ses çıkış cihazı görünmüyor

1. Cihazın Windows Ses Ayarları içinde aktif olduğunu kontrol edin.
2. Ana ekrandaki veya Settings → Ses bölümündeki yenile düğmesine basın.
3. Bluetooth cihazında bağlantının tamamlanması için birkaç saniye bekleyin.
4. Kaynak uygulamanın gerçekten bir ses oturumu oluşturduğundan emin olun.

## Çıkış cihazı seçildi ama ses taşınmadı

Bazı uygulamalar kalıcı Windows yönlendirmesini yeni bir ses akışı başlattıklarında uygular. Parçayı duraklatıp yeniden oynatın veya medya uygulamasını yeniden başlatın. **Varsayılan** seçimi yalnızca uygulamaya özel tercihi temizler; Windows'un genel cihazını değiştirmez.

Tarayıcı kullanıyorsanız seçim tek sekmeye değil tarayıcı işlemine uygulanır.

## Lyrics bulunamadı

Lyrics panelini kapatıp yeniden açmadan önce birkaç saniye bekleyin. Her uzak kaynak ayrı bir süre sınırına sahiptir ve Mediance sıradaki kaynağa geçebilir.

Parça adı veya sanatçı bilgisi kaynak uygulama tarafından eksik bildirilmişse güvenli eşleşme yapılamayabilir. Yanlış söz göstermemek için düşük güvenli sonuçlar reddedilir.

## Lyrics sabit görünüyor

Kaynak yalnızca düz söz sağlamış olabilir. Düz sözler otomatik zamanlanmış gibi gösterilmez. Panelde manuel zamanlama seçeneği varsa satır zamanlarını kendiniz oluşturabilirsiniz.

## Lyrics erken veya geç

Settings → Boyutlar → Şarkı sözü zamanlaması ayarını küçük adımlarla değiştirin. Önce ±0,2 saniye deneyin. Kaynağın zaman kodu baştan hatalıysa daha büyük bir düzeltme gerekebilir.

## Manuel zamanlamam yüklenmiyor

Yerel kaydın eşleşmesi için şarkı ve sanatçı kimliği, süre ve söz satırları yeterince benzer olmalıdır. Kaynak sözleri büyük ölçüde değiştirdiyse eski zamanlama yanlış eşleşmeyi önlemek için kullanılmaz.

`%LOCALAPPDATA%\Mediance\lyrics-timing.json` dosyasını elle düzenlemeyin. Dosya bozulursa Mediance `.bak` kopyasını otomatik kurtarmayı dener.

## Pencere görünmüyor

- Varsayılan `Ctrl + Alt + M` kısayolunu deneyin.
- Sistem tepsisindeki Mediance simgesine tıklayın.
- Görev Yöneticisi'nde Mediance çalışıyorsa kapatıp tekrar açın.

Monitör düzeni değiştiğinde kayıtlı konum görünür çalışma alanına taşınmalıdır. Sorun devam ederse aşağıdaki ayar sıfırlama adımlarını kullanın.

## Acrylic veya animasyonlar çalışmıyor

Windows Ayarları → Erişilebilirlik → Görsel efektler bölümünde saydamlık ve animasyon tercihlerini kontrol edin. Mediance bu sistem ayarlarına uyar. Güç tasarrufu ve yüksek kontrast gibi durumlarda düz arka plan kullanılması normal olabilir.

## Global kısayol çalışmıyor

Başka bir uygulama aynı kombinasyonu kaydetmiş olabilir. Settings içinden farklı bir kombinasyon seçin. Kısayol seçiminde en az bir değiştirici tuş ve bir ana tuş kullanılmalıdır.

## Ayarları sıfırlama

1. Mediance'ı tamamen kapatın.
2. Dosya Gezgini adres çubuğuna `%LOCALAPPDATA%\Mediance` yazın.
3. `widget-settings.json` dosyasını farklı bir adla yedekleyin.
4. Orijinal dosyayı silin ve Mediance'ı açın.

Manuel lyrics zamanlamalarını korumak istiyorsanız `lyrics-timing.json` dosyasını silmeyin.

## Hata raporu hazırlama

Bir GitHub issue açarken şunları ekleyin:

- Windows sürümü ve ekran ölçeği;
- kullandığınız medya uygulaması;
- sorunu tekrar oluşturma adımları;
- beklediğiniz ve gördüğünüz davranış;
- sorunun her seferinde olup olmadığı;
- gerekiyorsa `%LOCALAPPDATA%\Mediance\prototypes\acrylic.log` dosyasının ilgili son satırları.

Log dosyasını paylaşmadan önce yine de gözden geçirin. Şarkı adı veya lyrics eklemeniz gerekmez.
