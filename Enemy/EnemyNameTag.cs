using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EnemyNameTag : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] public GameObject nameTagContainer; 
    [SerializeField] private Color eliteNameColor = Color.magenta; // Inspector'dan ayarlanabilir elit rengi
    [SerializeField] private Color defaultNameColor = Color.white; // Inspector'dan ayarlanabilir varsayılan renk

    [Header("Settings")]
    [SerializeField] private Color enemyNameColor = Color.red; // Default color for enemy names

    private Camera mainCamera;
    private Transform targetTransform; // The enemy's transform
    private EnemyAI enemyAI; // Reference to the EnemyAI script to get the name
    private Vector3 initialNameTagLocalScale; // İsim etiketinin başlangıç lokal ölçeği

    private Image nameTagBackground; // YENİ: Background Image referansı (opsiyonel)
    private bool _isEliteVisuals = false; // Elit görsel durumunu saklamak için

    private void Awake()
    {
        // Get the parent enemy object's transform and EnemyAI component
        targetTransform = transform.parent; 
        if (targetTransform == null)
        {
            Debug.LogError("EnemyNameTag: Parent transform (enemy) not found!", this);
            enabled = false;
            return;
        }

        // Get the EnemyAI component from the parent
        enemyAI = targetTransform.GetComponent<EnemyAI>();
        if (enemyAI == null)
        {
            Debug.LogError("EnemyNameTag: EnemyAI component not found on the parent!", this);
            // We can still function without EnemyAI if we set the name directly or via another source
        }

        // Find TextMeshProUGUI and Image components within the nameTagContainer
        if (nameTagContainer != null)
        {
            nameText = nameTagContainer.GetComponentInChildren<TextMeshProUGUI>();
            nameTagBackground = nameTagContainer.GetComponentInChildren<Image>(); // Assuming there's one Image for the background

            if (nameText == null)
            {
                Debug.LogError("EnemyNameTag: TextMeshProUGUI component not found in children of nameTagContainer!", this);
            }
            else
            {
                defaultNameColor = nameText.color; // Mevcut rengi varsayılan olarak al
            }
            // nameTagBackground is optional, so no error if not found
        }
        else
        {
            Debug.LogError("EnemyNameTag: nameTagContainer is not assigned!", this);
            enabled = false;
            return;
        }

        initialNameTagLocalScale = nameTagContainer.transform.localScale; // Konteynerin lokal ölçeğini kaydet
        UpdateNameDisplay(); // Set initial name
    }

    private void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("EnemyNameTag: Main Camera not found!", this);
            enabled = false;
            return;
        }

        if (nameTagContainer == null) // nameTagContainer null ise Start içinde işlem yapma
        {
            Debug.LogError("EnemyNameTag: nameTagContainer is null, cannot proceed in Start.", this);
            enabled = false;
            return;
        }
        initialNameTagLocalScale = nameTagContainer.transform.localScale; // Başlangıç ölçeğini kaydet

        // Set up the canvas for world space rendering
        Canvas nameTagCanvas = nameTagContainer.GetComponent<Canvas>();
        if (nameTagCanvas != null)
        {
            nameTagCanvas.renderMode = RenderMode.WorldSpace;
            nameTagCanvas.worldCamera = mainCamera;
        }
        else
        {
            Debug.LogWarning("EnemyNameTag: No Canvas component found on nameTagContainer. Ensure it's a world space canvas.", this);
        }
        
        UpdateNameDisplay();
    }

    private void UpdateNameDisplay()
    {
        if (nameText == null || enemyAI == null) return;

        nameText.text = enemyAI.GetDisplayName(); // We'll add GetDisplayName() to EnemyAI

        // Elit durumuna göre rengi ayarla
        if (_isEliteVisuals)
        {
            nameText.color = eliteNameColor;
        }
        else
        {
            nameText.color = defaultNameColor;
        }
        
        if (nameTagContainer != null)
        {
            nameTagContainer.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null || targetTransform == null || nameTagContainer == null)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null || targetTransform == null || nameTagContainer == null) return;
        }
        
        
        // Billboard effect: Make the name tag face the camera
        nameTagContainer.transform.rotation = mainCamera.transform.rotation;

        // Ebeveynin (düşmanın) ölçek flip'ini dengeleyerek etiketin ters dönmesini engelle
        if (initialNameTagLocalScale != Vector3.zero) // Sadece başlangıç ölçeği ayarlandıysa devam et
        {
            float parentScaleXSign = Mathf.Sign(targetTransform.localScale.x);
            
            nameTagContainer.transform.localScale = new Vector3(
                Mathf.Abs(initialNameTagLocalScale.x) * parentScaleXSign,
                Mathf.Abs(initialNameTagLocalScale.y), // Y ve Z'yi pozitif tut (veya initialScale'deki işaretlerini koru)
                Mathf.Abs(initialNameTagLocalScale.z)
            );
        }
    }

    public void SetEliteVisuals(bool isElite)
    {
        _isEliteVisuals = isElite; // Elit durumunu sakla
        UpdateNameDisplay(); // Rengi hemen güncellemek için UpdateNameDisplay'i çağır

        if (isElite)
        {
            Debug.Log($"EnemyNameTag on {targetTransform.name}: Elite visuals activated. Text color should be {eliteNameColor}.");
        }
    }
} 