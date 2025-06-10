using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using Firebase.Auth;
using System.Threading.Tasks;  // Task sınıfı için eklendi
using Firebase;
using UnityEngine.Threading;

public class MainMenu : MonoBehaviour
{
    AudioManager audioManager;
    [SerializeField] private GameObject backgroundObject;
    [SerializeField] private TextMeshProUGUI connectionText; // Bağlantı durumu metni

    [Header("Login Panel")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField loginEmailInput;
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button closePanelButton;  
    [SerializeField] private TextMeshProUGUI loginStatusText;

    [Header("Registration Panel")]
    [SerializeField] private GameObject registrationPanel;  // Panel'in kendisi
    [SerializeField] private TMP_InputField registerEmailInput;
    [SerializeField] private TMP_InputField registerPasswordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;
    [SerializeField] private Button registerButton;        // Panel içindeki kayıt butonu
    [SerializeField] private TextMeshProUGUI registerStatusText;

    [Header("User Info Panel")]
    [SerializeField] private GameObject userInfoPanel;
    [SerializeField] private TextMeshProUGUI userEmailText;
    [SerializeField] private Button logoutButton;

    [Header("Error Message Panel")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private Button errorCloseButton;
    [SerializeField] private Image errorPanelBackground;  // Error panel içindeki arka plan paneli

    // Email format kontrolü için regex pattern
    private readonly string emailPattern = @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$";

    public enum ErrorType
    {
        Critical,  // Kritik hatalar (bağlantı kopması, sistem hatası)
        Warning,   // Uyarılar (veri kaybı olabilir, yavaş bağlantı)
        Info       // Bilgilendirme mesajları (bilgi verme amaçlı)
    }

    private void Awake()
    {
        // Önce UnityMainThreadDispatcher'ı oluştur
        if (UnityMainThreadDispatcher.Instance() == null)
        {
            Debug.LogError("UnityMainThreadDispatcher oluşturulamadı!");
            return;
        }

        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();

        // Firebase'i initialize et ve FirebaseAuthManager'ı oluştur
        InitializeFirebase();

        // UI elementlerini ayarla
        SetupUI();

        // Event listener'ları ayarla
        SetupEventListeners();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                Debug.Log("Firebase initialized successfully!");
                
                // FirebaseAuthManager'ı ana thread'de oluştur
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (FirebaseAuthManager.Instance == null)
                    {
                        GameObject firebaseManager = new GameObject("FirebaseAuthManager");
                        firebaseManager.AddComponent<FirebaseAuthManager>();
                        Debug.Log("FirebaseAuthManager oluşturuldu.");
                        
                        // FirebaseAuthManager oluşturulduktan sonra event'leri bağla
                        FirebaseAuthManager.Instance.OnAuthError += ShowError;
                        FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;
                        
                        // Kullanıcı giriş durumunu kontrol et
                        CheckUserLoginStatus();
                    }
                });
            }
            else
            {
                Debug.LogError($"Firebase initialization failed: {dependencyStatus}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    ShowError("Firebase bağlantısı başlatılamadı! Lütfen internet bağlantınızı kontrol edin.");
                });
            }
        });
    }

    private void SetupUI()
    {
        if (connectionText != null)
            connectionText.gameObject.SetActive(false);

        if (loginPanel != null)
            loginPanel.SetActive(false);

        if (registrationPanel != null)
            registrationPanel.SetActive(false);

        if (errorPanel != null)
            errorPanel.SetActive(false);

        if (userInfoPanel != null)
            userInfoPanel.SetActive(false);
    }

    private void SetupEventListeners()
    {
        if (errorCloseButton != null)
            errorCloseButton.onClick.AddListener(CloseErrorPanel);

        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutButton);
    }

    private void CheckUserLoginStatus()
    {
        if (FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsLoggedIn)
        {
            string email = FirebaseAuthManager.Instance.CurrentUser.Email;
            ShowUserInfo(email);
        }
        else
        {
            HideUserInfo();
        }
    }

    private void ShowUserInfo(string email)
    {
        if (userInfoPanel != null && userEmailText != null)
        {
            string username = email.Split('@')[0];
            userEmailText.text = $"Connected as:\n{username}";
            userInfoPanel.SetActive(true);
        }
    }

    private void HideUserInfo()
    {
        if (userInfoPanel != null)
        {
            userInfoPanel.SetActive(false);
        }
    }

    public void OnLogoutButton()
    {
        audioManager.PlaySFX(audioManager.button);
        if (FirebaseAuthManager.Instance != null)
        {
            StartCoroutine(LogoutCoroutine());
            HideUserInfo();
        }
    }

    private IEnumerator LogoutCoroutine()
    {
        var task = FirebaseAuthManager.Instance.LogOut();
        yield return new WaitUntil(() => task.IsCompleted);
    }

    public void ShowError(string message)
    {
        // Varsayılan olarak Critical hata tipi kullan
        ShowError(message, ErrorType.Critical, false);
    }

    public void ShowError(string message, ErrorType errorType, bool autoClose = false)
    {
        if (errorPanel != null && errorText != null)
        {
            // Hata mesajını ayarla
            errorText.text = message;
            
            // Hata tipine göre panelin rengini değiştir
            if (errorPanelBackground != null)
            {
                switch (errorType)
                {
                    case ErrorType.Critical:
                        errorPanelBackground.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);  // Kırmızı
                        audioManager.PlaySFX(audioManager.button);  // Daha ciddi bir ses eklenebilir
                        break;
                    case ErrorType.Warning:
                        errorPanelBackground.color = new Color(0.9f, 0.7f, 0.1f, 0.9f);  // Sarı
                        audioManager.PlaySFX(audioManager.button);
                        break;
                    case ErrorType.Info:
                        errorPanelBackground.color = new Color(0.2f, 0.6f, 0.8f, 0.9f);  // Mavi
                        audioManager.PlaySFX(audioManager.button);  // Daha yumuşak bir ses eklenebilir
                        break;
                }
            }
            
            // Uygun ikon ve başlık eklenebilir
            // Bu kısım UI'nızda ilgili elementler varsa eklenebilir
            
            // Paneli göster
            errorPanel.SetActive(true);
            
            // Otomatik kapanma
            if (autoClose)
            {
                StartCoroutine(AutoCloseErrorPanel(errorType));
            }
        }
    }
    
    private IEnumerator AutoCloseErrorPanel(ErrorType errorType)
    {
        // Hata tipine göre farklı bekleme süreleri
        float waitTime;
        switch (errorType)
        {
            case ErrorType.Critical:
                waitTime = 7f;  // Kritik hatalar için daha uzun süre
                break;
            case ErrorType.Warning:
                waitTime = 5f;
                break;
            case ErrorType.Info:
                waitTime = 3f;  // Bilgi mesajları için kısa süre
                break;
            default:
                waitTime = 5f;
                break;
        }
        
        yield return new WaitForSeconds(waitTime);
        
        if (errorPanel != null && errorPanel.activeSelf)
        {
            errorPanel.SetActive(false);
        }
    }

    // Yaygın hata mesajları için özelleştirilmiş metotlar
    public void ShowConnectionError(string message)
    {
        string enhancedMessage = "Bağlantı Hatası: " + message + "\n\nÖnerilen Çözümler:\n- İnternet bağlantınızı kontrol edin\n- Sunucu durumunu kontrol edin\n- Daha sonra tekrar deneyin";
        ShowError(enhancedMessage, ErrorType.Critical, false);
    }
    
    public void ShowLoginError(string message)
    {
        string baseMessage = "Giriş Hatası: ";
        string solutions = "\n\nÖnerilen Çözümler:";
        
        if (message.Contains("password is invalid") || message.Contains("wrong password"))
        {
            // Yanlış şifre hatası
            string enhancedMessage = baseMessage + "Şifreniz yanlış!" + 
                                    solutions + 
                                    "\n- Şifrenizi doğru girdiğinizden emin olun" + 
                                    "\n- Şifrenizi unuttuysanız şifre sıfırlama işlemi yapın";
            ShowError(enhancedMessage, ErrorType.Warning, false);
        }
        else if (message.Contains("user record does not exist") || message.Contains("no user record"))
        {
            // Kullanıcı bulunamadı hatası
            string enhancedMessage = baseMessage + "Bu e-posta adresiyle kayıtlı hesap bulunamadı!" + 
                                    solutions + 
                                    "\n- E-posta adresinizi doğru girdiğinizden emin olun" + 
                                    "\n- Hesabınız yoksa önce kayıt olun";
            ShowError(enhancedMessage, ErrorType.Warning, false);
        }
        else if (message.Contains("too many unsuccessful") || message.Contains("temporarily disabled"))
        {
            // Çok fazla başarısız deneme hatası
            string enhancedMessage = baseMessage + "Çok fazla başarısız giriş denemesi nedeniyle hesabınız geçici olarak kilitlendi!" + 
                                    solutions + 
                                    "\n- Bir süre bekleyin ve daha sonra tekrar deneyin" + 
                                    "\n- Şifrenizi unuttuysanız şifre sıfırlama işlemi yapın";
            ShowError(enhancedMessage, ErrorType.Warning, false);
        }
        else
        {
            // Genel giriş hatası
            string enhancedMessage = baseMessage + message + 
                                    solutions + 
                                    "\n- Hesap bilgilerinizi kontrol edin" + 
                                    "\n- Kayıtlı değilseniz yeni hesap oluşturun" + 
                                    "\n- İnternet bağlantınızı kontrol edin";
            ShowError(enhancedMessage, ErrorType.Warning, false);
        }
    }
    
    public void ShowRegistrationError(string message)
    {
        string baseMessage = "Kayıt Hatası: ";
        string solutions = "\n\nÖnerilen Çözümler:";
        
        if (message.Contains("email address is already in use"))
        {
            // E-posta zaten kullanımda hatası
            string enhancedMessage = baseMessage + "Bu e-posta adresi zaten kullanımda!" + 
                                    solutions + 
                                    "\n- Farklı bir e-posta adresi ile kayıt olmayı deneyin" +
                                    "\n- Şifrenizi unuttuysanız şifre sıfırlama işlemi yapın";
            ShowError(enhancedMessage, ErrorType.Warning, false);
        }
        else
        {
            // Diğer kayıt hataları
            string enhancedMessage = baseMessage + message + 
                                    solutions + 
                                    "\n- Ağ bağlantınızı kontrol edin" +
                                    "\n- Bilgilerinizi doğru girdiğinizden emin olun";
            ShowError(enhancedMessage, ErrorType.Warning, false);
        }
    }
    
    public void ShowInfoMessage(string message)
    {
        ShowError(message, ErrorType.Info, true);  // Bilgi mesajları otomatik kapanır
    }

    public void CloseErrorPanel()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
            audioManager.PlaySFX(audioManager.button);
        }
    }

    // Ana menüdeki Sign In butonuna tıklandığında çağrılır
    public void OpenLoginPanel()
    {
        audioManager.PlaySFX(audioManager.button);
        loginPanel.SetActive(true);
        registrationPanel.SetActive(false);
        backgroundObject.SetActive(false);
        
        // Input alanlarını temizle
        loginEmailInput.text = "";
        loginPasswordInput.text = "";
        loginStatusText.text = "";
    }

    public void CloseLoginPanel()
    {
        audioManager.PlaySFX(audioManager.button);
        loginPanel.SetActive(false);
        backgroundObject.SetActive(true);
    }

    public async void OnLoginButton()
    {
        if (string.IsNullOrEmpty(loginEmailInput.text) || 
            string.IsNullOrEmpty(loginPasswordInput.text))
        {
            ShowStatus(loginStatusText, "Lütfen tüm alanları doldurun!", Color.red);
            return;
        }

        // E-posta formatı kontrolü
        if (!Regex.IsMatch(loginEmailInput.text, emailPattern))
        {
            ShowStatus(loginStatusText, "Geçerli bir e-posta adresi girin!", Color.red);
            return;
        }

        // Şifre uzunluğu kontrolü
        if (loginPasswordInput.text.Length < 6)
        {
            ShowStatus(loginStatusText, "Şifre en az 6 karakter olmalı!", Color.red);
            return;
        }

        // FirebaseAuthManager kontrolü
        if (FirebaseAuthManager.Instance == null)
        {
            ShowStatus(loginStatusText, "Firebase bağlantısı başlatılamadı! Lütfen tekrar deneyin.", Color.red);
            return;
        }

        // Butonları devre dışı bırak
        loginButton.interactable = false;
        closePanelButton.interactable = false;
        
        ShowStatus(loginStatusText, "Giriş Yapılıyor", Color.white);
        // Animasyonu başlat
        Coroutine animationCoroutine = StartCoroutine(AnimateStatusText(loginStatusText, "Giriş Yapılıyor"));

        try
        {
            var loginTask = FirebaseAuthManager.Instance.LoginUser(loginEmailInput.text, loginPasswordInput.text);
            var timeoutTask = Task.Delay(20000); // 20 saniye timeout

            var completedTask = await Task.WhenAny(loginTask, timeoutTask);
            
            if (completedTask == loginTask)
            {
                // Animasyonu durdur
                if (animationCoroutine != null)
                    StopCoroutine(animationCoroutine);
                
                bool loginSuccess = await loginTask;
                if (loginSuccess)
                {
                    ShowStatus(loginStatusText, "Giriş başarılı!", Color.green);
                    ShowUserInfo(loginEmailInput.text);
                    
                    // Oyuna bağlanma işlemini başlat (animasyonlu)
                    StartCoroutine(ConnectToGameWithAnimation(loginStatusText));
                }
                else
                {
                    ShowStatus(loginStatusText, "Giriş başarısız!", Color.red);
                    
                    // Genel hata göster
                    ShowLoginError("Giriş işlemi başarısız oldu. E-posta adresinizi ve şifrenizi kontrol edip tekrar deneyin.");
                }
            }
            else
            {
                // Animasyonu durdur
                if (animationCoroutine != null)
                    StopCoroutine(animationCoroutine);
                
                // Timeout durumunda
                ShowStatus(loginStatusText, "Giriş zaman aşımına uğradı!", Color.red);
                ShowLoginError("Giriş işlemi zaman aşımına uğradı. İnternet bağlantınızı kontrol edip tekrar deneyin.");
                
                // Eğer kullanıcı giriş yapmış durumda ise çıkış yap
                if (FirebaseAuthManager.Instance.IsLoggedIn)
                {
                    await FirebaseAuthManager.Instance.LogOut();
                    HideUserInfo(); // User info panelini gizle
                }
            }
        }
        catch (System.Exception ex)
        {
            // Animasyonu durdur
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
                
            ShowStatus(loginStatusText, "Giriş başarısız!", Color.red);
            ShowLoginError(ex.Message);
            Debug.LogError("Giriş hatası (Exception): " + ex.Message);
        }
        finally
        {
            // Animasyonu durdur
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
                
            // Butonları tekrar aktif et
            loginButton.interactable = true;
            closePanelButton.interactable = true;
        }
    }

    private IEnumerator ConnectToGame()
    {
        yield return new WaitForSeconds(1.5f);
        loginPanel.SetActive(false);
        backgroundObject.SetActive(true);
    }

    // Animasyonlu bağlanma işlemi
    private IEnumerator ConnectToGameWithAnimation(TextMeshProUGUI statusText)
    {
        // Kısa bir bekleme süresi
        yield return new WaitForSeconds(0.7f);
        
        // Oyuna bağlanılıyor mesajı için nokta animasyonu başlat
        Coroutine connectingAnimation = StartCoroutine(AnimateStatusText(statusText, "Oyuna bağlanılıyor"));
        
        // Bağlantı işlemini başlat - ConnectToGame işlevini burada gerçekleştiriyoruz
        yield return new WaitForSeconds(1.5f);
        
        // Animasyonu durdur
        if (connectingAnimation != null)
            StopCoroutine(connectingAnimation);
            
        // Paneli kapat ve arka planı göster
        loginPanel.SetActive(false);
        backgroundObject.SetActive(true);
    }

    // Ana menüdeki Sign In butonuna tıklandığında çağrılır
    public void OpenRegistrationPanel()
    {
        audioManager.PlaySFX(audioManager.button);
        registrationPanel.SetActive(true);
        loginPanel.SetActive(false);
        backgroundObject.SetActive(false);
        
        // Input alanlarını temizle
        registerEmailInput.text = "";
        registerPasswordInput.text = "";
        confirmPasswordInput.text = "";
        registerStatusText.text = "";
    }

    // Registration panel'deki Close butonuna tıklandığında çağrılır
    public void CloseRegistrationPanel()
    {
        audioManager.PlaySFX(audioManager.button);
        registrationPanel.SetActive(false);
        backgroundObject.SetActive(true);
    }

    // Registration panel'deki Register butonuna tıklandığında çağrılır
    public async void OnRegisterButton()
    {
        if (string.IsNullOrEmpty(registerEmailInput.text) || 
            string.IsNullOrEmpty(registerPasswordInput.text) || 
            string.IsNullOrEmpty(confirmPasswordInput.text))
        {
            ShowStatus(registerStatusText, "Lütfen tüm alanları doldurun!", Color.red);
            return;
        }

        if (!Regex.IsMatch(registerEmailInput.text, emailPattern))
        {
            ShowStatus(registerStatusText, "Geçerli bir email adresi girin!", Color.red);
            return;
        }

        if (registerPasswordInput.text != confirmPasswordInput.text)
        {
            ShowStatus(registerStatusText, "Şifreler eşleşmiyor!", Color.red);
            return;
        }

        if (registerPasswordInput.text.Length < 6)
        {
            ShowStatus(registerStatusText, "Şifre en az 6 karakter olmalı!", Color.red);
            return;
        }
        
        // Butonları devre dışı bırak
        if (registerButton != null)
            registerButton.interactable = false;
        
        ShowStatus(registerStatusText, "Kayıt Yapılıyor", Color.white);
        // Animasyonu başlat
        Coroutine animationCoroutine = StartCoroutine(AnimateStatusText(registerStatusText, "Kayıt Yapılıyor"));
        
        try
        {
            // FirebaseAuthManager'dan sonuç al
            bool success = await FirebaseAuthManager.Instance.RegisterUser(registerEmailInput.text, registerPasswordInput.text);
            
            // Animasyonu durdur
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
                
            if (success)
            {
                ShowStatus(registerStatusText, "Kayıt başarılı!", Color.green);
                StartCoroutine(AutoCloseRegistrationPanel());
                ShowUserInfo(registerEmailInput.text);
            }
            else
            {
                // Başarısız durumu göster
                ShowStatus(registerStatusText, "Kayıt başarısız!", Color.red);
                
                // Genel hata göster
                ShowRegistrationError("Kayıt işlemi başarısız oldu. Bu e-posta adresi zaten kullanımda olabilir veya geçersiz olabilir.");
            }
        }
        catch (System.Exception ex)
        {
            // Animasyonu durdur
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
                
            // Hata mesajını göster
            ShowStatus(registerStatusText, "Kayıt başarısız!", Color.red);
            ShowRegistrationError(ex.Message);
            Debug.LogError("Kayıt hatası (Exception): " + ex.Message);
        }
        finally
        {
            // Butonları tekrar aktif et
            if (registerButton != null)
                registerButton.interactable = true;
        }
    }

    private void ShowStatus(TextMeshProUGUI statusText, string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }

    private IEnumerator AutoCloseRegistrationPanel()
    {
        yield return new WaitForSeconds(1.5f);
        registrationPanel.SetActive(false);
        backgroundObject.SetActive(true);
    }

    public void PlayButton()
    {
        StartCoroutine(PlayButtonCoroutine());
    }

    private IEnumerator PlayButtonCoroutine()
    {
        // Önce giriş kontrolü yap
        if (FirebaseAuthManager.Instance == null || !FirebaseAuthManager.Instance.IsLoggedIn)
        {
            ShowLoginError("Oyuna başlamak için önce giriş yapmalısınız.");
            yield break;
        }

        // Session kontrolü yap
        var validationTask = FirebaseAuthManager.Instance.ValidateSession();
        yield return new WaitUntil(() => validationTask.IsCompleted);

        if (!validationTask.Result)
        {
            ShowError("Oturumunuz başka bir cihazda açıldı!", ErrorType.Warning, false);
            StartCoroutine(LogoutCoroutine());
            yield break;
        }

        // UI'ı hazırla
        audioManager.PlaySFX(audioManager.button);
        audioManager.musicSource.Stop();
        HideUserInfo(); // User info panelini gizle
        
        if (backgroundObject != null)
        {
            backgroundObject.SetActive(false);
        }
        if (connectionText != null)
        {
            connectionText.gameObject.SetActive(true);
            StartCoroutine(AnimateLoadingText());
        }

        PhotonServerManager serverManager = FindObjectOfType<PhotonServerManager>();
        if (serverManager == null)
        {
            GameObject serverManagerPrefab = Resources.Load<GameObject>("PhotonServerManager");
            if (serverManagerPrefab != null)
            {
                serverManager = Instantiate(serverManagerPrefab).GetComponent<PhotonServerManager>();
            }
            else
            {
                Debug.LogError("PhotonServerManager prefabı bulunamadı!");
                HandleConnectionError("PhotonServerManager prefabı bulunamadı!");
                ShowUserInfo(FirebaseAuthManager.Instance.CurrentUser.Email); // User info panelini geri göster
                yield break;
            }
        }

        // Bağlantıyı başlat
        PhotonServerManager.Instance.StartGameFromMenu();

        // 15 saniye timeout kontrolü
        float timeoutDuration = 15f;
        float elapsedTime = 0f;
        bool isConnected = false;

        while (elapsedTime < timeoutDuration && !isConnected)
        {
            if (PhotonNetwork.IsConnectedAndReady)
            {
                isConnected = true;
                break;
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (!isConnected)
        {
            // Timeout oldu, bağlantıyı kes
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
            HandleConnectionError("Sunucuya bağlanılamadı! Lütfen internet bağlantınızı kontrol edip tekrar deneyin.");
            ShowUserInfo(FirebaseAuthManager.Instance.CurrentUser.Email); // User info panelini geri göster
        }
    }

    private void HandleConnectionError(string error = null)
    {
        if (!string.IsNullOrEmpty(error))
        {
            ShowConnectionError(error);
        }
        
        if (backgroundObject != null)
        {
            backgroundObject.SetActive(true);
        }
        if (connectionText != null)
        {
            connectionText.gameObject.SetActive(false);
        }
        if (audioManager != null && audioManager.musicSource != null && !audioManager.musicSource.isPlaying)
        {
            audioManager.musicSource.Play();
        }
    }

    private IEnumerator AnimateLoadingText()
    {
        string[] loadingStates = { "Loading.", "Loading..", "Loading..." };
        int currentState = 0;

        while (connectionText.gameObject.activeSelf)
        {
            connectionText.text = loadingStates[currentState];
            currentState = (currentState + 1) % loadingStates.Length;
            yield return new WaitForSeconds(0.2f);
        }
    }

    // Yeni nokta animasyonu metodu (Giriş Yapılıyor. -> Giriş Yapılıyor.. -> Giriş Yapılıyor...)
    private IEnumerator AnimateStatusText(TextMeshProUGUI statusText, string baseText)
    {
        string[] statusStates = { baseText + ".", baseText + "..", baseText + "..." };
        int currentState = 0;

        while (true)
        {
            statusText.text = statusStates[currentState];
            currentState = (currentState + 1) % statusStates.Length;
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator CheckConnectionStatus()
    {
        yield return new WaitForSeconds(15f); // 15 saniye bekle

        // Eğer hala bağlantı kurulmadıysa background'ı geri göster ve metni gizle
        if (!PhotonNetwork.IsConnected)
        {
            if (backgroundObject != null)
            {
                backgroundObject.SetActive(true);
            }
            if (connectionText != null)
            {
                connectionText.gameObject.SetActive(false);
            }
        }
    }

    public void GoToScene(int sceneIndex)
    {
        audioManager.PlaySFX(audioManager.button);
        audioManager.musicSource.Stop();
        StartCoroutine(ChangeSceneWithDelay(sceneIndex, 1f)); 
    }

    private IEnumerator ChangeSceneWithDelay(int sceneIndex, float delay)
    {
        yield return new WaitForSeconds(delay); 
        SceneManager.LoadScene(sceneIndex);
    }

    public void QuitApp()
    {
        Application.Quit();
        Debug.Log("Program kapatildi.");
    }

    // Auth state değiştiğinde çağrılacak metod
    private void OnAuthStateChanged(FirebaseUser user)
    {
        if (user != null)
        {
            ShowUserInfo(user.Email);
        }
        else
        {
            HideUserInfo();
        }
    }

    private void OnEnable()
    {
        if (PhotonServerManager.Instance != null)
        {
            PhotonServerManager.Instance.OnConnectionError += HandleConnectionError;
        }
    }

    private void OnDisable()
    {
        if (PhotonServerManager.Instance != null)
        {
            PhotonServerManager.Instance.OnConnectionError -= HandleConnectionError;
        }
    }
}
