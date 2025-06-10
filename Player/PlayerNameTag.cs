using UnityEngine;
using TMPro;
using Photon.Pun;
using System.Collections;

public class PlayerNameTag : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] public GameObject nameTagContainer;
    [SerializeField] private float yOffset = 0.9f; // Karakterin üzerinde ne kadar yüksekte görüneceği

    [Header("Settings")]
    [SerializeField] private bool hideLocalPlayerName = false; // Kendi ismimizi gizleyelim mi?
    [SerializeField] private Color localPlayerColor = Color.green; // Kendi ismimiz için renk
    [SerializeField] private Color otherPlayerColor = Color.white; // Diğer oyuncuların isim rengi

    private Camera mainCamera;
    private Transform targetTransform;
    private PlayerController playerController;
    private Canvas nameTagCanvas;
    private string playerName = "";
    private PlayerStats playerStats; // PlayerStats referansı

    private void Awake()
    {
        // Üst PhotonView bileşenine referans al (base.photonView'u kullan)
        playerController = GetComponentInParent<PlayerController>();
        targetTransform = transform.parent; // Oyuncu objesi
        playerStats = GetComponentInParent<PlayerStats>(); // PlayerStats'ı bul
        
        // Eğer nameTagCanvas null ise bileşeni al
        if (nameTagContainer != null)
        {
            nameTagCanvas = nameTagContainer.GetComponent<Canvas>();
        }
        
        // Eğer nameTagContainer null ise uyarı ver
        if (nameTagContainer == null)
        {
            Debug.LogError("PlayerNameTag: nameTagContainer is null! Please assign it in the inspector.");
            enabled = false;
            return;
        }

        if (playerStats == null)
        {
            Debug.LogWarning("PlayerNameTag: PlayerStats component could not be found in parent! Level might not display correctly initially.", this);
            // StartCoroutine içinde tekrar bulmayı deneyebiliriz.
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
        StartCoroutine(InitializeNameTag());
    }

    private IEnumerator InitializeNameTag()
    {
        // Ana kamera yoksa bulana kadar bekle
        while (mainCamera == null)
        {
            mainCamera = Camera.main;
            yield return null;
        }
        
        // Canvas'ın world space olduğundan emin ol
        if (nameTagCanvas != null)
        {
            nameTagCanvas.renderMode = RenderMode.WorldSpace;
            nameTagCanvas.worldCamera = mainCamera;
        }
        
        // Eğer Awake'de bulamadıysak PlayerStats'ı tekrar dene
        if (playerStats == null)
        {
            playerStats = GetComponentInParent<PlayerStats>();
            if (playerStats != null && base.photonView.IsMine)
            {
                // Yerel oyuncuysa ve statları bulduysak olaya abone ol
                playerStats.OnStatsUpdated += UpdateNameDisplay;
            }
        }

        // Photon nickname'i al
        if (base.photonView != null)
        {
            if (base.photonView.Owner != null && !string.IsNullOrEmpty(base.photonView.Owner.NickName))
            {
                playerName = base.photonView.Owner.NickName;
            }
            else
            {
                // Eğer nickname yoksa asenkron olarak Firebase'den kullanıcı adını almaya çalış
                StartCoroutine(GetPlayerNameFromFirebase());
            }
        }
        
        // Kullanıcı adını göster (ve seviyeyi)
        UpdateNameDisplay();
    }

    private IEnumerator GetPlayerNameFromFirebase()
    {
        if (FirebaseAuthManager.Instance == null)
        {
            yield break;
        }
        
        // Eğer bu local oyuncuysa
        if (base.photonView.IsMine)
        {
            // Firebase'den email al
            if (FirebaseAuthManager.Instance.IsLoggedIn && FirebaseAuthManager.Instance.CurrentUser != null)
            {
                string email = FirebaseAuthManager.Instance.CurrentUser.Email;
                string username = email.Split('@')[0]; // E-postadan kullanıcı adını al
                playerName = username;
                
                // Photon nickname'i güncelle ki diğer oyuncular da görebilsin
                PhotonNetwork.NickName = username;
                base.photonView.RPC("SyncPlayerName", RpcTarget.OthersBuffered, username);
            }
        }
        
        UpdateNameDisplay();
    }

    [PunRPC]
    private void SyncPlayerName(string name)
    {
        playerName = name;
        UpdateNameDisplay(); // İsim güncellendiğinde text'i tekrar ayarla
    }

    private void UpdateNameDisplay()
    {
        if (nameText == null) return;

        string ownerName = "Sahipsiz";
        if (base.photonView != null && base.photonView.Owner != null)
        {
            ownerName = base.photonView.Owner.NickName ?? "İsimsiz sahip";
        }
        else if (base.photonView == null)
        {
            ownerName = "PhotonView Yok";
        }

        // Eğer playerStats hala null ise tekrar bulmayı dene (nadiren gerekebilir)
        if (playerStats == null)
        {
            playerStats = GetComponentInParent<PlayerStats>();
             if (playerStats != null)
            {
                // Yerel oyuncuysa ve statları yeni bulduysak olaya abone ol
                 // Önce eski aboneliği kaldır (güvenlik için)
                playerStats.OnStatsUpdated -= UpdateNameDisplay; 
                playerStats.OnStatsUpdated += UpdateNameDisplay;
            }
        }

        int currentLevel = 1; // Varsayılan seviye
        if (playerStats != null)
        {
            currentLevel = playerStats.CurrentLevel;
            Debug.Log($"[PlayerNameTag] UpdateNameDisplay - Oyuncu: {ownerName} (View: {base.photonView?.ViewID ?? 0}), PlayerStats bulundu. PlayerStats Seviyesi: {playerStats.CurrentLevel}, Gösterilecek Seviye: {currentLevel}");
        }
        else
        {
            Debug.LogWarning($"[PlayerNameTag] UpdateNameDisplay - Oyuncu: {ownerName} (View: {base.photonView?.ViewID ?? 0}), PlayerStats NULL! Varsayılan seviye ({currentLevel}) gösteriliyor.");
        }
        
        // İsmi ve seviyeyi birleştir
        string displayText = string.IsNullOrEmpty(playerName) ? "Loading..." : playerName;
        nameText.text = $"{displayText} Lv.{currentLevel}";
            
        // Kendi oyuncumuza özel renk ata ve gizleme kontrolü
        if (base.photonView.IsMine)
        {
            nameText.color = localPlayerColor;
            if (hideLocalPlayerName && nameTagContainer != null)
            {
                nameTagContainer.SetActive(false);
            }
            else if (nameTagContainer != null)
            {
                 nameTagContainer.SetActive(true); // Eğer gizlenmeyecekse göster
            }
        }
        else // Diğer oyuncular
        {
            nameText.color = otherPlayerColor;
            if (nameTagContainer != null) 
            {
                nameTagContainer.SetActive(true);
            }
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }
        
        if (targetTransform == null)
        {
            return;
        }
        
        // Yazının pozisyonunu karakterin üzerine ayarla
        nameTagContainer.transform.position = targetTransform.position + Vector3.up * yOffset;
        
        // Yazıyı her zaman kameraya dönük tut (billboard effect)
        nameTagContainer.transform.rotation = mainCamera.transform.rotation;
    }

    // FirebaseAuthManager'deki oturum değişikliğini dinle
    public override void OnEnable()
    {
        base.OnEnable(); // Temel sınıfın metodunu çağır
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;
        }
        // PlayerStats olayına abone ol (eğer bulunduysa)
        if (playerStats != null)
        {
             // Önce eski aboneliği kaldır (güvenlik için)
            playerStats.OnStatsUpdated -= UpdateNameDisplay; 
            playerStats.OnStatsUpdated += UpdateNameDisplay;
             // Aktifleştiğinde bir kez güncelle (isim henüz yüklenmemiş olabilir)
             // UpdateNameDisplay(); // InitializeNameTag sonunda çağrılıyor zaten
        }
    }

    public override void OnDisable()
    {
        base.OnDisable(); // Temel sınıfın metodunu çağır
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged -= OnAuthStateChanged;
        }
         // PlayerStats olayından aboneliği kaldır
        if (playerStats != null)
        {
            playerStats.OnStatsUpdated -= UpdateNameDisplay;
        }
    }

    private void OnAuthStateChanged(Firebase.Auth.FirebaseUser user)
    {
        if (user != null && base.photonView.IsMine)
        {
            string email = user.Email;
            string username = email.Split('@')[0];
            playerName = username;
            
            // Photon nickname'i güncelle
            PhotonNetwork.NickName = username;
            base.photonView.RPC("SyncPlayerName", RpcTarget.OthersBuffered, username);
            
            UpdateNameDisplay(); // İsim güncellendiğinde text'i tekrar ayarla
        }
    }

    // PlayerHealth tarafından çağrılacak yardımcı metot
    public bool GetHideLocalPlayerNameSetting()
    {
        return hideLocalPlayerName;
    }
} 