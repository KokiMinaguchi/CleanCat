using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private CinemachineMixingCamera _mixingCamera;

    [SerializeField]
    Transform target;

    private CinemachinePositionComposer composer;
    private Camera _mainCamera;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _mixingCamera = GetComponent<CinemachineMixingCamera>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// MovingVirtualCameraÇÃWeightÇè„â∫Ç≥ÇπÇÈ
    /// </summary>
    /// <param name="isUpPriority"></param>
    public void ChangeMovingCameraPriority(bool isUpPriority)
    {
        if(isUpPriority)
        {
            _mixingCamera.ChildCameras[1].Priority = 1;
            //_mixingCamera.Weight1 += 0.05f;
            //_mixingCamera.Weight0 -= 0.05f;
            //_mixingCamera.Weight1 = Mathf.Clamp(_mixingCamera.Weight1, 0.0f, 1.0f);
            //_mixingCamera.Weight0 = Mathf.Clamp(_mixingCamera.Weight0, 0.0f, 1.0f);
        }
        else
        {
            _mixingCamera.ChildCameras[1].Priority = -1;
            //_mixingCamera.Weight1 -= 0.05f;
            //_mixingCamera.Weight0 += 0.05f;
            //_mixingCamera.Weight1 = Mathf.Clamp(_mixingCamera.Weight1, 0.0f, 1.0f);
            //_mixingCamera.Weight0 = Mathf.Clamp(_mixingCamera.Weight0, 0.0f, 1.0f);
        }
    }
}
