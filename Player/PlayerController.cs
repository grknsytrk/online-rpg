using UnityEngine;
using Photon.Realtime;
using Photon.Pun;
using TMPro;
using System.Collections;
using UnityEngine.UI;

// Karakter türleri enum'u
public enum CharacterType { Knight, Mage, Archer }

// Ana oyuncu kontrol sınıfı - hareket, animasyon, chat ve emoji sistemlerini yönetir
public class PlayerController : MonoBehaviourPunCallbacks
{
    [Header("Movement Speed")]
    public float movementSpeed; // Hareket hızı

    [Header("Chat Reference")]
    [SerializeField] private TMP_InputField chatInput; // Chat giriş alanı

    [Header("Character Sprites")]
    [SerializeField] private RuntimeAnimatorController[] characterAnimators; // Karakter animatörleri
    [SerializeField] private Sprite[] idleSprites; // Durma durumu sprite'ları
    [SerializeField] private CharacterType currentCharacterType; // Şu anki karakter türü

    [Header("Emote System")]
    [SerializeField] private KeyCode[] emoteKeys = new KeyCode[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 }; // Emote tuşları

    // Özel değişkenler
    private PlayerControls playerControls; // Oyuncu kontrolleri
    private Vector2 movement; // Hareket vektörü
    private Rigidbody2D rb; // Fizik bileşeni
    private Animator animator; // Animasyon bileşeni
    private SpriteRenderer spriteRenderer; // Sprite renderer bileşeni
    private PhotonView pv; // Photon view bileşeni
    public PhotonView PV { get { return pv; } } // Photon view property'si
    private bool facingLeft = false; // Sola bakıyor mu?
    public bool FacingLeft { get { return facingLeft; } set { facingLeft = value; } } // FacingLeft property'si

    // Durum değişkenleri
    private bool isChatting = false; // Chat yazıyor mu?
    private bool isQuitting = false; // Çıkış yapıyor mu?
    private bool isSettingsPanelOpen = false; // Ayarlar paneli açık mı?
    private bool isInventoryOpen = false; // Envanter açık mı?
    private bool isLocked = false; // Kilitli mi?
    private Sword swordComponent; // Kılıç bileşeni
    private PlayerEmoteSystem emoteSystem; // Emote sistemi referansı
    private Knockback knockbackComponent; // Knockback bileşeni referansı

    // --- İKSİR COOLDOWN İÇİN EKLENDİ ---
    private float potionCooldown = 1f; // İksir kullanım bekleme süresi
    private float lastPotionUseTime = -10f; // Son iksir kullanım zamanı
    // ----------------------------------

