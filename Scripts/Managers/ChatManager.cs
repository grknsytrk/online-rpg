using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public enum SystemMessageType
{
    General,        // Genel sistem mesajları (sarı)
    PvPKill,        // PvP öldürme mesajları (kırmızı)
    RareItem,       // Nadir eşya mesajları (mor/altın)
    LevelUp,        // Seviye atlama mesajları (yeşil)
    PlayerJoin,     // Oyuncu giriş mesajları (mavi)
    PlayerLeave,    // Oyuncu çıkış mesajları (gri)
    MasterChange    // Master client değişim mesajları (turuncu)
}

public class ChatManager : MonoBehaviourPunCallbacks
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private TextMeshProUGUI chatContent;
    [SerializeField] private ScrollRect scrollRect;
    
    [Header("Sistem Mesaj Renkleri")]
    [SerializeField] private Color generalMessageColor = Color.yellow;        // #FFFF00
    [SerializeField] private Color pvpKillMessageColor = new Color(1f, 0.2f, 0.2f);  // #FF3333 - Kırmızı
    [SerializeField] private Color rareItemMessageColor = new Color(0.8f, 0.4f, 1f); // #CC66FF - Mor
    [SerializeField] private Color levelUpMessageColor = new Color(0.3f, 1f, 0.3f);  // #4DFF4D - Yeşil
    [SerializeField] private Color playerJoinMessageColor = new Color(0.4f, 0.8f, 1f); // #66CCFF - Mavi
    [SerializeField] private Color playerLeaveMessageColor = new Color(0.7f, 0.7f, 0.7f); // #B3B3B3 - Gri
    [SerializeField] private Color masterChangeMessageColor = new Color(1f, 0.6f, 0.2f); // #FF9933 - Turuncu
    
    private readonly Queue<string> chatMessages = new Queue<string>();
    private const int MaxMessages = 50;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        if (chatInput != null)
        {
            chatInput.onSubmit.AddListener(OnInputSubmit);
            chatInput.onSelect.AddListener(OnInputFieldSelected);
            chatInput.onDeselect.AddListener(OnInputFieldDeselected);
        }
    }

    private void OnInputFieldSelected(string value)
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            if (player.photonView.IsMine)
            {
                player.SetChatting(true);
            }
        }
    }

    private void OnInputFieldDeselected(string value)
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            if (player.photonView.IsMine)
            {
                player.SetChatting(false);
            }
        }
    }

    private void OnInputSubmit(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        if (PhotonNetwork.IsConnected)
        {
            string playerName = PhotonNetwork.NickName;
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = "Player " + PhotonNetwork.LocalPlayer.ActorNumber;
            }
            
            photonView.RPC("ReceiveMessage", RpcTarget.All, playerName, message);
        }
        
        chatInput.text = "";
        chatInput.ActivateInputField();
    }

    [PunRPC]
    private void ReceiveMessage(string playerName, string message)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.Log($"ChatManager aktif edildi çünkü bir mesaj alındı: {playerName}: {message}");
        }
        
        string timestamp = System.DateTime.Now.ToString("[HH:mm:ss]");
        string formattedMessage = $"{timestamp} {playerName}: {message}";
        
        chatMessages.Enqueue(formattedMessage);
        if (chatMessages.Count > MaxMessages)
        {
            chatMessages.Dequeue();
        }

        UpdateChatDisplay();
    }

    [PunRPC]
    private void ReceiveSystemMessage(string message)
    {
        ReceiveSystemMessage(message, SystemMessageType.General);
    }

    [PunRPC]
    private void ReceiveSystemMessage(string message, SystemMessageType messageType)
    {
        // ChatManager aktif değilse etkinleştir
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.Log($"ChatManager sistem mesajı için aktif edildi: {message}");
        }
        
        string timestamp = System.DateTime.Now.ToString("[HH:mm:ss]");
        Color messageColor = GetColorForMessageType(messageType);
        string colorHex = ColorUtility.ToHtmlStringRGB(messageColor);
        string formattedMessage = $"{timestamp} <color=#{colorHex}>{message}</color>";
        
        chatMessages.Enqueue(formattedMessage);
        if (chatMessages.Count > MaxMessages)
        {
            chatMessages.Dequeue();
        }

        // GameObject aktifse güvenli bir şekilde güncelle
        if (gameObject.activeSelf)
        {
            UpdateChatDisplay();
        }
        else
        {
            Debug.LogWarning($"ChatManager: GameObject devre dışıyken sistem mesajı alındı, güncelleme atlandı: {message}");
        }
    }

    private Color GetColorForMessageType(SystemMessageType messageType)
    {
        switch (messageType)
        {
            case SystemMessageType.PvPKill:
                return pvpKillMessageColor;
            case SystemMessageType.RareItem:
                return rareItemMessageColor;
            case SystemMessageType.LevelUp:
                return levelUpMessageColor;
            case SystemMessageType.PlayerJoin:
                return playerJoinMessageColor;
            case SystemMessageType.PlayerLeave:
                return playerLeaveMessageColor;
            case SystemMessageType.MasterChange:
                return masterChangeMessageColor;
            case SystemMessageType.General:
            default:
                return generalMessageColor;
        }
    }

    private void UpdateChatDisplay()
    {
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning("ChatManager: GameObject aktif değilken UpdateChatDisplay çağrıldı!");
            return;
        }
        
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        chatContent.text = string.Join("\n", chatMessages);
        
        // Layout'un güncellenmesi için bir frame bekle
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)chatContent.transform);
        
        // Coroutine'i güvenli bir şekilde başlat
        if (gameObject.activeSelf)
        {
            try
            {
                StartCoroutine(ScrollToBottom());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ScrollToBottom coroutine başlatılırken hata: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ChatManager: GameObject devre dışı olduğundan ScrollToBottom coroutine'i başlatılamadı!");
            
            // Canvas'ı ve scroll'u hemen güncelle (coroutine olmadan)
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                scrollRect.normalizedPosition = new Vector2(0, 0);
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    private IEnumerator ScrollToBottom()
    {
        // UI'ın güncellenmesi için bekle
        yield return new WaitForEndOfFrame();
        
        // Canvas'ı güncelle
        Canvas.ForceUpdateCanvases();
        
        // Scroll'u en alta kaydır
        if (scrollRect != null)
        {
            scrollRect.normalizedPosition = new Vector2(0, 0);
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void SendSystemMessage(string message)
    {
        SendSystemMessage(message, SystemMessageType.General);
    }

    public void SendSystemMessage(string message, SystemMessageType messageType)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("ReceiveSystemMessage", RpcTarget.All, message, messageType);
        }
    }
}