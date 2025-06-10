using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;
using System;

public class DestructibleSpawner : MonoBehaviourPunCallbacks
{
    [SerializeField] private List<GameObject> destructiblePrefabs;
    private Dictionary<string, GameObject> prefabLookup;
    private List<(Vector3 position, Quaternion rotation, Vector3 scale, string prefabName)> existingDestructibles = new List<(Vector3, Quaternion, Vector3, string)>();
    private bool hasSpawned = false;
    private bool isInitialized = false;
    private HashSet<int> activeViewIds = new HashSet<int>();
    private HashSet<int> destroyedViewIds = new HashSet<int>();
    private HashSet<int> pendingOwnershipTransfers = new HashSet<int>();
    private HashSet<string> destroyedObjectPositions = new HashSet<string>();

    // Photon event kodları
    private const byte OwnershipTransferCode = 206;
    private const byte DestroyCode = 150;

    private void Awake()
    {
        Debug.Log("DestructibleSpawner Awake başladı");
        prefabLookup = new Dictionary<string, GameObject>();
        
        // Resources'dan prefabları yükle
        foreach (var prefab in destructiblePrefabs)
        {
            if (prefab != null)
            {
                string prefabPath = prefab.name;
                GameObject resourcePrefab = Resources.Load<GameObject>(prefabPath);
                if (resourcePrefab != null)
                {
                    prefabLookup[prefabPath] = resourcePrefab;
                    Debug.Log($"Prefab yüklendi: {prefabPath}");
                }
                else
                {
                    Debug.LogError($"Prefab bulunamadı Resources/{prefabPath}. Lütfen prefabın Resources klasöründe olduğundan emin olun.");
                }
            }
        }

        // Debug mesajlarını kapat
        PhotonNetwork.LogLevel = PunLogLevel.ErrorsOnly;
        
        // Sahne yüklenirken mesaj kuyruğunu duraklat
        PhotonNetwork.IsMessageQueueRunning = false;
        
        // Mevcut tüm PhotonView'lerin RPC buffer'ını temizle
        foreach (var destructible in destructiblePrefabs)
        {
            if (destructible != null)
            {
                var photonView = destructible.GetComponent<PhotonView>();
                if (photonView != null)
                {
                    PhotonNetwork.RemoveRPCs(photonView);
                }
            }
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.NetworkingClient.EventReceived -= OnPhotonEvent;
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void OnPhotonEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
        if (!PhotonNetwork.IsMessageQueueRunning)
            return;

        try
        {
            byte eventCode = photonEvent.Code;

            // Destroy eventi
            if (eventCode == DestroyCode && photonEvent.CustomData is object[] data && data.Length > 0)
            {
                int viewId = (int)data[0];
                destroyedViewIds.Add(viewId);
                pendingOwnershipTransfers.Remove(viewId);
                
                // Eğer data içinde pozisyon bilgisi varsa kaydet
                if (data.Length > 1 && data[1] is Vector3 position)
                {
                    string posKey = $"{position.x},{position.y},{position.z}";
                    destroyedObjectPositions.Add(posKey);
                }
            }

            // Ownership transfer eventi
            if (eventCode == OwnershipTransferCode && photonEvent.CustomData is object[] ownerData && ownerData.Length > 0)
            {
                int viewId = (int)ownerData[0];
                if (!destroyedViewIds.Contains(viewId))
                {
                    pendingOwnershipTransfers.Add(viewId);
                }
            }
        }
        catch
        {
            // Hata mesajlarını sessizce yok say
        }
    }

    private void Start()
    {
        if (!isInitialized)
        {
            StartCoroutine(InitializeDestructibles());
        }
    }

    private IEnumerator InitializeDestructibles()
    {
        if (isInitialized) yield break;
        isInitialized = true;

        // Sahnenin yüklenmesini bekle
        yield return new WaitForSeconds(0.2f);

        try
        {
            // Sahnedeki mevcut destructible'ları kaydet
            var localDestructibles = FindObjectsOfType<Destructible>();
            
            existingDestructibles.Clear();
            foreach (var destructible in localDestructibles)
            {
                if (destructible == null) continue;

                string prefabName = GetMatchingPrefabName(destructible.gameObject);
                if (!string.IsNullOrEmpty(prefabName))
                {
                    existingDestructibles.Add((
                        destructible.transform.position,
                        destructible.transform.rotation,
                        destructible.transform.localScale,
                        prefabName
                    ));
                }

                // Eğer bu obje networkte değilse yok et
                if (!destructible.photonView || !destructible.photonView.IsMine)
                {
                    if (destructible.photonView)
                    {
                        activeViewIds.Remove(destructible.photonView.ViewID);
                    }
                    Destroy(destructible.gameObject);
                }
            }

            // Eğer Master Client isek ve odadaysak spawn işlemini başlat
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && !hasSpawned)
            {
                yield return StartCoroutine(DelayedSpawn());
            }
        }
        finally
        {
            // İşlemler bitince mesaj kuyruğunu tekrar başlat
            PhotonNetwork.IsMessageQueueRunning = true;
        }
    }

    private string GetMatchingPrefabName(GameObject obj)
    {
        if (obj == null) return null;
        
        foreach (var prefab in destructiblePrefabs)
        {
            if (prefab != null && obj.name.StartsWith(prefab.name))
            {
                return prefab.name;
            }
        }
        return null;
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        StartCoroutine(OnJoinedRoomRoutine());
    }

