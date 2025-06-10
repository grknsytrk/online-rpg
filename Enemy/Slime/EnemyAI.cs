using UnityEngine;
using System.Collections;
using System.Collections.Generic; // List için eklendi
using System.Linq; // Linq sorguları için eklendi
using Photon.Pun;
using UnityEngine.UI; // Image veya GameObject referansı için
using Pathfinding; // A* Pathfinding Project için eklendi

// AI'nin AIPath'e ihtiyacı olduğunu belirtelim (Inspector'da otomatik eklenmesini sağlar)
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AIPath))]
public class EnemyAI : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    // Enemy states
    private enum State 
    {
        Roaming,
        Chasing // Yeni durum eklendi
    }

    [Header("Roaming")]
    // [SerializeField] private float roamingInterval = 2f; // Eski Tek Değer
    // [SerializeField] private float roamingPauseDuration = 1f; // Eski Tek Değer
    [SerializeField] private float minRoamingPauseDuration = 0.5f; // Hedefe vardıktan sonraki minimum duraklama süresi
    [SerializeField] private float maxRoamingPauseDuration = 1.5f; // Hedefe vardıktan sonraki maksimum duraklama süresi
    [SerializeField] private float minRoamingInterval = 1.5f; // Duraklamadan sonraki minimum bekleme süresi
    [SerializeField] private float maxRoamingInterval = 3.0f; // Duraklamadan sonraki maksimum bekleme süresi
    [SerializeField] private float maxRoamDistance = 5f; // Mevcut konumdan ne kadar uzağa gidebileceği
    [SerializeField] private float minRoamDistance = 1f; // Ne kadar yakına gidebileceği (0 olmaması için)
    // [SerializeField] private float roamingPauseDuration = 1f; // Kaldırıldı

    [Header("Chasing")]
    [SerializeField] private float detectionRadius = 7f; // Görüş alanı yarıçapı
    // [SerializeField] private float chaseUpdateInterval = 0.2f; // AIPath bunu kendi yönetiyor, kaldırıldı
    [SerializeField] private float detectionCheckInterval = 0.5f; // Oyuncu kontrol etme sıklığı
    [SerializeField] private LayerMask playerLayerMask; // Oyuncu katmanı
    [SerializeField] private LayerMask obstacleLayerMask; // Engel katmanı (Duvar vs.) - Linecast için hala lazım
    [SerializeField] private float fieldOfViewAngle = 90f; // Görüş açısı (derece)
    
    [Header("Elite Settings")] // YENİ BAŞLIK
    [SerializeField] private float eliteScaleMultiplier = 1.2f;
    [SerializeField] private int eliteDamageMultiplier = 2;

    [Header("Display Name")] // New Header for Display Name
    [SerializeField] private string displayName = "Slime"; // Default display name for the enemy

    [Header("Combat")]
    [SerializeField] private int contactDamage;
    [SerializeField] private float damageInterval;
    [SerializeField] private float knockbackPower;

    [Header("UI Feedback")] // Yeni başlık
    [SerializeField] private GameObject chaseIndicatorUI; // Kovalama göstergesi UI nesnesi (Canvas veya Image)

    private State currentState;
    // private EnemyPathfinding enemyPathfinding; // Kaldırıldı
    private Seeker seeker; // A* bileşeni
    private AIPath aiPath; // A* hareket bileşeni
    private Animator animator; // Animasyon için
    private PhotonView pv;
    private float lastDamageTime;
    private Transform targetPlayer = null; // Kovalama hedefi
    private Coroutine currentRoutine = null; // Aktif korutini tutmak için
    private float initialScaleX; // Sprite yönü için

    public bool IsElite { get; private set; } = false; // YENİ EKLENDİ

    // CircleCollider2D referansı (Inspector'dan atanabilir veya Awake'de bulunabilir)
    // Eğer farklı trigger collider'lar varsa Inspector'dan atamak daha güvenli olur.
    // Şimdilik Awake içinde aramayı deneyelim.
    private CircleCollider2D detectionCollider;
    private EnemyHealth enemyHealth; // EnemyHealth referansı

    private void Awake() 
    {
        // enemyPathfinding = GetComponent<EnemyPathfinding>(); // Kaldırıldı
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        animator = GetComponent<Animator>(); // Animator'u al
        pv = GetComponent<PhotonView>();
        enemyHealth = GetComponent<EnemyHealth>(); // EnemyHealth bileşenini al
        detectionCollider = GetComponentInChildren<CircleCollider2D>();
        initialScaleX = transform.localScale.x; // Başlangıç X ölçeğini kaydet

        if (seeker == null || aiPath == null)
        {
             Debug.LogError("EnemyAI: Seeker veya AIPath bileşeni bulunamadı! Lütfen prefab'a eklediğinizden emin olun.", this);
             enabled = false;
             return;
        }

        if (enemyHealth == null)
        {
             Debug.LogError("EnemyAI: EnemyHealth bileşeni bulunamadı!", this);
             enabled = false;
             return;
        }

        if (detectionCollider == null)
        {
            Debug.LogError("EnemyAI: CircleCollider2D alt nesnelerde bulunamadı! Lütfen düşman prefabına bir 'DetectionZone' child objesi ekleyin, ona CircleCollider2D ekleyin ve 'Is Trigger' olarak ayarlayın.", this);
            enabled = false;
            return;
        }

        // Bulunan collider'ın trigger olduğundan emin olalım (opsiyonel ama iyi bir kontrol)
        if (!detectionCollider.isTrigger)
        {
            Debug.LogWarning("EnemyAI: Algılanan CircleCollider2D bir trigger değil ('Is Trigger' işaretli değil). Görüş alanı düzgün çalışmayabilir.", detectionCollider);
        }

        // Yarıçapı hala script üzerinden ayarlayabiliriz
        detectionCollider.radius = detectionRadius;

        // Sadece Master Client AI'ı çalıştırır
        if (!PhotonNetwork.IsMasterClient && PhotonNetwork.IsConnected)
        {
            aiPath.enabled = false; // AIPath'i de devre dışı bırak
            enabled = false;
            return;
        }

        // AIPath ayarlarını doğrula/ayarla (opsiyonel ama önerilir)
        aiPath.canMove = true; // Hareket edebilir
        aiPath.enableRotation = false; // Dönüşü kendimiz yapacağız
        aiPath.orientation = OrientationMode.YAxisForward; // 2D için genellikle bu kullanılır, ama dönüş kapalı.
        aiPath.gravity = Vector3.zero; // 2D'de yerçekimi yok
        
        // Başlangıç durumu ve rutini
        currentState = State.Roaming;
        StartNewRoutine(RoamingRoutine());
        StartCoroutine(CheckForPlayerRoutine()); // Oyuncu kontrol rutinini başlat

        // Hasar olayına abone ol (Sadece Master Client)
        if (PhotonNetwork.IsMasterClient)
        {
            enemyHealth.OnDamagedByPlayer += HandleDamageTaken;
        }

        // UI göstergesini başlangıçta kapat (RPC ile, Buffered ekledim)
        if (PhotonNetwork.IsMasterClient)
        {
            pv.RPC("RPC_UpdateChaseIndicatorUI", RpcTarget.AllBuffered, false);
        }
    }

    private void Update()
    {
        // Sadece MasterClient kontrol etsin veya AI aktifse
        if (!PhotonNetwork.IsMasterClient || !enabled) return;

        // Animasyon kontrolü
        if (animator != null)
        {
            // Hız vektörünün büyüklüğüne bakarak hareket edip etmediğini anla
            bool isMoving = aiPath.velocity.sqrMagnitude > 0.01f; // Küçük bir eşik değer
            animator.SetBool("IsMoving", isMoving);
        }

        // Sprite yönünü ayarlama
        // aiPath.desiredVelocity, A* tarafından hesaplanan ideal hız vektörüdür
        if (aiPath.desiredVelocity.x > 0.01f) // Sağa gidiyor
        {
            transform.localScale = new Vector3(initialScaleX, transform.localScale.y, transform.localScale.z);
        }
        else if (aiPath.desiredVelocity.x < -0.01f) // Sola gidiyor
        {
            transform.localScale = new Vector3(-initialScaleX, transform.localScale.y, transform.localScale.z);
        }
        // Eğer x yönünde hareket yoksa, yönü değiştirme
    }


    private void OnDestroy() // Veya OnDisable
    {
        // Olay aboneliğini kaldır (Memory Leak önlemek için)
        if (enemyHealth != null && PhotonNetwork.IsMasterClient)
        {
            enemyHealth.OnDamagedByPlayer -= HandleDamageTaken;
        }
    }

    // Hasar alındığında çağrılacak fonksiyon
    private void HandleDamageTaken(int attackerViewID)
    {
        if (!enabled || !PhotonNetwork.IsMasterClient) return;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (attackerView != null && attackerView.transform != null)
        {
            Transform attackerTransform = attackerView.transform;

             // Zaten bu hedefi kovalıyorsak ekstra bir şey yapmaya gerek yok
             if (currentState == State.Chasing && targetPlayer == attackerTransform)
             {
                 return;
             }

            // --- Görüş Hattı Kontrolü Kaldırıldı --- 
            // Artık hasar aldığı anda, nereden gelirse gelsin kovalamaya başlayacak.
            // if (HasLineOfSight(attackerTransform))
            // {
                Debug.Log($"[Master AI - Damage] Düşman hasar aldı. Saldıran: {attackerTransform.name} (ViewID: {attackerViewID}). Kovalamaya başla!");
                // targetPlayer = attackerTransform; // Hedefi saldırgan olarak ayarla

                // Durum Chasing değilse veya hedef farklıysa Chasing'e geç/güncelle
                if (currentState != State.Chasing || targetPlayer != attackerTransform)
                {
                    targetPlayer = attackerTransform; // Hedefi burada ayarla
                    Debug.Log($"[Master AI - Damage] Durum Chasing olarak değiştiriliyor. Yeni Hedef: {targetPlayer.name}");
                    currentState = State.Chasing;
                    pv.RPC("RPC_UpdateChaseIndicatorUI", RpcTarget.All, true); // UI'ı güncelle
                    StartNewRoutine(ChasingRoutine()); // Kovalama rutinini başlat/yeniden başlat
                }
                // Eğer zaten aynı hedefi kovalıyorsa, ChasingRoutine zaten hedefini güncelleyecektir.
            // }
            // else
            // {
            //      Debug.Log($"Düşman hasar aldı ama saldırgana ({attackerViewID}) görüş hattı yok. Yine de kovalayacak!");
            //      // Eskiden burada bir şey yapmıyordu, şimdi if bloğu her zaman çalışacak.
            // }
        }
    }

    // Mevcut rutini durdurup yenisini başlatmak için yardımcı fonksiyon
    private void StartNewRoutine(IEnumerator routine)
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }
        // aiPath'in durdurulması/başlatılması artık ilgili rutinler tarafından yönetilecek.
        // aiPath.isStopped = true; // Buradan kaldırıldı
        // aiPath.SetPath(null); // Mevcut yolu temizle - AIPath hedefe ulaşınca veya yeni yol başlayınca zaten temizler
        currentRoutine = StartCoroutine(routine);
    }

    // Public method to get the display name
    public string GetDisplayName()
    {
        return displayName;
    }

    // Oyuncuları belirli aralıklarla kontrol eden korutin
    private IEnumerator CheckForPlayerRoutine()
    {
        while (true)
        {
            if (PhotonNetwork.IsMasterClient && enabled) // Sadece Master Client ve aktifse kontrol etsin
            {
                 FindClosestPlayerAndSetState();
            }
            yield return new WaitForSeconds(detectionCheckInterval);
        }
    }

    // Hedefe görüş hattı olup olmadığını kontrol eden fonksiyon (Değişiklik yok)
    private bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Vector2 directionToTarget = (target.position - transform.position).normalized;
        float distanceToTarget = Vector2.Distance(transform.position, target.position);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, distanceToTarget, obstacleLayerMask);
        if (hit.collider != null)
        {
            return false; // Engel var
        }
        return true; // Engel yok
    }

    // En yakın oyuncuyu bulur ve duruma göre state değiştirir
    private void FindClosestPlayerAndSetState()
    {
        if (currentState == State.Chasing)
        {
            bool stopChasing = false;
            if (targetPlayer == null) stopChasing = true;
            else
            {
                PlayerHealth targetHealth = targetPlayer.GetComponent<PlayerHealth>();
                if (targetHealth == null || targetHealth.GetCurrentHealth() <= 0)
                {
                    stopChasing = true; // Debug.Log("State Check: Hedef oyuncu öldü.");
                }
                else
                {
                    float distanceToTarget = Vector2.Distance(transform.position, targetPlayer.position);
                    if (distanceToTarget > detectionRadius)
                    {
                        stopChasing = true; // Debug.Log("State Check: Hedef menzil dışı.");
                    }
                    else if (!HasLineOfSight(targetPlayer))
                    {
                        // LoS kaybolunca Roaming'e dönmek yerine son bilinen konuma gitmeyi deneyebiliriz
                        // Şimdilik Roaming'e dönüyoruz
                        stopChasing = true; // Debug.Log("State Check: Hedef LoS yok.");
                    }
                }
            }

            if (stopChasing)
            {
                currentState = State.Roaming;
                pv.RPC("RPC_UpdateChaseIndicatorUI", RpcTarget.All, false);
                // AIPath'i durdurmaya gerek yok, StartNewRoutine zaten yapıyor.
                StartNewRoutine(RoamingRoutine());
            }
            // else: Kovalamaya devam et, ChasingRoutine zaten çalışıyor ve hedefi güncelliyor
        }
        else // currentState == State.Roaming
        {
            Transform closestPlayer = FindClosestPlayerInRadius();

            if (closestPlayer != null)
            {
                // --- DETAILED LOGGING START ---
                // Debug.Log($"[Master AI - Roaming Check] Found potential target: {closestPlayer.name} (ViewID: {closestPlayer.GetComponent<PhotonView>()?.ViewID})");

                Vector2 directionToPlayer = (closestPlayer.position - transform.position).normalized;
                Vector3 enemyForward = transform.localScale.x >= 0 ? transform.right : -transform.right;
                float angle = Vector2.Angle(enemyForward, directionToPlayer);

                // Debug.Log($"[Master AI - Roaming Check] Angle to {closestPlayer.name}: {angle} degrees (Required: <= {fieldOfViewAngle / 2})");

                if (angle <= fieldOfViewAngle / 2)
                {
                    bool hasLoS = HasLineOfSight(closestPlayer);
                    // Debug.Log($"[Master AI - Roaming Check] Line of Sight to {closestPlayer.name}: {hasLoS}");

                    if (hasLoS)
                    {
                        // Debug.Log($"[Master AI - Detection] Player in FoV and LoS: {closestPlayer.name}. Starting Chase!"); // Simplified original log
                        targetPlayer = closestPlayer;
                        currentState = State.Chasing;
                        pv.RPC("RPC_UpdateChaseIndicatorUI", RpcTarget.All, true);
                         // AIPath'i durdurmaya gerek yok, StartNewRoutine zaten yapıyor.
                        StartNewRoutine(ChasingRoutine());
                    }
                    // else { Debug.Log($"[Master AI - Roaming Check] Player {closestPlayer.name} is in FoV but Line of Sight is blocked."); }
                }
                // else { Debug.Log($"[Master AI - Roaming Check] Player {closestPlayer.name} is in range but outside FoV ({angle} > {fieldOfViewAngle / 2})."); }
                // --- DETAILED LOGGING END ---
            }
            else
            {
                // Optional log if no player is found in radius during Roaming state
                // Debug.Log("[Master AI - Roaming Check] No players found within detection radius.");
            }
        }
    }

    // Görüş alanındaki en yakın *canlı* oyuncuyu bulan fonksiyon (Değişiklik yok)
    private Transform FindClosestPlayerInRadius()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, playerLayerMask);

        // Debug.Log($"[Master AI - FindClosest] OverlapCircle found {hits.Length} colliders on Player layer.");

        Transform closest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            // Debug.Log($"[Master AI - FindClosest] Checking collider: {hit.gameObject.name}, Layer: {LayerMask.LayerToName(hit.gameObject.layer)}");

            if (hit.transform == transform || hit.gameObject == null) continue;

            PlayerController player = hit.GetComponent<PlayerController>();
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();

            // if (player == null) { Debug.Log($"[Master AI - FindClosest]   -> {hit.gameObject.name} skipped: Missing PlayerController."); continue; }
            // if (player.PV == null) { Debug.Log($"[Master AI - FindClosest]   -> {hit.gameObject.name} skipped: Missing PhotonView on PlayerController."); continue; }
            // if (playerHealth == null) { Debug.Log($"[Master AI - FindClosest]   -> {hit.gameObject.name} skipped: Missing PlayerHealth."); continue; }
            if (player == null || player.PV == null || playerHealth == null) continue;


            int currentHealth = playerHealth.GetCurrentHealth();
            // Debug.Log($"[Master AI - FindClosest]   -> {hit.gameObject.name} has components. Health: {currentHealth}");

            if (currentHealth <= 0) { 
                // Debug.Log($"[Master AI - FindClosest]   -> {hit.gameObject.name} skipped: Health <= 0."); 
                continue; 
            }

                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < minDist)
                {
                // Debug.Log($"[Master AI - FindClosest]   -> {hit.gameObject.name} is closer (Dist: {dist}). Setting as potential target.");
                    minDist = dist;
                    closest = hit.transform;
                }
            // else
            // {
                // Debug.Log($"[Master AI - FindClosest]   -> {hit.gameObject.name} is not closer (Dist: {dist}, MinDist: {minDist}).");
            // }
        }

        // if (closest != null) {
            // Debug.Log($"[Master AI - FindClosest] Final closest player: {closest.name}");
        // } else {
             // Debug.Log("[Master AI - FindClosest] No valid player found to be closest.");
        // }

        return closest;
    }

    private IEnumerator RoamingRoutine() 
    {
        // Debug.Log("Roaming başladı.");
        // aiPath.isStopped = true; // Başlangıçta duruyor - Gerek yok, ilk hedefi bulana kadar zaten hareket etmeyecek
        aiPath.maxSpeed = 1f; // Roaming hızını daha yavaş yapabiliriz (AIPath üzerinden)

        while (currentState == State.Roaming)
        {
            // 1. Yeni Ulaşılabilir Hedef Bul
            Vector3 targetPosition = GenerateReachableRoamingPosition();
            
            // Eğer geçerli bir hedef bulunamadıysa veya hedef çok yakınsa, kısa bir süre bekleyip tekrar dene
            if (targetPosition == transform.position)
            {
                 Debug.LogWarning("RoamingRoutine: Geçerli uzaklıkta hedef bulunamadı, kısa süre bekleniyor...");
                 yield return new WaitForSeconds(1f); // Kısa bekleme
                 continue; // Döngünün başına git
            }

            // 2. Hedefe Yol İsteği Gönder ve Hareketi Başlat
            aiPath.isStopped = false; // Harekete izin ver
            aiPath.destination = targetPosition; // AIPath bu hedefe otomatik yol arayacak
            // Debug.Log($"Roaming: Yeni hedef {targetPosition} için yol aranıyor...");

            // 3. Hedefe Ulaşmayı Bekle
            // aiPath.reachedDestination yerine aiPath.pathPending (yol hesaplanıyor mu?) ve aiPath.hasPath (yol var mı?) kontrolü
            // ve aiPath.remainingDistance (kalan mesafe) kontrolü daha sağlam olabilir.
            // Şimdilik reachedDestination ile devam edelim ama izleyelim.
            float waitStartTime = Time.time;
            float maxWaitTime = 10f; // Hedefe ulaşmak için maksimum bekleme süresi (takılmaları önlemek için)
            yield return new WaitUntil(() => (aiPath.reachedDestination || Time.time > waitStartTime + maxWaitTime || currentState != State.Roaming));

            if (Time.time > waitStartTime + maxWaitTime)
            {
                 Debug.LogWarning("RoamingRoutine: Hedefe ulaşma zaman aşımına uğradı!");
                 aiPath.isStopped = true; // Hareketi durdur
                 // Belki burada yeni bir tarama veya başka bir işlem gerekebilir
            }
            else if (currentState != State.Roaming)
            {
                // State değiştiyse rutin zaten duracak, burada ekstra bir şey yapmaya gerek yok
                 yield break;
            }
            // else: Hedefe ulaşıldı (veya state değişmedi)

             // State değişmişse döngüden çık
             if (currentState != State.Roaming) yield break;

            // 4. Hedefe Vardıktan Sonra Durakla (veya zaman aşımına uğradıysa)
            // Debug.Log("Roaming: Hedefe varıldı veya zaman aşımına uğradı, duraklama başlıyor.");
            aiPath.isStopped = true; // Hareketi durdur
            // yield return new WaitForSeconds(roamingPauseDuration); // Eski çağrı
            yield return new WaitForSeconds(Random.Range(minRoamingPauseDuration, maxRoamingPauseDuration)); // Rastgele duraklama
             
             if (currentState != State.Roaming) yield break; // State değiştiyse çık

            // 5. Ekstra bekleme (Interval)
            // yield return new WaitForSeconds(Random.Range(roamingInterval * 0.8f, roamingInterval * 1.2f)); // Eski çağrı
            yield return new WaitForSeconds(Random.Range(minRoamingInterval, maxRoamingInterval)); // Rastgele bekleme

              if (currentState != State.Roaming) yield break; // State değiştiyse çık
        }
         Debug.Log("Roaming bitti.");
         aiPath.isStopped = true; // Rutin biterken de durduralım
    }


    // Yeni ChasingRoutine
    private IEnumerator ChasingRoutine()
    {
         Debug.Log("Chasing başladı: " + (targetPlayer != null ? targetPlayer.name : "NULL"));
         aiPath.isStopped = false; // Kovalarken hareket etmeli (EN BAŞTA AYARLA)
         aiPath.maxSpeed = 3f; // Kovalama hızını daha yüksek yapabiliriz

        // Chasing başlarken hemen ilk hedefi ayarlayalım ve yol arayalım
        if (targetPlayer != null)
        {
            aiPath.destination = targetPlayer.position;
            Debug.Log($"[Master AI - Chasing] Başlangıç hedefi ayarlandı: {targetPlayer.name} at {targetPlayer.position}");
        }
        else
        {
            Debug.LogWarning("[Master AI - Chasing] Rutin başladı ama targetPlayer null!");
        }

        while (currentState == State.Chasing && targetPlayer != null)
        {
            // Hedef Can Kontrolü, Menzil Kontrolü, LoS Kontrolü zaten CheckForPlayerRoutine'de yapılıyor

            // AIPath'in hedefini sürekli güncel tut (oyuncu hareket ederse diye)
            // AIPath'in kendi 'repathRate' ayarı bunu zaten yapıyor olabilir ama garanti olsun.
            aiPath.destination = targetPlayer.position;

            yield return new WaitForSeconds(0.1f); // Çok sık güncelleme yapmamak için kısa bekleme
        }

        // Döngü bittiğinde (hedef kaybedildi veya öldü)
        Debug.Log("Chasing bitti.");
        aiPath.isStopped = true; // Hareketi durdur
    }

    // A* grafiğinde ulaşılabilir rastgele bir nokta bulan fonksiyon
    private Vector3 GenerateReachableRoamingPosition()
    {
        float minDistanceSqr = 1f * 1f; // En az 1 birim uzakta olsun (karesi)
        int maxAttempts = 15; // Deneme sayısını biraz artıralım
        Vector3 currentPosition = transform.position;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(minRoamDistance, maxRoamDistance);
            Vector3 potentialPosition = currentPosition + (Vector3)(randomDirection * randomDistance);

            // A* grafiğinde bu pozisyona en yakın ulaşılabilir noktayı bul
            // NNConstraint.None daha esnek olabilir ama Default genellikle iyi çalışır
            NNInfo nearestNodeInfo = AstarPath.active.GetNearest(potentialPosition, NNConstraint.Default);
            GraphNode node = nearestNodeInfo.node;

            // Eğer bulunan node geçerliyse, yürünebilirse VE mevcut konumdan yeterince uzaktaysa
            if (node != null && node.Walkable)
            {
                Vector3 nodePosition = (Vector3)node.position; // Veya nearestNodeInfo.position daha doğru olabilir?
                // Vector3 nodePosition = nearestNodeInfo.position; // Bunu deneyelim
                if ((nodePosition - currentPosition).sqrMagnitude > minDistanceSqr)
            {
                    // Debug.Log($"Ulaşılabilir Roaming Pozisyonu bulundu: {nodePosition}");
                    return nodePosition;
                }
                // else: Bulunan nokta çok yakın, tekrar dene
            }
            // else: Geçerli veya yürünebilir node bulunamadı, tekrar dene
        }

        // Geçerli nokta bulunamadıysa mevcut konumu döndür (fallback)
        Debug.LogWarning("GenerateReachableRoamingPosition: Ulaşılabilir uzaklıkta rastgele nokta bulunamadı. Mevcut pozisyon kullanılacak.");
        return currentPosition;
            }

    // Seeker.StartPath tarafından çağrılacak callback fonksiyonu
    public void OnPathComplete(Path p)
    {
        // Debug.Log("Yol hesaplandı. Hata var mı? " + p.error);
        if (!p.error)
    {
            // Yol başarıyla bulundu, AIPath otomatik olarak takip etmeye başlar (eğer isStopped false ise)
            // aiPath.SetPath(p); // Buna gerek yok, AIPath Seeker'ı dinler
        }
        else
        {
            Debug.LogError("Yol hesaplama hatası: " + p.errorLog);
            // Hata durumunda ne yapılacağına karar verilebilir (örn. durmak, tekrar denemek vb.)
            aiPath.isStopped = true;
    }
    }

    
    // Oyuncu ile temas durumunda hasar ver (Değişiklik yok)
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!PhotonNetwork.IsMasterClient || !enabled) return;
        if (Time.time - lastDamageTime < damageInterval) return;
        
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();
        if (playerController != null && playerController.PV != null)
        {
            lastDamageTime = Time.time;
            int targetPlayerViewID = playerController.PV.ViewID;
            pv.RPC("RPC_DamagePlayer", RpcTarget.All, targetPlayerViewID, contactDamage, pv.ViewID);
            pv.RPC("RPC_ApplyKnockback", RpcTarget.All, targetPlayerViewID, pv.ViewID, knockbackPower);
        }
    }

    
    // RPC_DamagePlayer (Değişiklik yok)
    [PunRPC]
    private void RPC_DamagePlayer(int targetPlayerViewID, int damageAmount, int attackerViewID)
    {
        if (!pv.IsMine) return; // Sadece bu AI'ın sahibi hasar RPC'sini göndermeli (zaten öyle olmalı)

        PlayerHealth targetPlayerHealth = null;
        PhotonView targetPv = PhotonView.Find(targetPlayerViewID);
        if (targetPv != null)
        {
            targetPlayerHealth = targetPv.GetComponent<PlayerHealth>();
        }

        if (targetPlayerHealth != null)
        {
            int finalDamage = damageAmount;
            if (this.IsElite) // ELİT KONTROLÜ
            {
                finalDamage *= eliteDamageMultiplier;
                Debug.Log($"Elite enemy {gameObject.name} dealing bonus damage. Original: {damageAmount}, Final: {finalDamage}");
            }
            
            // TakeDamage method'unu çağır, o RPC'yi gönderecek
            targetPlayerHealth.TakeDamage(finalDamage, pv.ViewID);
        }
    }
    
    // RPC_ApplyKnockback (Değişiklik yok)
    [PunRPC]
    private void RPC_ApplyKnockback(int targetPlayerViewID, int attackerViewID, float knockbackPower)
    {
        PhotonView targetView = PhotonView.Find(targetPlayerViewID);
        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (targetView != null && attackerView != null)
        {
            if (targetView.IsMine)
            {
                Knockback knockback = targetView.GetComponent<Knockback>();
                if (knockback == null)
                {
                    knockback = targetView.gameObject.AddComponent<Knockback>();
                }
                knockback.GetKnockbacked(attackerView.transform, knockbackPower);
            }
        }
    }


    // Görselleştirme için Gizmos (AIPath'in kendi görselleştirmesi de var)
    private void OnDrawGizmos()
    {
        // Görüş Alanı Çemberini Çiz
        if (detectionCollider != null)
        {
             Gizmos.color = Color.yellow;
             Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
        
        // Görüş Açısı Konisini Çiz
        Gizmos.color = Color.cyan;
        Vector3 fovLine1 = Quaternion.AngleAxis(fieldOfViewAngle / 2, Vector3.forward) * (transform.localScale.x >= 0 ? transform.right : -transform.right) * detectionRadius;
        Vector3 fovLine2 = Quaternion.AngleAxis(-fieldOfViewAngle / 2, Vector3.forward) * (transform.localScale.x >= 0 ? transform.right : -transform.right) * detectionRadius;
        Gizmos.DrawRay(transform.position, fovLine1);
        Gizmos.DrawRay(transform.position, fovLine2);

         // Hedefi ve yolu görselleştirme (AIPath zaten yapıyor olabilir)
         // if (aiPath != null && aiPath.hasPath)
         // {
         //     Gizmos.color = Color.magenta;
         //     Gizmos.DrawLine(transform.position, aiPath.destination);
         // }
    }

    // Kovalama UI göstergesini güncelleyen RPC fonksiyonu (Değişiklik yok)
    [PunRPC]
    private void RPC_UpdateChaseIndicatorUI(bool isActive)
    {
        if (chaseIndicatorUI != null)
        {
            chaseIndicatorUI.SetActive(isActive);
        }
        else
        {
            if (isActive) 
            {
                Debug.LogWarning("EnemyAI: Chase Indicator UI atanmamış veya RPC çağrıldığında henüz hazır değil!", this);
            }
        }
    }

    // YENİ EKLENEN METOT - IPunInstantiateMagicCallback
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        if (info.photonView.InstantiationData != null && info.photonView.InstantiationData.Length > 0)
        {
            this.IsElite = (bool)info.photonView.InstantiationData[0];
            Debug.Log($"OnPhotonInstantiate: Enemy {gameObject.name} (ViewID: {pv.ViewID}) IsElite set to: {this.IsElite}");

            if (this.IsElite)
            {
                // Elit ise ölçeği (1,1,1) yap
                transform.localScale = Vector3.one;
                initialScaleX = 1.0f; // Yönlendirme için kullanılacak X ölçeğini de güncelle
                Debug.Log($"Enemy {gameObject.name} is Elite. Scale set to {transform.localScale}, initialScaleX set to {initialScaleX}.");

                // Inform Health & NameTag components
                EnemyHealth healthComponent = GetComponent<EnemyHealth>();
                if (healthComponent != null)
                {
                    healthComponent.SetEliteStatus(true); // EnemyHealth'e haber ver
                }
                else
                {
                    Debug.LogWarning($"Elite enemy {gameObject.name} could not find its EnemyHealth component.");
                }

                EnemyNameTag nameTagComponent = GetComponentInChildren<EnemyNameTag>();
                if (nameTagComponent != null)
                {
                    nameTagComponent.SetEliteVisuals(true); // EnemyNameTag'e haber ver
                }
                else
                {
                    Debug.LogWarning($"Elite enemy {gameObject.name} could not find its EnemyNameTag component.");
                }
            }
            // Elit değilse, Awake içinde ayarlanan initialScaleX (yani prefab'daki 0.75) ve mevcut ölçek kullanılır.
            // Eğer prefab'ın ölçeği zaten (0.75, 0.75, 0.75) ise ekstra bir şey yapmaya gerek yok.
            // Ama emin olmak için burada da initialScaleX'i Awake'deki gibi ayarlayabiliriz.
            // else {
            // initialScaleX = transform.localScale.x; // Ya da prefab'dan gelen orijinal değer
            // }
        }
        else
        {
            Debug.LogWarning($"OnPhotonInstantiate: Enemy {gameObject.name} (ViewID: {pv.ViewID}) received no InstantiationData or data was empty.");
            // Veri yoksa, Awake'de ayarlanan initialScaleX kullanılır.
        }
    }
}