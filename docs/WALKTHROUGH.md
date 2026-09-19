# Mediance kullanım rehberi

Bu rehber uygulamayı ilk kez açan birinin ihtiyaç duyacağı bütün temel davranışları anlatır. Mediance hesabınıza giriş yapmaz ve medya oynatıcınızın yerine geçmez. Windows'un zaten yayınladığı medya oturumunu küçük, masaüstünde duran bir pencerede kullanıma açar.

## İlk açılış

Spotify, YouTube Music veya uyumlu başka bir uygulamada bir parça başlatın. Mediance açıldığında albüm kapağı, kaynak uygulama, şarkı adı, sanatçı, oynatma durumu ve zaman çizelgesi birkaç saniye içinde görünür.

Aynı anda birden fazla medya uygulaması açıksa Mediance şu sırayı izler:

1. Kullanıcı tarafından sabitlenmiş bir medya oturumu varsa onu korur.
2. Spotify açıksa Spotify'a öncelik verir.
3. Spotify yoksa YouTube Music veya Windows'un müzik olarak bildirdiği uygun tarayıcı oturumunu seçer.
4. Bunların hiçbiri yoksa Windows'un güncel medya oturumunu takip eder.

Bu nedenle tarayıcıda açılan sıradan bir YouTube videosu, açık Spotify oturumunu kendiliğinden devralmaz.

## Ana pencere

Widget'ın üst bölümünde kapak, kaynak, şarkı bilgisi ve medya düğmeleri bulunur. İnce ilerleme çizgisinin solunda geçen süre, sağında toplam süre gösterilir. Çizgiye tıklayarak veya basılı tutup sürükleyerek şarkının başka bir bölümüne gidebilirsiniz. Bu işlem yalnızca medya uygulaması zaman değiştirmeyi destekliyorsa kullanılabilir.

Pencereyi taşımak için cam yüzeydeki boş bir alana sol tuşla basılı tutup sürükleyin. Düğme, seçici ve lyrics satırları sürükleme alanı değildir. Fareyi bıraktığınız anda pencere hareketi biter.

Alt soldaki kilit düğmesi pencerenin yanlışlıkla taşınmasını engeller. Yanındaki raptiye düğmesi pencereyi diğer pencerelerin üzerinde tutar. Sağ altta lyrics ve settings düğmeleri vardır.

Pencere ekran kenarına yaklaştırıldığında bulunduğu monitörün çalışma alanına hizalanır. Konum her monitör için ayrı saklanır. Daha önce kullanılan monitör bağlı değilse pencere görünür bir çalışma alanına alınır.

## Medya kontrolleri

Önceki, oynat/duraklat ve sonraki düğmeleri seçili uygulamanın Windows'a bildirdiği yeteneklere göre açılır. Kaynağın desteklemediği bir komut kullanılabilir gösterilmez.

Mediance medya değişikliklerinde kendiliğinden öne gelmez ve yazı yazdığınız pencerenin odağını almaz. Windows medya bağlantısı geçici olarak kaybolursa uygulama arka planda yeniden bağlanmayı dener.

## Uygulamaya özel ses çıkışı

Medya kontrollerinin altındaki seçici, yalnızca seçili medya uygulamasının ses çıkışını değiştirir. Örneğin Spotify'ı kulaklığa yönlendirirken oyun veya sistem sesleri hoparlörde kalabilir.

- **Varsayılan:** Uygulamaya özel tercihi temizler ve uygulamayı Windows'un genel çıkışına döndürür.
- **Hoparlör/Kulaklık:** Uygulamayı seçilen aktif cihaza yönlendirir.
- **Yenile:** Yeni bağlanan veya kaldırılan cihazlar için listeyi yeniden okur.

Tarayıcı yönlendirmesi tek bir sekmeye değil tarayıcı işlemine uygulanır. Bazı uygulamalar yeni cihaz tercihini hemen kullanırken bazıları oynatmanın veya uygulamanın yeniden başlatılmasını isteyebilir.