    private IEnumerator OnJoinedRoomRoutine()
    {
        // Sahne yüklenene kadar bekle
        yield return new WaitForSeconds(0.5f);
        
        // Mesaj kuyruğunu duraklat
        PhotonNetwork.IsMessageQueueRunning = false;

        try
        {
            // Destructible'ları başlat
            if (!isInitialized)
            {
                yield return StartCoroutine(InitializeDestructibles());
            }
        }
        finally
        {
            // İşlemler bitince mesaj kuyruğunu tekrar başlat - cleanup should be synchronous
            PhotonNetwork.IsMessageQueueRunning = true;
        }

        // Add delay after the try-finally block
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator DelayedSpawn()
    {
        if (hasSpawned) yield break;

        bool wasMessageQueueRunning = PhotonNetwork.IsMessageQueueRunning;
        PhotonNetwork.IsMessageQueueRunning = false;

        try
        {
            yield return new WaitForSeconds(0.2f);
            
            var groupedDestructibles = new Dictionary<string, List<(Vector3 position, Quaternion rotation, Vector3 scale)>>();
            foreach (var (position, rotation, scale, prefabName) in existingDestructibles)
            {
                if (string.IsNullOrEmpty(prefabName)) continue;
                
                // Pozisyon kontrolü yap
                string posKey = $"{position.x},{position.y},{position.z}";
                if (destroyedObjectPositions.Contains(posKey))
                {
                    continue; // Bu pozisyondaki obje daha önce yok edilmiş, spawn etme
                }
                
                if (!groupedDestructibles.ContainsKey(prefabName))
                {
                    groupedDestructibles[prefabName] = new List<(Vector3, Quaternion, Vector3)>();
                }
                groupedDestructibles[prefabName].Add((position, rotation, scale));
            }

            foreach (var kvp in groupedDestructibles)
            {
                string prefabName = kvp.Key;
                var instances = kvp.Value;
                
                for (int i = 0; i < instances.Count; i++)
                {
                    var (position, rotation, scale) = instances[i];
                    GameObject spawnedObj = null;

                    try
                    {
                        spawnedObj = PhotonNetwork.InstantiateRoomObject(prefabName, position, rotation);
                        
                        if (spawnedObj != null)
                        {
                            spawnedObj.transform.localScale = scale;
                            var photonView = spawnedObj.GetComponent<PhotonView>();
                            if (photonView != null)
                            {
                                photonView.OwnershipTransfer = OwnershipOption.Takeover;
                                PhotonNetwork.RemoveRPCs(photonView);
                                activeViewIds.Add(photonView.ViewID);
                                destroyedViewIds.Remove(photonView.ViewID);
                                pendingOwnershipTransfers.Remove(photonView.ViewID);
                                
                                if (PhotonNetwork.IsMasterClient)
                                {
                                    photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Hata mesajlarını sessizce yok say
                    }

                    if (i % 3 == 2)
                    {
                        yield return null;
                    }
                }
            }
        }
        finally
        {
            hasSpawned = true;
            PhotonNetwork.IsMessageQueueRunning = wasMessageQueueRunning;
        }

        yield return null;
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient && !hasSpawned)
        {
            StartCoroutine(OnPlayerEnteredRoomRoutine());
        }
    }

    private IEnumerator OnPlayerEnteredRoomRoutine()
    {
        // Mesaj kuyruğunu durdur
        PhotonNetwork.IsMessageQueueRunning = false;
        
        // Aktif olmayan view ID'leri temizle
        CleanupInactiveViewIds();
        
        yield return new WaitForSeconds(1f);
        
        // Sadece ilk kez spawn edilmemişse spawn et
        if (!hasSpawned)
        {
            yield return StartCoroutine(DelayedSpawn());
        }
        
        // Mesaj kuyruğunu tekrar başlat
        yield return new WaitForSeconds(0.5f);
        PhotonNetwork.IsMessageQueueRunning = true;
    }

    private void CleanupInactiveViewIds()
    {
        try
        {
            var currentDestructibles = FindObjectsOfType<Destructible>();
            var currentViewIds = new HashSet<int>();
            
            foreach (var destructible in currentDestructibles)
            {
                if (destructible?.photonView != null)
                {
                    currentViewIds.Add(destructible.photonView.ViewID);
                }
            }

            activeViewIds.IntersectWith(currentViewIds);
            destroyedViewIds.RemoveWhere(id => !currentViewIds.Contains(id));
            pendingOwnershipTransfers.RemoveWhere(id => !currentViewIds.Contains(id));
        }
        catch
        {
            // Hata mesajlarını sessizce yok say
        }
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        StopAllCoroutines();
        CleanupAllIds();
        hasSpawned = false;
        isInitialized = false;
        existingDestructibles.Clear();
        
        // Odadan çıkarken mesaj kuyruğunu tekrar başlat
        PhotonNetwork.IsMessageQueueRunning = true;
    }

    private void CleanupAllIds()
    {
        activeViewIds.Clear();
        destroyedViewIds.Clear();
        pendingOwnershipTransfers.Clear();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        CleanupAllIds();
    }
}