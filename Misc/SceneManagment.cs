using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using Photon.Realtime;

public class SceneManagment : MonoBehaviourPunCallbacks
{
    private static SceneManagment instance;
    private string targetSceneName;
    private string pendingRoomName;
    private bool isWaitingForConnection = false;
    
    public static SceneManagment Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SceneManagment>();
                if (instance == null)
                {
                    Debug.LogError("Scene Management bulunamadı!");
                }
            }
            return instance;
        }
    }

    public string SceneTransitionName { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
            PhotonNetwork.AutomaticallySyncScene = false;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Sahne yüklendi: {scene.name}, fade kaldırılıyor...");
        if (UIFade.Instance != null)
        {
            UIFade.Instance.FadeToClear();
        }

        CleanupNetworkedObjects();
    }

    private void CleanupNetworkedObjects()
    {
        PhotonView[] photonViews = FindObjectsOfType<PhotonView>();
        foreach (PhotonView view in photonViews)
        {
            if (view.IsMine && !view.IsRoomView)
            {
                PhotonNetwork.Destroy(view.gameObject);
            }
        }
    }

    private bool IsSceneValid(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Sahne adı boş olamaz!");
            return false;
        }

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameFromBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameFromBuild == sceneName)
            {
                return true;
            }
        }

        Debug.LogError($"Sahne '{sceneName}' build settings'de bulunamadı!");
        return false;
    }

    public void SetTransitionName(string sceneTransitionName)
    {
        this.SceneTransitionName = sceneTransitionName;
    }

    public void TransitionToNewRoom(string sceneName, string roomName)
    {
        if (!IsSceneValid(sceneName))
        {
            if (UIFade.Instance != null) UIFade.Instance.FadeToClear();
            return;
        }

        PhotonNetwork.RemoveRPCs(PhotonNetwork.LocalPlayer);
        
        targetSceneName = sceneName;
        pendingRoomName = roomName;
        Debug.Log($"Hedef sahne ayarlandı: {targetSceneName}");

        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Photon'a bağlanılıyor...");
            isWaitingForConnection = true;
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.DestroyPlayerObjects(PhotonNetwork.LocalPlayer);
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            CreateOrJoinRoom();
        }
    }

    private void CreateOrJoinRoom()
    {
        if (string.IsNullOrEmpty(pendingRoomName))
        {
            Debug.LogError("Oda adı boş olamaz!");
            if (UIFade.Instance != null) UIFade.Instance.FadeToClear();
            return;
        }

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 4,
            IsVisible = true,
            IsOpen = true,
            CleanupCacheOnLeave = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { { "targetScene", targetSceneName } }
        };

        Debug.Log($"Odaya katılınıyor: {pendingRoomName}");
        PhotonNetwork.JoinOrCreateRoom(pendingRoomName, roomOptions, TypedLobby.Default);
    }

    #region Photon Callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log("Master'a bağlanıldı!");
        if (isWaitingForConnection)
        {
            isWaitingForConnection = false;
            CreateOrJoinRoom();
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Odaya katılındı: {PhotonNetwork.CurrentRoom.Name}");
        
        if (!string.IsNullOrEmpty(targetSceneName) && IsSceneValid(targetSceneName))
        {
            Debug.Log($"Sahne yükleniyor: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Photon bağlantısı kesildi: {cause}");
        isWaitingForConnection = false;
        if (UIFade.Instance != null) UIFade.Instance.FadeToClear();
        SceneManager.LoadScene("MainMenu");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Odaya katılma başarısız: {message}");
        isWaitingForConnection = false;
        if (UIFade.Instance != null) UIFade.Instance.FadeToClear();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Oda oluşturma başarısız: {message}");
        isWaitingForConnection = false;
        if (UIFade.Instance != null) UIFade.Instance.FadeToClear();
    }
    #endregion
}

