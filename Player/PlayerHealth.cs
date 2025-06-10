using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(PlayerStats))] // PlayerStats referansı için
[RequireComponent(typeof(Animator))] // Animator referansı için
[RequireComponent(typeof(Collider2D))] // Collider2D referansı için
public class PlayerHealth : MonoBehaviourPunCallbacks
{
    [Header("Health Settings")]
    // [SerializeField] private int maxHealth = 100; // KALDIRILDI - PlayerStats'tan alınacak
    [SerializeField] private int currentHealth;
    [SerializeField] private float invincibilityDuration = 0.5f;
    [SerializeField] private float damageInvincibilityDuration = 0.2f;
    [SerializeField] private float knockbackTakenPower = 15f; // Oyuncudan alınan knockback gücü

    [Header("Regeneration Settings")]
    [SerializeField] private float regenerationDelay = 10f; // Yenilenme için hasarsız bekleme süresi
    [SerializeField] private float regenerationInterval = 3f;  // Yenilenme tikleri arası süre
    [SerializeField] private float regenerationPercent = 5f;   // Her tikte max HP yüzdesi

    [Header("Defense Settings")] // Yeni başlık
    [SerializeField] private float defenseConstantK = 150f; // Azalan verim formülü için K sabiti

    [Header("UI Elements")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI healthValueText;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject damageEffect;
    [SerializeField] private GameObject playerDeathVFXPrefab; // Oyuncu ölüm efekti prefabı
    [SerializeField] private Color invincibilityTintColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Dokunulmazlık renk tonu

    private PhotonView pv;
    private SpriteRenderer spriteRenderer;
    private bool isInvincible = false;
    private PlayerController playerController;
    private bool uiInitialized = false;
    private Knockback knockbackComponent;
    private Flash flash;
    private float lastDamageTime = -1f; // Son hasar zamanı (-1 başlangıçta kontrolü kolaylaştırır)
    private Coroutine manageRegenCoroutine = null; // Yenilenme korutini referansı
    private PlayerStats playerStats; // PlayerStats referansı
    private Animator animator; // Animator referansı
    private Collider2D mainCollider; // Ana collider referansı
    private GameObject swordObject; // Kılıç GameObject referansı
    private Sword swordComponent; // Kılıç Component referansı
    private PlayerNameTag playerNameTagComponent; // İsim etiketi component referansı
    private bool wasSwordActiveBeforeDeath = false;

    private void Awake()
    {
        pv = GetComponent<PhotonView>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerController = GetComponent<PlayerController>();
        knockbackComponent = GetComponent<Knockback>();
        flash = GetComponent<Flash>();
        playerStats = GetComponent<PlayerStats>(); // PlayerStats bileşenini al
        animator = GetComponent<Animator>(); // Animator bileşenini al
        mainCollider = GetComponent<Collider2D>(); // Collider'ı al
        
        // Kılıcı ve component'ini bul
        swordComponent = GetComponentInChildren<Sword>(true); // true: Deaktif olanları da bul
        if (swordComponent != null)
        {
            swordObject = swordComponent.gameObject;
        }
        else
        {
            Debug.LogWarning("PlayerHealth: Sword component/object could not be found in children!", this);
        }

        // İsim etiketi component'ini bul
        playerNameTagComponent = GetComponentInChildren<PlayerNameTag>(true); // true: Deaktif olanları da bul
        if (playerNameTagComponent == null)
        {
            Debug.LogWarning("PlayerHealth: PlayerNameTag component could not be found in children!", this);
        }

        if (flash == null)
        {
            Debug.LogWarning("PlayerHealth: Flash bileşeni bulunamadı! Hasar efekti çalışmayacak.", this);
        }

        if (mainCollider == null)
        {
            Debug.LogError("PlayerHealth: Collider2D bileşeni bulunamadı!", this);
        }

        if (playerStats == null)
        {
            Debug.LogError("PlayerHealth: PlayerStats bileşeni bulunamadı! Can sistemi düzgün çalışmayacak.", this);
            enabled = false;
            return;
        }

        // Tag'i baştan ekle (eğer mevcut değilse)
        EnsureTagExists("HealthCanvas");
        EnsureTagExists("SpawnPoint");
    }

    private void Start()
    {
        // Can ayarlama işlemi buradan kaldırıldı.
        lastDamageTime = Time.time; 

        if (pv.IsMine)
        {
            SetupHealthUI();
            if (playerStats != null)
            { 
                playerStats.OnStatsCalculated += HandleStatsCalculated;
                playerStats.OnLevelUp += HandleLevelUp;
            }
            else
            {
                 Debug.LogError("PlayerHealth Start: PlayerStats null! Olaylara abone olunamadı.");
            }

            manageRegenCoroutine = StartCoroutine(ManageRegenerationRoutine());
            
            // Oyun başladığında ilk canı senkronize etmek için bir gecikme ekleyelim
            // Böylece PlayerStats ve diğer başlangıç işlemleri tamamlandıktan sonra çalışacak
            StartCoroutine(DelayedHealthSync());
        }
        else
        {
            healthSlider = null;
            fillImage = null;
            frameImage = null;
            healthValueText = null;
        }
    }

    public override void OnDisable() // OnDestroy yerine OnDisable daha güvenli olabilir
    {
        base.OnDisable();
        // Korutini durdur
        if (pv.IsMine && manageRegenCoroutine != null)
        {
            StopCoroutine(manageRegenCoroutine);
            manageRegenCoroutine = null;
        }
        // Olay aboneliklerini kaldır
        if (pv.IsMine && playerStats != null)
        {
            playerStats.OnStatsCalculated -= HandleStatsCalculated;
            playerStats.OnLevelUp -= HandleLevelUp;
        }
    }

    // Tag'in varlığını kontrol et, yoksa uyarı ver
    private void EnsureTagExists(string tagName)
    {
        try
        {
            // Tag'in varlığını kontrol et
            GameObject tempObject = new GameObject("TagChecker");
            try
            {
                tempObject.tag = tagName;
                // Tag mevcut, sorun yok
            }
            catch (UnityException)
            {
                // Tag bulunamadı - sadece uyarı ver, crash etme
                Debug.LogWarning($"UYARI: \"{tagName}\" tag'i tanımlı değil! Lütfen Edit > Project Settings > Tags and Layers menüsünden bu tag'i ekleyin.");
                
                // Yeniden doğma için özel kontrol
                if (tagName == "SpawnPoint")
                {
                    Debug.LogWarning("SpawnPoint tag'i bulunamadığından, oyuncu öldüğünde rastgele bir konumda yeniden doğacak.");
                }
            }
            finally
            {
                // Geçici objeyi her durumda temizle
                Destroy(tempObject);
            }
        }
        catch (System.Exception e)
        {
            // Herhangi bir hata durumunda sadece log al, oyunu crash ettirme
            Debug.LogError($"Tag kontrolü sırasında hata: {e.Message}");
        }
    }

    private void SetupHealthUI()
    {
        // Birkaç kez deneme yapalım
        StartCoroutine(FindUIElementsRoutine());
    }
    
    private IEnumerator FindUIElementsRoutine()
    {
        int attempts = 0;
        int maxAttempts = 10;
        
        while (!uiInitialized && attempts < maxAttempts)
        {
            attempts++;
            
            // If UI elements are not assigned via inspector, try to find them
            if (healthSlider == null)
            {
                try
                {
                    Canvas healthCanvas = GameObject.FindGameObjectWithTag("HealthCanvas")?.GetComponent<Canvas>();
                    if (healthCanvas != null)
                    {
                        healthSlider = healthCanvas.GetComponentInChildren<Slider>();
                        fillImage = healthSlider?.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
                        frameImage = healthSlider?.transform.Find("Frame")?.GetComponent<Image>();
                        healthValueText = healthCanvas.GetComponentInChildren<TextMeshProUGUI>();
                        
                        if (healthSlider != null)
                        {
                            uiInitialized = true;
                            // Set initial UI values
                            healthSlider.maxValue = playerStats.TotalMaxHealth;
                            healthSlider.value = currentHealth;
                            
                            if (healthValueText != null)
                            {
                                healthValueText.text = $"{currentHealth}/{playerStats.TotalMaxHealth}";
                            }
                            
                            Debug.Log("PlayerHealth: UI başarıyla bulundu ve PlayerStats'a göre ayarlandı!");
                            break;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"UI arama hatası: {e.Message}");
                }
            }
            else
            {
                uiInitialized = true;
                break;
            }
            
            // PlayerStats hazır değilse veya MaxHealth 0 ise bekle
            if (playerStats == null || playerStats.TotalMaxHealth <= 0)
            {
                Debug.LogWarning("FindUIElementsRoutine: PlayerStats hazır değil veya MaxHealth <= 0. UI kurulumu erteleniyor...");
                yield return new WaitForSeconds(0.5f);
                continue; // Döngünün başına dön
            }

            // UI Bulunduysa
            if (healthSlider != null)
            {
                uiInitialized = true;
                healthSlider.maxValue = playerStats.TotalMaxHealth; // Max değeri ayarla
                healthSlider.value = currentHealth;
                
                if (healthValueText != null)
                {
                    healthValueText.text = $"{currentHealth}/{playerStats.TotalMaxHealth}";
                }
                
                Debug.Log("PlayerHealth: UI başarıyla bulundu ve PlayerStats'a göre ayarlandı!");
                break;
            }
            
            yield return new WaitForSeconds(0.5f);
        }
        
        if (!uiInitialized)
        {
            Debug.LogWarning($"PlayerHealth: {maxAttempts} denemeden sonra UI bulunamadı!");
        }
    }

    public void TakeDamage(int damage, int attackerViewID = -1)
    {
        if (isInvincible) return;

        // Send RPC to all clients to apply damage
        pv.RPC("RPC_TakeDamage", RpcTarget.All, damage, attackerViewID);
        
        // Hasar aldıktan sonra canı tüm clientlara senkronize et (sadece owner yapmalı)
        if (pv.IsMine)
        {
            SyncHealthToAll();
        }
    }

    [PunRPC]
    private void RPC_TakeDamage(int damage, int attackerViewID)
    {
        if (isInvincible) return;

        // --- Hasar Azaltma (Savunma - Azalan Verim) ---
        int defense = playerStats != null ? playerStats.TotalDefense : 0; 
        
        // Formül: Reduction = Defense / (Defense + K)
        float reductionPercentage = 0f;
        if (defense + defenseConstantK > 0) // Bölme hatasını önle (K negatif olmamalı ama tedbir)
        {
             reductionPercentage = (float)defense / (defense + defenseConstantK);
        }
        
        // Hasar Çarpanı = 1 - Azaltma Yüzdesi
        float damageMultiplier = 1f - reductionPercentage;
        // Çarpanın negatif olmadığından emin ol (Teorik olarak olmaz ama garanti)
        damageMultiplier = Mathf.Max(0f, damageMultiplier); 

        int finalDamage = Mathf.RoundToInt(damage * damageMultiplier);
        finalDamage = Mathf.Max(1, finalDamage); // En az 1 hasar almasını sağla
        
        Debug.Log($"Incoming Damage: {damage}, Defense: {defense}, K: {defenseConstantK}, Reduction%: {reductionPercentage:P1}, Multiplier: {damageMultiplier:F3}, Final Damage: {finalDamage}");
        // -----------------------------------------------

        currentHealth = Mathf.Max(0, currentHealth - finalDamage); // Azaltılmış hasarı uygula
        SFXManager.Instance?.PlaySound(SFXNames.PlayerDamageTaken); // Play damage taken sound using constant
        
        // --- Flash Efektini Başlat --- 
        if (flash != null)
        {
            StartCoroutine(flash.FlashRoutine());
        }
        // ----------------------------

        // --- Knockback Efektini Uygula ---
        if (knockbackComponent != null && attackerViewID != -1)
        {
            PhotonView attackerPV = PhotonView.Find(attackerViewID);
            if (attackerPV != null)
            {
                knockbackComponent.GetKnockbacked(attackerPV.transform, knockbackTakenPower);
                Debug.Log($"PlayerHealth: Knockback applied from attacker ViewID: {attackerViewID} with power: {knockbackTakenPower}");
            }
            else
            {
                Debug.LogWarning($"PlayerHealth: Could not find attacker with ViewID: {attackerViewID} for knockback");
            }
        }
        // ----------------------------------

        // Show damage effect
        if (damageEffect != null)
        {
            Instantiate(damageEffect, transform.position, Quaternion.identity);
        }

        // Update UI - UI sadece kendi oyuncumuz için güncellenmeli
        if (pv.IsMine)
        {
            UpdateHealthUI();
        }

        // Only owner handles invincibility, death, and regeneration logic
        if (pv.IsMine)
        {
            lastDamageTime = Time.time; // Son hasar zamanını güncelle
            // Eğer yenilenme korutini varsa durdur ve yeniden başlat
            if (manageRegenCoroutine != null)
            {
                StopCoroutine(manageRegenCoroutine);
            }
            manageRegenCoroutine = StartCoroutine(ManageRegenerationRoutine());

            // Normal hasar sonrası dokunulmazlık (renk tonu olmadan)
            StartCoroutine(InvincibilityFrames(false));

            if (currentHealth <= 0)
            {
                Die(attackerViewID);
            }
        }
        else
        {
            // Diğer clientlarda da dokunulmazlık efektini başlat (ama dokunulmazlık durumunu değiştirme)
            StartCoroutine(InvincibilityVisualEffectOnly());
        }
    }

    public void Heal(int amount)
    {
        if (!pv.IsMine) return;

        pv.RPC("RPC_Heal", RpcTarget.All, amount);
        
        // İyileştikten sonra can değerini senkronize et
        SyncHealthToAll();
    }

    [PunRPC]
    private void RPC_Heal(int amount)
    {
        if (playerStats == null) return;
        
        // İyileşme öncesindeki can değerini sakla
        int beforeHeal = currentHealth;
        
        // İyileştirme miktarını max canı geçmeyecek şekilde uygula
        currentHealth = Mathf.Min(playerStats.TotalMaxHealth, currentHealth + amount);
        
        // Gerçek iyileşme miktarını hesapla
        int actualHealAmount = currentHealth - beforeHeal;
        
        // ============= HEALİNG NUMBER GÖSTERİMİ =============
        // İyileşme miktarını yeşil renkte göster (sadece iyileşme olmuşsa)
        if (DamageNumberManager.Instance != null && actualHealAmount > 0)
        {
            Vector3 playerPosition = transform.position + Vector3.up * 0.5f; // Oyuncunun biraz üstünde
            DamageNumberManager.Instance.ShowHealingNumber(playerPosition, actualHealAmount);
            Debug.Log($"Healing Number shown: +{actualHealAmount} HP at position {playerPosition}");
        }
        // ===================================================
        
        UpdateHealthUI();
    }

    private void Die(int killerViewID)
    {
        if (!pv.IsMine) return;

        playerController.SetLocked(true);
        // Ölüm anında collider'ı devre dışı bırak (Yerel olarak yeterli olmalı)
        if (mainCollider != null)
        {
            mainCollider.enabled = false;
        }
        else
        {
             Debug.LogError("Die: Main Collider referansı null!");
        }
        Debug.Log($"Player died! Killed by ViewID: {killerViewID}. Collider disabled.");

        // Kılıcın mevcut durumunu kaydet
        wasSwordActiveBeforeDeath = swordObject != null && swordObject.activeSelf;
        
        // Kılıcı ve isim etiketini (konteynerini) gizle
        if (swordObject != null)
        {
            swordObject.SetActive(false);
        }
        if (playerNameTagComponent != null && playerNameTagComponent.nameTagContainer != null)
        {
            playerNameTagComponent.nameTagContainer.SetActive(false);
            Debug.Log("Die: Nametag container disabled.");
        }
        else if (playerNameTagComponent != null) // Component bulundu ama container null
        {
            Debug.LogWarning("Die: PlayerNameTag component found, but its nameTagContainer is null!");
        }

        // Ölüm Efektini Tetikle (RPC ile)
        if (playerDeathVFXPrefab != null)
        {
             pv.RPC("RPC_SpawnPlayerDeathVFX", RpcTarget.All);
        }

        // PvP Kill Mesajı Gönder
        if (killerViewID != -1)
        {
            PhotonView killerPV = PhotonView.Find(killerViewID);
            if (killerPV != null)
            {
                string killerName = killerPV.Owner?.NickName ?? "Bilinmeyen Oyuncu";
                string victimName = pv.Owner?.NickName ?? "Bilinmeyen Oyuncu";
                
                // ChatManager'a sistem mesajı gönder
                ChatManager chatManager = FindObjectOfType<ChatManager>();
                if (chatManager != null)
                {
                    string killMessage = MessageColorUtils.BuildPvPKillMessage(killerName, victimName);
                    chatManager.SendSystemMessage(killMessage, SystemMessageType.PvPKill);
                    Debug.Log($"PvP Kill Message: {killMessage}");
                }
                else
                {
                    Debug.LogWarning("ChatManager bulunamadı, PvP kill mesajı gönderilemedi.");
                }
            }
        }

        // Animator'ı "IsDead" durumuna geçir
        if (animator != null)
        {
            try { animator.SetBool("IsDead", true); } // Yeni boolean parametre
            catch { Debug.LogWarning("Animator'da 'IsDead' parametresi bulunamadı."); }
        }

        // Respawn Coroutine'i başlat
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        // Ölüm süresi / efekti beklemesi için
        yield return new WaitForSeconds(2f); // Bu süre ayarlanabilir

        // Respawn with full health
        if (playerStats == null) { Debug.LogError("RespawnRoutine: PlayerStats null!"); yield break; }
        currentHealth = playerStats.TotalMaxHealth;
        UpdateHealthUI();
        
        // Can değerini yeniden doğduktan sonra senkronize et
        SyncHealthToAll();

        // Find a valid spawn point
        Vector3 spawnPoint = FindValidSpawnPoint();
        transform.position = spawnPoint;
        transform.localScale = Vector3.one; // Scale'i sıfırladığımızdan emin olalım

        // Yeniden doğduktan sonra collider'ı tekrar aktif et
        if (mainCollider != null)
        {
            mainCollider.enabled = true;
             Debug.Log("RespawnRoutine: Collider enabled.");
        }
        else
        {
             Debug.LogError("RespawnRoutine: Main Collider referansı null!");
        }

        // İsim etiketini (konteynerini) tekrar göster (ama sadece kendi oyuncumuz değilse veya gizlenmesi istenmiyorsa)
        if (playerNameTagComponent != null && playerNameTagComponent.nameTagContainer != null)
        {
            bool shouldShow = true;
            // Eğer bu kendi oyuncumuzsa ve NameTag script'inde gizle ayarı açıksa, gösterme
            if (pv.IsMine && playerNameTagComponent.GetHideLocalPlayerNameSetting())
            {
                shouldShow = false;
            }

            if (shouldShow)
            {
                playerNameTagComponent.nameTagContainer.SetActive(true);
                Debug.Log("RespawnRoutine: Nametag container enabled.");
            }
            else
            {
                playerNameTagComponent.nameTagContainer.SetActive(false); // Gizlenmesi gerekiyorsa kapalı kalsın
                Debug.Log("RespawnRoutine: Nametag container kept disabled due to local hide setting.");
            }
        }
        else if (playerNameTagComponent != null)
        {
             Debug.LogWarning("RespawnRoutine: PlayerNameTag component found, but its nameTagContainer is null!");
        }
        
        // Kılıcı önceki durumuna göre tekrar göster
        if (swordObject != null && wasSwordActiveBeforeDeath)
        {
             swordObject.SetActive(true);
             Debug.Log("RespawnRoutine: Sword object reactivated based on pre-death state.");
        }
        
        // Kılıcın hasar collider'ını başlangıçta devre dışı bırak (Kılıç görünür olsa bile)
        if (swordComponent != null && swordComponent.weaponCollider != null)
        {
            swordComponent.weaponCollider.gameObject.SetActive(false);
            Debug.Log("RespawnRoutine: Sword weapon collider explicitly disabled.");
        } 
        else if (swordComponent != null) // Sword bulundu ama collider null
        {
             Debug.LogWarning("RespawnRoutine: Sword component found, but its weaponCollider is null. Cannot disable collider.");
        }
        // Else (swordComponent == null) durumu zaten Awake'de loglanmıştı.

        // Animator'ı tekrar etkinleştir ve durumu güncelle
        if (animator != null)
        {
            try
            {
                animator.SetBool("IsDead", false); // Canlanma durumunu ayarla
            }
            catch { Debug.LogWarning("Animator'da 'IsDead' parametresi bulunamadı."); }
        }
        
        // Doğduktan sonra dokunulmazlık ve yanıp sönme efektini başlat
        StartCoroutine(InvincibilityFrames(true));
        
        // Dokunulmazlık süresi kadar bekleyip kilidi kaldır
        yield return new WaitForSeconds(invincibilityDuration);
        playerController.SetLocked(false);
    }

    private Vector3 FindValidSpawnPoint()
    {
        // Try to find a spawn point in the scene
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        
        if (spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            return spawnPoints[randomIndex].transform.position;
        }
        
        // Fallback to a random position if no spawn points are found
        return new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), 0);
    }