Widget'ın lyrics alanı dışındayken fare tekerleği seçili uygulamanın sesini yüzde 5 artırır veya azaltır. Ekranın üst bölümünde kısa süreli bir ses göstergesi belirir. Bu özellik ayarlardan kapatılabilir ve sistemin genel ses seviyesini değiştirmez.

## Şarkı sözleri

Lyrics paneli siz açana kadar ağ isteği yapılmaz. Panel açıldığında Mediance önce zaman kodu bulunan kaynakları arar. Doğru şarkı olduğundan emin olmak için başlık, sanatçı, albüm ve yaklaşık süre karşılaştırılır. Belirsiz bir eşleşmede yanlış söz göstermek yerine sonuç bulunamadı durumu kullanılır.

Zaman kodlu söz bulunduğunda aktif satır çalan bölüme göre ilerler. Ayarlardan iki görünüm seçilebilir:

- **2 satır:** Şu anki ve sonraki satır.
- **3 satır:** Önceki, şu anki ve sonraki satır.

Bir satıra tıklamak şarkıyı o satırın başlangıcına götürür. Lyrics alanında fare tekerleğiyle yakın satırlara bakabilirsiniz; birkaç saniye işlem yapılmadığında görünüm yeniden çalan satıra döner.

Sözler kaynaktan erken veya geç geliyorsa **Settings → Lyrics → Şarkı sözü zamanlaması** ayarı kullanılabilir. Varsayılan değer satırı yarım saniye erken gösterir ve ±2 saniye aralığında değiştirilebilir.

## Manuel lyrics zamanlama

Bazen doğru söz metni bulunur fakat kaynağında zaman kodu yoktur. **Otomatik şarkı sözü senkronu** açıksa Mediance, yalnızca seçili medya uygulamasının sesinden mevcut konumda en fazla 75 saniyelik bir örnek yakalar. Çok dilli Whisper modeli cihazda bağımsız bir konuşma çözümlemesi üretir; bu çözümleme doğrulanmış sözlerin o konuma en yakın bölümüyle sırayla eşleştirilir. Yeterince çok satır ve kelime eşleşmedikçe sonuç senkronlu kabul edilmez; sistem şarkının ilerleyen bölümlerinde üç denemeye kadar devam eder.

İlk kullanımda yerel model bir kez indirilir. Yakalanan ses bellekte işlenir ve arşivlenmez. Güvenilir zaman çizelgesi tamamlandıktan sonra sonraki çalımlarda doğrudan yüklenir. Şarkıyı duraklatmak, ileri geri sarmak veya uygulamayı değiştirmek aktif yakalamayı iptal eder. Ayarı **Settings → Lyrics** altından kapatabilirsiniz.

Otomatik eşleşme tamamlanamazsa manuel zamanlama seçeneği gösterilmeye devam eder.

1. Zamanlamayı başlatın. Mediance parçayı başa alır ve oynatır.
2. Ekrandaki güncel satır söylenmeye başladığında büyük işaretleme düğmesine veya Space tuşuna basın.
3. Her satır için aynı işlemi tekrarlayın.
4. Son satır işaretlendiğinde zamanlama otomatik kaydedilir ve normal akan lyrics görünümüne geçilir.

Yarım kalan çalışma kaydedilmez. Baştan başlatabilir veya iptal edebilirsiniz. Daha önce zamanladığınız bir şarkıda **Yeniden senkronla** seçeneği görünür; yeni denemeyi iptal ederseniz eski çalışan zamanlama korunur.

Kabul edilen otomatik ve tamamlanan manuel zamanlamalar `%LOCALAPPDATA%\Mediance\lyrics-timing.json` dosyasında tutulur. Dosyada şarkı adı, sanatçı veya söz metni bulunmaz; yalnızca eşleştirme parmak izleri ve zaman değerleri saklanır. İnternet kaynağında gerçek senkronize söz daha sonra bulunursa kaynak zamanlaması yerel kaydın önüne geçer.

## Settings penceresi

