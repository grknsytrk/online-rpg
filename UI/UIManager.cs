using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using System.Collections.Generic; // Dictionary için eklendi
using UnityEngine.EventSystems;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Settings UI")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;

    [Header("Horizontal Layout")]
    [SerializeField] private Transform horizontalLayout; // Reference to the horizontal layout
    
    [Header("Chat UI")]
    [SerializeField] private Button chatButton;
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private TMP_InputField chatInputField; // Chat giriş alanı
    private bool isChatOpen = false;

    [Header("Inventory UI")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Animator playerAnimator; // Image yerine Animator kullanacağız
    [Header("Inventory Bonus Stats UI")] // Yeni Başlık
    [SerializeField] private TextMeshProUGUI totalAttackBonusText; // Yeni Eklendi
    [SerializeField] private TextMeshProUGUI totalDefenseBonusText; // Yeni Eklendi
    private bool isInventoryOpen = false;
    private Image characterImage; // Image referansını saklayalım
    private PlayerController localPlayerRef; // Local player referansını saklayalım

    [Header("Button Images")]
    [SerializeField] private Sprite settingsOpenSprite;  // Panel açıkken gösterilecek sprite
    [SerializeField] private Sprite settingsClosedSprite; // Panel kapalıyken gösterilecek sprite
    
    [Header("Settings Panels")]
    [SerializeField] private Button configButton;
    [SerializeField] private Button statsButton;
    [SerializeField] private GameObject configPanel;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private GameObject defaultPanel; // Inspector'dan config paneli buraya atanacak
    
    [Header("Stats UI")]
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private TextMeshProUGUI levelValueText;
    [SerializeField] private TextMeshProUGUI expValueText;
    [SerializeField] private TextMeshProUGUI attackValueText;
    [SerializeField] private TextMeshProUGUI defenseValueText;
    
    [Header("Audio Settings")]
    [SerializeField] private Slider musicVolumeSlider;
    
    [Header("Level Up UI")] // Yeni Başlık
    [SerializeField] private TextMeshProUGUI levelUpText; // Level atlama mesajı için
    [SerializeField] private float levelUpMessageDuration = 3f; // Mesajın ekranda kalma süresi
    
    private Image buttonImage;
    private bool isSettingsOpen = false;
    private GameObject currentSettingsPanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        buttonImage = settingsButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = settingsClosedSprite;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (chatPanel != null)
        {
            chatPanel.SetActive(false);
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        // Tüm panelleri başlangıçta kapat
        if (configPanel != null) configPanel.SetActive(false);
        if (statsPanel != null) statsPanel.SetActive(false);

        // Level up text'i başlangıçta gizle
        if (levelUpText != null)
        {
            levelUpText.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        // Müzik slider'ını başlat
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = 1f;  // Slider'ı full dolu başlat
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
    }

    private PlayerController GetLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in players)
        {
            if (player.PV.IsMine)
            {
                return player;
            }
        }
        return null;
    }

    public void OnSettingsButtonClick()
    {
        Debug.Log("Settings button clicked");
        PlayerController localPlayer = GetLocalPlayer();
        if (localPlayer == null || !localPlayer.PV.IsMine)
        {
            Debug.LogWarning("Cannot open settings - no local player found or not mine");
            return;
        }

        Sword swordComponent = localPlayer.GetComponentInChildren<Sword>();

        // Eğer inventory açıksa, inventory'i kapat ve kontrolü geri ver
        if (isInventoryOpen)
        {
            // Envanteri kapat
            isInventoryOpen = false;
            inventoryPanel.SetActive(false);
            if (playerAnimator != null)
            {
                playerAnimator.gameObject.SetActive(false);
            }

            // Settings butonunu normale döndür
            if (buttonImage != null)
            {
                buttonImage.sprite = settingsClosedSprite;
            }

            // Tüm UI elementlerini tekrar göster
            if (horizontalLayout != null)
            {
                foreach (Transform child in horizontalLayout)
                {
                    child.gameObject.SetActive(true);
                }
            }

            // Önce sword kilidini aç
            if (swordComponent != null)
            {
                swordComponent.SetLocked(false);
            }
            // Hemen ardından player kilidini aç
            localPlayer.SetInventoryPanelState(false);
            localPlayer.SetLocked(false);
            return;
        }

        // Normal settings davranışı
        isSettingsOpen = !isSettingsOpen;
        Debug.Log($"Settings panel will be: {isSettingsOpen}");

        if (buttonImage != null)
        {
            buttonImage.sprite = isSettingsOpen ? settingsOpenSprite : settingsClosedSprite;
        }

        settingsPanel.SetActive(isSettingsOpen);

        // Settings panel açıldığında default paneli göster
        if (isSettingsOpen && defaultPanel != null)
        {
            SwitchSettingsPanel(defaultPanel);
        }
        else
        {
            // Panel kapandığında aktif paneli de kapat
            if (currentSettingsPanel != null)
                currentSettingsPanel.SetActive(false);
        }

        // Hide/Show other buttons in horizontal layout
        if (horizontalLayout != null)
        {
            foreach (Transform child in horizontalLayout)
            {
                if (child.gameObject != settingsButton.gameObject)
                {
                    child.gameObject.SetActive(!isSettingsOpen);
                }
            }
        }

        // Önce sword kilit durumunu güncelle
        if (swordComponent != null)
        {
            swordComponent.SetLocked(isSettingsOpen);
        }
        // Hemen ardından player kilit durumunu güncelle
        localPlayer.SetLocked(isSettingsOpen);
    }

    public void OnInventoryButtonClick()
    {
        localPlayerRef = GetLocalPlayer();
        if (localPlayerRef == null || !localPlayerRef.PV.IsMine) return;

        Sword swordComponent = localPlayerRef.GetComponentInChildren<Sword>();

        // Eğer inventory açıksa, inventory'i kapat ve kontrolü geri ver
        if (isInventoryOpen)
        {
            // Envanteri kapat
            isInventoryOpen = false;
            inventoryPanel.SetActive(false);
            if (playerAnimator != null)
            {
                playerAnimator.gameObject.SetActive(false);
            }

            // Settings butonunu normale döndür
            if (buttonImage != null)
            {
                buttonImage.sprite = settingsClosedSprite;
            }

            // Tüm UI elementlerini tekrar göster
            if (horizontalLayout != null)
            {
                foreach (Transform child in horizontalLayout)
                {
                    child.gameObject.SetActive(true);
                }
            }

            // Önce sword kilidini aç
            if (swordComponent != null)
            {
                swordComponent.SetLocked(false);
            }
            // Hemen ardından player kilidini aç
            localPlayerRef.SetInventoryPanelState(false);
            localPlayerRef.SetLocked(false);
            return;
        }

        // Envanteri aç
        isInventoryOpen = true;
        inventoryPanel.SetActive(true);
        UpdateEquippedItemBonuses(); // Envanter açıldığında bonusları hemen güncelle
        
        // Karakter animasyonunu göster ve oynat
        if (playerAnimator != null && localPlayerRef != null)
        {
            Animator characterAnimator = localPlayerRef.GetComponent<Animator>();
            if (characterAnimator != null)
            {
                Debug.Log("Character animator found, copying controller...");
                // Karakterin animator controller'ını UI animator'a kopyala
                playerAnimator.runtimeAnimatorController = characterAnimator.runtimeAnimatorController;
                
                // UI Image'i al ve referansını sakla
                characterImage = playerAnimator.GetComponent<Image>();
                if (characterImage != null)
                {
                    // İlk sprite'ı ayarla
                    SpriteRenderer playerSprite = localPlayerRef.GetComponent<SpriteRenderer>();
                    if (playerSprite != null)
                    {
                        characterImage.sprite = playerSprite.sprite;
                        characterImage.SetNativeSize();
                    }
                }
                
                playerAnimator.gameObject.SetActive(true);
                
                // Sürekli aşağı yürüme animasyonunu ayarla
                playerAnimator.SetFloat("Speed", 1); // Yürüme animasyonunu aktifleştir
                playerAnimator.SetFloat("Horizontal", 0);
                playerAnimator.SetFloat("Vertical", -1);

                // Animatörü update modunu None yaparak ana karakterden bağımsız hale getir
                //playerAnimator.updateMode = AnimatorUpdateMode.norm;
                
                Debug.Log("Animation parameters set");
            }
        }

        // Settings sprite'ını çıkış butonuna çevir
        if (buttonImage != null)
        {
            buttonImage.sprite = settingsOpenSprite;
        }

        // Diğer panelleri kapat
        if (isSettingsOpen)
        {
            isSettingsOpen = false;
            settingsPanel.SetActive(false);
            if (currentSettingsPanel != null)
                currentSettingsPanel.SetActive(false);
        }
        if (isChatOpen)
        {
            OnChatButtonClick();
        }

        // TÜM butonları gizle (inventory dahil)
        if (horizontalLayout != null)
        {
            foreach (Transform child in horizontalLayout)
            {
                if (child.gameObject != settingsButton.gameObject)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        // Önce sword'u kilitle
        if (swordComponent != null)
        {
            swordComponent.SetLocked(true);
        }
        // Hemen ardından player'ı kilitle
        localPlayerRef.SetInventoryPanelState(true);
        localPlayerRef.SetLocked(true);
    }

    public void OnChatButtonClick()
    {
        isChatOpen = !isChatOpen;
        if (chatPanel != null)
        {
            chatPanel.SetActive(isChatOpen);
        }
        
        // ============= OYUNCU KONTROLÜ BİLDİRİMİ =============
        // Chat durumunu PlayerController'a bildir
        PlayerController localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.SetChatting(isChatOpen);
            Debug.Log($"UIManager: Chat {(isChatOpen ? "açıldı" : "kapandı")}, oyuncu kontrolleri {(isChatOpen ? "kilitlendi" : "açıldı")}");
        }
        
        // Chat açılıyorsa input field'ı focus et
        if (isChatOpen && chatInputField != null)
        {
            // Bir frame bekleyerek input field'ın tam olarak aktif olmasını sağlayalım
            StartCoroutine(ActivateChatInputDelayed());
        }
        // Chat kapanıyorsa input field'dan focus'u kaldır  
        else if (!isChatOpen && chatInputField != null)
        {
            chatInputField.DeactivateInputField();
            // Focus kaldırıldığında emin olmak için bir de blur yapalım
            EventSystem.current.SetSelectedGameObject(null);
            Debug.Log("UIManager: Chat input field defocus edildi ve selection temizlendi");
        }
        // ====================================================
    }

    // ============= YENİ EKLENEN COROUTINE =============
    private IEnumerator ActivateChatInputDelayed()
    {
        yield return null; // Bir frame bekle
        if (chatInputField != null && isChatOpen)
        {
            chatInputField.ActivateInputField();
            chatInputField.Select();
            Debug.Log("UIManager: Chat input field gecikmeli olarak focus edildi");
        }
    }
    // =================================================

    public void OnDisconnectButtonClick()
    {
        // Oyuncu odadan çıkıyor
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        // Uygulamayı kapat
        Application.Quit();
    }

    public void SwitchSettingsPanel(GameObject newPanel)
    {
        // Mevcut paneli kapat
        if (currentSettingsPanel != null)
            currentSettingsPanel.SetActive(false);

        // Yeni paneli aç
        newPanel.SetActive(true);
        currentSettingsPanel = newPanel;

        // Eğer yeni açılan panel stats paneli ise, istatistikleri güncelle
        if (newPanel == statsPanel)
        {
            UpdateStatsPanel();
        }
    }

    private void OnMusicVolumeChanged(float volume)
    {
        if (BiomeMusicManager.Instance != null)
        {
            BiomeMusicManager.Instance.SetVolume(volume);
        }
    }

    private void OnDestroy()
    {
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsButtonClick);
        }
        if (chatButton != null)
        {
            chatButton.onClick.RemoveListener(OnChatButtonClick);
        }
        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveListener(OnInventoryButtonClick);
        }
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }
    }

    public Button GetSettingsButton() => settingsButton;
    public GameObject GetSettingsPanel() => settingsPanel;
    
    // Chat status için public getter
    public bool IsChatOpen() => isChatOpen;

    private void Update()
    {
        // =========== KLAVYE İNPUT KONTROLLERİ ===========
        HandleKeyboardInputs();

        // Eğer inventory açıksa ve gerekli referanslar varsa sprite'ı güncelle
        if (isInventoryOpen && characterImage != null && localPlayerRef != null)
        {
            SpriteRenderer playerSprite = localPlayerRef.GetComponent<SpriteRenderer>();
            if (playerSprite != null)
            {
                characterImage.sprite = playerSprite.sprite;
            }

            // Sürekli aşağı yürüme animasyonunu korumak için parametreleri tekrar ayarla
            if (playerAnimator != null)
            {
                playerAnimator.SetFloat("Speed", 1);
                playerAnimator.SetFloat("Horizontal", 0);
                playerAnimator.SetFloat("Vertical", -1);
            }
        }
        
        // Eğer stats paneli aktifse, bilgileri güncelle
        if (currentSettingsPanel == statsPanel && statsPanel != null && statsPanel.activeSelf)
        {
            UpdateStatsPanel();
        }

        // Envanter açıksa bonusları güncelle (Her frame'de)
        if (isInventoryOpen)
        {
            UpdateEquippedItemBonuses();

            // Envanter açıkken sol tıklama kontrolü
            if (Input.GetMouseButtonDown(0)) // Sol fare tuşuna basıldıysa
            {
                // Fare pozisyonundaki UI elemanını al
                PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
                eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

                bool clickedOnSlot = false;
                foreach (RaycastResult result in results)
                {
                    if (result.gameObject.GetComponentInParent<InventorySlotUI>() != null)
                    {
                        clickedOnSlot = true;
                        break;
                    }
                }

                if (!clickedOnSlot)
                {
                    bool clickedOnAnyButton = false;
                    foreach (RaycastResult buttonResult in results)
                    {
                        // Sadece UnityEngine.UI.Button kontrolü yeterli olmayabilir, 
                        // TMP_Button gibi başka buton türleri de olabilir.
                        // Şimdilik genel bir Button component'i arayalım.
                        if (buttonResult.gameObject.GetComponent<Button>() != null || buttonResult.gameObject.GetComponent<Selectable>() != null) // Selectable daha genel bir kontrol olabilir
                        {
                            clickedOnAnyButton = true;
                            Debug.Log("Clicked on a UI Selectable (e.g., Button), not deselecting slot based on outside click.");
                            break;
                        }
                    }

                    if (!clickedOnAnyButton) // Sadece slota VE butona/seçilebilir UI'a tıklanmadıysa seçimi kaldır
                    {
                        InventorySlotUI.DeselectCurrentSlot();
                        Debug.Log("Clicked outside inventory slots (and not on a UI Selectable), deselected current slot.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Klavye input'larını kontrol eder (Tab, ESC vb.)
    /// </summary>
    private void HandleKeyboardInputs()
    {
        // TAB tuşu ile envanter açma/kapatma
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // Chat yazma modundayken Tab'ı devre dışı bırak
            if (IsChatInputActive()) return;
            // Settings açıkken Tab'ı devre dışı bırak
            if (isSettingsOpen) return;

            Debug.Log("Tab tuşuna basıldı - Envanter toggle");
            OnInventoryButtonClick();
        }

        // T tuşu ile chat açma/kapatma
        if (Input.GetKeyDown(KeyCode.T))
        {
            // Envanter açıkken T'yi devre dışı bırak
            if (isInventoryOpen) return;
            // Settings açıkken T'yi devre dışı bırak  
            if (isSettingsOpen) return;

            Debug.Log("T tuşuna basıldı - Chat toggle");
            OnChatButtonClick();
        }

        // ESC tuşu ile panel/menü kapatma
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("ESC tuşuna basıldı - Panel kapatma kontrolü");
            HandleEscapeKey();
        }
    }

    /// <summary>
    /// Chat input alanının aktif olup olmadığını kontrol eder
    /// </summary>
    private bool IsChatInputActive()
    {
        // Chat paneli açık mı ve chat input field'ı seçili mi kontrol et
        if (isChatOpen && chatInputField != null)
        {
            return chatInputField.isFocused;
        }
        return false;
    }

    /// <summary>
    /// ESC tuşuna basıldığında öncelik sırasına göre panelleri kapatır
    /// Chat artık ESC ile kapanmıyor, sadece T tuşu ile kontrol ediliyor
    /// </summary>
    private void HandleEscapeKey()
    {
        // Öncelik sırası:
        // 1. Chat yazma modundaysa -> sadece input field'dan çık (chat'i kapatma)
        // 2. Chat açıksa -> Chat'i kapat
        // 3. Envanter açıksa -> Envanteri kapat
        // 4. Settings açıksa -> Settings'i kapat

        // Eğer chat input field aktifse, sadece focus'u kaldır (chat'i kapatma)
        if (IsChatInputActive())
        {
            chatInputField.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
            Debug.Log("ESC: Chat input field defocus edildi (chat paneli açık kalıyor)");
            return;
        }
        
        // Chat açıksa chat'i kapat
        if (isChatOpen)
        {
            Debug.Log("ESC: Chat kapatılıyor");
            OnChatButtonClick(); // Chat toggle
        }
        else if (isInventoryOpen)
        {
            Debug.Log("ESC: Envanter kapatılıyor");
            OnInventoryButtonClick(); // Envanter toggle
        }
        else if (isSettingsOpen)
        {
            Debug.Log("ESC: Settings kapatılıyor");
            OnSettingsButtonClick(); // Settings toggle
        }
        else
        {
            Debug.Log("ESC: Kapatılacak panel yok");
        }
    }

    // Chat input alanındaki metni döndürür
    public string GetChatInputText()
    {
        // Eğer chat input alanı varsa içindeki metni döndür
        if (chatInputField != null)
        {
            return chatInputField.text;
        }
        
        // Chat input alanı yoksa boş string döndür
        return string.Empty;
    }
    
    // Stats Panelini güncelleyen method
    private void UpdateStatsPanel()
    {
        if (hpValueText == null) return; // Text referansı yoksa çık (Bu kontrol genişletilebilir)

        PlayerController localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            // Can Bilgisi
            PlayerHealth playerHealth = localPlayer.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                hpValueText.text = $"{playerHealth.GetCurrentHealth()}/{playerHealth.GetMaxHealth()}";
            }
            else
            {
                hpValueText.text = "N/A"; // PlayerHealth bulunamazsa
            }

            // Seviye, XP, Saldırı ve Savunma Bilgisi
            PlayerStats playerStats = localPlayer.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                if (levelValueText != null) // Seviye text referansı var mı?
                {
                     levelValueText.text = $"{playerStats.CurrentLevel}";
                }
                if (expValueText != null) // XP text referansı var mı?
                {
                     expValueText.text = $"{playerStats.CurrentXP} / {playerStats.XPToNextLevel}";
                }
                if (attackValueText != null) // Saldırı text referansı var mı?
                {
                    attackValueText.text = $"{playerStats.BaseAttackDynamic}";
                }
                if (defenseValueText != null) // Savunma text referansı var mı?
                {
                    defenseValueText.text = $"{playerStats.BaseDefenseDynamic}";
                }
            }
            else
            {
                 // PlayerStats bulunamazsa seviye, xp, saldırı ve savunma için de N/A yaz
                 if (levelValueText != null) levelValueText.text = "N/A";
                 if (expValueText != null) expValueText.text = "N/A";
                 if (attackValueText != null) attackValueText.text = "N/A";
                 if (defenseValueText != null) defenseValueText.text = "N/A";
            }
        }
        else
        {
            hpValueText.text = "N/A"; // Oyuncu bulunamazsa
            // Oyuncu bulunamazsa diğer statlar için de N/A yaz
            if (levelValueText != null) levelValueText.text = "N/A";
            if (expValueText != null) expValueText.text = "N/A";
            if (attackValueText != null) attackValueText.text = "N/A";
            if (defenseValueText != null) defenseValueText.text = "N/A";
        }
    }

    // Yeni eklenen metot
    private void UpdateEquippedItemBonuses()
    {
        if (!isInventoryOpen) // Envanter kapalıysa bir şey yapma
        {
            if (totalAttackBonusText != null) totalAttackBonusText.text = ""; // Veya gizle
            if (totalDefenseBonusText != null) totalDefenseBonusText.text = ""; // Veya gizle
            return;
        }

        if (EquipmentManager.Instance == null)
        {
            if (totalAttackBonusText != null) totalAttackBonusText.text = "N/A";
            if (totalDefenseBonusText != null) totalDefenseBonusText.text = "N/A";
            return;
        }

        Dictionary<SlotType, InventoryItem> equippedItems = EquipmentManager.Instance.GetEquippedItems();
        int currentAttackBonus = 0;
        int currentDefenseBonus = 0;

        if (equippedItems != null)
        {
            foreach (InventoryItem item in equippedItems.Values)
            {
                if (item != null) // Item null değilse kontrol et
                {
                    currentAttackBonus += item.Damage; 
                    currentDefenseBonus += item.Defense;
                }
            }
        }

        if (totalAttackBonusText != null)
        {
            totalAttackBonusText.text = $"{currentAttackBonus}";
        }

        if (totalDefenseBonusText != null)
        {
            totalDefenseBonusText.text = $"{currentDefenseBonus}";
        }
    }

    // Yeni: Level atlama mesajını göstermek için public metot
    public void ShowLevelUpMessage(int newLevel)
    {
        if (levelUpText == null)
        {
            Debug.LogWarning("LevelUpText referansı UIManager'da ayarlanmamış!");
            return;
        }

        levelUpText.text = $"Tebrikler! {newLevel}. seviyeye ulaştınız!";
        levelUpText.gameObject.SetActive(true);
        StartCoroutine(HideLevelUpMessageAfterDelay());
    }

    // Yeni: Level atlama mesajını belirli bir süre sonra gizleyen Coroutine
    private IEnumerator HideLevelUpMessageAfterDelay()
    {
        yield return new WaitForSeconds(levelUpMessageDuration);
        if (levelUpText != null)
        {
            levelUpText.gameObject.SetActive(false);
        }
    }

    // Yeni: Yere Bırak Butonu için metot
    public void OnDropItemButtonClicked()
    {
        if (InventorySlotUI.currentlySelectedSlot != null && InventorySlotUI.currentlySelectedSlot.CurrentItem != null)
        {
            Debug.Log($"Drop Item button clicked for item: {InventorySlotUI.currentlySelectedSlot.CurrentItem.ItemName}");
            DropManager.Instance.ShowDropConfirmationPanel(InventorySlotUI.currentlySelectedSlot, InventorySlotUI.currentlySelectedSlot.CurrentItem);
        }
        else
        {
            UIFeedbackManager.Instance?.ShowTooltip("Yere bırakmak için bir eşya seçin.");
            Debug.Log("Drop Item button clicked, but no item is selected or slot is invalid.");
        }
    }
}