    private void UpdateHealthUI()
    {
        if (!pv.IsMine) return;
        if (playerStats == null) return; // PlayerStats null ise UI güncellenemez
        
        if (healthSlider != null)
        {
            healthSlider.maxValue = playerStats.TotalMaxHealth; // Max değeri güncelle
            healthSlider.value = currentHealth;
        }
        
        if (healthValueText != null)
        {
            healthValueText.text = $"{currentHealth}/{playerStats.TotalMaxHealth}"; // Max değeri kullan
        }
    }

    private IEnumerator InvincibilityFrames(bool applyTintColor)
    {
        isInvincible = true;
        
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            
            if (applyTintColor)
            {
                // Yeniden doğma: Sadece gri yap, yanıp sönme yok
                spriteRenderer.color = invincibilityTintColor;
                yield return new WaitForSeconds(invincibilityDuration);
            }
            else
            {
                // Normal hasar: Görünürlüğü aç/kapa (yanıp sönme)
                float elapsedTime = 0;
                float blinkInterval = 0.1f;
                while (elapsedTime < damageInvincibilityDuration)
                {
                    spriteRenderer.enabled = !spriteRenderer.enabled;
                    yield return new WaitForSeconds(blinkInterval);
                    elapsedTime += blinkInterval;
                }
            }
            
            // Süre bittiğinde orijinal durumu geri yükle
            spriteRenderer.enabled = true; // Her durumda görünür yap
            spriteRenderer.color = originalColor; // Her durumda orijinal rengi ata
        }
        else // SpriteRenderer yoksa sadece bekle
        {
            yield return new WaitForSeconds(invincibilityDuration);
        }
        