    // Obje oluşturulduğunda çalışır
    private void Awake()
    {
        // Bileşenleri al
        pv = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        swordComponent = GetComponentInChildren<Sword>();
        emoteSystem = GetComponentInChildren<PlayerEmoteSystem>(); // Emote sistemini referans al
        
        // Knockback component'ini ekle
        knockbackComponent = GetComponent<Knockback>();
        if (knockbackComponent == null)
        {
            knockbackComponent = gameObject.AddComponent<Knockback>();
            Debug.Log("Knockback component added to player");
        }
        
        // Bu bizim karakterimiz mi kontrol et
        if (pv.IsMine)
        {
            playerControls = new PlayerControls(); // Kontrolleri başlat
            Debug.Log($"This is my character! ViewID: {pv.ViewID}");
            SetRandomCharacter(); // Rastgele karakter seç

            // Ekipman sistemini başlat - prefabda varsa ekleme
            var equipmentHandler = GetComponent<CharacterEquipmentHandler>();
            if (equipmentHandler == null)
            {
                Debug.Log("CharacterEquipmentHandler bulunamadı, ekleniyor...");
                equipmentHandler = gameObject.AddComponent<CharacterEquipmentHandler>();
            }
            else
            {
                Debug.Log("CharacterEquipmentHandler zaten mevcut, tekrar eklenmiyor.");
            }
            
            // Equipment Manager'ı dinle
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnItemEquipped += OnEquipmentChanged;
                EquipmentManager.Instance.OnItemUnequipped += OnEquipmentChanged;
            }
        }
        else
        {
            Debug.Log($"This is another player's character! ViewID: {pv.ViewID}");
        }
    }

    // Oyuncuyu kilitle/kilit aç
    public void SetLocked(bool locked)
    {
        if (!pv.IsMine) return; // Sadece kendi karakterimiz için
        isLocked = locked;
        
        // Kilit kaldırıldığında hareketi sıfırla
        if (!locked)
        {
            movement = Vector2.zero;
        }
    }

    // Rastgele karakter seçme fonksiyonu
    private void SetRandomCharacter()
    {
        if (!pv.IsMine) return; // Sadece kendi karakterimiz için
        
        // Rastgele karakter türü seç
        int randomType = Random.Range(0, System.Enum.GetValues(typeof(CharacterType)).Length);
        
        // Photon üzerinden karakteri senkronize et
        pv.RPC("InitializeCharacter", RpcTarget.AllBuffered, randomType);
    }

    // Karakter başlatma RPC fonksiyonu
    [PunRPC]
    private void InitializeCharacter(int characterType)
    {
        currentCharacterType = (CharacterType)characterType; // Karakter türünü ayarla
        UpdateCharacterAppearance(); // Görünümü güncelle
    }

    // Karakter görünümünü güncelleme fonksiyonu
    private void UpdateCharacterAppearance()
    {
        // Animatör güncelle
        if (characterAnimators != null && characterAnimators.Length > (int)currentCharacterType)
        {
            animator.runtimeAnimatorController = characterAnimators[(int)currentCharacterType];
        }
        
        // Sprite güncelle
        if (idleSprites != null && idleSprites.Length > (int)currentCharacterType)
        {
            spriteRenderer.sprite = idleSprites[(int)currentCharacterType];
        }
    }

    // Manuel karakter değiştirme için
    public void ChangeCharacterType(int newType)
    {
        if (!pv.IsMine) return; // Sadece kendi karakterimiz için
        
        // Geçerli tür kontrolü
        if (newType >= 0 && newType < System.Enum.GetValues(typeof(CharacterType)).Length)
        {
            pv.RPC("InitializeCharacter", RpcTarget.AllBuffered, newType);
        }
    }

    // Uygulama kapanırken çalışır
    void OnApplicationQuit()
    {
        // isQuitting bayrağı hala birden fazla disconnect çağrısını önlemek için yararlı olabilir
        if (pv != null && pv.IsMine && PhotonNetwork.IsConnected && !isQuitting)
        {
            isQuitting = true;
            Debug.Log("OnApplicationQuit: Photon bağlantısı kesiliyor.");
            PhotonNetwork.Disconnect(); // Sadece bağlantıyı kes, temizliği Photon'a bırak
        }
    }

    // Obje aktif olduğunda çalışır
    public override void OnEnable()
    {
        base.OnEnable();
        if (pv.IsMine)
        {
            playerControls?.Enable(); // Kontrolleri aktif et
        }
    }

    // Obje pasif olduğunda çalışır
    public override void OnDisable()
    {
        base.OnDisable();
        if (pv.IsMine && playerControls != null)
        {
            playerControls.Disable(); // Kontrolleri pasif et
        }
    }

    // Oyuncu odadan çıktığında çalışır
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Sadece master client için

        StartCoroutine(CleanupPlayerObjects(otherPlayer)); // Temizlik coroutine'i başlat
    }

    private IEnumerator CleanupPlayerObjects(Player player)
    {
        // Wait a bit for objects to update
        yield return new WaitForSeconds(0.2f);

        // Clean up the disconnected player's objects
        if (pv.Owner == player || (pv.Owner == null && player.ActorNumber == pv.ViewID / PhotonNetwork.MAX_VIEW_IDS))
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
        {
            if (pv.IsMine)
            {
                // If our character is being destroyed, notify other players
                photonView.RPC("PlayerDisconnected", RpcTarget.All);
            }
        }

        if (pv.IsMine && EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnItemEquipped -= OnEquipmentChanged;
            EquipmentManager.Instance.OnItemUnequipped -= OnEquipmentChanged;
        }
    }

    [PunRPC]
    private void PlayerDisconnected()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        if (gameObject != null)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!pv.IsMine) return;

        PlayerInput();
        UpdateAnimator();
        CheckEmoteInput(); // Emote girdisini kontrol et
        CheckPotionInput(); // İksir kullanım girdisini kontrol et (Eklendi)
    }

    private void FixedUpdate()
    {
        if (!pv.IsMine) return;

        // Eğer knockback etkisi altındaysa Move fonksiyonunu çalıştırma
        if (knockbackComponent != null && knockbackComponent.gettingKnockbacked)
        {
            return; // Knockback sırasında hareketi engelle
        }

        Move();
        AdjustPlayerFacingDirection();
    }

    private void PlayerInput()
    {
        // Eğer hareket kısıtlanmışsa veya knockback alıyorsa input'u engelle
        if (isChatting || isSettingsPanelOpen || isInventoryOpen || isLocked || (knockbackComponent != null && knockbackComponent.gettingKnockbacked))
        {
            movement = Vector2.zero;
            return;
        }

        // Hareket kısıtlanmamışsa input'u al
        if (playerControls != null)
        {
            movement = playerControls.Movement.Move.ReadValue<Vector2>();
        }
    }

    private void Move()
    {
        if (rb != null)
        {
            rb.MovePosition(rb.position + movement * (movementSpeed * Time.fixedDeltaTime));
        }
    }

    private void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("Horizontal", movement.x);
            animator.SetFloat("Vertical", movement.y);
            animator.SetFloat("Speed", movement.sqrMagnitude);

            if (PhotonNetwork.InRoom)
            {
                pv.RPC("SyncAnimations", RpcTarget.Others, movement.x, movement.y, movement.sqrMagnitude);
            }
        }
    }

    [PunRPC]
    private void SyncAnimations(float horizontal, float vertical, float speed)
    {
        if (animator != null)
        {
            animator.SetFloat("Horizontal", horizontal);
            animator.SetFloat("Vertical", vertical);
            animator.SetFloat("Speed", speed);
        }
    }

    private void AdjustPlayerFacingDirection()
    {
        Vector3 mousePos = Input.mousePosition;
        Vector3 playerScreenPoint = Camera.main.WorldToScreenPoint(transform.position);

        if (mousePos.x < playerScreenPoint.x)
        { 
            FacingLeft = true;
        }
        else
        {
            FacingLeft = false;
        }
    }

    // Remove or update these static methods
    public static bool IsLocalPlayerFacingLeft()
    {
        PlayerController localPlayer = FindLocalPlayer();
        return localPlayer != null ? localPlayer.facingLeft : false;
    }

    public static Vector2 GetLocalPlayerPosition()
    {
        PlayerController localPlayer = FindLocalPlayer();
        return localPlayer != null && localPlayer.rb != null ? 
               localPlayer.rb.position : Vector2.zero;
    }

    // Helper method
    private static PlayerController FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            if (player.pv.IsMine)
                return player;
        }
        return null;
    }

    public void SetChatting(bool chatting)
    {
        isChatting = chatting;
    }

    public void SetInventoryPanelState(bool isOpen)
    {
        if (!pv.IsMine) return;
        isInventoryOpen = isOpen;
        
        // Envanter durumu değiştiğinde hareketi sıfırla
        movement = Vector2.zero;
    }

    private void OnEquipmentChanged(SlotType slotType, InventoryItem item)
    {
        // Ekipman değişikliklerine göre karakter görünümünü güncelle
        switch (slotType)
        {
            case SlotType.Sword:
                bool hasSword = item != null;
                UpdateSwordVisibility(hasSword);
                
                // Diğer oyunculara kılıç durumunu bildir
                if (pv.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                {
                    pv.RPC("SyncSwordVisibility", RpcTarget.Others, hasSword);
                }
                break;
            // Diğer ekipman tipleri için gerekli güncellemeleri ekle
        }
    }

    private void UpdateSwordVisibility(bool hasSword)
    {
        if (swordComponent != null)
        {
            // Kılıç görünürlüğünü güncelle
            swordComponent.gameObject.SetActive(hasSword);
            
            // Debug mesajı ekleyelim
            Debug.Log($"Kılıç görünürlüğü güncellendi (PlayerController): {hasSword}, ViewID: {pv.ViewID}");
        }
    }
    
    [PunRPC]
    private void SyncSwordVisibility(bool isVisible)
    {
        if (swordComponent != null)
        {
            // Eğer bu kendi karakterimiz değilse, kılıç görünürlüğünü güncelle
            if (!pv.IsMine)
            {
                swordComponent.gameObject.SetActive(isVisible);
                Debug.Log($"Diğer oyuncunun kılıç görünürlüğü senkronize edildi: {isVisible}, ViewID: {pv.ViewID}");
            }
        }
    }

    // Emote tuş girişlerini kontrol et
    private void CheckEmoteInput()
    {
        // Eğer oyuncu chat yapıyorsa veya inventoryde ise emote'ları engelle
        if (isChatting || isSettingsPanelOpen || isInventoryOpen || isLocked)
            return;
            
        // Emote tuşlarını kontrol et
        for (int i = 0; i < emoteKeys.Length; i++)
        {
            //Debug.Log("Emote tuşları: " + emoteKeys.Length);
            if (Input.GetKeyDown(emoteKeys[i]))
            {
                ShowPlayerEmote(i);
                break;
            }
        }
    }
    
    // Belirtilen emote'u göster
    public void ShowPlayerEmote(int emoteIndex)
    {
        if (emoteSystem != null)
        {
            emoteSystem.ShowEmote(emoteIndex);
        }
        else
        {
            Debug.LogWarning("Emote sistemi bulunamadı!");
        }
    }

    // İksir kullanım girdisini kontrol et (Eklendi)
    private void CheckPotionInput()
    {
        // Eğer hareket kısıtlıysa veya başka bir UI açıksa kullanma
        if (isChatting || isSettingsPanelOpen || isInventoryOpen || isLocked || (knockbackComponent != null && knockbackComponent.gettingKnockbacked))
            return;

        // 'F' tuşuna basıldıysa
        if (Input.GetKeyDown(KeyCode.F))
        {
            UseEquippedPotion();
        }
    }

    // Kuşanılmış iksiri kullan (Eklendi)
    private void UseEquippedPotion()
    {
        // --- COOLDOWN KONTROLÜ EKLENDİ ---
        if (Time.time < lastPotionUseTime + potionCooldown)
        {
            Debug.Log($"İksir bekleme süresinde. Kalan Süre: {((lastPotionUseTime + potionCooldown) - Time.time):F1}sn");
            // İsteğe bağlı: Kullanıcıya UI üzerinden geri bildirim verilebilir.
            // UIFeedbackManager.Instance?.ShowGeneralMessage("İksir bekleme süresinde!", 1f);
            return;
        }
        // --------------------------------

        if (EquipmentManager.Instance == null)
        {
            Debug.LogWarning("EquipmentManager bulunamadı!");
            return;
        }

        // İksir slotundaki itemi al
        InventoryItem potionItem = EquipmentManager.Instance.GetEquippedItem(SlotType.Potion);

        // Item var mı ve Potion tipinde mi kontrol et
        if (potionItem == null || potionItem.ItemType != SlotType.Potion)
        {
            Debug.Log("Kullanılacak iksir kuşanılmamış.");
            return;
        }

        // ItemData'dan iyileştirme miktarını al
        ItemData itemData = ItemDatabase.Instance?.GetItemById(potionItem.ItemId);
        if (itemData == null || itemData.HealAmount <= 0)
        {
            Debug.LogWarning($"İksir ({potionItem.ItemName}) için geçerli HealAmount bulunamadı veya sıfır.");
            return;
        }

        // Oyuncunun can bileşenini al
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealth bileşeni bulunamadı!");
            return;
        }

        // --- CAN KONTROLÜ EKLENDİ ---
        if (playerHealth.GetCurrentHealth() >= playerHealth.GetMaxHealth())
        {
            Debug.Log("Can zaten dolu, iksir kullanılmadı.");
            // İsteğe bağlı: Kullanıcıya mesaj gösterilebilir.
            // UIFeedbackManager.Instance?.ShowGeneralMessage("Canın zaten dolu!", 2f);
            return; // Can doluysa işlemi durdur
        }
        // --------------------------

        // Can basma işlemini yap (Heal metodu RPC olduğu için network'e yayılacak)
        playerHealth.Heal(itemData.HealAmount);
        Debug.Log($"{potionItem.ItemName} kullanıldı, +{itemData.HealAmount} can.");

        // --- SES EFEKTİ EKLENDİ ---
        SFXManager.Instance?.PlaySound(SFXNames.PotionConsume);
        // --------------------------

        // --- COOLDOWN GÜNCELLEME EKLENDİ ---
        lastPotionUseTime = Time.time;
        // --------------------------------

        // İksir miktarını azalt
        potionItem.Amount--;

        // Eğer miktar sıfırsa, ekipmandan kaldır
        if (potionItem.Amount <= 0)
        {
            Debug.Log($"{potionItem.ItemName} bitti, slottan kaldırılıyor.");
            // addToInventory = false olacak şekilde RemoveEquipment çağırmak önemli değil,
            // çünkü iksirler zaten envantere eklenmez.
            // SaveToFirebase parametresi de RemoveEquipment içinde yönetiliyor.
            EquipmentManager.Instance.RemoveEquipment(SlotType.Potion);
        }
        else // Miktar hala varsa, UI'ı güncelle ve kaydet
        {
            // Slot UI'ını güncelle
            InventorySlotUI potionSlotUI = EquipmentManager.Instance.GetEquipmentSlotUI(SlotType.Potion);
            if (potionSlotUI != null)
            {
                potionSlotUI.UpdateUI(potionItem); // Miktar değişikliğini UI'a yansıt
            }
            // Ekipman durumunu kaydet (miktar değiştiği için)
            EquipmentManager.Instance.SaveEquipmentManual();
            Debug.Log($"{potionItem.ItemName} kalan miktar: {potionItem.Amount}");
        }
    }
}