Settings ayrı, yarı saydam bir pencere olarak widget'ın yanında açılır. Değişiklikler anında uygulanır, otomatik kaydedilir ve kısa bir “kaydedildi” vurgusu görünür. Üstteki arama alanı yazdığınız ayarı içeren kategoriye doğrudan geçer.

### Görünüm

Tema, görünüm modu, cam yoğunluğu ve Album temasının blur, yakınlaştırma ve karartma ayarları burada bulunur. Tema profilleri dışa aktarılabilir ve daha sonra yeniden içe alınabilir.

### Öğeler

Albüm kapağı, oynatma çizgisi, medya kontrol grubu, ses çıkışı seçicisi ve lyrics düğmesi buradan açılıp kapatılır. Widget genişliği, kapak boyutu, yazı ölçeği ve kontrol boyutu aynı kategoridedir. Şarkı adı, sanatçı ve kaynak her zaman görünür; temel kontroller tek bir anahtarla yönetilir.

### Lyrics

İki/üç satırlı söz görünümü, ±2 saniyelik zaman farkı ve cihaz üzerinde otomatik senkron seçeneği burada bulunur. Otomatik senkron çalışırken lyrics paneli model indirme yüzdesini, dinlenen saniyeyi, analiz/eşleştirme aşamasını ve üç denemeden hangisinin çalıştığını gösterir.

### Sistem

Kapatıldığında tepsiye küçültme, Windows ile sessiz başlangıç, global kısayol, uygulamaya özel ses çıkışı ve güncelleme denetimi burada bulunur. Güncelleme denetimi yalnızca GitHub Releases bilgisini okur. Yeni sürüm varsa bildirim gösterir; indirme veya kurulum başlatmaz. Release sayfası yalnızca kullanıcı düğmeye bastığında açılır.

Global kısayolu değiştirmek için **Kısayol seç** düğmesine basıp istediğiniz kombinasyonu yapın. Kombinasyonda en az bir Ctrl, Alt, Shift veya Windows tuşu bulunmalıdır. Windows kombinasyonu başka bir uygulamaya ayırmışsa mevcut kısayol korunur.

## Sistem tepsisi ve kapatma

Sistem tepsisi simgesine sol tıklamak widget'ı gösterir veya gizler. Sağ tık menüsünde göster/gizle, ayarlar ve çıkış seçenekleri bulunur.

**Kapatıldığında tepsiye küçült** açıksa X düğmesi uygulamayı kapatmak yerine gizler. Tamamen kapatmak için tepsi menüsündeki **Çıkış** kullanılmalıdır. Bu ayar kapalıysa X uygulamadan çıkar.

## Ayarlar ve yerel dosyalar

Mediance kullanıcı dosyalarını `%LOCALAPPDATA%\Mediance` altında tutar:

- `widget-settings.json`: görünüm, kısayol ve pencere tercihleri;
- `widget-settings.json.bak`: son sağlam ayar yedeği;
- `lyrics-timing.json`: kabul edilen otomatik ve tamamlanan manuel zamanlamalar;
- `lyrics-learning.json`: başarısız denemelerin anonim sayaçları ve birleşen geçici satır çıpaları;
- `Models\ggml-small.bin`: ilk otomatik senkron kullanımında indirilen yerel konuşma modeli;
- `lyrics-timing.json.bak`: son sağlam zamanlama yedeği;
- `prototypes\acrylic.log`: içerik barındırmayan yerel hata günlüğü.

Kayıt sırasında bir dosya bozulursa Mediance bozuk kopyayı `.invalid` uzantısıyla korur ve son sağlam yedeği kullanmayı dener.

## Kısayollar

| İşlem | Varsayılan giriş |
|---|---|
| Widget'ı göster/gizle | `Ctrl + Alt + M` |
| Manuel lyrics satırını işaretle | `Space` |
| Manuel zamanlamayı iptal et | Ekrandaki İptal düğmesi |

Global göster/gizle kısayolu Settings içinden değiştirilebilir.
