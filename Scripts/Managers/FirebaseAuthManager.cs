using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;

public class FirebaseAuthManager : MonoBehaviour
{
    private static FirebaseAuthManager instance;
    public static FirebaseAuthManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<FirebaseAuthManager>();
            return instance;
        }
    }

    private FirebaseAuth auth;
    private FirebaseUser currentUser;
    private DatabaseReference dbReference;
    private string currentSessionId;
    
    // Timeout süresi (30 saniye)
    private const long SESSION_TIMEOUT_MS = 30000;
    // Yeniden giriş için bekleme süresi (3 saniye)
    private const float LOGIN_COOLDOWN_SEC = 3f;
    private float lastLogoutTime;

    private bool isQuitting = false;
    private string logPrefix = "[Auth] "; // Log prefix

    public bool IsLoggedIn => currentUser != null;
    public string UserId => currentUser?.UserId;
    public FirebaseUser CurrentUser => currentUser;

    // Hata mesajları için event
    public event System.Action<string> OnAuthError;
    public event System.Action<FirebaseUser> OnAuthStateChanged; // Yeni event ekle

    private void TriggerError(string message)
    {
        Debug.LogError($"{logPrefix}Hata: {message}"); // Hataları da logla
        OnAuthError?.Invoke(message);
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"{logPrefix}FirebaseAuthManager başlatılıyor...");
            InitializeFirebaseAuth();
        }
        else
        {
             Debug.LogWarning($"{logPrefix}FirebaseAuthManager zaten mevcut, bu kopya yok ediliyor.");
            Destroy(gameObject);
        }
    }

    private void InitializeFirebaseAuth()
    {
        try
        {
            auth = FirebaseAuth.DefaultInstance;
            dbReference = FirebaseDatabase.DefaultInstance.RootReference;
             Debug.Log($"{logPrefix}Firebase Auth ve Database referansları alındı.");

            // Varolan oturumu kapat
            if (auth.CurrentUser != null)
            {
                 Debug.LogWarning($"{logPrefix}Başlangıçta aktif kullanıcı bulundu ({auth.CurrentUser.Email}), çıkış yapılıyor...");
                auth.SignOut();
                currentUser = null; // currentUser'ı burada da null yap
            }

            auth.StateChanged += AuthStateChanged;
             Debug.Log($"{logPrefix}AuthStateChanged olayına abone olundu.");
        }
        catch (Exception e)
        {
             Debug.LogError($"{logPrefix}Firebase başlatma hatası: {e.Message}");
             if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
             TriggerError("Firebase başlatılamadı. İnternet bağlantınızı kontrol edin.");
        }
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        if (auth.CurrentUser != currentUser) // Gerçek bir değişiklik olduysa logla
        {
            if (auth.CurrentUser != null)
            {
                currentUser = auth.CurrentUser;
                Debug.Log($"{logPrefix}AuthStateChanged: Kullanıcı GİRİŞ YAPTI -> ID: {currentUser.UserId}, Email: {currentUser.Email}");
                OnAuthStateChanged?.Invoke(currentUser); // Event'i burada tetikle
            }
            else
            {
                // currentUser null ise ve önceden null değilse çıkış yapılmıştır
                if (currentUser != null)
                {
                    Debug.Log($"{logPrefix}AuthStateChanged: Kullanıcı ÇIKIŞ YAPTI -> Eski ID: {currentUser.UserId}");
                    currentUser = null;
                    OnAuthStateChanged?.Invoke(null); // Event'i burada tetikle
                }
                // else // Zaten null ise tekrar loglamaya gerek yok
                // {
                //     Debug.Log($"{logPrefix}AuthStateChanged: Kullanıcı zaten çıkış yapmış durumda.");
                // }
            }
        }
        // else // State değişmediyse loglamaya gerek yok
        // {
        //     Debug.Log($"{logPrefix}AuthStateChanged tetiklendi ancak kullanıcı durumu değişmedi ({ (currentUser == null ? "Çıkış" : "Giriş") }).");
        // }
    }

    // Yeni kullanıcı kaydı
    public async Task<bool> RegisterUser(string email, string password)
    {
         Debug.Log($"{logPrefix}RegisterUser çağrıldı: Email={email}");
        try
        {
            var result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            // currentUser = result.User; // AuthStateChanged halledecek

            // result.User null olabilir mi? Kontrol edelim.
            if (result == null || result.User == null) {
                Debug.LogError($"{logPrefix}Kayıt başarılı görünüyor ancak User nesnesi alınamadı!");
                TriggerError("Kayıt sırasında beklenmedik bir sorun oluştu.");
                return false;
            }

            Debug.Log($"{logPrefix}Kullanıcı başarıyla kaydedildi: ID={result.User.UserId}, Email={result.User.Email}");
            
            // Kayıt sonrası otomatik giriş için session oluştur
            Debug.Log($"{logPrefix}CreateSession çağrılıyor (kayıt sonrası otomatik giriş)...");
            bool sessionCreated = await CreateSession(result.User);
            Debug.Log($"{logPrefix}CreateSession sonucu (kayıt sonrası): {sessionCreated}");
            if (!sessionCreated)
            {
                Debug.LogError($"{logPrefix}Session oluşturulamadı (kayıt sonrası). Kullanıcı çıkış yapılıyor.");
                await LogOut(); // LogOut session'ı silip SignOut yapar
                TriggerError("Kayıt başarılı ancak oturum başlatılamadı. Lütfen giriş yapın.");
                return false;
            }
            
            // Başarılı kayıttan sonra otomatik giriş yapılıyor (AuthStateChanged tetiklenecek)
            Debug.Log($"{logPrefix}RegisterUser başarılı (Session oluşturuldu). UserID: {result.User.UserId}");
            return true;
        }
        catch (Exception e)
        {
            string errorMessage = "Kayıt hatası: ";
            FirebaseException firebaseEx = e as FirebaseException;
            if (firebaseEx != null)
            {
                 AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
                 Debug.LogError($"{logPrefix}Firebase Kayıt Hatası: Kod={errorCode}, Mesaj={firebaseEx.Message}");
                 switch (errorCode)
                 {
                     case AuthError.EmailAlreadyInUse: errorMessage += "Bu email adresi zaten kullanımda!"; break;
                     case AuthError.WeakPassword: errorMessage += "Şifre çok zayıf. En az 6 karakter kullanın."; break;
                     case AuthError.InvalidEmail: errorMessage += "Geçersiz email adresi!"; break;
                     default: errorMessage += "Beklenmeyen bir Firebase hatası oluştu."; break;
                 }
            }
            else
            {
                 Debug.LogError($"{logPrefix}Kayıt hatası (Firebase değil): {e.Message}");
                 if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
                 errorMessage += "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";
            }

            TriggerError(errorMessage);
            return false;
        }
    }

    // Kullanıcı girişi
    public async Task<bool> LoginUser(string email, string password)
    {
         Debug.Log($"{logPrefix}LoginUser çağrıldı: Email={email}");
        // Hızlı giriş-çıkış kontrolü
        if (Time.time - lastLogoutTime < LOGIN_COOLDOWN_SEC)
        {
            string waitMsg = $"Çok hızlı giriş-çıkış yapıyorsunuz. Lütfen {(LOGIN_COOLDOWN_SEC - (Time.time - lastLogoutTime)):F1} saniye bekleyin.";
            Debug.LogWarning($"{logPrefix}{waitMsg}");
            TriggerError(waitMsg);
            return false;
        }

        try
        {
            Debug.Log($"{logPrefix}SignInWithEmailAndPasswordAsync çağrılıyor...");
            var authResult = await auth.SignInWithEmailAndPasswordAsync(email, password);

            // authResult veya User null kontrolü
             if (authResult == null || authResult.User == null)
             {
                 Debug.LogError($"{logPrefix}Giriş başarılı görünüyor ancak User nesnesi alınamadı!");
                 TriggerError("Giriş sırasında beklenmedik bir sorun oluştu.");
                 // Oturumu kapatmayı dene
                 auth?.SignOut();
                 return false;
             }
             Debug.Log($"{logPrefix}Firebase girişi başarılı. UserID: {authResult.User.UserId}. Aktiflik kontrolü yapılıyor...");


            // Kullanıcı aktif mi kontrol et
            var isUserActive = await CheckIfUserIsActive(authResult.User.UserId);
             Debug.Log($"{logPrefix}Aktiflik kontrol sonucu: {isUserActive}");
            if (isUserActive)
            {
                // Kullanıcı zaten aktif, çıkış yap ve hata mesajı döndür
                Debug.LogWarning($"{logPrefix}Bu hesap ({authResult.User.UserId}) zaten başka bir cihazda aktif. Çıkış yapılıyor.");
                auth.SignOut(); // Firebase'den çıkış yap
                currentUser = null; // Lokal state'i güncelle (AuthStateChanged tetiklenmeyebilir)
                OnAuthStateChanged?.Invoke(null); // Manuel tetikle
                TriggerError("Bu hesap şu anda başka bir cihazda aktif. Lütfen diğer oturumdan çıkış yapıp tekrar deneyin.");
                return false;
            }

            // Kullanıcı aktif değilse devam et
            // currentUser = authResult.User; // AuthStateChanged halledecek

            // Session oluştur
            Debug.Log($"{logPrefix}CreateSession çağrılıyor...");
            bool sessionCreated = await CreateSession(authResult.User); // User objesini gönderelim
            Debug.Log($"{logPrefix}CreateSession sonucu: {sessionCreated}");
            if (!sessionCreated)
            {
                Debug.LogError($"{logPrefix}Session oluşturulamadı. Giriş iptal ediliyor, çıkış yapılıyor.");
                await LogOut(); // LogOut session'ı silip SignOut yapar
                TriggerError("Oturum başlatılamadı. Lütfen tekrar deneyin.");
                return false;
            }

            // Başarılı giriş logu AuthStateChanged içinde yapılacak
            Debug.Log($"{logPrefix}LoginUser başarılı (Session oluşturuldu). UserID: {authResult.User.UserId}");
            return true;
        }
        catch (Exception e)
        {
            string errorMessage = "Giriş hatası: ";
             FirebaseException firebaseEx = e as FirebaseException;
             if (firebaseEx != null)
             {
                 AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
                 Debug.LogError($"{logPrefix}Firebase Giriş Hatası: Kod={errorCode}, Mesaj={firebaseEx.Message}");
                 switch (errorCode)
                 {
                     case AuthError.WrongPassword: errorMessage += "Şifre yanlış!"; break;
                     case AuthError.UserNotFound: errorMessage += "Bu email adresi ile kayıtlı kullanıcı bulunamadı!"; break;
                     case AuthError.InvalidEmail: errorMessage += "Geçersiz email adresi!"; break;
                     case AuthError.UserDisabled: errorMessage += "Bu kullanıcı hesabı devre dışı bırakılmış."; break;
                     default: errorMessage += "Beklenmeyen bir Firebase hatası oluştu."; break;
                 }
             }
            else
            {
                Debug.LogError($"{logPrefix}Giriş hatası (Firebase değil): {e.Message}");
                if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
                errorMessage += "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";
            }

            TriggerError(errorMessage);
            // Giriş başarısızsa çıkış yapıldığından emin ol
            auth?.SignOut();
            currentUser = null;
            OnAuthStateChanged?.Invoke(null);
            return false;
        }
    }

    // Kullanıcının aktif olup olmadığını kontrol et
    private async Task<bool> CheckIfUserIsActive(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
         Debug.Log($"{logPrefix}CheckIfUserIsActive çağrıldı: UserID={userId}");

        try
        {
            var userSessionRef = dbReference.Child("onlineUsers").Child(userId);
             Debug.Log($"{logPrefix}Transaction başlatılıyor... Ref: {userSessionRef.ToString()}");
            bool isActive = false;
            string activeSessionId = null; // Aktif session ID'sini loglamak için

            // Transaction ile atomik kontrol
            var transactionResult = await userSessionRef.RunTransaction(mutableData =>
            {
                // Debug.Log($"{logPrefix}Transaction çalışıyor... Data: {mutableData.GetRawJsonValue()}");
                if (!mutableData.HasChildren)
                {
                     Debug.Log($"{logPrefix}Transaction: Kullanıcı için session verisi yok, aktif değil.");
                    isActive = false;
                    return TransactionResult.Success(mutableData); // Veri yoksa dokunma
                }

                var sessionData = mutableData.Value as Dictionary<string, object>;
                if (sessionData != null && sessionData.ContainsKey("lastActive") && sessionData.ContainsKey("sessionId"))
                {
                    long lastActiveTimestamp = 0;
                    // Timestamp Long veya String olabilir, ikisini de dene
                    if (sessionData["lastActive"] is long) {
                         lastActiveTimestamp = (long)sessionData["lastActive"];
                    } else if (long.TryParse(sessionData["lastActive"].ToString(), out long parsedTimestamp)) {
                         lastActiveTimestamp = parsedTimestamp;
                    } else {
                         Debug.LogError($"{logPrefix}Transaction: lastActive timestamp ({sessionData["lastActive"]}) çözümlenemedi!");
                         isActive = false; // Hatalı veri, aktif değil say
                         mutableData.Value = null; // Hatalı veriyi sil
                         return TransactionResult.Success(mutableData);
                    }

                    activeSessionId = sessionData["sessionId"].ToString(); // Session ID'yi al
                    long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long timeDifference = currentTimestamp - lastActiveTimestamp;
                     // Debug.Log($"{logPrefix}Transaction: LastActive={lastActiveTimestamp}, Current={currentTimestamp}, Diff={timeDifference}ms, Timeout={SESSION_TIMEOUT_MS}ms");


                    // Timeout süresi içinde aktivite varsa aktif sayılır
                    if (timeDifference <= SESSION_TIMEOUT_MS)
                    {
                        Debug.LogWarning($"{logPrefix}Transaction: Aktif oturum bulundu! SessionID: {activeSessionId}, Fark: {timeDifference}ms");
                        isActive = true;
                        // Aktif oturum varsa veri üzerinde değişiklik yapma
                    }
                    else
                    {
                        Debug.Log($"{logPrefix}Transaction: Oturum zaman aşımına uğramış (SessionID: {activeSessionId}, Fark: {timeDifference}ms). Veri siliniyor.");
                        mutableData.Value = null; // Zaman aşımına uğramış veriyi sil
                        isActive = false;
                    }
                }
                else
                {
                     Debug.LogWarning($"{logPrefix}Transaction: Session verisi eksik veya hatalı formatta. Aktif değil sayılıyor.");
                     mutableData.Value = null; // Hatalı veriyi sil
                     isActive = false;
                }

                return TransactionResult.Success(mutableData);
            });

             //Debug.Log($"{logPrefix}Transaction tamamlandı. WasSuccessful: {transactionResult.IsSuccessful}");
             // Transaction başarısız olursa ne yapmalı? Şimdilik isActive false dönecek.
             // TransactionResult'ın IsSuccessful propertysi yok, bu kontrolü kaldırıyoruz.
             // Transaction'ın kendisi hata verirse catch bloğuna düşer, başarılıysa isActive flag'ine göre devam eder.

            Debug.Log($"{logPrefix}CheckIfUserIsActive sonuç: {isActive}");
            return isActive;
        }
        catch (Exception e)
        {
            Debug.LogError($"{logPrefix}Kullanıcı aktivite kontrolü sırasında hata: {e.Message}");
            if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
            TriggerError("Oturum kontrolü sırasında bir hata oluştu. Lütfen tekrar deneyin.");
            return false; // Hata durumunda aktif değil varsayalım
        }
    }

    // Çıkış yapma
    public async Task LogOut()
    {
        string userIdBeforeLogout = currentUser?.UserId ?? "Yok";
         Debug.Log($"{logPrefix}LogOut çağrıldı. Mevcut Kullanıcı ID: {userIdBeforeLogout}");
        if (auth != null && currentUser != null)
        {
            await RemoveSession(); // Önce session'ı sil
            auth.SignOut(); // Sonra Firebase'den çıkış yap
            // currentUser = null; // AuthStateChanged halledecek
            lastLogoutTime = Time.time; // Cooldown için zamanı kaydet
             // Başarılı çıkış logu AuthStateChanged içinde yapılacak
             Debug.Log($"{logPrefix}LogOut tamamlandı (Session silindi, SignOut çağrıldı).");
        }
        else
        {
             Debug.LogWarning($"{logPrefix}LogOut: Kullanıcı zaten çıkış yapmış veya Auth null.");
             // Zaten çıkış yapmışsa bile session kalmış olabilir mi? Kontrol edelim.
             if (!string.IsNullOrEmpty(userIdBeforeLogout) && userIdBeforeLogout != "Yok") {
                 await RemoveSession(userIdBeforeLogout); // ID ile silmeyi dene
             }
        }
    }

    // Şifre sıfırlama maili gönderme
    public async Task<bool> SendPasswordResetEmail(string email)
    {
         Debug.Log($"{logPrefix}SendPasswordResetEmail çağrıldı: Email={email}");
        if (string.IsNullOrEmpty(email)) {
            TriggerError("Lütfen geçerli bir email adresi girin.");
            return false;
        }
        try
        {
            await auth.SendPasswordResetEmailAsync(email);
            Debug.Log($"{logPrefix}Şifre sıfırlama maili gönderildi: {email}");
            return true;
        }
        catch (Exception e)
        {
             FirebaseException firebaseEx = e as FirebaseException;
             string errorMsg;
             if (firebaseEx != null) {
                 AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
                 Debug.LogError($"{logPrefix}Şifre sıfırlama Firebase Hatası: Kod={errorCode}, Mesaj={firebaseEx.Message}");
                 if (errorCode == AuthError.UserNotFound) {
                     errorMsg = "Bu email adresi ile kayıtlı kullanıcı bulunamadı!";
                 } else if (errorCode == AuthError.InvalidEmail) {
                     errorMsg = "Geçersiz email adresi!";
                 } else {
                     errorMsg = "Şifre sıfırlama maili gönderilirken bir hata oluştu.";
                 }
             } else {
                 Debug.LogError($"{logPrefix}Şifre sıfırlama hatası: {e.Message}");
                 if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
                 errorMsg = "Şifre sıfırlama maili gönderilirken beklenmeyen bir hata oluştu.";
             }
            TriggerError(errorMsg);
            return false;
        }
    }

    // CreateSession overload'u User objesini kabul etsin
    private async Task<bool> CreateSession(FirebaseUser user)
    {
        if (user == null)
        {
            Debug.LogError($"{logPrefix}CreateSession: User nesnesi null!");
            return false;
        }
         Debug.Log($"{logPrefix}CreateSession çağrıldı: UserID={user.UserId}");

        try
        {
            currentSessionId = System.Guid.NewGuid().ToString();
             Debug.Log($"{logPrefix}Yeni Session ID oluşturuldu: {currentSessionId}");
            var sessionData = new Dictionary<string, object>
            {
                { "sessionId", currentSessionId },
                { "lastActive", ServerValue.Timestamp }, // ServerValue kullan
                { "deviceId", SystemInfo.deviceUniqueIdentifier },
                { "platform", Application.platform.ToString() },
                { "email", user.Email },
                { "clientVersion", Application.version },
                { "lastLoginTime", ServerValue.Timestamp } // ServerValue kullan
            };
             Debug.Log($"{logPrefix}Session verisi hazırlandı: DeviceID={sessionData["deviceId"]}, Platform={sessionData["platform"]}");


            var userSessionRef = dbReference.Child("onlineUsers").Child(user.UserId);
             Debug.Log($"{logPrefix}Session Transaction başlatılıyor... Ref: {userSessionRef.ToString()}");

            // Transaction ile güvenli session oluşturma
            bool sessionCreatedSuccessfully = false;
            string existingSessionId = null; // Mevcut session ID'sini loglamak için
            var transactionResult = await userSessionRef.RunTransaction(mutableData =>
            {
                // Debug.Log($"{logPrefix}Session Transaction çalışıyor... Data: {mutableData.GetRawJsonValue()}");
                if (!mutableData.HasChildren)
                {
                    Debug.Log($"{logPrefix}Transaction: Mevcut session yok, yeni session oluşturuluyor.");
                    mutableData.Value = sessionData;
                    sessionCreatedSuccessfully = true;
                }
                else
                {
                    // Aktif oturum varsa yeni oturuma izin verme (CheckIfUserIsActive zaten kontrol etti ama çift kontrol)
                     existingSessionId = mutableData.Child("sessionId").Value?.ToString() ?? "Bilinmiyor";
                     Debug.LogWarning($"{logPrefix}Transaction: Aktif oturum zaten mevcut! SessionID: {existingSessionId}. Yeni session oluşturulamadı.");
                    sessionCreatedSuccessfully = false;
                    // Mevcut veriye dokunma, Abort et
                    return TransactionResult.Abort();
                }
                return TransactionResult.Success(mutableData);
            });

             //Debug.Log($"{logPrefix}Session Transaction tamamlandı. WasSuccessful: {transactionResult.IsSuccessful}, SessionCreated: {sessionCreatedSuccessfully}");

             // Sadece sessionCreatedSuccessfully kontrolü yeterli
             if (!sessionCreatedSuccessfully)
             {
                 // Transaction başarısız olduysa veya session oluşturulamadıysa
                 //Debug.LogError($"{logPrefix}Session oluşturulamadı (Transaction başarısız veya mevcut session var).");
                 currentSessionId = null; // Oluşturulan ID'yi geçersiz kıl
                 // Eğer Transaction abort ettiyse (mevcut session varsa) spesifik hata ver
                 if (!sessionCreatedSuccessfully && existingSessionId != null) {
                     TriggerError("Bu hesap zaten başka bir cihazda aktif.");
                 } else {
                     TriggerError("Oturum başlatılamadı. Lütfen tekrar deneyin.");
                 }
                return false;
            }

            // OnDisconnect handler'ı ayarla
            Debug.Log($"{logPrefix}OnDisconnect handler ayarlanıyor...");
            await userSessionRef.OnDisconnect().RemoveValue();
            Debug.Log($"{logPrefix}OnDisconnect handler ayarlandı.");

            // Heartbeat başlat
            Debug.Log($"{logPrefix}Session Heartbeat başlatılıyor...");
            StartSessionHeartbeat();

            Debug.Log($"{logPrefix}Session başarıyla oluşturuldu. Session ID: {currentSessionId}, UserID: {user.UserId}");
            return true;
        }
        catch (Exception e)
        {
            //Debug.LogError($"{logPrefix}Session oluşturma hatası: {e.Message}");
            if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
            TriggerError("Oturum başlatılamadı. Lütfen internet bağlantınızı kontrol edip tekrar deneyin.");
            currentSessionId = null; // Hata durumunda ID'yi temizle
            return false;
        }
    }

    private void StartSessionHeartbeat()
    {
        Debug.Log($"{logPrefix}StartSessionHeartbeat çağrıldı.");
        StopAllCoroutines(); // Önceki heartbeat'leri durdur
        StartCoroutine(SessionHeartbeatCoroutine());
    }

    private IEnumerator SessionHeartbeatCoroutine()
    {
        Debug.Log($"{logPrefix}SessionHeartbeatCoroutine başladı. Aralık: 30sn");
        float waitDuration = 30f; // Bekleme süresini değişkende tutalım
        var waitTime = new WaitForSeconds(waitDuration);

        // İlk güncellemeden önce bekleme ekleyelim
        Debug.Log($"{logPrefix}Heartbeat: İlk güncelleme için {waitDuration} saniye bekleniyor...");
        yield return waitTime;

        while (currentUser != null && !string.IsNullOrEmpty(currentSessionId))
        {
            // Debug.Log($"{logPrefix}Heartbeat döngüsü: Timestamp güncelleniyor...");
            // Timestamp güncelleme işlemini ayrı bir coroutine'de başlatıp bekleyelim
             yield return StartCoroutine(UpdateSessionTimestampCoroutine());

            // Debug.Log($"{logPrefix}Heartbeat döngüsü: Session doğrulanıyor...");
            // Session doğrulama işlemini ayrı bir coroutine'de başlatıp bekleyelim
             yield return StartCoroutine(ValidateSessionCoroutine()); // Validate şimdi heartbeat içinde

            // Debug.Log($"{logPrefix}Heartbeat döngüsü: Bekleniyor...");
            yield return waitTime;
        }
         Debug.LogWarning($"{logPrefix}SessionHeartbeatCoroutine durdu. User: {(currentUser == null ? "Null" : currentUser.UserId)}, SessionID: {currentSessionId}");
    }

    private IEnumerator UpdateSessionTimestampCoroutine()
    {
        // Debug.Log($"{logPrefix}UpdateSessionTimestampCoroutine başlatıldı.");
        var task = UpdateSessionTimestamp();
        yield return new WaitUntil(() => task.IsCompleted);
         // Debug.Log($"{logPrefix}UpdateSessionTimestampCoroutine tamamlandı.");
    }

    private async Task UpdateSessionTimestamp()
    {
        if (currentUser == null || string.IsNullOrEmpty(currentSessionId)) {
            // Debug.LogWarning($"{logPrefix}UpdateSessionTimestamp: Kullanıcı null veya session ID yok, işlem atlandı.");
            return;
        }

        // Debug.Log($"{logPrefix}UpdateSessionTimestamp çalışıyor... UserID: {currentUser.UserId}, SessionID: {currentSessionId}");

        try
        {
            var userSessionRef = dbReference.Child("onlineUsers").Child(currentUser.UserId);
            // Debug.Log($"{logPrefix}UpdateSessionTimestamp: Ref: {userSessionRef.ToString()}");

            // Transaction ile güncelleme daha güvenli
            var transactionResult = await userSessionRef.RunTransaction(mutableData =>
            {
                // Debug.Log($"{logPrefix}Timestamp Transaction çalışıyor... Data: {mutableData.GetRawJsonValue()}");
                if (!mutableData.HasChildren)
                {
                     Debug.LogWarning($"{logPrefix}Timestamp Transaction: Session verisi bulunamadı, işlem iptal.");
                     return TransactionResult.Abort(); // Session yoksa güncelleme yapma
                }

                var sessionData = mutableData.Value as Dictionary<string, object>;
                if (sessionData != null)
                {
                    string storedSessionId = sessionData.ContainsKey("sessionId") ?
                        sessionData["sessionId"].ToString() : "";

                    if (storedSessionId == currentSessionId)
                    {
                         // Debug.Log($"{logPrefix}Timestamp Transaction: Doğru session ({currentSessionId}), timestamp güncelleniyor.");
                        sessionData["lastActive"] = ServerValue.Timestamp;
                        mutableData.Value = sessionData;
                        return TransactionResult.Success(mutableData);
                    }
                    else
                    {
                         Debug.LogWarning($"{logPrefix}Timestamp Transaction: Session ID eşleşmedi! (DB: {storedSessionId} vs Local: {currentSessionId}). İşlem iptal.");
                         return TransactionResult.Abort(); // Farklı session, dokunma
                    }
                }
                else
                {
                    Debug.LogWarning($"{logPrefix}Timestamp Transaction: Session verisi hatalı formatta, işlem iptal.");
                    return TransactionResult.Abort();
                }
            });

             // Debug.Log($"{logPrefix}Timestamp Transaction tamamlandı. WasSuccessful: {transactionResult.IsSuccessful}");
             // TransactionResult'ın IsSuccessful propertysi yok, bu kontrolü kaldırıyoruz.
             // Başarısızlık durumu (Abort veya Hata) transactionResult içinde loglandı veya catch'e düşer.
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{logPrefix}Session timestamp güncelleme hatası: {e.Message}");
            // Kritik hata durumunda oturumu sonlandır
            if (e.Message.Contains("Permission denied") || e.Message.Contains("disconnected") || e.Message.Contains("Database connection failed"))
            {
                Debug.LogWarning($"{logPrefix}Kritik hata: Session güncellenemedi, oturum sonlandırılıyor.");
                StopAllCoroutines(); // Heartbeat'i durdur
                await LogOut(); // Oturumu kapat
                TriggerError("Bağlantı sorunu nedeniyle oturumunuz sonlandırıldı. Lütfen tekrar giriş yapın.");
            }
        }
    }

    private IEnumerator ValidateSessionCoroutine()
    {
        // Debug.Log($"{logPrefix}ValidateSessionCoroutine başlatıldı.");
        var task = ValidateSession();
        yield return new WaitUntil(() => task.IsCompleted);

        // Debug.Log($"{logPrefix}ValidateSessionCoroutine tamamlandı. Sonuç: {task.Result}");
        if (!task.IsFaulted && !task.Result) // Hata yoksa ve sonuç false ise
        {
            Debug.LogWarning($"{logPrefix}ValidateSession: Session geçersiz bulundu, oturum kapatılıyor...");
            StopAllCoroutines(); // Heartbeat'i durdur
            StartCoroutine(LogOutCoroutine()); // LogOut işlemini coroutine ile başlat
            TriggerError("Oturumunuz başka bir cihazda açıldı veya geçersiz kılındı!");
        }
        else if (task.IsFaulted)
        {
             Debug.LogError($"{logPrefix}ValidateSessionCoroutine sırasında hata oluştu: {task.Exception}");
        }
    }

    private IEnumerator LogOutCoroutine()
    {
         Debug.Log($"{logPrefix}LogOutCoroutine başlatıldı.");
        var task = LogOut();
        yield return new WaitUntil(() => task.IsCompleted);
         Debug.Log($"{logPrefix}LogOutCoroutine tamamlandı.");
    }

    public async Task<bool> ValidateSession()
    {
        if (currentUser == null || string.IsNullOrEmpty(currentSessionId))
        {
             // Debug.LogWarning($"{logPrefix}ValidateSession: Kullanıcı null veya session ID yok.");
             return false;
        }
         // Debug.Log($"{logPrefix}ValidateSession çalışıyor... UserID: {currentUser.UserId}, SessionID: {currentSessionId}");

        try
        {
            var userSessionRef = dbReference.Child("onlineUsers").Child(currentUser.UserId);
             // Debug.Log($"{logPrefix}ValidateSession: Veri alınıyor... Ref: {userSessionRef.ToString()}");
            var snapshot = await userSessionRef.GetValueAsync();

            if (!snapshot.Exists)
            {
                 Debug.LogWarning($"{logPrefix}ValidateSession: Kullanıcı için session verisi bulunamadı.");
                 return false;
            }

            string storedSessionId = snapshot.Child("sessionId").Value?.ToString();
            string storedDeviceId = snapshot.Child("deviceId").Value?.ToString();
             // Debug.Log($"{logPrefix}ValidateSession: DB Verisi -> SessionID={storedSessionId}, DeviceID={storedDeviceId}");
             // Debug.Log($"{logPrefix}ValidateSession: Lokal Veri -> SessionID={currentSessionId}, DeviceID={SystemInfo.deviceUniqueIdentifier}");


            bool isValid = storedSessionId == currentSessionId &&
                           storedDeviceId == SystemInfo.deviceUniqueIdentifier;

            // Debug.Log($"{logPrefix}ValidateSession sonuç: {isValid}");
            return isValid;
        }
        catch (Exception e)
        {
            Debug.LogError($"{logPrefix}Session doğrulama hatası: {e.Message}");
            return false; // Hata durumunda geçersiz say
        }
    }

    // RemoveSession için ID parametresi alan overload eklendi
    private async Task RemoveSession(string userIdToRemove = null)
    {
        string targetUserId = userIdToRemove ?? currentUser?.UserId;
        if (string.IsNullOrEmpty(targetUserId) || isQuitting)
        {
             Debug.LogWarning($"{logPrefix}RemoveSession: Geçersiz UserID ({targetUserId}) veya çıkış yapılıyor, işlem atlandı.");
             return;
        }
         Debug.Log($"{logPrefix}RemoveSession çağrıldı: UserID={targetUserId}");

        try
        {
            var userSessionRef = dbReference.Child("onlineUsers").Child(targetUserId);
            Debug.Log($"{logPrefix}RemoveSession: Ref: {userSessionRef.ToString()}");

            // OnDisconnect handler'ı kaldır (sadece mevcut kullanıcı için anlamlı)
            if (!isQuitting && targetUserId == currentUser?.UserId)
            {
                 Debug.Log($"{logPrefix}RemoveSession: OnDisconnect handler iptal ediliyor...");
                 try
                 {
                     await userSessionRef.OnDisconnect().Cancel();
                     Debug.Log($"{logPrefix}RemoveSession: OnDisconnect handler iptal edildi.");
                 }
                 catch (Exception cancelError)
                 {
                     // Bu hata genellikle bağlantı zaten koptuysa olur, görmezden gelinebilir.
                     Debug.LogWarning($"{logPrefix}OnDisconnect iptal hatası (genellikle önemsiz): {cancelError.Message}");
                 }
            }

            // Session'ı sil
            Debug.Log($"{logPrefix}RemoveSession: Session verisi siliniyor...");
            await userSessionRef.RemoveValueAsync();
            Debug.Log($"{logPrefix}RemoveSession: Session verisi silindi.");

            // Eğer mevcut kullanıcının session'ı silindiyse, lokal ID'yi temizle ve coroutine'leri durdur
            if (targetUserId == currentUser?.UserId)
            {
                currentSessionId = null;
                StopAllCoroutines(); // Heartbeat'i durdur
                 Debug.Log($"{logPrefix}RemoveSession: Lokal session ID temizlendi ve coroutine'ler durduruldu.");
            }
        }
        catch (Exception e)
        {
            // Çıkış sırasında oluşan hatalar genellikle önemli değildir.
            if (!isQuitting)
            {
                Debug.LogError($"{logPrefix}Session silme hatası: {e.Message}");
                if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
            }
            else
            {
                 Debug.Log($"{logPrefix}Çıkış sırasında session silme hatası (muhtemelen önemsiz): {e.Message}");
            }
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log($"{logPrefix}OnApplicationQuit çağrıldı.");
        isQuitting = true;
        if (currentUser != null)
        {
            // Senkron olarak session'ı silmeye çalış (Firebase bunu doğrudan desteklemez)
            // En iyi yöntem OnDisconnect kullanmak veya sunucu tarafı bir çözüm.
            // Burada sadece lokal state'i temizleyip SignOut yapabiliriz.
             Debug.Log($"{logPrefix}Çıkış yapılıyor ve lokal state temizleniyor...");
            // RemoveSession senkron çalışmaz, bu yüzden burada çağırmak çok etkili olmayabilir.
            // await RemoveSession(); // await burada kullanılamaz

            // Firebase'den çıkış yap
            auth?.SignOut();
            currentUser = null;
            currentSessionId = null;
            StopAllCoroutines(); // Coroutine'leri durdur
        }
         Debug.Log($"{logPrefix}OnApplicationQuit tamamlandı.");
    }

    private void OnDestroy()
    {
         Debug.Log($"{logPrefix}OnDestroy çağrıldı.");
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
             Debug.Log($"{logPrefix}AuthStateChanged aboneliği kaldırıldı.");
        }
        StopAllCoroutines(); // Tüm coroutine'leri durdur
         Debug.Log($"{logPrefix}Tüm coroutine'ler durduruldu.");
    }
}