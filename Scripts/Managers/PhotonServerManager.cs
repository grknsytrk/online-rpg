using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Realtime;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using ExitGames.Client.Photon;

public class PhotonServerManager : MonoBehaviourPunCallbacks
{
    private static PhotonServerManager instance;
    public static PhotonServerManager Instance => instance;

    public event System.Action<string> OnConnectionError;

    [SerializeField] private int gameSceneIndex = 1;
    private bool isConnectingFromMenu = false;
    private bool isSceneLoading = false;

    [Header("Network Settings")]
    [SerializeField] private int disconnectTimeout = 500000; 
    [SerializeField] private bool keepAliveInBackground = true;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);

        
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = disconnectTimeout;
        PhotonNetwork.KeepAliveInBackground = (float)(keepAliveInBackground ? 3000 : 0); 
        Application.runInBackground = true;
        
        // Güvenli bir başlangıç için mesaj kuyruğu ayarları
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.ReuseEventInstance = true;
        
        // Bağlantı problemlerini daha iyi izlemek için log ayarları
        PhotonNetwork.LogLevel = PunLogLevel.Informational;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Başlangıçta mesaj kuyruğunu açık tutmaya çalış
        PhotonNetwork.IsMessageQueueRunning = true;
        Debug.Log("PhotonServerManager started, message queue initially set to running.");
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void StartGameFromMenu()
    {
        isConnectingFromMenu = true;

        // Firebase'den gelen email'i kullan
        if (FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsLoggedIn)
        {
            string email = FirebaseAuthManager.Instance.CurrentUser.Email;
            string nickname = email.Split('@')[0];
            PhotonNetwork.NickName = nickname;
            Debug.Log($"Connecting to server as {PhotonNetwork.NickName}...");
        }
        else
        {
            Debug.LogError("Firebase authentication required!");
            return;
        }
        
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to server!");
        if (isConnectingFromMenu)
        {
            JoinMainWorld();
        }
    }

    private void JoinMainWorld()
    {
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 20,
            IsVisible = true,
            IsOpen = true,
            CleanupCacheOnLeave = true
        };

        PhotonNetwork.JoinOrCreateRoom("World", roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined the World!");
        StartCoroutine(DebugPlayersCoroutine());

        if (isConnectingFromMenu && !isSceneLoading)
        {
            isSceneLoading = true;
            StartCoroutine(LoadGameSceneCoroutine());
        }

       
        StartCoroutine(SendJoinMessage());
    }

    private IEnumerator SendJoinMessage()
    {
        yield return new WaitForSeconds(1f);
        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            string joinMessage = MessageColorUtils.BuildPlayerJoinMessage(PhotonNetwork.NickName);
            chatManager.SendSystemMessage(joinMessage, SystemMessageType.PlayerJoin);
        }
    }

    private IEnumerator LoadGameSceneCoroutine()
    {
        // Yükleme sırasında mesaj kuyruğunu devre dışı bırak
        Debug.Log("Setting message queue to false for scene loading...");
        PhotonNetwork.IsMessageQueueRunning = false;

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(gameSceneIndex);
        }

        while (SceneManager.GetActiveScene().buildIndex != gameSceneIndex)
        {
            yield return null;
        }

        // Yükleme tamamlandıktan sonra güvenli bir şekilde bekle
        yield return new WaitForSeconds(1.0f);

        // Sahne tamamen yüklendikten sonra mesaj kuyruğunu tekrar aktif et
        Debug.Log("Scene loaded, setting message queue back to true...");
        PhotonNetwork.IsMessageQueueRunning = true;
        
        // Mesaj kuyruğunun işlenmesi için biraz daha bekle
        yield return new WaitForSeconds(0.5f);
        
        isSceneLoading = false;

        SpawnPlayer();
    }

    private IEnumerator DebugPlayersCoroutine()
    {
        while (PhotonNetwork.InRoom)
        {
            //Debug.Log("=== Odadaki Oyuncular ===");
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                //Debug.Log($"Oyuncu: {player.NickName} (ID: {player.ActorNumber})");
            }
            //Debug.Log("========================");
            
            yield return new WaitForSeconds(5f);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isSceneLoading && scene.buildIndex == gameSceneIndex)
        {
            StartCoroutine(OnSceneLoadedDelayedSpawn());
        }
    }

    private IEnumerator OnSceneLoadedDelayedSpawn()
    {
        // Sahnenin tam olarak yüklenmesi için daha uzun bekle
        yield return new WaitForSeconds(1.5f);
        
        // Photon'un tamamen hazır olması için ek kontrol
        if (PhotonNetwork.IsConnected)
        {
            // Mesaj kuyruğunu aktif et
            PhotonNetwork.IsMessageQueueRunning = true;
            yield return new WaitForSeconds(0.5f);
            
            SpawnPlayer();
        }
        else
        {
            Debug.LogWarning("Photon bağlantısı yok, yeniden deneniyor...");
            yield return new WaitForSeconds(2.0f);
            // Bir daha dene ve mesaj kuyruğunu direkt aktif et
            PhotonNetwork.IsMessageQueueRunning = true;
            yield return new WaitForSeconds(0.5f);
            SpawnPlayer();
        }
    }

    private void SpawnPlayer()
    {
        // Photon bağlantısını ve mesaj kuyruğunu kontrol et
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("Photon bağlantısı yok, oyuncu oluşturulamıyor! Yeniden bağlanma deneniyor...");
            
            // Yeniden bağlanma denemesi
            if (!PhotonNetwork.Reconnect())
            {
                Debug.LogError("Yeniden bağlanma başarısız! Photon'a yeniden bağlanmayı deneyin.");
                
                // Kullanıcıya bir uyarı göstermek için burada bir UI gösterme kodu eklenebilir
                return;
            }
            
            // Yeniden bağlantıyı bekle
            StartCoroutine(RetrySpawnAfterReconnect());
            return;
        }

        // Oyuncu bir odada mı kontrol et
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("Oyuncu bir odada değil, odaya katılmayı deneyin!");
            StartCoroutine(RetryJoinRoomAndSpawn());
            return;
        }

        // Mesaj kuyruğu hazır mı kontrol et
        if (!PhotonNetwork.IsMessageQueueRunning)
        {
            Debug.LogWarning("Message queue is not running, delaying player spawn...");
            // Mesaj kuyruğu hazır olana kadar yeniden deneme
            StartCoroutine(RetrySpawnWhenReady());
            return;
        }

        Debug.Log("SpawnPlayer called");

        PlayerController[] existingPlayers = FindObjectsOfType<PlayerController>();
        foreach (var existingPlayer in existingPlayers)
        {
            if (existingPlayer.photonView != null && existingPlayer.photonView.IsMine)
            {
                Debug.Log("Player already exists, won't create a new one.");
                return;
            }
        }

        Vector3 randomPosition = new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));

        Debug.Log($"Creating player... Position: {randomPosition}");
        try 
        {
            var playerInstance = PhotonNetwork.Instantiate("Player", randomPosition, Quaternion.identity);
            Debug.Log($"Player instantiated successfully: {playerInstance.name}");
            
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetPlayerCameraFollow();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error spawning player: {e.Message}\n{e.StackTrace}");
        }
    }
    
    // Yeniden bağlantı sonrası spawn denemesi
    private IEnumerator RetrySpawnAfterReconnect()
    {
        int attempts = 0;
        int maxAttempts = 5;
        
        while (!PhotonNetwork.IsConnected && attempts < maxAttempts)
        {
            Debug.Log($"Reconnecting attempt {attempts+1}/{maxAttempts}...");
            yield return new WaitForSeconds(1.0f);
            attempts++;
        }
        
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Reconnected successfully!");
            
            // Odaya yeniden katılmayı dene
            if (!PhotonNetwork.InRoom)
            {
                StartCoroutine(RetryJoinRoomAndSpawn());
            }
            else
            {
                // Mesaj kuyruğunun hazır olması için bekle
                PhotonNetwork.IsMessageQueueRunning = true;
                yield return new WaitForSeconds(0.5f);
                SpawnPlayer();
            }
        }
        else
        {
            Debug.LogError("Failed to reconnect to Photon network!");
        }
    }
    
    // Odaya yeniden katılma ve spawn etme
    private IEnumerator RetryJoinRoomAndSpawn()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("Cannot join room - not connected to Photon!");
            yield break;
        }
        
        Debug.Log("Attempting to join room...");
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 20,
            IsVisible = true,
            IsOpen = true,
            CleanupCacheOnLeave = true
        };
        
        // Önce lobiye katıl
        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
            Debug.Log("Joining lobby...");
            yield return new WaitForSeconds(1.0f);
        }
        
        // Odaya katıl veya oluştur
        PhotonNetwork.JoinOrCreateRoom("World", roomOptions, TypedLobby.Default);
        Debug.Log("Joining or creating room 'World'...");
        
        // Odaya katılana kadar bekle
        float waitStartTime = Time.time;
        float maxWaitTime = 10.0f;  // en fazla 10 saniye bekle
        
        while (!PhotonNetwork.InRoom && (Time.time - waitStartTime) < maxWaitTime)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        if (PhotonNetwork.InRoom)
        {
            Debug.Log("Successfully joined room!");
            // Mesaj kuyruğunu aktifleştir ve bekle
            PhotonNetwork.IsMessageQueueRunning = true;
            yield return new WaitForSeconds(0.5f);
            SpawnPlayer();
        }
        else
        {
            Debug.LogError("Failed to join room within time limit!");
        }
    }

    // Mesaj kuyruğu hazır olana kadar bekleme ve yeniden deneme
    private IEnumerator RetrySpawnWhenReady()
    {
        int maxAttempts = 15;  // Maksimum deneme sayısı artırıldı
        int attempts = 0;
        float waitTime = 0.3f; // İlk bekleme süresi azaltıldı
        
        Debug.Log("RetrySpawnWhenReady started - forcing message queue to run");
        
        // İlk olarak mesaj kuyruğunu çalıştırmayı dene
        PhotonNetwork.IsMessageQueueRunning = true;
        yield return new WaitForSeconds(0.2f);
        
        // Eğer şimdi mesaj kuyruğu çalışıyorsa direkt spawn et
        if (PhotonNetwork.IsMessageQueueRunning)
        {
            Debug.Log("Message queue is now running after forcing it, spawning player...");
            // Spawn işleminden önce biraz daha bekle
            yield return new WaitForSeconds(0.3f);
            SpawnPlayer();
            yield break;
        }
        
        while (!PhotonNetwork.IsMessageQueueRunning && attempts < maxAttempts)
        {
            Debug.Log($"Waiting for Photon message queue... Attempt {attempts+1}/{maxAttempts}");
            
            // Her yeni denemede mesaj kuyruğunu aktif etmeyi dene
            PhotonNetwork.IsMessageQueueRunning = true;
            
            yield return new WaitForSeconds(waitTime);
            attempts++;
            waitTime *= 1.2f; // Her denemede bekleme süresini arttır
            
            // Her 3 denemede bir oda durumunu kontrol et
            if (attempts % 3 == 0 && !PhotonNetwork.InRoom)
            {
                Debug.LogWarning("Player is not in room, trying to rejoin...");
                // Odada değilsek tekrar bağlanmayı dene
                if (!PhotonNetwork.InLobby && PhotonNetwork.IsConnected)
                {
                    PhotonNetwork.JoinLobby();
                    yield return new WaitForSeconds(1.0f);
                    
                    // Lobby'ye girdikten sonra ana odaya gir
                    RoomOptions roomOptions = new RoomOptions
                    {
                        MaxPlayers = 20, 
                        IsVisible = true,
                        IsOpen = true
                    };
                    PhotonNetwork.JoinOrCreateRoom("World", roomOptions, TypedLobby.Default);
                    yield return new WaitForSeconds(1.0f);
                }
            }
        }
        
        if (PhotonNetwork.IsMessageQueueRunning)
        {
            Debug.Log("Message queue is now running, spawning player...");
            // Spawn işleminden önce biraz daha bekle
            yield return new WaitForSeconds(0.5f);
            SpawnPlayer(); // Güvenli şekilde spawner metodu tekrar çağır
        }
        else
        {
            Debug.LogError("Failed to spawn player: Photon message queue did not become available after multiple attempts.");
            
            // Son çare: Message queue'yi zorla aktifleştir
            PhotonNetwork.IsMessageQueueRunning = true;
            Debug.Log("Forcing message queue to run as last resort");
            
            // Tam emin olmak için biraz daha bekle
            yield return new WaitForSeconds(1.0f);
            
            // Yine dene (son deneme)
            if (PhotonNetwork.InRoom)
            {
                SpawnPlayer();
            }
            else
            {
                Debug.LogError("Cannot spawn player: Not in room!");
            }
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float x = Random.Range(-5f, 5f);
        float z = Random.Range(-5f, 5f);
        return new Vector3(x, 0, z);
    }

    private void TriggerError(string message)
    {
        OnConnectionError?.Invoke(message);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        string playerName = newPlayer?.NickName ?? "Yeni Oyuncu";
        Debug.Log($"Player joined: {playerName}");
        
        // Oyuncu odaya katılma sistem mesajı
        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            string joinMessage = MessageColorUtils.BuildPlayerJoinMessage(playerName);
            chatManager.SendSystemMessage(joinMessage, SystemMessageType.PlayerJoin);
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        string playerName = otherPlayer?.NickName ?? otherPlayer?.ActorNumber.ToString() ?? "Unknown";
        Debug.Log($"Player left room: {playerName}");

        if (!PhotonNetwork.IsMasterClient) return;

        StartCoroutine(CleanupPlayerObjects(otherPlayer));
    }

    private IEnumerator CleanupPlayerObjects(Player player)
    {
        yield return new WaitForSeconds(0.2f);

        PhotonView[] views = FindObjectsOfType<PhotonView>();
        foreach (var view in views)
        {
            if (view == null) continue;

            bool isPlayersObject = view.Owner == player || 
                                 (view.Owner == null && view.CreatorActorNr == player.ActorNumber);

            if (isPlayersObject)
            {
                Debug.Log($"Cleaning up: {view.gameObject.name}");
                PhotonNetwork.Destroy(view.gameObject);
            }
        }

        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            string playerName = player?.NickName ?? "Bir oyuncu";
            string leaveMessage = MessageColorUtils.BuildPlayerLeaveMessage(playerName);
            chatManager.SendSystemMessage(leaveMessage, SystemMessageType.PlayerLeave);
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        string masterName = newMasterClient?.NickName ?? newMasterClient?.ActorNumber.ToString() ?? "Unknown";
        Debug.Log($"Master Client switched. New Master: {masterName}");

        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(CleanupOrphanedObjects());

            ChatManager chatManager = FindObjectOfType<ChatManager>();
            if (chatManager != null)
            {
                string masterMessage = MessageColorUtils.BuildMasterChangeMessage(masterName);
                chatManager.SendSystemMessage(masterMessage, SystemMessageType.MasterChange);
            }
        }
    }

    private IEnumerator CleanupOrphanedObjects()
    {
        yield return new WaitForSeconds(0.5f);

        PhotonView[] views = FindObjectsOfType<PhotonView>();
        foreach (var view in views)
        {
            if (view == null) continue;

            if (view.Owner == null && !view.IsRoomView)  
            {
                Debug.Log($"Cleaning up orphaned object: {view.gameObject.name}");
                PhotonNetwork.Destroy(view.gameObject);
            }
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to create room! Error: {message}");
        isSceneLoading = false;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room! Error: {message}");
        isSceneLoading = false;
    }

    public void DisconnectFromServer()
    {
        PhotonNetwork.Disconnect();
        Debug.Log("Disconnected from server.");
    }

    private void OnApplicationQuit()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
    }
}
