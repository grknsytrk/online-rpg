using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class AreaExit : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;
    [SerializeField] private string sceneTransitionName;
    [SerializeField] private float waitToLoadTime = 1f;

    private SceneManagment sceneManager;
    private bool isTransitioning = false;

    private void Start()
    {
        sceneManager = SceneManagment.Instance;
        if (sceneManager == null)
        {
            Debug.LogError("SceneManagment bulunamadı! Lütfen sahnede SceneManagment component'inin olduğundan emin olun.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (sceneManager == null || isTransitioning) return;

        PlayerController playerController = other.gameObject.GetComponent<PlayerController>();
        PhotonView photonView = other.gameObject.GetComponent<PhotonView>();

        if (playerController != null && photonView != null && photonView.IsMine)
        {
            isTransitioning = true;
            sceneManager.SetTransitionName(sceneTransitionName);
            UIFade.Instance.FadeToBlack();
            StartCoroutine(LoadSceneRoutine());
        }
    }

    private IEnumerator LoadSceneRoutine()
    {
        float timer = waitToLoadTime;
        while (timer >= 0)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        if (sceneManager == null)
        {
            UIFade.Instance.FadeToClear();
            yield break;
        }

        sceneManager.TransitionToNewRoom(sceneToLoad, sceneToLoad + "_Room");
    }
}
