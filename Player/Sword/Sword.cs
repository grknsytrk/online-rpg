using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Photon.Realtime;
using Photon.Pun;

public class Sword : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject slashAnimPrefab;
    [SerializeField] private Transform slashAnimSpawnPoint;
    [SerializeField] public Transform weaponCollider;
    [SerializeField] private float swordAttackCD = 0.5f;

    private PlayerControls playerControls;
    private Animator myAnimator;
    private PlayerController playerController;
    public Transform activeWeapon;
    private bool attackButtonDown, isAttacking = false;
    private bool isSettingsPanelOpen = false;
    private bool isLocked = false;

    private GameObject slashAnim;
    private PhotonView swordPV;

    private void Awake()
    {
        // Assign PhotonView first
        swordPV = GetComponent<PhotonView>(); 
        if (swordPV == null) {
            Debug.LogError($"Sword Awake: GetComponent<PhotonView> FAILED on GO: {gameObject.name}! This Sword might not function correctly over the network.", this.gameObject);
            // Optionally, disable the script if PhotonView is crucial and missing
            // this.enabled = false;
            // return;
        }

        // Awake başlangıcında swordPV null olabilir, ?. ile güvenli erişim -> Artık yukarıda atandı ve kontrol edildi.
        Debug.Log($"Sword Awake START - ViewID: {swordPV?.ViewID}, GO Name: {gameObject.name}"); 
        playerController = GetComponentInParent<PlayerController>();
        myAnimator = GetComponent<Animator>();
        // swordPV = GetComponent<PhotonView>(); // Moved up
        playerControls = new PlayerControls();

        if (myAnimator == null)
        {
            Debug.LogError($"Sword Awake: GetComponent<Animator> FAILED on GO: {gameObject.name}! ViewID: {swordPV?.ViewID}", this.gameObject);
        }
        else
        {
            Debug.Log($"Sword Awake: Animator FOUND on GO: {gameObject.name}, ViewID: {swordPV?.ViewID}");
        }

         if (playerController == null)
        {
             Debug.LogError($"Sword Awake: PlayerController component could not be found in parent objects on GO: {gameObject.name}! ViewID: {swordPV?.ViewID}", this.gameObject);
        }
         Debug.Log($"Sword Awake END - ViewID: {swordPV?.ViewID}");
    }

    public override void OnEnable()
    {
        base.OnEnable();
        Debug.Log($"Sword OnEnable START - ViewID: {swordPV?.ViewID}, GO Name: {gameObject.name}, myAnimator is {(myAnimator == null ? "NULL" : "Assigned")}");
        if (playerControls == null) 
        {
            playerControls = new PlayerControls();
        }
        playerControls.Enable();
        
        if (playerController != null && playerController.PV != null && playerController.PV.IsMine)
        {
            if (PhotonNetwork.InRoom && swordPV != null)
            {
                // Debug.Log($"Sword OnEnable: Sending SyncSwordVisibility(true) RPC [Buffered] - ViewID: {swordPV.ViewID}");
            }
             else if (swordPV == null) {
                 Debug.LogWarning($"Sword OnEnable: swordPV is null, cannot send RPC. GO: {gameObject.name}");
             }
        }
         Debug.Log($"Sword OnEnable END - ViewID: {swordPV?.ViewID}");
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Debug.Log($"Sword OnDisable START - ViewID: {swordPV?.ViewID}, GO Name: {gameObject.name}");
        playerControls?.Disable();
        
        if (playerController != null && playerController.PV != null && playerController.PV.IsMine)
        {
            if (PhotonNetwork.InRoom && swordPV != null)
            {
                // Debug.Log($"Sword OnDisable: Sending SyncSwordVisibility(false) RPC [Buffered] - ViewID: {swordPV.ViewID}");
            }
             else if (swordPV == null) {
                 Debug.LogWarning($"Sword OnDisable: swordPV is null, cannot send RPC. GO: {gameObject.name}");
             }
        }
         Debug.Log($"Sword OnDisable END - ViewID: {swordPV?.ViewID}");
    }

    void Start()
    {
         Debug.Log($"Sword Start - ViewID: {swordPV?.ViewID}, GO Name: {gameObject.name}, IsMine: {playerController?.PV?.IsMine}");
        if (playerController != null && playerController.PV != null && playerController.PV.IsMine)
        {
             if (playerControls == null) 
            {
                 Debug.LogError($"Sword Start: playerControls is null! Cannot subscribe to input events. ViewID: {swordPV?.ViewID}");
                 return; 
            }
            playerControls.Combat.Attack.started += _ => startAttacking();
            playerControls.Combat.Attack.canceled += _ => stopAttacking();
             Debug.Log($"Sword Start: Input subscribed for ViewID: {swordPV?.ViewID}");
        }
         else
         {
             Debug.Log($"Sword Start: Not subscribing input. IsMine: {playerController?.PV?.IsMine}, ViewID: {swordPV?.ViewID}");
         }
    }

    private void Update()
    {
        if (playerController?.PV == null || !playerController.PV.IsMine || isLocked || isSettingsPanelOpen) return;

        MouseFollowWithOffset();
        Attack();
    }

    private void startAttacking()
    {
        if (isLocked || isSettingsPanelOpen) return;
         Debug.Log($"Start Attacking - ViewID: {swordPV?.ViewID}");
        attackButtonDown = true;
    }

    private void stopAttacking()
    {
        if (isLocked || isSettingsPanelOpen) return;
         Debug.Log($"Stop Attacking - ViewID: {swordPV?.ViewID}");
        attackButtonDown = false;
    }

    [PunRPC]
    public void SyncSwordVisibility(bool isVisible)
    {
        // Add a check for swordPV at the start of the RPC execution for safety
        if (swordPV == null) {
            // Attempt to get PhotonView again right here
            swordPV = GetComponent<PhotonView>();
            if (swordPV == null)
            {
                // If still null, then abort.
                Debug.LogError($"SyncSwordVisibility RPC Received but swordPV is STILL NULL after retry! GO: {gameObject.name}, isVisible: {isVisible}. Aborting visibility change on this instance.");
                return; // Cannot proceed without PhotonView
            } else {
                 Debug.LogWarning($"SyncSwordVisibility RPC: swordPV was null but found just now. ViewID: {swordPV.ViewID}");
            }
        }

         // Artık swordPV'nin null olmadığını biliyoruz.
         Debug.Log($"SyncSwordVisibility RPC Received - ViewID: {swordPV.ViewID}, GO Name: {gameObject.name}, isVisible: {isVisible}");
        // Bu RPC tüm oyuncularda çalışır
        // isVisible true ise kılıcı göster, false ise gizle
        if (weaponCollider != null)
        {
            weaponCollider.gameObject.SetActive(isVisible);
        }
        
        // Aktif silahın görünürlüğünü ayarla
        if (activeWeapon != null)
        {
            activeWeapon.gameObject.SetActive(isVisible);
        }
        
        // IsMine kontrolü burada gereksiz çünkü RPC RpcTarget.Others ile gönderiliyor.
        
        // swordPV null kontrolü artık gereksiz, başta yapıldı.
        /*
        if (swordPV != null)
        {
            // Debug.Log($"SwordVisibility senkronize edildi. Görünürlük: {isVisible}, ViewID: {swordPV.ViewID}"); // Zaten başta log var
        }
        else
        {
            Debug.LogWarning($"SyncSwordVisibility: swordPV is null! Visibility: {isVisible}, GO: {gameObject.name}");
        }
        */
    }

    private void Attack()
    {
        if (playerController?.PV == null || !playerController.PV.IsMine || isLocked || isSettingsPanelOpen) return;
        
        if(attackButtonDown && !isAttacking)
        {
            isAttacking = true;
             Debug.Log($"Attack Initiated - ViewID: {swordPV?.ViewID}, Sending RPC...");
            
            if (PhotonNetwork.InRoom && swordPV != null)
            {
                swordPV.RPC("NetworkedAttack", RpcTarget.All);
            }
            else if (!PhotonNetwork.InRoom)
            {
                 Debug.LogWarning($"Attack: Not in room, calling NetworkedAttack locally. ViewID: {swordPV?.ViewID}");
                NetworkedAttack();
            }
             else
             {
                 Debug.LogError($"Attack: swordPV is null, cannot send RPC! GO: {gameObject.name}");
             }
            
            StartCoroutine(AttackCDRoutine());
        }
    }

    [PunRPC]
    private void NetworkedAttack()
    {
        // Add a check for swordPV at the start of the RPC execution for safety
        if (swordPV == null) {
            // Attempt to get PhotonView again right here
            swordPV = GetComponent<PhotonView>();
            if (swordPV == null) {
                // If still null, then abort.
                 Debug.LogError($"NetworkedAttack RPC Received but swordPV is STILL NULL after retry! GO: {gameObject.name}. Aborting attack logic on this instance.");
                return; // Cannot proceed without PhotonView
             } else {
                 Debug.LogWarning($"NetworkedAttack RPC: swordPV was null but found just now. ViewID: {swordPV.ViewID}");
             }
        }

        // RPC geldiğinde swordPV'nin durumunu logla (Artık null olmamalı)
        Debug.Log($"NetworkedAttack RPC Received - ViewID: {swordPV.ViewID}, GO Name: {gameObject.name}, IsMine: {swordPV.IsMine}, myAnimator is {(myAnimator == null ? "NULL" : "Assigned")}");

        // Check for myAnimator (it might initialize after PV in Awake)
        if (myAnimator == null)
        {
            Debug.LogWarning($"NetworkedAttack RPC: myAnimator was NULL. Attempting GetComponent<Animator> again... ViewID: {swordPV.ViewID}");
            myAnimator = GetComponent<Animator>(); // Try to get it again
             if (myAnimator == null) {
                 // If still null, log error and potentially abort or continue without animation
                 Debug.LogError($"NetworkedAttack: myAnimator is STILL NULL after retry! Cannot play animation. ViewID: {swordPV.ViewID}, GO: {gameObject.name}");
                 // Decide if you want to return or continue without animation:
                 return; // Abort if animation is critical
             } else {
                 Debug.LogWarning($"NetworkedAttack: Animator found just now. Possible timing issue was resolved. ViewID: {swordPV.ViewID}");
             }
        }

        // Diğer null kontrolleri (weaponCollider vs.) yerinde kalmalı.
        if (weaponCollider == null)
        {
            Debug.LogError($"NetworkedAttack: weaponCollider is null! ViewID: {swordPV.ViewID}, GO: {gameObject.name}");
            // return; 
        }
        if (slashAnimPrefab == null)
        {
            Debug.LogError($"NetworkedAttack: slashAnimPrefab is null! Cannot instantiate slash animation. ViewID: {swordPV.ViewID}, GO: {gameObject.name}");
            // return; 
        }
        if (slashAnimSpawnPoint == null)
        {
             Debug.LogError($"NetworkedAttack: slashAnimSpawnPoint is null! Cannot determine position for slash animation. ViewID: {swordPV.ViewID}, GO: {gameObject.name}");
            // return; 
        }

        // Null kontrollerinden sonra kullan
        if (myAnimator != null) 
        {
            myAnimator.SetTrigger("Attack");
             Debug.Log($"NetworkedAttack: Attack trigger set on Animator. ViewID: {swordPV.ViewID}");
            SFXManager.Instance?.PlaySound(SFXNames.SwordSwing); // Play sword swing sound using constant
        } else {
             // Bu durum, yukarıdaki yeniden deneme başarısız olursa yaşanabilir.
             Debug.LogWarning($"NetworkedAttack: Cannot set Attack trigger because myAnimator is still null. ViewID: {swordPV.ViewID}");
        }

        if (weaponCollider != null) weaponCollider.gameObject.SetActive(true);

        if (slashAnimPrefab != null && slashAnimSpawnPoint != null)
        {
            // Instantiate öncesi log
             Debug.Log($"NetworkedAttack: Instantiating slashAnimPrefab. ViewID: {swordPV.ViewID}");
            slashAnim = Instantiate(slashAnimPrefab, slashAnimSpawnPoint.position, Quaternion.identity);
            if (slashAnim != null && this.transform.parent != null)
            {
                 slashAnim.transform.parent = this.transform.parent;
                 Debug.Log($"NetworkedAttack: Slash animation instantiated and parented. ViewID: {swordPV.ViewID}");
            }
            else if (slashAnim != null)
            {
                Debug.LogWarning($"NetworkedAttack: Slash animation instantiated but parent is null. ViewID: {swordPV.ViewID}");
            }
             else 
            {
                 Debug.LogError($"NetworkedAttack: Failed to instantiate slashAnimPrefab! ViewID: {swordPV.ViewID}");
            }
        }
    }

    IEnumerator AttackCDRoutine()
    {
        yield return new WaitForSeconds(swordAttackCD);
        isAttacking = false;
         Debug.Log($"AttackCDRoutine Finished - ViewID: {swordPV?.ViewID}");
    }

    public void DoneAttack()
    {
         Debug.Log($"DoneAttack called - ViewID: {swordPV?.ViewID}");
        if (weaponCollider != null) 
        {
            weaponCollider.gameObject.SetActive(false);
        } else {
             Debug.LogWarning($"DoneAttack: weaponCollider is null. ViewID: {swordPV?.ViewID}");
        }
    }

    public void SwingUpFlipAnim()
    {
        if (slashAnim == null) {
             Debug.LogWarning($"SwingUpFlipAnim: slashAnim is null! ViewID: {swordPV?.ViewID}");
             return;
         }
        slashAnim.gameObject.transform.rotation = Quaternion.Euler(-180, 0, 0);

        if (playerController != null && playerController.FacingLeft)
        {
            slashAnim.GetComponent<SpriteRenderer>().flipX = true;
        }
    }

    public void SwingDownFlipAnim()
    {
        if (slashAnim == null) {
             Debug.LogWarning($"SwingDownFlipAnim: slashAnim is null! ViewID: {swordPV?.ViewID}");
             return;
         }
        slashAnim.gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);

        if (playerController != null && playerController.FacingLeft)
        {
            slashAnim.GetComponent<SpriteRenderer>().flipX = true;
        }
    }

    private void MouseFollowWithOffset()
    {
        if (playerController == null || Camera.main == null) return; 

        Vector3 mousePos = Input.mousePosition;
        Vector3 playerScreenPoint = Camera.main.WorldToScreenPoint(playerController.transform.position);
        float angle = Mathf.Atan2(mousePos.y, mousePos.x) * Mathf.Rad2Deg;

        if (activeWeapon == null || weaponCollider == null) {
             return;
         }

        if (mousePos.x < playerScreenPoint.x)
        {
            activeWeapon.transform.rotation = Quaternion.Euler(0, -180, angle);
            weaponCollider.transform.rotation = Quaternion.Euler(0, -180, 0);
        }
        else
        {
            activeWeapon.transform.rotation = Quaternion.Euler(0, 0, angle);
            weaponCollider.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }

    public void SetSettingsPanelState(bool isOpen)
    {
        if (playerController?.PV == null || !playerController.PV.IsMine) return;
        isSettingsPanelOpen = isOpen;
         Debug.Log($"SetSettingsPanelState: {isOpen} - ViewID: {swordPV?.ViewID}");
        
        if (isOpen)
        {
            StopAllCoroutines();
            isAttacking = false;
            attackButtonDown = false;
            DoneAttack();
        }
    }

    public void SetLocked(bool locked)
    {
        if (playerController?.PV == null || !playerController.PV.IsMine) return;
        isLocked = locked;
         Debug.Log($"SetLocked: {locked} - ViewID: {swordPV?.ViewID}");
        
        if (!locked)
        {
            attackButtonDown = false;
            isAttacking = false;
        }
    }
}
