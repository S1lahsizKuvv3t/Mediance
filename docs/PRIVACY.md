# Gizlilik

Son güncelleme: 17 Eylül 2026

Mediance hesap gerektirmeden çalışır. Uygulamada reklam, kullanıcı profili, dinleme geçmişi veya telemetri sistemi bulunmaz.

## Bilgisayarınızda işlenen bilgiler

Mediance aşağıdaki bilgileri Windows'un yerel API'lerinden okuyabilir:

- kaynak uygulamanın adı;
- şarkı başlığı, sanatçı ve albüm;
- oynatma durumu ve zaman çizelgesi;
- albüm kapağı;
- aktif ses çıkış cihazları;
- seçili uygulamanın ses oturumları ve kayıtlı çıkış tercihi.

Bu bilgiler widget'ı çalıştırmak için kullanılır. Mediance dinlediğiniz parçaların geçmişini oluşturmaz. Albüm kapakları normal kullanım sırasında bellekte işlenir ve arşivlenmez.

## Otomatik lyrics senkronu

Bu ayar etkinse ve güvenilir düz söz bulunmasına rağmen hiçbir kaynak zaman kodu sağlayamazsa Mediance yalnızca seçili medya uygulamasının Windows ses sürecini yakalayabilir. Sistem genelindeki ses, mikrofon ve diğer uygulamaların sesi bu yakalama için kullanılmaz.

Yakalanan ses:

- bellekte 16 kHz mono olarak tutulur;
- yerel Whisper modeliyle aynı bilgisayarda çözümlenir;
- Mediance veya başka bir konuşma tanıma sunucusuna gönderilmez;
- çözümleme veya iptal tamamlandığında arşivlenmez.

Çok dilli Whisper modeli ilk kullanımda Whisper.net'in sabit Hugging Face model kaynağından `%LOCALAPPDATA%\Mediance\Models\ggml-small.bin` yoluna indirilir. Bu kalıcı dosya yalnızca genel model ağırlıklarını içerir; kullanıcı sesi, şarkı bilgisi veya söz içermez.

Bağımsız transcript doğrulanmış söz metniyle bellekte eşleştirilir. Yalnızca güven eşiğini geçen satır zamanları aşağıda açıklanan yerel zamanlama dosyasına yazılır.

## Lyrics istekleri

Lyrics paneli kapalıyken söz araması yapılmaz. Paneli açtığınızda doğru parçayı bulmak için aşağıdaki bilgiler etkin sağlayıcılara gönderilebilir:

- şarkı başlığı;
- sanatçı;
- albüm;
- yaklaşık süre.

Sağlayıcı zinciri sürüme ve erişilebilirliğe göre LRCLIB, Better Lyrics, AMLL TTML, Apple/iTunes katalog hizmetleri, Şarkı Analizi, SozMuzik, Genius ve ŞarkıSözleri BBS uçlarını kullanabilir. Bu hizmetlerin kendi gizlilik ve kayıt politikaları geçerlidir.

Sonuçlar kısa süreli olarak yalnızca uygulama belleğinde önbelleğe alınır. Bu önbellek uygulama kapandığında kaybolur.

## Yerel lyrics zamanlamaları

Otomatik eşleşme kabul edildiğinde veya kullanıcı bir şarkıyı manuel olarak zamanladığında tamamlanan zaman değerleri `%LOCALAPPDATA%\Mediance\lyrics-timing.json` dosyasına yazılır.

Bu dosya şunları içermez:

- şarkı adı;
- sanatçı adı;
- albüm adı;
- söz metni.

Eşleştirme için SHA-256 tabanlı şarkı, söz ve satır parmak izleri; şarkı süresi; satır zamanları ve güncelleme tarihi saklanır. Dosya yereldir ve Mediance tarafından bir sunucuya yüklenmez.

## Ayarlar

Görünüm, pencere konumu, global kısayol ve benzeri tercihler `%LOCALAPPDATA%\Mediance\widget-settings.json` içinde tutulur. Son sağlam kayıt `.bak` dosyasında korunabilir. Bozuk bir dosya kurtarma amacıyla `.invalid` uzantısıyla saklanabilir.

## Tanılama günlüğü

Mediance yerel bir hata günlüğü oluşturabilir:

`%LOCALAPPDATA%\Mediance\prototypes\acrylic.log`

Günlük hata türü, hata kodu ve uygulama yaşam döngüsü olayları için kullanılır. Lyrics metni, lyrics sorgu adresi veya dinleme geçmişi bilinçli olarak günlüğe yazılmaz.

## Ağ üzerinden gönderilmeyen bilgiler

Mediance'ın kendi telemetri veya hesap sunucusu yoktur. Aşağıdaki veriler Mediance tarafından merkezi bir hizmete gönderilmez:

- ayarlar;
- pencere konumu;
- ses çıkışı tercihi;
- global kısayol;
- yerel lyrics zamanlama dosyası;
- tanılama günlüğü.

## Verileri silme

Mediance'ı tamamen kapattıktan sonra `%LOCALAPPDATA%\Mediance` klasörünü silmek bütün yerel ayarları, günlükleri, zamanlamaları ve indirilen konuşma modelini kaldırır. Uygulama klasörünü silmek bu yerel verileri kendiliğinden kaldırmaz.

## Değişiklikler

Gizlilik davranışı değişirse bu belge ve üstteki tarih güncellenir. Telemetri gibi yeni bir veri toplama özelliği varsayılan olarak sessizce eklenmemelidir.
