using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections.Generic; // List için eklendi

public class DropManager : MonoBehaviour
{
    public static DropManager Instance { get; private set; }

    [Header("Drop Confirmation Panel UI")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Image itemIconDisplay;
    [SerializeField] private TextMeshProUGUI confirmationMessageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_InputField quantityInputField;

    [Header("Loot Settings")]
    [Tooltip("Yere bırakılacak LootItem prefab'ının Resources klasörü içindeki tam yolu (örn: Prefabs/LootItem).")]
    [SerializeField] private string lootItemPrefabPath = "Items/Loots/LootItem"; // GameObject referansı yerine string path
    [Tooltip("Oyuncunun pozisyonuna göre itemin düşeceği yerel ofset.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0.5f, 0.5f, 0f); // Vector3 ofset


    private InventorySlotUI _sourceSlotForDrop;
    private InventoryItem _itemBeingDropped; // Referans olarak tutulacak, miktarı UI'dan alınacak
    private int _maxDroppableAmount;
    private PlayerController _localPlayerController;

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
            quantityInputField.onValueChanged.AddListener(ValidateAndUpdateConfirmationQuantity);
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmDrop);
        if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelDrop);
    }

    private PlayerController GetLocalPlayer()
    {
        if (_localPlayerController != null && _localPlayerController.PV != null && _localPlayerController.PV.IsMine) return _localPlayerController;

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in players)
        {
            if (player.PV != null && player.PV.IsMine)
            {
                _localPlayerController = player;
                return player;
            }
        }
        Debug.LogError("DropManager: Yerel oyuncu bulunamadı!");
        return null;
    }

    public void ShowDropConfirmationPanel(InventorySlotUI sourceSlot, InventoryItem itemToDrop)
    {
        if (itemToDrop == null || sourceSlot == null)
        {
            Debug.LogError("ShowDropConfirmationPanel: Item veya kaynak slot null!");
            UIFeedbackManager.Instance?.ShowTooltip("Yere bırakılacak eşya bulunamadı.");
            return;
        }

        if (string.IsNullOrEmpty(lootItemPrefabPath))
        {
            Debug.LogError("DropManager: LootItem Prefab Path atanmamış!");
            UIFeedbackManager.Instance?.ShowTooltip("LootItem prefab yolu ayarlanmamış.");
            return;
        }

        _sourceSlotForDrop = sourceSlot;
        // _itemBeingDropped'i direkt atamak yerine, panel açıldığında güncel itemi slot üzerinden alacağız.
        // Bu, itemin slotta değişme ihtimaline karşı daha güvenli.
        _itemBeingDropped = itemToDrop; // Geçici olarak referansla
        _maxDroppableAmount = itemToDrop.Amount;


        if (itemIconDisplay != null)
        {
            if (itemToDrop.ItemIcon != null)
            {
                itemIconDisplay.sprite = itemToDrop.ItemIcon;
                itemIconDisplay.gameObject.SetActive(true);
            }
            else
            {
                itemIconDisplay.gameObject.SetActive(false);
            }
        }

        bool isStackableAndMultiple = itemToDrop.IsStackable && itemToDrop.Amount > 1;
        if (quantityInputField != null)
        {
            quantityInputField.gameObject.SetActive(isStackableAndMultiple);
            quantityInputField.text = "1"; // Her zaman 1 ile başla
        }
        
        UpdateConfirmationMessage(1); // Başlangıçta 1 adet için mesajı güncelle

        if (confirmationPanel != null) confirmationPanel.SetActive(true);
        else Debug.LogError("DropManager: Confirmation Panel atanmamış!");
    }

    private void ValidateAndUpdateConfirmationQuantity(string newQuantityStr)
    {
        if (_sourceSlotForDrop == null || _sourceSlotForDrop.CurrentItem == null) {
            // Eğer kaynak slottaki item değiştiyse veya null ise, paneli kapatabiliriz veya hata verebiliriz.
            // Şimdilik sadece loglayıp geçelim, HandleConfirmDrop'ta tekrar kontrol edilecek.
            Debug.LogWarning("ValidateAndUpdateConfirmationQuantity: Kaynak slot veya item null/değişmiş olabilir.");
            HidePanel();
            return;
        }
        // Güncel itemi ve max miktarı slot üzerinden alalım
        InventoryItem currentItemInSlot = _sourceSlotForDrop.CurrentItem;
        _itemBeingDropped = currentItemInSlot; // _itemBeingDropped'i güncel tut
        _maxDroppableAmount = currentItemInSlot.Amount;

        int selectedQuantity = 1;
        if (quantityInputField != null && quantityInputField.gameObject.activeSelf)
        {
            if (int.TryParse(newQuantityStr, out int parsedQuantity))
            {
                selectedQuantity = Mathf.Clamp(parsedQuantity, 1, _maxDroppableAmount);
            }
            else if (string.IsNullOrEmpty(newQuantityStr)) // Boş bırakılırsa 1 kabul et
            {
                 selectedQuantity = 1;
            }
            // Eğer parse edilemediyse (harf vs) de 1'de kalacak (çünkü başlangıç değeri 1)


            // Input field'ın metnini senkronize et (eğer farklıysa ve döngüye girmeyecekse)
            if (quantityInputField.text != selectedQuantity.ToString())
            {
                quantityInputField.onValueChanged.RemoveListener(ValidateAndUpdateConfirmationQuantity);
                quantityInputField.text = selectedQuantity.ToString();
                quantityInputField.onValueChanged.AddListener(ValidateAndUpdateConfirmationQuantity);
            }
        }
        UpdateConfirmationMessage(selectedQuantity);
    }
    
    private void UpdateConfirmationMessage(int quantity)
    {
        if (_itemBeingDropped == null && _sourceSlotForDrop != null) _itemBeingDropped = _sourceSlotForDrop.CurrentItem;
        if (_itemBeingDropped == null) return; // Hala null ise bir şey yapma

        if (confirmationMessageText != null)
        {
            confirmationMessageText.text = $"<color=#FFD700>{_itemBeingDropped.ItemName}</color> (x{quantity}) eşyasını yere bırakmak istediğinize emin misiniz?";
        }
    }


    private void HandleConfirmDrop()
    {
        if (_sourceSlotForDrop == null || _sourceSlotForDrop.CurrentItem == null)
        {
            UIFeedbackManager.Instance?.ShowTooltip("Eşya artık slotta değil veya geçersiz.");
            Debug.LogError("HandleConfirmDrop: Kaynak slot veya item bilgisi kaybolmuş/değişmiş.");
            HidePanel();
            return;
        }
        // En güncel itemi ve miktarı al
        InventoryItem itemToActuallyDrop = _sourceSlotForDrop.CurrentItem;
        _maxDroppableAmount = itemToActuallyDrop.Amount; // Onay anındaki max miktar

        PlayerController localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            UIFeedbackManager.Instance?.ShowTooltip("Eşya bırakılamadı: Oyuncu bulunamadı.");
            HidePanel();
            return;
        }

        int quantityToDrop = 1;
        if (quantityInputField != null && quantityInputField.gameObject.activeSelf)
        {
            if (int.TryParse(quantityInputField.text, out int parsedQuantity) && parsedQuantity > 0)
            {
                quantityToDrop = Mathf.Clamp(parsedQuantity, 1, _maxDroppableAmount);
            } else { // Parse edilemedi veya 0 girildi, 1'e ayarla
                quantityToDrop = 1;
            }
        }
         quantityToDrop = Mathf.Min(quantityToDrop, itemToActuallyDrop.Amount); // Son bir kontrol

        if (quantityToDrop <= 0)
        {
            UIFeedbackManager.Instance?.ShowTooltip("Bırakılacak miktar geçersiz.");
            HidePanel();
            return;
        }

        bool removedSuccessfully = false;
        string originalItemId = itemToActuallyDrop.ItemId; // RPC için item ID'yi sakla

        if (_sourceSlotForDrop.IsDesignatedEquipmentSlot())
        {
            // Ekipman slotlarından item düşürmek için önce envantere alınması gerekebilir.
            // Ya da direkt düşürme desteklenebilir. Şimdilik hata verelim.
            // UIFeedbackManager.Instance?.ShowTooltip("Ekipman slotundaki eşyayı doğrudan yere bırakamazsınız. Önce envanterinize alın.");
            // Debug.LogWarning("Ekipman slotundan item düşürme deneniyor. Bu özellik henüz tam desteklenmiyor.");
            
            // Eğer yine de düşürmek istenirse (örneğin tek item ise):
            if (itemToActuallyDrop.Amount == quantityToDrop) // Sadece tam yığınsa ve o miktarsa
            {
                EquipmentManager.Instance?.UnequipItem(_sourceSlotForDrop.SlotType, false); // Envantere eklemeden çıkar
                removedSuccessfully = true;
            }
            else if (itemToActuallyDrop.IsStackable && itemToActuallyDrop.Amount > quantityToDrop)
            {
                 // Normalde stackable ekipman olmaz ama potansiyel bir durum için
                itemToActuallyDrop.RemoveFromStack(quantityToDrop);
                _sourceSlotForDrop.UpdateUI(itemToActuallyDrop);
                EquipmentManager.Instance?.SaveEquipmentManual();
                removedSuccessfully = true;
            } else {
                 UIFeedbackManager.Instance?.ShowTooltip("Ekipman slotundaki eşyayı bu miktarda bırakamazsınız.");
                 HidePanel();
                 return;
            }
        }
        else // Normal envanter slotu
        {
            if (itemToActuallyDrop.IsStackable && itemToActuallyDrop.Amount > quantityToDrop)
            {
                itemToActuallyDrop.RemoveFromStack(quantityToDrop);
                _sourceSlotForDrop.UpdateUI(itemToActuallyDrop);
                InventoryManager.Instance?.SaveInventory();
                removedSuccessfully = true;
            }
            else // Son item veya stackable değil (ve miktar eşleşiyorsa)
            {
                InventoryItem removed = InventoryManager.Instance.RemoveItem(_sourceSlotForDrop.SlotIndex, true);
                removedSuccessfully = (removed != null && removed.ItemId == originalItemId);
            }
        }

        if (!removedSuccessfully)
        {
            UIFeedbackManager.Instance?.ShowTooltip("Eşya envanterden kaldırılamadı.");
            HidePanel();
            return;
        }

        // LootItem'ı oyuncunun önüne spawn et
        Transform playerTransform = localPlayer.transform;
        Vector3 spawnPosition = playerTransform.position + playerTransform.right * dropOffset.x + playerTransform.up * dropOffset.y + playerTransform.forward * dropOffset.z;


        if (PhotonNetwork.IsConnected)
        {
            // PhotonNetwork.Instantiate prefab'ın adını Resources klasöründen alır.
            // Eğer prefab doğrudan atanıyorsa ve Resources klasöründe DEĞİLSE, bu yöntem çalışmaz.
            // Bu durumda prefab'ın Resources klasöründe olduğundan emin olmalı veya farklı bir Instantiate yöntemi kullanmalısınız.
            // Şimdilik, prefab'ın Resources altında olduğunu varsayarak string adını kullanıyoruz.
            // Eğer prefab doğrudan atanıyorsa ve PhotonNetwork.Instantiate(gameObjectPrefab, ...) kullanmak istiyorsanız,
            // prefab'ın bir PhotonView bileşenine sahip olduğundan ve Proje Ayarları > Photon Unity Networking > PrefabPool içinde kayıtlı olduğundan emin olun.
            // VEYA en kolayı, prefab'ın bir string adıyla Resources klasöründe olmasıdır.
            
            // Eğer lootItemPrefab direkt GameObject ise ve Resources'ta değilse, PhotonNetwork.Instantiate için adını almamız gerekir.
            // Ancak en güvenli yol, prefab'ı Resources klasörüne koymak ve adını kullanmaktır.
            // Bu yüzden string tabanlı Instantiate'e geri dönüyoruz ama prefab'ın varlığını kontrol ediyoruz.
            string prefabNameToUse = lootItemPrefabPath; // Eğer prefab Resources'daysa adı yeterli

            GameObject lootObj = PhotonNetwork.Instantiate(prefabNameToUse, spawnPosition, Quaternion.identity);
            if (lootObj != null)
            {
                PhotonView pv = lootObj.GetComponent<PhotonView>();
                if (pv != null)
                {
                    pv.RPC("RPC_InitializeLoot", RpcTarget.AllBuffered, originalItemId, quantityToDrop);
                    UIFeedbackManager.Instance?.ShowTooltip($"{itemToActuallyDrop.ItemName} (x{quantityToDrop}) yere bırakıldı.");
                }
                else Debug.LogError("LootItem prefab'ında PhotonView bulunamadı!");
            }
            else Debug.LogError($"PhotonNetwork.Instantiate ile {prefabNameToUse} oluşturulamadı. Prefab Resources klasöründe mi ve yol doğru mu?");
        }
        else
        {
            Debug.LogWarning("Photon ağına bağlı değil. Item lokal olarak spawn ediliyor (test amaçlı).");
            GameObject lootPrefabRes = Resources.Load<GameObject>(lootItemPrefabPath);
            if (lootPrefabRes != null) {
                GameObject lootObjInstance = Instantiate(lootPrefabRes, spawnPosition, Quaternion.identity);
                LootItem lootItemComponent = lootObjInstance.GetComponent<LootItem>();
                if (lootItemComponent != null) {
                    // Lokal başlatma için LootItem'da bir public metot gerekebilir
                    // Örneğin: lootItemComponent.InitializeForLocal(originalItemId, quantityToDrop);
                    Debug.Log("LootItem için lokal başlatma metodu gerekiyor ve çağrılmalı.");
                } else {
                    Debug.LogError("Instantiate edilen LootItem prefab'ında LootItem scripti bulunamadı.");
                }
            }
            else Debug.LogWarning($"Resources.Load ile {lootItemPrefabPath} yüklenemedi.");
        }
        InventorySlotUI.DeselectCurrentSlot(); // Item bırakıldıktan sonra seçimi kaldır
        HidePanel();
    }

    private void HandleCancelDrop()
    {
        HidePanel();
    }

    private void HidePanel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (quantityInputField != null)
        {
            quantityInputField.text = "1";
            quantityInputField.gameObject.SetActive(false);
        }
        _sourceSlotForDrop = null;
        _itemBeingDropped = null;
    }

    private void OnDestroy()
    {
        if (quantityInputField != null) quantityInputField.onValueChanged.RemoveAllListeners();
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        if (Instance == this) Instance = null;
    }
} 