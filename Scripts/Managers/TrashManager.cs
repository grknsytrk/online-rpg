using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // Gerekirse diye eklendi

public class TrashManager : MonoBehaviour
{
    public static TrashManager Instance { get; private set; }

    [Header("Confirmation Panel UI")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Image itemIconDisplay;
    [SerializeField] private TextMeshProUGUI confirmationMessageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_InputField quantityInputField;

    private InventorySlotUI _sourceSlotForTrash;
    private InventoryItem _itemBeingTrashed;
    private int _maxSellableAmount;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Eğer sahneler arası geçişte de aktif kalması gerekiyorsa
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (quantityInputField != null) 
        {
            quantityInputField.gameObject.SetActive(false);
            // Listener'ı sadece bir kez Awake'de ekleyelim.
            quantityInputField.onValueChanged.AddListener(UpdateConfirmationForQuantity);
            Debug.Log("TrashManager Awake: Listener added to quantityInputField.");
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmTrash);
        if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelTrash);
    }

    public void ShowTrashConfirmationPanel(InventorySlotUI sourceSlot, InventoryItem itemToTrash)
    {
        Debug.Log("---- ShowTrashConfirmationPanel START ----"); // Yeni Log
        if (itemToTrash == null || sourceSlot == null)
        {
            Debug.LogError("ShowTrashConfirmationPanel: Item veya kaynak slot null!");
            return;
        }

        if (itemToTrash.ItemValue <= 0)
        {
            UIFeedbackManager.Instance?.ShowTooltip($"<color=#FFD700>{itemToTrash.ItemName}</color> satılamaz (değeri yok).");
            return;
        }

        _sourceSlotForTrash = sourceSlot;
        _itemBeingTrashed = itemToTrash;
        _maxSellableAmount = _itemBeingTrashed.Amount;
        Debug.Log($"ShowTrash: _itemBeingTrashed set to {(itemToTrash != null ? itemToTrash.ItemName : "NULL")}, _maxSellableAmount set to {_maxSellableAmount}"); // Log Güncellendi

        if (itemIconDisplay != null)
        {
            if (itemToTrash.ItemIcon != null)
            {
                itemIconDisplay.sprite = itemToTrash.ItemIcon;
                itemIconDisplay.gameObject.SetActive(true);
            }
            else
            {
                itemIconDisplay.gameObject.SetActive(false);
            }
        }

        if (_itemBeingTrashed.IsStackable && _itemBeingTrashed.Amount > 1)
        {
            if (quantityInputField != null)
            {
                quantityInputField.gameObject.SetActive(true);
                 // Listener'ı değiştirmeden önce text'i set et.
                quantityInputField.text = ""; // Önce temizle
                quantityInputField.text = "1"; // Sonra "1" yap (bu onValueChanged'ı tetiklemeli)
                Debug.Log("ShowTrash: quantityInputField text set to 1");
            }
            // UpdateConfirmationForQuantity("1"); // Bu satır, inputField.text atamasıyla tetiklenmeli
        }
        else
        {
            if (quantityInputField != null) quantityInputField.gameObject.SetActive(false);
            
            int coinAmount = Mathf.Max(1, (int)(_itemBeingTrashed.ItemValue * 0.05f)); // Değerin %5'i
            string formattedCoinString = CurrencyUtils.FormatCopperValue(coinAmount); // Tek item için formatla
            string colorizedCoinString = MessageColorUtils.ColorizeCurrency(formattedCoinString);
            string colorizedItemName = MessageColorUtils.ColorizeItemName(_itemBeingTrashed.ItemName);

            if (confirmationMessageText != null)
            {
                confirmationMessageText.text = $"{colorizedItemName} (x1) eşyasını {colorizedCoinString} karşılığında satmak istediğinize emin misiniz?";
            }
        }

        if (confirmationPanel != null) confirmationPanel.SetActive(true);
        else Debug.LogError("TrashManager: Confirmation Panel atanmamış!");
        Debug.Log("---- ShowTrashConfirmationPanel END ----");
    }

    private void UpdateConfirmationForQuantity(string newQuantityStr)
    {
        Debug.Log("---- UpdateConfirmationForQuantity START ----"); 
        Debug.Log($"newQuantityStr: {newQuantityStr}, _itemBeingTrashed: {(_itemBeingTrashed != null ? _itemBeingTrashed.ItemName : "NULL")}, _maxSellableAmount: {_maxSellableAmount}");

        if (_itemBeingTrashed == null) 
        {
            Debug.LogError("UpdateConfirmationForQuantity: _itemBeingTrashed is NULL!"); 
            return;
        }

        int selectedQuantity = 1;
        if (string.IsNullOrEmpty(newQuantityStr)) // Eğer string boşsa, 0 olarak kabul etme, 1 yap
        {
             Debug.LogWarning("UpdateConfirmationForQuantity: newQuantityStr is empty, defaulting selectedQuantity to 1.");
             // selectedQuantity zaten 1, input field'ı da 1'e çekebiliriz.
             if (quantityInputField != null && quantityInputField.text != "1") {
                // InputField'ı güncellerken listener döngüsünü önlemek için dikkatli ol
                quantityInputField.onValueChanged.RemoveListener(UpdateConfirmationForQuantity);
                quantityInputField.text = "1";
                quantityInputField.onValueChanged.AddListener(UpdateConfirmationForQuantity);
             }
        }
        else if (int.TryParse(newQuantityStr, out int parsedQuantity))
        {
            selectedQuantity = Mathf.Clamp(parsedQuantity, 1, _maxSellableAmount);
        }
        else // Parse başarısız olursa (örn: harf girilirse), 1'e dön
        {
            Debug.LogWarning($"UpdateConfirmationForQuantity: Could not parse '{newQuantityStr}', defaulting to 1.");
            selectedQuantity = 1;
             if (quantityInputField != null && quantityInputField.text != "1") {
                quantityInputField.onValueChanged.RemoveListener(UpdateConfirmationForQuantity);
                quantityInputField.text = "1";
                quantityInputField.onValueChanged.AddListener(UpdateConfirmationForQuantity);
             }
        }
        
        // Kullanıcı manuel olarak 0 veya boş girerse veya parse edilemeyen bir şey girerse diye
        // inputField'in metnini `selectedQuantity`'ye senkronize et (eğer farklıysa)
        if (quantityInputField != null && quantityInputField.text != selectedQuantity.ToString())
        {
            Debug.Log($"UpdateConfirmation: Syncing inputField.text from '{quantityInputField.text}' to '{selectedQuantity}'");
            quantityInputField.onValueChanged.RemoveListener(UpdateConfirmationForQuantity);
            quantityInputField.text = selectedQuantity.ToString();
            quantityInputField.onValueChanged.AddListener(UpdateConfirmationForQuantity);
        }

        int coinAmountPerItem = Mathf.Max(1, (int)(_itemBeingTrashed.ItemValue * 0.05f)); // Değerin %5'i
        int totalCopperValue = coinAmountPerItem * selectedQuantity;
        
        // Para dönüşümünü hesapla ve metni oluştur
        string formattedCoinString = CurrencyUtils.FormatCopperValue(totalCopperValue);
        string colorizedCoinString = MessageColorUtils.ColorizeCurrency(formattedCoinString);
        string colorizedItemName = MessageColorUtils.ColorizeItemName(_itemBeingTrashed.ItemName);

        if (confirmationMessageText != null)
        {
            confirmationMessageText.text = $"{colorizedItemName} (x{selectedQuantity}) eşyasını {colorizedCoinString} karşılığında satmak istediğinize emin misiniz?";
            Debug.Log($"Updated confirmationMessageText: {confirmationMessageText.text}"); 
        }
        Debug.Log("---- UpdateConfirmationForQuantity END ----"); 
    }

    private void HandleConfirmTrash()
    {
        if (_sourceSlotForTrash == null || _itemBeingTrashed == null)
        {
            Debug.LogError("HandleConfirmTrash: Kaynak slot veya item bilgisi kaybolmuş.");
            HidePanel();
            return;
        }

        if (_sourceSlotForTrash.CurrentItem != _itemBeingTrashed && !(_sourceSlotForTrash.CurrentItem?.ItemId == _itemBeingTrashed.ItemId && _sourceSlotForTrash.CurrentItem?.Amount >= _itemBeingTrashed.Amount) )
        {
            if (_sourceSlotForTrash.CurrentItem == null || _sourceSlotForTrash.CurrentItem.ItemId != _itemBeingTrashed.ItemId) {
                 Debug.LogWarning("Satılmak istenen item artık kaynak slotta değil veya değişmiş. İşlem iptal edildi.");
                 UIFeedbackManager.Instance?.ShowTooltip("Eşya artık kaynak slotta değil!");
                 HidePanel();
                 return;
            }
            _maxSellableAmount = _sourceSlotForTrash.CurrentItem.Amount;
        }

        ItemData copperCoinData = ItemDatabase.Instance?.GetItemById(CurrencyUtils.COPPER_COIN_ID);
        ItemData silverCoinData = ItemDatabase.Instance?.GetItemById(CurrencyUtils.SILVER_COIN_ID);
        ItemData goldCoinData = ItemDatabase.Instance?.GetItemById(CurrencyUtils.GOLD_COIN_ID);

        if (copperCoinData == null || silverCoinData == null || goldCoinData == null)
        {
            Debug.LogError($"TrashManager: Para birimi ItemData (Bakır, Gümüş veya Altın) bulunamadı! Satış yapılamıyor.");
            UIFeedbackManager.Instance?.ShowTooltip("Para birimi ayarları eksik!");
            HidePanel();
            return;
        }

        int quantityToSell = 1;
        if (quantityInputField != null && quantityInputField.gameObject.activeSelf && int.TryParse(quantityInputField.text, out int parsedQuantity))
        {
            quantityToSell = Mathf.Clamp(parsedQuantity, 1, _maxSellableAmount); 
        }
        if (!(_itemBeingTrashed.IsStackable && _itemBeingTrashed.Amount > 1)) {
            quantityToSell = 1;
        }
        if (_sourceSlotForTrash.CurrentItem != null) { 
             quantityToSell = Mathf.Min(quantityToSell, _sourceSlotForTrash.CurrentItem.Amount);
        } else { 
            Debug.LogError("HandleConfirmTrash: Kaynak slottaki item null oldu. Satış iptal.");
            HidePanel();
            return;
        }
        if (quantityToSell <= 0) { 
            Debug.LogWarning("HandleConfirmTrash: Satılacak miktar 0 veya daha az. Satış yapılmadı.");
            HidePanel();
            return;
        }

        int coinAmountPerItem = Mathf.Max(1, (int)(_itemBeingTrashed.ItemValue * 0.05f)); // Değerin %5'i
        int totalCopperValue = coinAmountPerItem * quantityToSell;

        bool removedSuccessfully = false;
        InventoryItem itemInSourceSlot = _sourceSlotForTrash.CurrentItem; 

        // Dinamik oranları al (HandleConfirmTrash için)
        ItemData copperItemInfo = ItemDatabase.Instance?.GetItemById(CurrencyUtils.COPPER_COIN_ID);
        ItemData silverItemInfo = ItemDatabase.Instance?.GetItemById(CurrencyUtils.SILVER_COIN_ID);

        int actualCopperPerSilver = (copperItemInfo != null && copperItemInfo.MaxStackSize > 1)
                                     ? copperItemInfo.MaxStackSize 
                                     : 99;
        int actualSilverPerGold = (silverItemInfo != null && silverItemInfo.MaxStackSize > 1)
                                   ? silverItemInfo.MaxStackSize
                                   : 99;

        if (itemInSourceSlot == null || itemInSourceSlot.ItemId != _itemBeingTrashed.ItemId) {
             Debug.LogError("Satılmak istenen item slotta bulunamadı veya değişti. İşlem iptal.");
             HidePanel();
             return;
        }

        if (_sourceSlotForTrash.IsDesignatedEquipmentSlot())
        {
            Debug.Log($"Ekipman slotundan ({_sourceSlotForTrash.SlotType}) {itemInSourceSlot.ItemName} (x{quantityToSell}) kaldırılıyor.");
            if (itemInSourceSlot.IsStackable && itemInSourceSlot.Amount > quantityToSell)
            {
                itemInSourceSlot.Amount -= quantityToSell;
                EquipmentManager.Instance?.GetEquipmentSlotUI(_sourceSlotForTrash.SlotType)?.UpdateUI(itemInSourceSlot);
                EquipmentManager.Instance?.SaveEquipmentManual(); 
                removedSuccessfully = true;
            }
            else
            {
                EquipmentManager.Instance?.RemoveEquipment(_sourceSlotForTrash.SlotType, true);
                removedSuccessfully = true; 
            }
        }
        else 
        {
            Debug.Log($"Envanter slotundan (Index: {_sourceSlotForTrash.SlotIndex}) {itemInSourceSlot.ItemName} (x{quantityToSell}) kaldırılıyor.");
            if (itemInSourceSlot.IsStackable && itemInSourceSlot.Amount > quantityToSell)
            {
                itemInSourceSlot.Amount -= quantityToSell;
                _sourceSlotForTrash.UpdateUI(itemInSourceSlot); 
                InventoryManager.Instance?.ManualSave(); 
                removedSuccessfully = true;
            }
            else
            {
                InventoryItem removedItem = InventoryManager.Instance?.RemoveItem(_sourceSlotForTrash.SlotIndex, true);
                removedSuccessfully = (removedItem != null && removedItem.ItemId == _itemBeingTrashed.ItemId);
            }
        }

        if (!removedSuccessfully)
        {
            Debug.LogError("Item kaynaktan kaldırılamadı! Satış iptal.");
            HidePanel();
            return;
        }

        // Paraları Ekle - YENİ YÖNTEM
        if (InventoryManager.Instance.TryAddCurrency(totalCopperValue))
        {
            string formattedEarnedAmount = CurrencyUtils.FormatCopperValue(totalCopperValue);
            string colorizedEarnedAmount = MessageColorUtils.ColorizeCurrency(formattedEarnedAmount);
            string colorizedItemName = MessageColorUtils.ColorizeItemName(_itemBeingTrashed.ItemName);
            UIFeedbackManager.Instance?.ShowTooltip($"{colorizedItemName} (x{quantityToSell}) satıldı, +{colorizedEarnedAmount} kazanıldı.");
        }
        else
        {
            // Para eklenemezse (örn: envanterde hiç yer yoksa gibi ekstrem bir durum)
            string colorizedItemName = MessageColorUtils.ColorizeItemName(_itemBeingTrashed.ItemName);
            UIFeedbackManager.Instance?.ShowTooltip($"{colorizedItemName} (x{quantityToSell}) satıldı, ancak para envantere eklenemedi (yer yok?)!");
            // Item geri iade edilmeli mi? Bu senaryo TryAddCurrency içinde ele alınmaya çalışılıyor ama garanti değil.
            // Şimdilik sadece bir log mesajı bırakıyoruz.
            Debug.LogError($"Item satıldıktan sonra para ({totalCopperValue} bakır) envantere eklenemedi. Item: {_itemBeingTrashed.ItemName}");
        }

        HidePanel();
    }

    private void HandleCancelTrash()
    {
        HidePanel();
    }

    private void HidePanel()
    {
        Debug.Log("---- HidePanel START ----"); // Yeni Log
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        
        if (quantityInputField != null)
        {
            // Listener'ı Awake'de eklediğimiz için burada kaldırmamıza gerek yok,
            // InputField etkisizleşince zaten çalışmaz.
            // Ancak emin olmak için kaldırılabilir veya AddListener'dan önce RemoveAllListeners denebilir.
            // quantityInputField.onValueChanged.RemoveListener(UpdateConfirmationForQuantity); 
            quantityInputField.text = ""; 
            quantityInputField.gameObject.SetActive(false);
            Debug.Log("HidePanel: quantityInputField reset and deactivated.");
        }

        _sourceSlotForTrash = null;
        _itemBeingTrashed = null;
        _maxSellableAmount = 0;
        Debug.Log("HidePanel: References cleared.");
        Debug.Log("---- HidePanel END ----"); // Yeni Log
    }

    private void OnDestroy()
    {
        if (quantityInputField != null)
        {
             quantityInputField.onValueChanged.RemoveAllListeners(); // En temizi OnDestroy'da tümünü kaldırmak
             Debug.Log("TrashManager OnDestroy: All listeners removed from quantityInputField.");
        }
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();

        if (Instance == this) Instance = null;
    }
} 