using UnityEngine;

public class EnemyPathfinding : MonoBehaviour
{
    // Movement speed of the enemy
    [SerializeField] private float moveSpeed = 2f;

    // Components required for movement
    private Rigidbody2D rb;
    private Vector2 moveDirection;
    private Knockback knockback;
    private float initialScaleX; // Başlangıç X ölçeğini saklamak için
    private Animator animator; // Animator referansı eklendi

    private void Awake() 
    {
        // Get required components
        knockback = GetComponent<Knockback>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>(); // Animator bileşenini al
        initialScaleX = transform.localScale.x; // Başlangıç X ölçeğini kaydet
    }

    private void FixedUpdate() 
    {
        // Only move if not being knockbacked
        if (knockback.gettingKnockbacked)
            return;

        // Move the enemy using calculated direction
        rb.MovePosition(rb.position + moveDirection * (moveSpeed * Time.fixedDeltaTime));

        // Flip the sprite based on movement direction
        if (moveDirection.x != 0) // Sadece yatay hareket varsa çevir
        {
            float scaleX = moveDirection.x < 0 ? -initialScaleX : initialScaleX;
            transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
        }
    }

    // Method to set movement target
    public void MoveTo(Vector2 targetPosition) 
    {
        // Calculate direction to the target position
        moveDirection = (targetPosition - (Vector2)transform.position).normalized;
        // Animasyonu başlat (eğer animator varsa)
        if (animator != null)
        {
            animator.SetBool("IsMoving", true); 
        }
    }

    // Method to stop movement by resetting the move direction
    public void Stop()
    {
        moveDirection = Vector2.zero;
        // Animasyonu durdur (eğer animator varsa)
         if (animator != null)
        {
            animator.SetBool("IsMoving", false); 
        }
    }
}