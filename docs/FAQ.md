# Sık sorulan sorular

## Mediance yalnızca Spotify ile mi çalışır?

Hayır. Windows medya oturumu yayınlayan uygulamalarla çalışır. Spotify, YouTube Music ve uyumlu tarayıcılar ana kullanım alanlarıdır. Her uygulama aynı komutları desteklemediği için bazı düğmeler kaynağa göre devre dışı kalabilir.

## Neden Spotify açıkken izlediğim YouTube videosu görünmüyor?

Bu bilinçli bir seçimdir. Mediance açık bir Spotify oturumunu veya YouTube Music'i sıradan bir tarayıcı videosundan daha öncelikli sayar. Öncelikli müzik uygulamaları kapandığında Windows'un güncel medya oturumuna döner.

## Spotify hesabımı bağlamam gerekiyor mu?

Hayır. Mediance Spotify OAuth kullanmaz, şifrenizi istemez ve hesabınıza bağlanmaz. Medya bilgilerini Windows'un yerel medya oturumundan alır.

## Bilgisayarın genel ses çıkışını mı değiştiriyor?

Hayır. Ses çıkışı seçimi yalnızca o anda seçili medya uygulamasına uygulanır. **Varsayılan** seçildiğinde uygulama yeniden Windows'un genel çıkışını takip eder.

## Bir tarayıcı sekmesini ayrı cihaza gönderebilir miyim?

Şu anda yönlendirme tarayıcı işlemi seviyesindedir. Tek bir sekmeye özel yönlendirme yapılmaz; aynı tarayıcıdaki diğer sesler de etkilenebilir.

## Yeni bağladığım kulaklık listede görünmüyor.

Ses seçicisinin yanındaki yenile düğmesini veya Settings içindeki Ses sayfasını kullanın. Windows cihazı henüz aktif göstermiyorsa birkaç saniye bekleyip tekrar deneyin.

## Şarkı sözleri neden bazı parçalarda bulunamıyor?

Her parçanın güvenilir bir söz kaydı veya zaman kodlu sürümü bulunmayabilir. Mediance yanlış şarkının sözlerini göstermekten kaçınmak için belirsiz eşleşmeleri reddeder. Kaynak sitelerin geçici olarak çevrimdışı veya sınırlı olması da sonucu etkileyebilir.

## Genius'ta söz var ama neden otomatik akmıyor?

Bir sayfada söz metninin bulunması zaman kodu bulunduğu anlamına gelmez. Mediance düz metinden tahmini senkron üretmez. Doğru düz metin doğrulanırsa manuel zamanlama seçeneği sunulur.

## Manuel zamanlamam uygulamayı kapatınca kaybolur mu?

Tamamlanan zamanlama yerel olarak saklanır ve sonraki açılışta yüklenir. Yarım bırakılan çalışma kaydedilmez. Kayıt dosyasının son sağlam yedeği de tutulur.

## Manuel zamanlamayı değiştirebilir miyim?

Evet. Kullanıcı tarafından zamanlanmış bir şarkıda **Yeniden senkronla** seçeneği görünür. Yeni denemeyi iptal ederseniz önceki zamanlama korunur.

## Mediance şarkı sözlerini veya dinleme geçmişimi kaydediyor mu?

Hayır. Dinleme geçmişi oluşturulmaz. Manuel zamanlama dosyasında şarkı adı, sanatçı veya söz metni yerine tek yönlü parmak izleri ve zaman değerleri bulunur.

## Lyrics araması sırasında internete ne gönderiliyor?

Lyrics paneli açıldığında başlık, sanatçı, albüm ve yaklaşık süre etkin söz sağlayıcılarına gönderilebilir. Ayrıntılar [Gizlilik](PRIVACY.md) sayfasındadır.

## Lyrics birkaç saniye erken veya geç görünüyor.

Settings içindeki **Boyutlar → Şarkı sözü zamanlaması** ayarını değiştirin. Değer ±2 saniye aralığında ayarlanabilir.

## Uygulamayı kapattım ama hâlâ çalışıyor.

**Kapatıldığında tepsiye küçült** açıksa X yalnızca pencereyi gizler. Tam çıkış için sistem tepsisindeki Mediance simgesine sağ tıklayıp **Çıkış** seçin.

## Mediance'ı nasıl geri getiririm?

Varsayılan olarak `Ctrl + Alt + M` tuşlarına basın veya sistem tepsisi simgesine tıklayın. Kısayol daha önce değiştirildiyse kayıtlı kombinasyonu kullanın.

## Global kısayol neden kaydedilemiyor?

Seçtiğiniz kombinasyon başka bir uygulama veya Windows tarafından kullanılıyor olabilir. En az bir değiştirici tuş içeren farklı bir kombinasyon deneyin. Başarısız seçim eski çalışan kısayolu değiştirmez.

## Acrylic görünüm neden düz renge dönüştü?

Windows saydamlık efektleri kapalıysa, güç tasarrufu veya erişilebilirlik politikası etkinse sistem Acrylic yerine düz arka plan kullanabilir. Mediance bu işletim sistemi tercihine uyar.

## Pencere kayıp monitörde kaldıysa ne olur?

Mediance kayıtlı monitörü bulamazsa pencereyi mevcut çalışma alanlarından birine taşır. Sorun sürerse ayar dosyasını sıfırlama adımlarını [Sorun giderme](TROUBLESHOOTING.md) belgesinde bulabilirsiniz.

## Windows SmartScreen neden uyarı gösteriyor?

Beta paketi henüz dijital olarak imzalanmamışsa Windows yayıncıyı doğrulayamaz. Dosyayı yalnızca resmi GitHub Releases sayfasından indirin ve yayınlanan SHA-256 değeriyle karşılaştırın.

## Windows 10 destekleniyor mu?

Mevcut hedef Windows 11 24H2 x64'tür. Windows 10 için destek sözü verilmemektedir.

## Otomatik güncelleme var mı?

İlk beta sürümünde yoktur. Yeni sürüm yayınlandığında paket GitHub Releases üzerinden indirilecek ve mevcut klasör güncellenecektir. Kullanıcı ayarları uygulama klasörünün dışında tutulduğu için güncelleme sırasında korunur.

## Uygulamayı nasıl tamamen kaldırırım?

Mediance'ı kapatın ve uygulama klasörünü silin. Yerel ayarları ve manuel zamanlamaları da kaldırmak isterseniz `%LOCALAPPDATA%\Mediance` klasörünü silin. Windows ile başlatma açıksa önce Settings üzerinden kapatmanız önerilir.
