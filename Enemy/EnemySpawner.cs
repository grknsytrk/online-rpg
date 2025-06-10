using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using Pathfinding; // A* Pathfinding Project için eklendi

public class EnemySpawner : MonoBehaviourPunCallbacks
{
    [Header("Spawn Area Settings")]
    [Tooltip("Spawn alanının merkezi. Eğer null ise, bu GameObject'in dönüşümü kullanılacak.")]
    [SerializeField] private Transform spawnAreaCenter;
    [Tooltip("Dikdörtgen spawn alanının boyutu (Width, Height).")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(20f, 20f); // Default to 20x20 square

    [Header("Enemy & Spawn Settings")]
    [Tooltip("Düşmanın oluşturulacağı prefab. Bir PhotonView bileşenine sahip olmalı.")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Bu spawner'ın herhangi bir anda yönetebileceği maksimum düşman sayısı.")]
    [SerializeField] private int maxManagedEnemies = 5;
    [Tooltip("Maksimum kapasitenin altındaysa, spawnlama denemeleri arasındaki minimum süre (saniye cinsinden).")]
    [SerializeField] private float spawnInterval = 10f;
    [Tooltip("Spawnerın daha fazla düşman üretip üretmemesi gerektiğini ne sıklıkla (saniye cinsinden) kontrol ettiği.")]
    [SerializeField] private float checkInterval = 2f;

    private List<PhotonView> managedEnemies = new List<PhotonView>();
    private float timeSinceLastSpawnAttempt = 0f;

    void Start()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            enabled = false; // Only the MasterClient should run spawner logic
            return;
        }

        if (enemyPrefab == null)
        {
            Debug.LogError($"EnemyPrefab not assigned to Spawner '{gameObject.name}'! Disabling spawner.", this);
            enabled = false;
            return;
        }
        if (enemyPrefab.GetComponent<PhotonView>() == null)
        {
            Debug.LogError($"EnemyPrefab '{enemyPrefab.name}' on Spawner '{gameObject.name}' is missing a PhotonView component! This is required for network instantiation. Disabling spawner.", this);
            enabled = false;
            return;
        }
        // It's good practice to also check for EnemyHealth if your counting logic relies on it.
        if (enemyPrefab.GetComponent<EnemyHealth>() == null)
        {
            Debug.LogWarning($"EnemyPrefab '{enemyPrefab.name}' on Spawner '{gameObject.name}' is missing an EnemyHealth component. Counting live enemies might not be fully accurate if an alternative death detection isn't in place.", this);
        }

        if (spawnAreaCenter == null)
        {
            spawnAreaCenter = transform; // Default to this GameObject's transform if not set
        }

        StartCoroutine(SpawnLogicRoutine());
    }

