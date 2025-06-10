using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class AreaEntrance : MonoBehaviour
{
    [SerializeField] private string transitionName;

    private void Start()
    {
        if (transitionName == SceneManagment.Instance.SceneTransitionName)
        {
            // Local player'ı bul
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            foreach (var player in players)
            {
                if (player.GetComponent<PhotonView>().IsMine)
                {
                    player.transform.position = this.transform.position;
                    CameraController cameraController = FindObjectOfType<CameraController>();
                    if (cameraController != null)
                    {
                        cameraController.SetPlayerCameraFollow();
                    }
                    UIFade.Instance.FadeToClear();
                    break;
                }
            }
        }
    }
}
