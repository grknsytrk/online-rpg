using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class CameraController : MonoBehaviourPunCallbacks
{
    private static CameraController instance;
    public static CameraController Instance { get { return instance; } }
    private CinemachineVirtualCamera cinemachineVirtualCamera;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        
        cinemachineVirtualCamera = GetComponent<CinemachineVirtualCamera>();
        if (cinemachineVirtualCamera == null)
        {
            cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }
    }

    private void Start()
    {
        SetPlayerCameraFollow();
    }

    public void SetPlayerCameraFollow()
    {
        if (cinemachineVirtualCamera == null) return;

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            if (player.photonView.IsMine)
            {
                cinemachineVirtualCamera.Follow = player.transform;
                Debug.Log($"Camera following player: {player.photonView.ViewID}");
                break;
            }
        }
    }

    // Yeni oyuncu spawn olduğunda kamerayı ayarla
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SetPlayerCameraFollow();
    }
}
