using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flash : MonoBehaviour
{
    [SerializeField] private Material whiteFlashMaterial;
    [SerializeField] private float restoreDefaultMaterialTime;

    private Material defaultMaterial;
    private SpriteRenderer SpriteRenderer;
    private Knockback knockback;

    private void Awake()
    {
        knockback = GetComponent<Knockback>();
        SpriteRenderer = GetComponent<SpriteRenderer>();
        if (SpriteRenderer != null)
        {
            defaultMaterial = SpriteRenderer.material;
        }
        else
        {
            Debug.LogError("Flash: SpriteRenderer bulunamadı!", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (knockback != null)
        {
            restoreDefaultMaterialTime = knockback.knockbackTime;
        }
        else
        {
            restoreDefaultMaterialTime = 0.1f;
            Debug.LogWarning("Flash: Knockback bileşeni bulunamadı, varsayılan flash süresi kullanılıyor.", this);
        }
    }

    public IEnumerator FlashRoutine()
    {
        if (SpriteRenderer == null) yield break;

        if (whiteFlashMaterial == null)
        {
            Debug.LogWarning("Flash: White Flash Material atanmamış!", this);
            yield break;
        }

        if (defaultMaterial == null)
        {
            if (SpriteRenderer != null) defaultMaterial = SpriteRenderer.material;
            if (defaultMaterial == null)
            {
                Debug.LogError("Flash: Default material alınamadı.", this);
                yield break;
            }
        }

        SpriteRenderer.material = whiteFlashMaterial;
        yield return new WaitForSeconds(restoreDefaultMaterialTime);
        
        if (SpriteRenderer != null)
        {
            SpriteRenderer.material = defaultMaterial;
        }
    }

}
