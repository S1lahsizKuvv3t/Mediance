# Gizlilik

Son güncelleme: 9 Eylül 2026

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

## Lyrics istekleri

Lyrics paneli kapalıyken söz araması yapılmaz. Paneli açtığınızda doğru parçayı bulmak için aşağıdaki bilgiler etkin sağlayıcılara gönderilebilir:

- şarkı başlığı;
- sanatçı;
- albüm;
- yaklaşık süre.

Sağlayıcı zinciri sürüme ve erişilebilirliğe göre LRCLIB, Better Lyrics, AMLL TTML, Apple/iTunes katalog hizmetleri, Şarkı Analizi, SozMuzik, Genius ve ŞarkıSözleri BBS uçlarını kullanabilir. Bu hizmetlerin kendi gizlilik ve kayıt politikaları geçerlidir.

Sonuçlar kısa süreli olarak yalnızca uygulama belleğinde önbelleğe alınır. Bu önbellek uygulama kapandığında kaybolur.

## Manuel lyrics zamanlamaları

Kullanıcı bir şarkıyı manuel olarak zamanladığında tamamlanan zaman değerleri `%LOCALAPPDATA%\Mediance\lyrics-timing.json` dosyasına yazılır.

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
- manuel lyrics zamanlama dosyası;
- tanılama günlüğü.

## Verileri silme

Mediance'ı tamamen kapattıktan sonra `%LOCALAPPDATA%\Mediance` klasörünü silmek bütün yerel ayarları, günlükleri ve manuel zamanlamaları kaldırır. Uygulama klasörünü silmek bu yerel verileri kendiliğinden kaldırmaz.

## Değişiklikler

Gizlilik davranışı değişirse bu belge ve üstteki tarih güncellenir. Telemetri gibi yeni bir veri toplama özelliği varsayılan olarak sessizce eklenmemelidir.