        isInvincible = false;
    }

    public int GetCurrentHealth()
    {
        // Sadece kendi sahip olduğumuz objenin can değerini döndürmek yerine,
        // başka oyuncuların da bizim canımızı görebilmesi için
        // PhotonView isMine kontrolünü kaldırıyoruz.
        return currentHealth;
    }

    public int GetMaxHealth() // Artık PlayerStats'tan alıyor
    {
        return playerStats != null ? playerStats.TotalMaxHealth : 0;
    }

    public float GetHealthPercentage() // Artık PlayerStats'tan alıyor
    {
        if (playerStats == null || playerStats.TotalMaxHealth <= 0) return 0f;
        return (float)currentHealth / playerStats.TotalMaxHealth;
    }

    // Oyuncu ölüm efektini tüm clientlarda oluşturan RPC
    [PunRPC]
    private void RPC_SpawnPlayerDeathVFX()
    {
        if (playerDeathVFXPrefab != null)
        {
            Instantiate(playerDeathVFXPrefab, transform.position, Quaternion.identity);
        }
    }

    // Can yenileme yönetim korutini
    private IEnumerator ManageRegenerationRoutine()
    {
        if (!pv.IsMine) yield break; 
        if (playerStats == null) { Debug.LogError("ManageRegenerationRoutine: PlayerStats null!"); yield break; }

        Debug.Log("ManageRegenerationRoutine başlatıldı.");

        while (true) 
        {
            yield return new WaitUntil(() => Time.time - lastDamageTime >= regenerationDelay && currentHealth < playerStats.TotalMaxHealth);

            Debug.Log("Yenilenme koşulları sağlandı, yenilenme döngüsü başlıyor.");

            while (Time.time - lastDamageTime >= regenerationDelay && currentHealth < playerStats.TotalMaxHealth)
            {
                int healAmount = Mathf.Max(1, Mathf.RoundToInt(playerStats.TotalMaxHealth * (regenerationPercent / 100f)));
                
                Debug.Log($"Yenileniyor: +{healAmount}");
                Heal(healAmount); 

                yield return new WaitForSeconds(regenerationInterval);
            }
            
            Debug.Log("Yenilenme döngüsü bitti (Can doldu veya hasar alındı).");
        }
    }

    // Karakterin ve alt nesnelerinin SpriteRenderer'larını aktif/pasif yapan yardımcı fonksiyon
    private void SetRenderersEnabled(bool enabled)
    {
        // Ana renderer
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = enabled;
        }
        // Çocuklardaki rendererlar
        foreach (var rend in GetComponentsInChildren<SpriteRenderer>())
        {
            // Ana renderer'ı tekrar işlememek için kontrol (GetComponentInChildren kendini de dahil eder)
            if (rend != spriteRenderer) 
            {
                 // rend.enabled = enabled; // Bu satır da kaldırıldı, Animator yönetsin
            }
        }
    }

    // PlayerStats.OnStatsCalculated tetiklendiğinde çağrılır
    private void HandleStatsCalculated()
    {
        if (!pv.IsMine) return;
        // Sadece UI'ı güncelle
        UpdateHealthUI();
        
        // Stats değiştiğinde de canı senkronize et
        SyncHealthToAll();
    }

    // PlayerStats.OnLevelUp tetiklendiğinde çağrılır
    private void HandleLevelUp(int newLevel)
    {
        if (!pv.IsMine) return;
        if (playerStats == null) return;

        Debug.Log($"PlayerHealth: Seviye atlandı (Yeni Seviye: {newLevel}), can dolduruluyor.");
        // Canı yeni maksimuma doldur
        currentHealth = playerStats.TotalMaxHealth; 
        UpdateHealthUI();
    }

    /// <summary>
    /// PlayerStats tarafından çağrılarak başlangıç canını ayarlar.
    /// </summary>
    public void SetCurrentHealth(int health)
    {
        if (!pv.IsMine)
        {
            Debug.LogWarning("SetCurrentHealth sadece yerel oyuncu için çağrılmalı.");
            return;
        }
        currentHealth = health;
        Debug.Log($"SetCurrentHealth: Can ayarlandı -> {currentHealth}");
        // UI'ı da hemen güncelle
        UpdateHealthUI(); 
    }

    // Yeni method: Tüm clientlara can değerini senkronize et
    public void SyncHealthToAll()
    {
        if (!pv.IsMine) return; // Sadece kendi playerimiz için çağırılmalı
        
        // Tüm clientlara can değerini gönder
        pv.RPC("RPC_SyncHealth", RpcTarget.AllBuffered, currentHealth);
    }

    [PunRPC]
    private void RPC_SyncHealth(int newHealth)
    {
        // Gelen can değerini güncelle
        currentHealth = newHealth;
        
        // UI güncellemesi sadece bizim kontrolümüzdeki oyuncu için yapılmalı
        if (pv.IsMine)
        {
            UpdateHealthUI();
        }
    }

    // Geciktirilmiş can senkronizasyonu için yeni korutin
    private IEnumerator DelayedHealthSync()
    {
        // Oyun nesnesi oluşturulduktan ve can değeri ayarlandıktan sonra çalışması için kısa bir süre bekle
        yield return new WaitForSeconds(4f);
        
        // Eğer PlayerStats hazır ve can değeri 0'dan büyükse senkronize et
        if (playerStats != null && currentHealth > 0)
        {
            Debug.Log($"DelayedHealthSync: Sending current health ({currentHealth}) to all clients");
            SyncHealthToAll();
        }
        else
        {
            // PlayerStats hazır değilse biraz daha bekle
            yield return new WaitForSeconds(0.5f);
            if (playerStats != null)
            {
                currentHealth = playerStats.TotalMaxHealth; // Emin olmak için canı maksimum değere ayarla
                Debug.Log($"DelayedHealthSync: Waited longer, sending health ({currentHealth}) to all clients");
                SyncHealthToAll();
            }
        }
    }

    // Odaya katıldığında da senkronize et
    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        
        if (pv.IsMine && playerStats != null)
        {
            // Odaya katıldığımızda canı senkronize et
            Debug.Log($"OnJoinedRoom: Sending health ({currentHealth}) to all clients");
            SyncHealthToAll();
        }
    }

    // Diğer clientlarda sadece görsel efekt için dokunulmazlık
    private IEnumerator InvincibilityVisualEffectOnly()
    {
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            
            // Normal hasar: Görünürlüğü aç/kapa (yanıp sönme)
            float elapsedTime = 0;
            float blinkInterval = 0.1f;
            while (elapsedTime < damageInvincibilityDuration)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled;
                yield return new WaitForSeconds(blinkInterval);
                elapsedTime += blinkInterval;
            }
            
            // Süre bittiğinde orijinal durumu geri yükle
            spriteRenderer.enabled = true; // Her durumda görünür yap
            spriteRenderer.color = originalColor; // Her durumda orijinal rengi ata
        }
        else // SpriteRenderer yoksa sadece bekle
        {
            yield return new WaitForSeconds(damageInvincibilityDuration);
        }
    }
} 