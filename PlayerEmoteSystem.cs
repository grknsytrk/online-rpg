using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;

// Oyuncu emoji/emote sistemi - çok oyunculu ağ üzerinden emoji gösterimini sağlar
public class PlayerEmoteSystem : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private GameObject emoteContainer; // Emoji container objesi
    [SerializeField] private Image emoteImage; // Emoji gösterecek image bileşeni
    [SerializeField] private GameObject chatEmote; // Chat emote GameObject referansı
    
    [Header("Emote Settings")]
    [SerializeField] private Sprite[] emoteSprites; // Kullanılabilecek emoji/emote sprite'ları
    [SerializeField] private float displayDuration = 2f; // Gösterim süresi
    
    [Header("Animation Settings")]
    [SerializeField] private float scaleUpDuration = 0.3f; // Büyüme animasyon süresi
    [SerializeField] private float scaleDownDuration = 0.3f; // Küçülme animasyon süresi
    [SerializeField] private Vector3 maxScale = new Vector3(1f, 1f, 1f); // Maksimum büyüklük
    [SerializeField] private Vector3 minScale = Vector3.zero; // Minimum büyüklük
    
    // Özel değişkenler
    private Camera mainCamera; // Ana kamera referansı
    private Transform targetTransform; // Hedef transform (oyuncu)
    private Canvas emoteCanvas; // Emoji canvas'ı
    private bool showingEmote = false; // Şu an emoji gösteriliyor mu?
    private Coroutine emoteDisplayCoroutine; // Emoji gösterim coroutine'i
    private Vector3 originalScale; // Orijinal ölçek
    private UIManager uiManager; // UIManager referansı
    
    // Obje oluşturulduğunda çalışır
    private void Awake()
    {
        targetTransform = transform.parent; // Oyuncu objesi
        
        // Emoji container kontrolü ve ayarları
        if (emoteContainer != null)
        {
            emoteCanvas = emoteContainer.GetComponent<Canvas>();
            originalScale = emoteContainer.transform.localScale; // Orijinal ölçeği kaydet
        }
        
        // Gerekli bileşenlerin kontrolü
        if (emoteContainer == null || emoteImage == null)
        {
            Debug.LogError("PlayerEmoteSystem: emoteContainer veya emoteImage atanmamış!");
            enabled = false;
            return;
        }
        
        // Başlangıçta emote'u gizle
        emoteImage.gameObject.SetActive(false);
        if (chatEmote != null)
        {
            chatEmote.SetActive(false);
        }
        
        // UIManager referansını bul
        uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogWarning("PlayerEmoteSystem: UIManager bulunamadı!");
        }
    }
    
    // Oyun başladığında çalışır
    private void Start()
    {
        mainCamera = Camera.main; // Ana kamerayı al
        
        // Canvas'ın world space olmasını sağla
        if (emoteCanvas != null)
        {
            emoteCanvas.renderMode = RenderMode.WorldSpace;
            emoteCanvas.worldCamera = mainCamera;
        }
    }
    
    // Emote göster - Oyuncu kontrolleri tarafından çağrılır
    public void ShowEmote(int emoteIndex)
    {
        // Geçerli bir emote index'i mi kontrol et
        if (emoteIndex < 0 || emoteIndex >= emoteSprites.Length)
        {
            Debug.LogWarning($"Geçersiz emote index: {emoteIndex}");
            return;
        }
        
        // Sadece kendi karakterimiz için RPC çağrısı yapalım
        if (photonView.IsMine)
        {
            // İlk önce normal emote için RPC
            photonView.RPC("RPC_ShowEmote", RpcTarget.All, emoteIndex);
            
            // Ayrıca chat emote durumunu da güncelle
            if (chatEmote != null)
            {
                bool isChatOpen = uiManager != null && uiManager.IsChatOpen();
                string chatText = uiManager != null ? uiManager.GetChatInputText() : string.Empty;
                bool shouldShowChatEmote = isChatOpen && !string.IsNullOrEmpty(chatText);
                
                // Chat emoji durumunu tüm oyunculara gönder
                photonView.RPC("RPC_UpdateChatEmoteVisibility", RpcTarget.All, shouldShowChatEmote, emoteIndex);
            }
            
            Debug.Log($"Emote RPC gönderildi: {emoteIndex}");
        }
    }
    
    // Ağ üzerinden emoji gösterme fonksiyonu
    [PunRPC]
    private void RPC_ShowEmote(int emoteIndex)
    {
        // Geçerli sprite kontrolü
        if (emoteIndex < 0 || emoteIndex >= emoteSprites.Length || emoteSprites[emoteIndex] == null)
        {
            Debug.LogWarning($"RPC_ShowEmote: Geçersiz emote index veya sprite: {emoteIndex}");
            return;
        }
        
        // Önceki emote gösterimi varsa durdur
        if (emoteDisplayCoroutine != null)
        {
            StopCoroutine(emoteDisplayCoroutine);
        }
        
        // Sprite'ı ayarla ve göster
        emoteImage.sprite = emoteSprites[emoteIndex];
        emoteImage.gameObject.SetActive(true);
        showingEmote = true;
        
        // Büyüme animasyonu başlat
        StartCoroutine(ScaleUpAnimation());
        
        // Chatbox açıksa chat emote'u da güncelle
        if (chatEmote != null)
        {
            Image chatEmoteImage = chatEmote.GetComponentInChildren<Image>();
            if (chatEmoteImage != null)
            {
                chatEmoteImage.sprite = emoteSprites[emoteIndex];
                
                // Chat açık ve içeriği varsa chat emote'u göster
                if (uiManager != null && photonView.IsMine)
                {
                    bool isChatOpen = uiManager.IsChatOpen();
                    string chatText = uiManager.GetChatInputText();
                    bool shouldShowChatEmote = isChatOpen && !string.IsNullOrEmpty(chatText);
                    
                    if (shouldShowChatEmote)
                    {
                        chatEmote.SetActive(true);
                    }
                }
            }
        }
        
        // Belirli bir süre sonra gizlemek için coroutine başlat
        emoteDisplayCoroutine = StartCoroutine(HideEmoteAfterDelay());
        
        Debug.Log($"Emote gösterildi (ID: {emoteIndex}, ViewID: {photonView.ViewID})");
    }
    
    // Büyüme animasyonu
    private IEnumerator ScaleUpAnimation()
    {
        float elapsedTime = 0f; // Geçen süre
        Vector3 startScale = minScale; // Başlangıç ölçeği
        Vector3 targetScale = maxScale; // Hedef ölçek
        
        emoteImage.transform.localScale = startScale; // Başlangıç ölçeğini ayarla
        
        // Animasyon döngüsü
        while (elapsedTime < scaleUpDuration)
        {
            elapsedTime += Time.deltaTime; // Zamanı güncelle
            float t = elapsedTime / scaleUpDuration; // Yüzdelik hesaplama
            t = Mathf.SmoothStep(0, 1, t); // Yumuşak geçiş için
            
            // Sadece emote image'ını ölçeklendir
            emoteImage.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null; // Bir frame bekle
        }
        
        emoteImage.transform.localScale = targetScale; // Son ölçeği kesin olarak ayarla
    }
    
    // Küçülme animasyonu
    private IEnumerator ScaleDownAnimation()
    {
        float elapsedTime = 0f; // Geçen süre
        Vector3 startScale = emoteImage.transform.localScale; // Şu anki ölçek
        Vector3 targetScale = minScale; // Hedef ölçek (küçük)
        
        // Animasyon döngüsü
        while (elapsedTime < scaleDownDuration)
        {
            elapsedTime += Time.deltaTime; // Zamanı güncelle
            float t = elapsedTime / scaleDownDuration; // Yüzdelik hesaplama
            t = Mathf.SmoothStep(0, 1, t); // Yumuşak geçiş için
            
            // Sadece emote image'ını ölçeklendir
            emoteImage.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null; // Bir frame bekle
        }
        
        emoteImage.transform.localScale = targetScale; // Son ölçeği ayarla
        emoteImage.gameObject.SetActive(false); // Emoji'yi gizle
        
        // Chat emote'u da gizle
        if (chatEmote != null)
        {
            chatEmote.SetActive(false);
        }
    }
    
    // Belirli süre sonra emoji'yi gizleme coroutine'i
    private IEnumerator HideEmoteAfterDelay()
    {
        // Gösterim süresi boyunca bekle
        yield return new WaitForSeconds(displayDuration);
        
        // Küçülme animasyonunu başlat
        yield return StartCoroutine(ScaleDownAnimation());
        
        showingEmote = false; // Artık emoji gösterilmiyor
        emoteDisplayCoroutine = null; // Coroutine'i temizle
    }
    
    // Her frame sonunda çalışır (kamera takibi için)
    private void LateUpdate()
    {
        // Kamera kontrolü
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }
        
        // Hedef veya emoji yok mu kontrol et
        if (targetTransform == null || !showingEmote)
        {
            return;
        }
        
        // Emote'u her zaman kameraya dönük tut (billboard effect)
        emoteContainer.transform.rotation = mainCamera.transform.rotation;
    }

    // Her frame çalışır
    void Update()
    {
        // Chat emoji durumunu kontrol et ve senkronize et
        if (uiManager != null && photonView.IsMine)
        {
            bool isChatOpen = uiManager.IsChatOpen(); // Chat açık mı?
            string chatText = uiManager.GetChatInputText(); // Chat metni
            bool shouldShowChatEmote = isChatOpen && !string.IsNullOrEmpty(chatText); // Chat emoji gösterilmeli mi?
            
            // Chat emoji durumu değiştiyse RPC ile herkese bildir
            if (chatEmote != null && chatEmote.activeSelf != shouldShowChatEmote)
            {
                // Tüm oyunculara chat emoji durumunu gönder
                photonView.RPC("RPC_UpdateChatEmoteVisibility", RpcTarget.All, shouldShowChatEmote, GetCurrentEmoteIndex());
            }
        }
    }
    
    // Chat emoji görünürlük güncelleme RPC'si
    [PunRPC]
    private void RPC_UpdateChatEmoteVisibility(bool isVisible, int emoteIndex)
    {
        // Chat emoji görünürlüğünü tüm oyuncularda güncelle
        if (chatEmote != null)
        {
            // Önce emoji sprite'ını ayarla
            Image chatEmoteImage = chatEmote.GetComponentInChildren<Image>();
            if (chatEmoteImage != null && emoteSprites != null && emoteIndex >= 0 && emoteIndex < emoteSprites.Length)
            {
                chatEmoteImage.sprite = emoteSprites[emoteIndex];
            }
            
            // Sonra görünürlüğü ayarla
            chatEmote.SetActive(isVisible);
        }
    }
    
    // Şu anki emoji'nin index'ini bulma fonksiyonu
    private int GetCurrentEmoteIndex()
    {
        // Şu an görünen emoji sprite'ının indexini bul
        if (emoteImage != null && emoteImage.sprite != null)
        {
            for (int i = 0; i < emoteSprites.Length; i++)
            {
                if (emoteSprites[i] == emoteImage.sprite)
                {
                    return i;
                }
            }
        }
        
        // Varsayılan olarak ilk emojiyi kullan
        return 0;
    }
    
    // Şu anki emote sprite'ını dönüp gerektiğinde kullanmak için
    public Sprite GetCurrentEmoteSprite()
    {
        // Şu anki emoji sprite'ını kontrol et
        if (emoteImage != null && emoteImage.sprite != null)
        {
            return emoteImage.sprite;
        }
        
        // Varsayılan olarak ilk emote sprite'ını dön (eğer varsa)
        if (emoteSprites != null && emoteSprites.Length > 0)
        {
            return emoteSprites[0];
        }
        
        return null; // Hiçbir sprite yok
    }
    
    // Belirli bir emote sprite'ını döndüren fonksiyon
    public Sprite GetEmoteSprite(int index)
    {
        // Index kontrolü yap
        if (emoteSprites != null && index >= 0 && index < emoteSprites.Length)
        {
            return emoteSprites[index];
        }
        
        return null; // Geçersiz index
    }
} 