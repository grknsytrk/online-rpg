using UnityEngine;
using Photon.Pun; // Oyuncu kontrolü ve kilitleme için gerekebilir

public class Merchant : MonoBehaviour
{
    [Header("Shop UI")]
    [SerializeField] private GameObject shopUIPanel; // Inspector'dan atanacak Alışveriş UI Paneli
    [SerializeField] private KeyCode interactionKey = KeyCode.E; // Etkileşim tuşu

    private bool isShopOpen = false;
    private PlayerController localPlayerController; // Etkileşimde bulunabilecek yerel oyuncu

    void Start()
    {
        // shopUIPanel referansı artık ShopUIManager üzerinden yönetilecek.
        // if (shopUIPanel != null)
        // {
        //     shopUIPanel.SetActive(false); 
        // }
        // else
        // {
        //     Debug.LogError("Merchant: Shop UI Panel referansı atanmamış!");
        // }
    }

    void Update()
    {
        // Sadece yerel oyuncu tüccarla etkileşime girebilir
        if (localPlayerController != null) // Oyuncu etkileşim alanında mı diye kontrol et
        {
            if (Input.GetKeyDown(interactionKey))
            {
                Debug.Log($"Merchant Update: Etkileşim tuşu '{interactionKey}' basıldı.");
                Debug.Log($"Merchant Update: Kontrol ediliyor -> UIManager.Instance != null: {(UIManager.Instance != null)}, " +
                          $"!UIManager.Instance.IsChatOpen(): {((UIManager.Instance != null) ? (!UIManager.Instance.IsChatOpen()).ToString() : "N/A (UI Manager Null)")}, " +
                          $"InventoryManager.Instance != null: {(InventoryManager.Instance != null)}, " +
                          $"InventoryManager.Instance.InventoryUIParent != null: {((InventoryManager.Instance != null) ? (InventoryManager.Instance.InventoryUIParent != null).ToString() : "N/A (Inv Manager Null)")}, " +
                          $"!InventoryManager.Instance.InventoryUIParent.activeSelf: {((InventoryManager.Instance != null && InventoryManager.Instance.InventoryUIParent != null) ? (!InventoryManager.Instance.InventoryUIParent.activeSelf).ToString() : "N/A (Inv Manager veya UI Parent Null)")}, " +
                          $"isShopOpen: {isShopOpen}");

                // Başka bir UI paneli açık değilse koşulunu kaldırıyoruz.
                // bool canToggleShop = UIManager.Instance != null &&
                //                      !UIManager.Instance.IsChatOpen() &&
                //                      InventoryManager.Instance != null && 
                //                      InventoryManager.Instance.InventoryUIParent != null && 
                //                      !InventoryManager.Instance.InventoryUIParent.activeSelf;

                // if (canToggleShop) // Eski if koşulu
                if (!isShopOpen) // Eğer dükkan kapalıysa direkt açmayı dene
                {
                    Debug.Log("Merchant Update: Koşullar (basitleştirilmiş) sağlandı. ToggleShop() çağrılıyor.");
                    ToggleShop();
                }
                else // Eğer dükkan zaten açıksa, tuşa basıldığında kapat
                {
                    Debug.Log("Merchant Update: Dükkan zaten açık. Kapatmak için ToggleShop() çağrılıyor.");
                    ToggleShop();
                }
                // else // Eski else bloğu, artık bu kadar detaylı kontrole gerek yok (şimdilik)
                // {
                //     Debug.LogWarning("Merchant Update: Dükkan açılamadı çünkü koşullar sağlanmadı.");
                //     if (UIManager.Instance == null) Debug.LogError("Dükkan AÇILAMAMA NEDENİ: UIManager.Instance NULL!");
                //     else if (UIManager.Instance.IsChatOpen()) Debug.LogWarning("Dükkan AÇILAMAMA NEDENİ: Chat açık!");
                //     if (InventoryManager.Instance == null) Debug.LogError("Dükkan AÇILAMAMA NEDENİ: InventoryManager.Instance NULL!");
                //     else if (InventoryManager.Instance.InventoryUIParent == null) Debug.LogError("Dükkan AÇILAMAMA NEDENİ: InventoryManager.Instance.InventoryUIParent NULL!");
                //     else if (InventoryManager.Instance.InventoryUIParent.activeSelf) Debug.LogWarning("Dükkan AÇILAMAMA NEDENİ: Envanter açık!");
                // }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Oyuncu trigger alanına girdiğinde
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            // Sadece yerel oyuncu için referans al
            if (pc != null && pc.PV.IsMine)
            {
                localPlayerController = pc;
                // Oyuncuya etkileşimde bulunabileceğine dair bir ipucu gösterilebilir (örn: UI text)
                // UIFeedbackManager.Instance?.ShowTooltip($"Tüccarla konuşmak için [{interactionKey}] tuşuna bas.");
                Debug.Log("Yerel oyuncu tüccar etkileşim alanına girdi.");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Oyuncu trigger alanından çıktığında
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null && pc.PV.IsMine && pc == localPlayerController)
            {
                // Eğer dükkan açıksa kapat
                if (isShopOpen)
                {
                    ToggleShop();
                }
                localPlayerController = null;
                // UIFeedbackManager.Instance?.HideTooltip(); // İpucunu gizle
                Debug.Log("Yerel oyuncu tüccar etkileşim alanından çıktı.");
            }
        }
    }

    public void ToggleShop()
    {
        if (localPlayerController == null) return;

        isShopOpen = !isShopOpen;
        // shopUIPanel.SetActive(isShopOpen); // ShopUIManager yönetecek

        PlayerStats playerStats = localPlayerController.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogError("Merchant: PlayerStats componenti bulunamadı!");
            isShopOpen = false; // Güvenlik için dükkanı açma
            return;
        }

        // Oyuncunun hareketini ve kılıç kullanımını kilitle/aç
        localPlayerController.SetLocked(isShopOpen);
        Sword swordComponent = localPlayerController.GetComponentInChildren<Sword>();
        if (swordComponent != null)
        {
            swordComponent.SetLocked(isShopOpen);
        }

        if (isShopOpen)
        {
            Debug.Log("Dükkan paneli açılıyor (Merchant üzerinden)...");
            if (ShopUIManager.Instance != null)
            {
                ShopUIManager.Instance.InitializeShop(playerStats);
            }
            else
            {
                Debug.LogError("Merchant: ShopUIManager.Instance bulunamadı!");
                // Failed to open, so revert state and locks
                isShopOpen = false; 
                localPlayerController.SetLocked(false); 
                if (swordComponent != null) // swordComponent was locked above
                {
                    swordComponent.SetLocked(false); // Unlock it
                }
                return; 
            }

            // Gerekirse diğer UI panellerini kapatması için UIManager'a haber verilebilir
            // UIManager.Instance?.CloseAllPanelsApartFromShop(shopUIPanel); 
        }
        else
        {
            Debug.Log("Dükkan paneli kapanıyor (Merchant üzerinden)...");
            if (ShopUIManager.Instance != null)
            {
                ShopUIManager.Instance.DeinitializeShop();
            }
            // Oyuncunun normal fare durumuna geri dön - KALDIRILIYOR
            // Cursor.lockState = CursorLockMode.Locked; // KALDIRILDI
            // Cursor.visible = false; // KALDIRILDI
            // Debug.Log("Merchant: İmleç oyun için ayarlandı (Kilitli ve Gizli)."); // KALDIRILDI
        }
    }
} 