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

Sözler kaynaktan erken veya geç geliyorsa **Settings → Boyutlar → Şarkı sözü zamanlaması** ayarı kullanılabilir. Varsayılan değer satırı yarım saniye erken gösterir ve ±2 saniye aralığında değiştirilebilir.

## Manuel lyrics zamanlama

Bazen doğru söz metni bulunur fakat kaynağında zaman kodu yoktur. Bu durumda Mediance düz metni senkronizeymiş gibi oynatmaz; bunun yerine manuel zamanlama seçeneği gösterir.

1. Zamanlamayı başlatın. Mediance parçayı başa alır ve oynatır.
2. Ekrandaki güncel satır söylenmeye başladığında büyük işaretleme düğmesine veya Space tuşuna basın.
3. Her satır için aynı işlemi tekrarlayın.
4. Son satır işaretlendiğinde zamanlama otomatik kaydedilir ve normal akan lyrics görünümüne geçilir.

Yarım kalan çalışma kaydedilmez. Baştan başlatabilir veya iptal edebilirsiniz. Daha önce zamanladığınız bir şarkıda **Yeniden senkronla** seçeneği görünür; yeni denemeyi iptal ederseniz eski çalışan zamanlama korunur.

Tamamlanan zamanlamalar `%LOCALAPPDATA%\Mediance\lyrics-timing.json` dosyasında tutulur. Dosyada şarkı adı, sanatçı veya söz metni bulunmaz; yalnızca eşleştirme parmak izleri ve zaman değerleri saklanır. İnternet kaynağında gerçek senkronize söz daha sonra bulunursa kaynak zamanlaması yerel kaydın önüne geçer.

## Settings penceresi

Settings ayrı, yarı saydam bir pencere olarak widget'ın yanında açılır. Değişiklikler anında uygulanır ve otomatik kaydedilir.

### Görünüm

- Pencere konumunu kilitleme
- Her zaman üstte tutma
- Albüm kapağına göre ambient ışık
- Fare tekerleğiyle uygulama sesi
- Kapatıldığında sistem tepsisine küçültme
- Windows ile sessizce başlatma
- Mat arka plan ve kenarlık
- Cam yoğunluğu
- Midnight, Prism ve Clear Glass temaları
- Global kısayol seçimi

Global kısayolu değiştirmek için **Kısayol seç** düğmesine basıp istediğiniz kombinasyonu yapın. Kombinasyonda en az bir Ctrl, Alt, Shift veya Windows tuşu bulunmalıdır. Windows kombinasyonu başka bir uygulamaya ayırmışsa mevcut kısayol korunur.

### Öğeler

Albüm kapağı, ilerleme çizgisi, başlık, sanatçı, kaynak, kontroller, ses çıkışı seçicisi, tek tek medya düğmeleri, marka, kapatma düğmesi, oynatma durumu ve lyrics düğmesi ayrı ayrı gösterilip gizlenebilir. Gizlenen öğenin kapladığı alan da kapanır.

### Boyutlar

Widget genişliği, kapak boyutu, yazı ölçeği ve kontrol boyutu burada ayarlanır. Aynı sayfada iki/üç satırlı lyrics görünümü ve lyrics zaman farkı bulunur.

### Ses

Seçili medya uygulaması, aktif çıkış cihazları ve uygulamanın kayıtlı çıkış tercihi gösterilir. Ana widget'taki seçici kapalı olsa bile ses yönlendirmesi bu sayfadan yönetilebilir.

## Sistem tepsisi ve kapatma

Sistem tepsisi simgesine sol tıklamak widget'ı gösterir veya gizler. Sağ tık menüsünde göster/gizle, ayarlar ve çıkış seçenekleri bulunur.

**Kapatıldığında tepsiye küçült** açıksa X düğmesi uygulamayı kapatmak yerine gizler. Tamamen kapatmak için tepsi menüsündeki **Çıkış** kullanılmalıdır. Bu ayar kapalıysa X uygulamadan çıkar.

## Ayarlar ve yerel dosyalar

Mediance kullanıcı dosyalarını `%LOCALAPPDATA%\Mediance` altında tutar:

- `widget-settings.json`: görünüm, kısayol ve pencere tercihleri;
- `widget-settings.json.bak`: son sağlam ayar yedeği;
- `lyrics-timing.json`: tamamlanan manuel zamanlamalar;
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
