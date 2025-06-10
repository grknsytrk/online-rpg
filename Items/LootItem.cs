using UnityEngine;
using Photon.Pun;
using System.Collections; // Added for IEnumerator

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))] 
[RequireComponent(typeof(PhotonView))] // PhotonView gerekliliği eklendi
public class LootItem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D itemCollider; 
    private PhotonView pv; // PhotonView referansı

    [Header("Settings")]
    // [SerializeField] private float pickupDelay = 0.5f; // Kaldırıldı, ağ kontrolü var
    [SerializeField] private float hoverAmplitude = 0.1f; // How much the item bobs up and down
    [SerializeField] private float hoverSpeed = 2f; // How fast the item bobs

    private ItemData _itemData;
    // private bool _canPickup = false; // Kaldırıldı
    private bool isPickedUp = false; // Itemin zaten toplanıp toplanmadığını takip etmek için
    private float _initialY;
    private float _timer;
    private int _lootQuantity = 1; // Eklendi: Düşen itemin miktarı

    void Awake()
    {
        pv = GetComponent<PhotonView>(); // PhotonView'ı al

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (itemCollider == null) itemCollider = GetComponent<Collider2D>();

        if (itemCollider != null)
        {
            itemCollider.isTrigger = true; 
        }
        else
        {
            Debug.LogError("LootItem requires a Collider2D component!", this);
            enabled = false; 
            return;
        }
        
        _initialY = transform.position.y;
        _timer = 0f; 

        // StartCoroutine(EnablePickupAfterDelay()); // Kaldırıldı
    }

    void Update()
    {
        // Hover effect can remain local
        _timer += Time.deltaTime * hoverSpeed;
        float newY = _initialY + Mathf.Sin(_timer) * hoverAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    // Initialize yerine RPC kullanıyoruz
    // public void Initialize(ItemData data) { ... } // Eski fonksiyon kaldırıldı

    [PunRPC]
    private void RPC_InitializeLoot(string itemId, int quantity) // Miktar parametresi eklendi
    {
        _itemData = ItemDatabase.Instance?.GetItemById(itemId);
        _lootQuantity = Mathf.Max(1, quantity); // Miktarı sakla, en az 1 olmalı

        if (_itemData != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = _itemData.Icon; 
            Debug.Log($"LootItem RPC Initialized with: {_itemData.itemName} (x{_lootQuantity}) (ViewID: {pv.ViewID})");
        }
        else
        {
             Debug.LogError($"Failed to RPC initialize LootItem (ViewID: {pv.ViewID}). ItemData for ID '{itemId}' is null or SpriteRenderer is missing.", this);
             // If initialization fails, the master client might decide to destroy it, 
             // but let's not destroy it locally here to avoid issues.
        }
    }

    // private IEnumerator EnablePickupAfterDelay() { ... } // Kaldırıldı

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the item has already been requested for pickup
        if (isPickedUp || _itemData == null) return;

        PlayerController player = other.GetComponent<PlayerController>();
        
        // Check if it's the player and specifically the *local* player who owns the character
        if (player != null && player.PV != null && player.PV.IsMine)
        {
            // ================== ENVANTER DOLU KONTROLÜ ==================
            // Envanter dolu mu kontrol et - doluysa loot'u toplamaya çalışma!
            if (InventoryManager.Instance != null)
            {
                // Envanterde boş slot var mı kontrol et
                bool hasEmptySlot = false;
                for (int i = 0; i < InventoryManager.Instance.InventorySize; i++)
                {
                    var existingItem = InventoryManager.Instance.GetItemAt(i);
                    if (existingItem == null)
                    {
                        hasEmptySlot = true;
                        break;
                    }
                    // Aynı item türü ve yığınlanabilir mi kontrol et
                    else if (_itemData != null && existingItem.ItemId == _itemData.ItemId && 
                             existingItem.IsStackable && existingItem.Amount < existingItem.MaxStackSize)
                    {
                        hasEmptySlot = true; // Yığına eklenebilir
                        break;
                    }
                }

                if (!hasEmptySlot)
                {
                    // Envanter dolu - tooltip göster ve loot'u toplama
                    Debug.Log($"Envanter dolu! {_itemData.itemName} toplanamıyor.");
                    UIFeedbackManager.Instance?.ShowTooltip("Envanter dolu! Loot toplanamıyor.");
                    return; // Loot'u toplamaya çalışma
                }
            }
            // ================== ENVANTER KONTROLÜ SONU ==================

            Debug.Log($"Local player (ViewID: {player.PV.ViewID}) attempting to pick up loot: {_itemData.itemName} (ViewID: {pv.ViewID})");
            
            // Send an RPC to the Master Client requesting pickup
            // Gönderen oyuncunun ViewID'sini de yolluyoruz.
            pv.RPC("RPC_RequestPickup", RpcTarget.MasterClient, player.PV.ViewID);
        }
    }

    [PunRPC]
    private void RPC_RequestPickup(int requestingPlayerViewId, PhotonMessageInfo info)
    {
        // Bu RPC sadece Master Client üzerinde çalışmalı
        if (!PhotonNetwork.IsMasterClient) return;

        // Item zaten başka birisi tarafından alınmaya çalışıldıysa veya alınmışsa işlemi durdur
        if (isPickedUp)
        {
            Debug.LogWarning($"Loot pickup request for '{_itemData?.itemName}' (ViewID: {pv.ViewID}) denied. Already picked up.");
            return; 
        }

        // Item'i alınmış olarak işaretle (diğer istekleri reddetmek için)
        isPickedUp = true; 

        Debug.Log($"MasterClient: Processing pickup request for '{_itemData?.itemName}' (ViewID: {pv.ViewID}) from player (ViewID: {requestingPlayerViewId})");

        // İsteği yapan oyuncuyu bul
        PhotonView requestingPlayerPV = PhotonView.Find(requestingPlayerViewId);
        if (requestingPlayerPV != null)
        {
            // İsteği yapan oyuncuya itemi onaylama RPC'si gönder
            // ItemID ve MİKTARI gönder
            pv.RPC("RPC_ConfirmPickup", requestingPlayerPV.Owner, _itemData.ItemId, _lootQuantity); 
            Debug.Log($"MasterClient: Sent pickup confirmation for '{_itemData.itemName}' (x{_lootQuantity}) to player {requestingPlayerViewId}");
        }
        else
        {
            Debug.LogError($"MasterClient: Could not find PhotonView for requesting player {requestingPlayerViewId}. Cannot confirm pickup.");
        }

        // Item objesini ağ üzerinden yok et (bu işlem biraz gecikmeli olabilir)
        // Onay RPC'si gittikten sonra yok etmek daha mantıklı.
        pv.RPC("RPC_SelfDestroy", RpcTarget.AllBuffered); // Tüm istemcilere kendini yok etme RPC'si gönder
        Debug.Log($"MasterClient: Ordering destruction of loot item '{_itemData?.itemName}' (ViewID: {pv.ViewID})");
    }

    [PunRPC]
    private void RPC_ConfirmPickup(string itemId, int quantity) // Miktar parametresi eklendi
    {
        // Bu RPC sadece itemi almaya çalışan yerel client üzerinde işlem yapmalı
        // RPC hedeflemesi doğru yapıldığı için ekstra IsMine kontrolüne gerek yok,
        // ama güvenlik için eklenebilir: if (!pv.IsMine) return; (Eğer pv bu scriptin değil de oyuncunun olsaydı)

        Debug.Log($"Client: Received pickup confirmation for item ID: {itemId}, Quantity: {quantity}");

        ItemData confirmedItemData = ItemDatabase.Instance?.GetItemById(itemId);
        if (confirmedItemData == null)
        {
            Debug.LogError($"Client: Could not find ItemData for confirmed pickup ID: {itemId}");
            return;
        }

        // Item'i envantere ekle - doğru miktar ile
        InventoryItem inventoryItemInstance = new InventoryItem(confirmedItemData, quantity); // Gelen quantity kullanıldı
        bool addedSuccessfully = InventoryManager.Instance.AddItem(inventoryItemInstance);

        if (addedSuccessfully)
        {
            Debug.Log($"Client: Item '{confirmedItemData.itemName}' successfully added to inventory.");
            // UI Feedback veya ses efekti burada eklenebilir
            SFXManager.Instance?.PlaySound(SFXNames.LootPickup); // Play loot pickup sound using constant
        }
        else
        {
             Debug.Log($"Client: Failed to add confirmed item '{confirmedItemData.itemName}' to inventory (maybe full?).");
             // UIFeedbackManager.Instance?.ShowGeneralMessage("Envanter Dolu!", 1.5f);
        }
        
        // Obje zaten MasterClient tarafından ağ üzerinden yok ediliyor, burada ek bir şey yapmaya gerek yok.
    }

    // YENİ EKLENEN RPC METODU
    [PunRPC]
    private void RPC_SelfDestroy()
    {
        if (gameObject == null) return; // Zaten yok edilmiş veya edilmekte

        // Tüm istemciler itemi hemen etkileşim dışı bırakabilir
        // isPickedUp = true; // Bu zaten Master'da RPC_RequestPickup içinde set ediliyor.
        // if (itemCollider != null)
        // {
        // itemCollider.enabled = false;
        // }

        if (pv.IsMine) // Sadece itemin sahibi ağ üzerinden yok etme işlemini yapar
        {
            Debug.Log($"Owner (ViewID: {pv.ViewID}) is performing network destroy for loot item '{_itemData?.itemName}'");
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            // Sahibi olmayan istemciler, itemin sahibi tarafından yok edileceğini bilir.
            // İsteğe bağlı olarak, objeyi yerel olarak hemen devre dışı bırakabilirler.
            Debug.Log($"Non-owner client received RPC_SelfDestroy for loot item (ViewID: {pv.ViewID}). Owner will handle network destruction.");
            // if (spriteRenderer != null) spriteRenderer.enabled = false; // Görsel olarak hemen kaldırabilir
            // gameObject.SetActive(false); // Veya tamamen deaktif edilebilir
        }
    }
} 