    private IEnumerator SpawnLogicRoutine()
    {
        while (true)
        {
            // Extra safety check, in case MasterClient changes or spawner is disabled unexpectedly
            if (!PhotonNetwork.IsMasterClient || !enabled)
            {
                yield return new WaitForSeconds(checkInterval);
                continue;
            }

            int currentManagedEnemyCount = CountManagedEnemies();
            // Uncomment for debugging:
            // Debug.Log($"Spawner '{gameObject.name}': Managed Enemies: {currentManagedEnemyCount}/{maxManagedEnemies}");

            bool canSpawn = currentManagedEnemyCount < maxManagedEnemies;

            if (canSpawn)
            {
                timeSinceLastSpawnAttempt += checkInterval;
                if (timeSinceLastSpawnAttempt >= spawnInterval)
                {
                    SpawnEnemy();
                    timeSinceLastSpawnAttempt = 0f; // Reset after a spawn attempt
                }
            }
            else
            {
                timeSinceLastSpawnAttempt = 0; // Reset timer if max capacity is reached
                // Uncomment for debugging:
                // Debug.Log($"Spawner '{gameObject.name}': Max managed enemies reached. Pausing spawn.");
            }
            
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private int CountManagedEnemies()
    {
        int liveAndManagedCount = 0;
        // Iterate backwards for safe removal from the list if an enemy is destroyed
        for (int i = managedEnemies.Count - 1; i >= 0; i--)
        {
            PhotonView enemyPV = managedEnemies[i];
            if (enemyPV == null || enemyPV.gameObject == null) // Enemy was destroyed
            {
                managedEnemies.RemoveAt(i);
                continue;
            }

            // Check if the enemy is alive using EnemyHealth component
            EnemyHealth health = enemyPV.GetComponent<EnemyHealth>();
            if (health != null && !health.DetectDeath()) // DetectDeath() should return true if health <= 0
            {
                liveAndManagedCount++;
            }
            else if (health == null || health.DetectDeath())
            {
                // If it's dead or has no health script, it might be about to be destroyed.
                // It will be removed from the list in a subsequent check once 'enemyPV' becomes null after PhotonNetwork.Destroy.
                // No direct removal here to avoid issues if destruction is delayed.
            }
        }
        return liveAndManagedCount;
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null) return;

        float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        float randomY = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
        Vector3 potentialSpawnPosition = spawnAreaCenter.position + new Vector3(randomX, randomY, 0);

        // A* Pathfinding ile pozisyon geçerliliğini kontrol et
        if (AstarPath.active == null)
        {
            Debug.LogError($"Spawner '{gameObject.name}': A* Pathfinding sistemi aktif değil! Pozisyon kontrolü yapılamıyor.", this);
            // A* olmadan devam etmeyi veya spawn etmemeyi seçebilirsiniz.
            // Şimdilik A* olmadan devam edelim, ancak bu uyarı önemlidir.
        }
        else
        {
            NNConstraint constraint = NNConstraint.Default;
            constraint.walkable = true; // Sadece yürünebilir nodeları ara
            NNInfo nodeInfo = AstarPath.active.GetNearest(potentialSpawnPosition, constraint);

            if (nodeInfo.node == null || !nodeInfo.node.Walkable)
            {
                Debug.LogWarning($"Spawner '{gameObject.name}': Geçersiz (yürünemez veya node bulunamadı) spawn pozisyonu: {potentialSpawnPosition} for {enemyPrefab.name}. Bu seferlik spawn edilmeyecek.", this);
                // İsteğe bağlı: Birkaç kez daha farklı pozisyon denenebilir veya bir sonraki interval beklenebilir.
                return; // Spawn etme
            }
            // Geçerli bir pozisyon bulundu, node'un pozisyonunu kullan
            potentialSpawnPosition = nodeInfo.position;
        }

        // Elite Düşman Şansı
        bool isElite = Random.Range(0, 100) < 5; // %5 şans
        object[] instantiationData = new object[] { isElite };

        // Debug.Log($"MasterClient - Spawner '{gameObject.name}': Attempting to spawn enemy '{enemyPrefab.name}' at {potentialSpawnPosition}");
        GameObject newEnemyGO = PhotonNetwork.Instantiate(enemyPrefab.name, potentialSpawnPosition, Quaternion.identity, 0, instantiationData);
        
        if (newEnemyGO != null)
        {
            PhotonView newEnemyPV = newEnemyGO.GetComponent<PhotonView>();
            if (newEnemyPV != null)
            {
                managedEnemies.Add(newEnemyPV);
                // Debug.Log($"Spawner '{gameObject.name}': Successfully spawned and now managing '{newEnemyGO.name}' (ViewID: {newEnemyPV.ViewID})");
            }
            else
            {
                // This should ideally not happen if the prefab is set up correctly.
                Debug.LogError($"Spawner '{gameObject.name}': Spawned enemy '{enemyPrefab.name}' is MISSING a PhotonView component! Cannot manage it. It might be destroyed.", newEnemyGO);
                // Consider destroying it if it's unusable:
                // if (PhotonNetwork.IsMasterClient) { PhotonNetwork.Destroy(newEnemyGO); }
            }
        }
        else
        {
            Debug.LogError($"Spawner '{gameObject.name}': PhotonNetwork.Instantiate returned null for prefab '{enemyPrefab.name}'. Check if prefab is in a Resources folder and name is correct.");
        }
    }

    // Draw a gizmo in the editor to visualize the spawn area
    void OnDrawGizmosSelected()
    {
        Transform center = spawnAreaCenter != null ? spawnAreaCenter : transform;
        Gizmos.color = Color.cyan; // Use a distinct color for spawner radius
        Gizmos.DrawWireCube(center.position, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0.1f)); // Draw a flat cube for 2D

        // Optional: Draw lines to managed enemies for debugging
        // Gizmos.color = Color.green;
        // foreach(PhotonView pv in managedEnemies)
        // {
        //     if (pv != null && pv.gameObject != null)
        //     {
        //         Gizmos.DrawLine(center.position, pv.transform.position);
        //     }
        // }
    }

    // Stop coroutines if the spawner is disabled
    public override void OnDisable()
    {
        base.OnDisable(); // Important for PhotonView callbacks
        if (PhotonNetwork.IsMasterClient) // Only stop if it was running logic
        {
            StopAllCoroutines();
        }
    }

    // Optional: If the spawner is destroyed, clear its list.
    // void OnDestroy()
    // {
    //     if (PhotonNetwork.IsMasterClient)
    //     {
    //          managedEnemies.Clear();
    //     }
    // }
} 