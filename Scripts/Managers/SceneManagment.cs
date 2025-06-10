using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagement : MonoBehaviourPunCallbacks
{
    private static SceneManagement instance;
    public static SceneManagement Instance
    {
        get { return instance; }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            // Scene yükleme eventini dinlemeye başla
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Event dinlemeyi durdur
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void ChangeScene(string sceneName, Vector3 spawnPosition)
    {
        // Spawn pozisyonunu kaydet
        PlayerPrefs.SetFloat("SpawnPosX", spawnPosition.x);
        PlayerPrefs.SetFloat("SpawnPosY", spawnPosition.y);
        PlayerPrefs.SetFloat("SpawnPosZ", spawnPosition.z);

        // Mevcut oyuncuyu yok et
        if (PhotonNetwork.IsConnected)
        {
            PhotonView playerPhotonView = PhotonNetwork.LocalPlayer.TagObject as PhotonView;
            if (playerPhotonView != null)
            {
                PhotonNetwork.Destroy(playerPhotonView.gameObject);
            }
        }

        // Sahneyi yükle
        SceneManager.LoadScene(sceneName);
    }

    // OnLevelWasLoaded yerine bu metodu kullanıyoruz
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PhotonNetwork.IsConnected)
        {
            Vector3 spawnPos = new Vector3(
                PlayerPrefs.GetFloat("SpawnPosX", 0),
                PlayerPrefs.GetFloat("SpawnPosY", 0),
                PlayerPrefs.GetFloat("SpawnPosZ", 0)
            );

            // Player prefabını spawn et
            GameObject player = PhotonNetwork.Instantiate("Player", Vector3.zero, Quaternion.identity);
            
            // Spawn olduktan sonra en yakın AreaEntrance'ı bul
            AreaEntrance[] entrances = FindObjectsOfType<AreaEntrance>();
            AreaEntrance nearestEntrance = null;
            float nearestDistance = float.MaxValue;

            foreach (var entrance in entrances)
            {
                float distance = Vector3.Distance(entrance.transform.position, spawnPos);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEntrance = entrance;
                }
            }

            // En yakın entrance'a ışınla
            if (nearestEntrance != null)
            {
                player.transform.position = nearestEntrance.transform.position;
            }
            else
            {
                player.transform.position = spawnPos;
            }
        }
    }
} 