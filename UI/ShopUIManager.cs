using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopUIManager : MonoBehaviour
{
    public static ShopUIManager Instance { get; private set; }

    [Header("Shop Panel References")]
    [SerializeField] private GameObject shopPanel; // Ana alışveriş paneli
    [SerializeField] private Transform buyItemContainer;    // Satın alınabilecek itemlerin listeleneceği parent
    [SerializeField] private Transform sellItemContainer;   // Oyuncunun satabileceği itemlerin listeleneceği parent
    [SerializeField] private GameObject shopItemPrefab;     // Dükkandaki her bir item için kullanılacak prefab

    [Header("Item Details UI")]
    [SerializeField] private TextMeshProUGUI selectedItemNameText;
    [SerializeField] private TextMeshProUGUI selectedItemDescriptionText;
    [SerializeField] private TextMeshProUGUI selectedItemValueText;
    [SerializeField] private Image selectedItemIcon;
    [SerializeField] private GameObject itemDetailsPanel; // Item detaylarını içeren panel
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private TMP_InputField quantitySellInputField; // YENİ: Satış miktarı giriş alanı
    [SerializeField] private Button increaseQuantityButton; // YENİ: Miktar artırma butonu
    [SerializeField] private Button decreaseQuantityButton; // YENİ: Miktar azaltma butonu

    [Header("Player Currency UI")]
    [SerializeField] private TextMeshProUGUI playerTotalCopperText; // YENİ: Oyuncunun toplam bakırını gösterecek

    private InventoryItem currentSelectedItem;
    private PlayerStats localPlayerStats;
    private const float SELL_PERCENTAGE = 0.20f; // Tüccarın item değerinin %50'sini ödemesi
    private int currentSellQuantity = 1; // YENİ: Mevcut satış miktarı

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Eğer ShopUIManager'ın sahneler arası geçişte de kalmasını istiyorsan:
            // DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // ShopPanel'in başlangıçta deaktif olduğundan emin olalım, Instance set edildikten sonra.
        // Bu kontrol ShopUIManager'ın kendisi deaktif bir objede değilse anlamlı.
        if (shopPanel != null) 
        { 
            shopPanel.SetActive(false); 
        } 
        else 
        { 
            Debug.LogError("Shop Panel referansı atanmamış!"); 
        }

        buyButton?.onClick.AddListener(BuySelectedItem);
        sellButton?.onClick.AddListener(SellSelectedItem);
        if (quantitySellInputField != null)
        {
            quantitySellInputField.onValueChanged.AddListener(OnSellQuantityChanged);
            quantitySellInputField.gameObject.SetActive(false); // Başlangıçta gizle
        }
        increaseQuantityButton?.onClick.AddListener(IncrementSellQuantity);
        decreaseQuantityButton?.onClick.AddListener(DecrementSellQuantity);
        SetQuantityButtonsActive(false); // Başlangıçta butonları gizle
    }

    public void OpenShop(PlayerStats playerStats)
    {
        localPlayerStats = playerStats;
        if (shopPanel == null) return;

        // Listener'ları her açılışta yeniden ekle, çünkü CloseShop'ta kaldırılıyorlar.
        buyButton?.onClick.RemoveAllListeners(); // Önce temizle
        sellButton?.onClick.RemoveAllListeners(); // Önce temizle
        buyButton?.onClick.AddListener(BuySelectedItem);
        sellButton?.onClick.AddListener(SellSelectedItem);
        
        if (quantitySellInputField != null)
        {
            quantitySellInputField.onValueChanged.RemoveAllListeners(); // Önce temizle
            quantitySellInputField.onValueChanged.AddListener(OnSellQuantityChanged);
            quantitySellInputField.gameObject.SetActive(false); // Her açılışta gizle
        }
        increaseQuantityButton?.onClick.RemoveAllListeners();
        decreaseQuantityButton?.onClick.RemoveAllListeners();
        increaseQuantityButton?.onClick.AddListener(IncrementSellQuantity);
        decreaseQuantityButton?.onClick.AddListener(DecrementSellQuantity);
        SetQuantityButtonsActive(false); // Her açılışta butonları gizle

        shopPanel.SetActive(true);
        Debug.Log("Shop UI Açıldı.");
        PopulateBuyTab();
        PopulateSellTab();
        SwitchToBuyTab(); // Varsayılan olarak satın alma sekmesini göster
        ClearSelectedItemDetails(); // Seçili item detaylarını temizle

        UpdatePlayerTotalCopperDisplay();

        // Oyuncunun hareketini ve kılıç kullanımını UI açıkken kilitleme (Merchant scripti hallediyor)
    }

    public void CloseShop()
    {
        if (shopPanel == null) return;

        shopPanel.SetActive(false);
        localPlayerStats = null;
        currentSelectedItem = null;
        Debug.Log("Shop UI Kapandı.");

        buyButton?.onClick.RemoveAllListeners();
        sellButton?.onClick.RemoveAllListeners();
        if (quantitySellInputField != null)
        {
            quantitySellInputField.onValueChanged.RemoveAllListeners();
            quantitySellInputField.gameObject.SetActive(false);
        }
        increaseQuantityButton?.onClick.RemoveAllListeners();
        decreaseQuantityButton?.onClick.RemoveAllListeners();
        SetQuantityButtonsActive(false);

        UpdatePlayerTotalCopperDisplay(); // Kapandığında da N/A veya boş göstermesi için
    }

    void PopulateBuyTab()
    {
        if (buyItemContainer == null || shopItemPrefab == null || ItemDatabase.Instance == null) return;

        // Önceki itemleri temizle
        foreach (Transform child in buyItemContainer)
        {
            Destroy(child.gameObject);
        }

        List<ItemData> allItems = ItemDatabase.Instance.GetAllItems();
        foreach (ItemData itemData in allItems)
        {
            if (itemData != null && itemData.isPurchasableByPlayer) // Oyuncu tarafından satın alınabilir mi?
            {
                GameObject itemObj = Instantiate(shopItemPrefab, buyItemContainer);
                ShopItemUI shopItemUI = itemObj.GetComponent<ShopItemUI>();
                if (shopItemUI != null)
                {
                    shopItemUI.Setup(itemData, true, this); // true: buy tab
                }
            }
        }
        Debug.Log($"Satın alma sekmesi {buyItemContainer.childCount} item ile dolduruldu.");
    }

    void PopulateSellTab()
    {
        if (sellItemContainer == null || shopItemPrefab == null || InventoryManager.Instance == null) return;

        // Önceki itemleri temizle
        foreach (Transform child in sellItemContainer)
        {
            Destroy(child.gameObject);
        }

        // Oyuncunun envanterindeki satılabilir itemleri listele
        // InventoryManager'dan tüm itemları alıp burada filtrelemek ve göstermek gerekecek.
        // Şimdilik bu kısmı basit tutuyorum, InventoryManager.Instance.GetPlayerItems() gibi bir metoda ihtiyaç olacak.
        
        // Geçici: Tüm envanteri alıp filtreleyelim (Bu performans açısından optimize edilebilir)
        for (int i = 0; i < InventoryManager.Instance.InventorySize; i++)
        {
            InventoryItem invItem = InventoryManager.Instance.GetItemAt(i);
            if (invItem != null)
            {
                ItemData itemData = ItemDatabase.Instance.GetItemById(invItem.ItemId);
                if (itemData != null && itemData.isSellableToMerchant) // Tüccara satılabilir mi?
                {
                    GameObject itemObj = Instantiate(shopItemPrefab, sellItemContainer);
                    ShopItemUI shopItemUI = itemObj.GetComponent<ShopItemUI>();
                    if (shopItemUI != null)
                    {
                        // Satış için InventoryItem'ı da gönderebiliriz veya sadece ItemData yeterli olabilir.
                        // Şimdilik ItemData ve miktarı kullanmayı deneyelim, gerekirse InventoryItem'ı da ekleriz.
                        shopItemUI.Setup(itemData, false, this, invItem.Amount); // false: sell tab, invItem.Amount
                    }
                }
            }
        }
        Debug.Log($"Satış sekmesi {sellItemContainer.childCount} item ile dolduruldu.");
    }

    public void SelectShopItem(ItemData itemData, bool isBuyItem, int quantity = 1)
    {
        if (itemData == null) 
        {
            ClearSelectedItemDetails();
            return;
        }
        if (itemDetailsPanel != null) itemDetailsPanel.SetActive(true); // Paneli görünür yap

        currentSelectedItem = new InventoryItem(itemData, quantity); 
        currentSellQuantity = 1; 

        if (selectedItemNameText != null) selectedItemNameText.text = itemData.ItemName;
        if (selectedItemDescriptionText != null) selectedItemDescriptionText.text = itemData.Description;
        if (selectedItemIcon != null) 
        { 
            selectedItemIcon.sprite = itemData.Icon;
            selectedItemIcon.gameObject.SetActive(itemData.Icon != null);
        }

        int displayPrice = itemData.value; // Satın alma fiyatı direkt itemın değeri
        if (!isBuyItem) // Eğer satış sekmesindeyse
        {
            displayPrice = Mathf.FloorToInt(itemData.value * SELL_PERCENTAGE);
            displayPrice = Mathf.Max(1, displayPrice); // En az 1 bakır
        }

        if (selectedItemValueText != null) selectedItemValueText.text = CurrencyUtils.FormatCopperValue(displayPrice);
        
        if (quantitySellInputField != null)
        {
            quantitySellInputField.gameObject.SetActive(false); // Miktar alanı bu senaryoda gizli
        }
        SetQuantityButtonsActive(false); // Miktar butonlarını gizle
        if(sellButton != null) sellButton.GetComponentInChildren<TextMeshProUGUI>().text = "Sat";


        buyButton?.gameObject.SetActive(isBuyItem);
        sellButton?.gameObject.SetActive(!isBuyItem && itemData.isSellableToMerchant);
    }
    
    public void SelectShopInventoryItem(InventoryItem invItem, bool isBuyItem)
    {
        if (invItem == null)
        {
            ClearSelectedItemDetails();
            return;
        }
        ItemData itemData = ItemDatabase.Instance.GetItemById(invItem.ItemId);
        if (itemData == null) 
        {
            ClearSelectedItemDetails();
            return;
        }
        if (itemDetailsPanel != null) itemDetailsPanel.SetActive(true); // Paneli görünür yap

        currentSelectedItem = invItem;
        currentSellQuantity = 1; 

        if (selectedItemNameText != null) selectedItemNameText.text = itemData.ItemName;
        if (selectedItemDescriptionText != null) selectedItemDescriptionText.text = itemData.Description;
        if (selectedItemIcon != null) 
        { 
            selectedItemIcon.sprite = itemData.Icon;
            selectedItemIcon.gameObject.SetActive(itemData.Icon != null);
        }
        
        if (!isBuyItem && itemData.isSellableToMerchant && invItem.Amount > 0) // Satış sekmesi ve satılabilir item
        {
            if (quantitySellInputField != null)
            {
                if (invItem.IsStackable && invItem.Amount > 1)
                {
                    quantitySellInputField.gameObject.SetActive(true);
                    quantitySellInputField.text = "1"; // Varsayılan 1
                    SetQuantityButtonsActive(true); // Miktar butonlarını göster
                }
                else
                {
                    quantitySellInputField.gameObject.SetActive(false);
                    SetQuantityButtonsActive(false); // Miktar butonlarını gizle
                }
            }
            UpdateSellConfirmationUI(); // Fiyatı ve buton metnini güncelle
        }
        else // Satın alma veya satılamaz item
        {
            if (quantitySellInputField != null)
            {
                quantitySellInputField.gameObject.SetActive(false);
            }
            SetQuantityButtonsActive(false); // Miktar butonlarını gizle
            int displayPrice = itemData.value; // Satın alma için
            if (selectedItemValueText != null) selectedItemValueText.text = CurrencyUtils.FormatCopperValue(displayPrice);
            if(sellButton != null) sellButton.GetComponentInChildren<TextMeshProUGUI>().text = "Sat";
        }

        buyButton?.gameObject.SetActive(isBuyItem);
        sellButton?.gameObject.SetActive(!isBuyItem && itemData.isSellableToMerchant);
    }

    private void OnSellQuantityChanged(string quantityStr)
    {
        if (currentSelectedItem == null || !sellButton.gameObject.activeSelf) return;

        if (int.TryParse(quantityStr, out int quantity))
        {
            ItemData itemData = ItemDatabase.Instance.GetItemById(currentSelectedItem.ItemId);
            if (itemData == null) return;

            // Miktarı itemin envanterdeki miktarıyla sınırla
            int maxAmount = currentSelectedItem.Amount;
            quantity = Mathf.Clamp(quantity, 1, maxAmount);

            if (quantitySellInputField.text != quantity.ToString()) // Eğer clamp sonrası değer değiştiyse input'u güncelle
            {
                quantitySellInputField.text = quantity.ToString();
                // Bu, onValueChanged'ı tekrar tetikleyebilir, dikkatli olmalı veya listener'ı geçici kaldırmalı
            }
            currentSellQuantity = quantity;
        }
        else
        {
            currentSellQuantity = 1; // Geçersiz giriş durumunda 1'e ayarla
            if (quantitySellInputField.text != "1") quantitySellInputField.text = "1";
        }
        UpdateSellConfirmationUI();
    }

    private void UpdateSellConfirmationUI()
    {
        if (currentSelectedItem == null) return;

        ItemData itemData = ItemDatabase.Instance.GetItemById(currentSelectedItem.ItemId);
        if (itemData == null) return;

        int pricePerItem = Mathf.FloorToInt(itemData.value * SELL_PERCENTAGE);
        pricePerItem = Mathf.Max(1, pricePerItem); // En az 1 bakır

        int totalSellPrice = pricePerItem * currentSellQuantity;

        if (selectedItemValueText != null)
        {
            selectedItemValueText.text = $"{CurrencyUtils.FormatCopperValue(totalSellPrice)} (x{currentSellQuantity})";
        }

        if (sellButton != null && sellButton.gameObject.activeSelf)
        {
            TextMeshProUGUI sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>();
            if (sellButtonText != null)
            {
                sellButtonText.text = $"Sat (x{currentSellQuantity})";
            }
        }
    }

    private void SetQuantityButtonsActive(bool isActive)
    {
        increaseQuantityButton?.gameObject.SetActive(isActive);
        decreaseQuantityButton?.gameObject.SetActive(isActive);
    }

    private void IncrementSellQuantity()
    {
        if (currentSelectedItem == null || quantitySellInputField == null || !quantitySellInputField.gameObject.activeSelf) return;
        
        int currentVal = 1;
        if (int.TryParse(quantitySellInputField.text, out currentVal))
        {
            currentVal++;
        }
        // Miktarı itemin envanterdeki miktarıyla sınırla
        int maxAmount = currentSelectedItem.Amount;
        quantitySellInputField.text = Mathf.Clamp(currentVal, 1, maxAmount).ToString();
        // OnSellQuantityChanged tetiklenecek ve currentSellQuantity güncellenecektir.
    }

    private void DecrementSellQuantity()
    {
        if (currentSelectedItem == null || quantitySellInputField == null || !quantitySellInputField.gameObject.activeSelf) return;

        int currentVal = 1;
        if (int.TryParse(quantitySellInputField.text, out currentVal))
        {
            currentVal--;
        }
        // Miktarı itemin envanterdeki miktarıyla sınırla (minimum 1)
        int maxAmount = currentSelectedItem.Amount;
        quantitySellInputField.text = Mathf.Clamp(currentVal, 1, maxAmount).ToString();
        // OnSellQuantityChanged tetiklenecek ve currentSellQuantity güncellenecektir.
    }

    void ClearSelectedItemDetails()
    {
        currentSelectedItem = null;
        currentSellQuantity = 1; 
        if (selectedItemNameText != null) selectedItemNameText.text = "";
        if (selectedItemDescriptionText != null) selectedItemDescriptionText.text = "";
        if (selectedItemValueText != null) selectedItemValueText.text = "";
        if (selectedItemIcon != null) selectedItemIcon.gameObject.SetActive(false);
        
        if (quantitySellInputField != null)
        {
            quantitySellInputField.gameObject.SetActive(false);
            quantitySellInputField.text = "1";
        }
        SetQuantityButtonsActive(false); // Miktar butonlarını gizle
        if(sellButton != null)
        {
            TextMeshProUGUI sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>();
            if (sellButtonText != null) sellButtonText.text = "Sat";
        }

        buyButton?.gameObject.SetActive(false);
        sellButton?.gameObject.SetActive(false);
        if (itemDetailsPanel != null) itemDetailsPanel.SetActive(false); // Paneli gizle
    }

    public void BuySelectedItem()
    {
        if (currentSelectedItem == null || localPlayerStats == null)
        {
            UIFeedbackManager.Instance?.ShowTooltip("Satın almak için bir eşya seçin.");
            return;
        }

        ItemData itemDataToProcess = ItemDatabase.Instance?.GetItemById(currentSelectedItem.ItemId);
        if (itemDataToProcess == null)
        {
            Debug.LogError($"BuySelectedItem: ItemDatabase'de item bulunamadı: ID={currentSelectedItem.ItemId}");
            UIFeedbackManager.Instance?.ShowTooltip("Satın alınacak eşya bilgisi bulunamadı!");
            return;
        }

        int itemPrice = itemDataToProcess.value; // Item'ın temel bakır değeri

        if (InventoryManager.Instance.TryRemoveCurrency(itemPrice))
        {
            // Parayı başarıyla çıkardı, itemi envantere ekle
            InventoryItem newItem = new InventoryItem(itemDataToProcess, 1); // Şimdilik 1 adet
            bool addedToInventory = InventoryManager.Instance.AddItem(newItem, -1, true);

            if (addedToInventory)
            {
                UIFeedbackManager.Instance?.ShowTooltip($"{currentSelectedItem.ItemName} satın alındı!");
                PopulateSellTab(); // Satış sekmesini de güncelle (item oraya gitmiş olabilir)
                localPlayerStats.SavePlayerData(); // Para değiştiği için kaydet

                // SFX ÇAL
                SFXManager.Instance?.PlaySound(SFXNames.ItemBought);

                // Seçili itemi temizle ve UI'ı güncelle
                ClearSelectedItemDetails();
            }
            else
            {
                UIFeedbackManager.Instance?.ShowTooltip("Envanter dolu, satın alma başarısız!");
                InventoryManager.Instance.TryAddCurrency(itemPrice);
            }
        }
        else
        {
            UIFeedbackManager.Instance?.ShowTooltip("Yetersiz bakır!");
        }

        UpdatePlayerTotalCopperDisplay();
    }

    public void SellSelectedItem()
    {
        if (currentSelectedItem == null || localPlayerStats == null)
        {
            UIFeedbackManager.Instance?.ShowTooltip("Satmak için envanterinizden bir eşya seçin.");
            return;
        }

        ItemData itemDataToProcess = ItemDatabase.Instance?.GetItemById(currentSelectedItem.ItemId);
        if (itemDataToProcess == null)
        {
            Debug.LogError($"SellSelectedItem: ItemDatabase'de item bulunamadı: ID={currentSelectedItem.ItemId}");
            UIFeedbackManager.Instance?.ShowTooltip("Satılacak eşya bilgisi bulunamadı!");
            ClearSelectedItemDetails();
            return;
        }

        if (!itemDataToProcess.isSellableToMerchant)
        {
            UIFeedbackManager.Instance?.ShowTooltip($"{itemDataToProcess.ItemName} tüccara satılamaz.");
            ClearSelectedItemDetails();
            return;
        }

        int sellPricePerItem = Mathf.FloorToInt(itemDataToProcess.value * SELL_PERCENTAGE);
        sellPricePerItem = Mathf.Max(1, sellPricePerItem);
        
        // Miktarı al (quantitySellInputField aktifse oradan, değilse currentSelectedItem.Amount veya 1)
        int quantityToSell = currentSellQuantity; // currentSellQuantity zaten OnSellQuantityChanged ile güncelleniyor

        if (quantitySellInputField != null && quantitySellInputField.gameObject.activeSelf)
        {
            if (int.TryParse(quantitySellInputField.text, out int inputQuantity))
            {
                // Miktarı, seçili item'ın envanterdeki miktarıyla ve 1 ile sınırla
                quantityToSell = Mathf.Clamp(inputQuantity, 1, currentSelectedItem.Amount);
            }
            else
            {
                quantityToSell = 1; // Hatalı giriş durumunda 1 sat
            }
        }
        else // Miktar alanı aktif değilse, ya item stacklenebilir değil ya da miktarı 1
        {
            quantityToSell = Mathf.Min(1, currentSelectedItem.Amount); // En fazla eldeki kadar sat (genelde 1 olur)
        }
        
        if (quantityToSell <= 0) {
             UIFeedbackManager.Instance?.ShowTooltip("Satılacak miktar geçersiz.");
             return;
        }


        bool removedFromInventory = InventoryManager.Instance.RemoveSpecificItem(currentSelectedItem.ItemId, quantityToSell, true);

        if (removedFromInventory)
        {
            int totalIncome = sellPricePerItem * quantityToSell;
            if (InventoryManager.Instance.TryAddCurrency(totalIncome))
            {
                UIFeedbackManager.Instance?.ShowTooltip($"{itemDataToProcess.ItemName} (x{quantityToSell}) satıldı, {CurrencyUtils.FormatCopperValue(totalIncome)} kazanıldı.");
            }
            else
            {
                UIFeedbackManager.Instance?.ShowTooltip("Satış başarılı ancak para envantere eklenemedi (yer yok?)!");
                Debug.LogError($"Para eklenemediği için satılan {itemDataToProcess.ItemName} iade edilemedi.");
                // İade mekanizması eklenebilir: InventoryManager.Instance.AddItem(new InventoryItem(itemDataToProcess, quantityToSell));
            }
        }
        else
        {
            UIFeedbackManager.Instance?.ShowTooltip("Eşya envanterden kaldırılamadı, satış iptal.");
        }

        UpdatePlayerTotalCopperDisplay();
        PopulateSellTab(); // Satış sonrası satış sekmesini yenile
        // Eğer satılan item bittiyse veya farklı bir item seçildiyse detaylar temizlenmeli.
        // PopulateSellTab sonrası seçili item kalmayabilir, ClearSelectedItemDetails() burada mantıklı.
        ClearSelectedItemDetails();
    }

    void UpdatePlayerTotalCopperDisplay()
    {
        if (playerTotalCopperText != null && InventoryManager.Instance != null)
        {
            int totalCopper = InventoryManager.Instance.CalculateTotalCopperValue();
            playerTotalCopperText.text = $"Para: {CurrencyUtils.FormatCopperValue(totalCopper)}";
        }
        else if (playerTotalCopperText != null)
        {
            playerTotalCopperText.text = "Para: <color=#B87333>N/A</color>";
        }
    }
    
    // Bu metot Merchant scripti tarafından çağrılacak
    public void InitializeShop(PlayerStats playerStats)
    {
        OpenShop(playerStats);
    }

    // Merchant ToggleShop içinde shopUIPanel.SetActive(false) yerine bunu çağırabilir.
    public void DeinitializeShop()
    {
        CloseShop();
    }

    private void PopulateShopTab(Transform container, List<ItemData> itemsToDisplay, bool isBuyTab)
    {
        // Implementasyonu burada yapılabilir
    }

    private void SwitchToBuyTab()
    {
        // Implementasyonu burada yapılabilir
    }
} 