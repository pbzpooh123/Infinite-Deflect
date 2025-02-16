using Unity.Netcode;
using UnityEngine;

public class PlayerCameraManager : NetworkBehaviour
{
    [SerializeField] private GameObject cameraPrefab;
    private GameObject activeCamera;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            SpawnPlayerCamera();
        }
    }

    private void SpawnPlayerCamera()
    {
        // Instantiate the camera
        activeCamera = Instantiate(cameraPrefab);
        
        // No need to network the camera since it's client-side only
        DontDestroyOnLoad(activeCamera);
    }

    public override void OnNetworkDespawn()
    {
        if (activeCamera != null)
        {
            Destroy(activeCamera);
        }
    }
}