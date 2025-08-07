using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤー移動クラス
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    #region Field

    private bool _dash;

    [SerializeField, Range(1.0f, 10.0f)]
    [Header("移動スピード")]
    private float _walkSpeed;

    [SerializeField]
    private float _dashSpeed;

    private float _rotationVelocity;

    private float _targetRotation = 0.0f;
    [SerializeField, Range(0.1f, 5.0f)]
    [Header("回転速度")]
    public float RotationSmoothTime = 0.12f;

    [SerializeField]
    private GameObject _aimTarget;

    [SerializeField, Range(0.0f, 1.0f)]
    [Header("減衰係数")]
    private float _attenuation = 1.0f;

    //private Rigidbody _rb;
    private CharacterController _controller;
    private GameObject _mainCamera;
    // 入力
    //private PlayerInput _input;
    // プレイヤーの各種パラメータ
    private PlayerParameter _parameter;
    Plane plane = new Plane();
    float distance = 0;

    private Vector2 _inputDirection;

    [SerializeField]
    [Range(0.0f, 500.0f)]
    float _gravity = 20.0f;

    //[SerializeField]
    //CameraController _cameraController;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        
        //var input = GetComponent<PlayerInput>().
        _controller = GetComponent<CharacterController>();
        // プレイヤーの各種パラメータ
        _parameter = GetComponent<PlayerParameter>();
    }

    private void Update()
    {
        // 入力方向計算（カメラの方向を進行方向にする）
        Vector3 moveDirection = new Vector3(_inputDirection.x, 0f, _inputDirection.y);

        //if(moveDirection != Vector3.zero)
        //{
        //    _cameraController.ChangeMovingCameraPriority(true);
        //}
        //else
        //{
        //    _cameraController.ChangeMovingCameraPriority(false);
        //}
            // カメラの方向から、X-Z平面の単位ベクトルを取得
        Vector3 cameraForward = Vector3.Scale(Camera.main.transform.forward, new Vector3(1, 0, 1)).normalized;
        moveDirection =
        cameraForward * _inputDirection.y +
        _mainCamera.transform.right * _inputDirection.x;

        float targetSpeed = _dash ? _dashSpeed : _walkSpeed;

        // プレイヤーが動いている間、移動方向を向くために回転する
        if (moveDirection.sqrMagnitude != 0f)
        {
            _targetRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;// +
                                                                                              //_mainCamera.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                RotationSmoothTime);

            // rotate to face input direction relative to camera position
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }

        //// カメラとマウスの位置を元にRayを準備
        //var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        //// プレイヤーの高さにPlaneを更新して、カメラの情報を元に地面判定して距離を取得
        //plane.SetNormalAndPosition(Vector3.up, transform.localPosition);
        //if (plane.Raycast(ray, out distance))
        //{
        //    // 距離を元に交点を算出して、交点の方を向く
        //    var lookPoint = ray.GetPoint(distance);
        //    transform.LookAt(lookPoint);
        //}

        // プレイヤー移動
        moveDirection *= targetSpeed;
        moveDirection.y = moveDirection.y - (_gravity * Time.deltaTime);
        _controller.Move(moveDirection * Time.deltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        switch (context.phase)
        {
            case InputActionPhase.Performed:
                _inputDirection = context.ReadValue<Vector2>();

                break;
            case InputActionPhase.Canceled:
                _inputDirection = Vector2.zero;

                break;
            default:
                break;
        }
        Debug.Log("move!" + _inputDirection);
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        switch (context.phase)
        {
            case InputActionPhase.Started:
                _dash = true;
                break;
            case InputActionPhase.Canceled:
                _dash = false;
                break;
            default:
                break;
        }
        Debug.Log(_dash);
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        Debug.Log(context.phase);
    }
}
