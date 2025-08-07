using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// �v���C���[�ړ��N���X
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    #region Field

    private bool _dash;

    [SerializeField, Range(1.0f, 10.0f)]
    [Header("�ړ��X�s�[�h")]
    private float _walkSpeed;

    [SerializeField]
    private float _dashSpeed;

    private float _rotationVelocity;

    private float _targetRotation = 0.0f;
    [SerializeField, Range(0.1f, 5.0f)]
    [Header("��]���x")]
    public float RotationSmoothTime = 0.12f;

    [SerializeField]
    private GameObject _aimTarget;

    [SerializeField, Range(0.0f, 1.0f)]
    [Header("�����W��")]
    private float _attenuation = 1.0f;

    //private Rigidbody _rb;
    private CharacterController _controller;
    private GameObject _mainCamera;
    // ����
    //private PlayerInput _input;
    // �v���C���[�̊e��p�����[�^
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
        // �v���C���[�̊e��p�����[�^
        _parameter = GetComponent<PlayerParameter>();
    }

    private void Update()
    {
        // ���͕����v�Z�i�J�����̕�����i�s�����ɂ���j
        Vector3 moveDirection = new Vector3(_inputDirection.x, 0f, _inputDirection.y);

        //if(moveDirection != Vector3.zero)
        //{
        //    _cameraController.ChangeMovingCameraPriority(true);
        //}
        //else
        //{
        //    _cameraController.ChangeMovingCameraPriority(false);
        //}
            // �J�����̕�������AX-Z���ʂ̒P�ʃx�N�g�����擾
        Vector3 cameraForward = Vector3.Scale(Camera.main.transform.forward, new Vector3(1, 0, 1)).normalized;
        moveDirection =
        cameraForward * _inputDirection.y +
        _mainCamera.transform.right * _inputDirection.x;

        float targetSpeed = _dash ? _dashSpeed : _walkSpeed;

        // �v���C���[�������Ă���ԁA�ړ��������������߂ɉ�]����
        if (moveDirection.sqrMagnitude != 0f)
        {
            _targetRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;// +
                                                                                              //_mainCamera.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                RotationSmoothTime);

            // rotate to face input direction relative to camera position
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }

        //// �J�����ƃ}�E�X�̈ʒu������Ray������
        //var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        //// �v���C���[�̍�����Plane���X�V���āA�J�����̏������ɒn�ʔ��肵�ċ������擾
        //plane.SetNormalAndPosition(Vector3.up, transform.localPosition);
        //if (plane.Raycast(ray, out distance))
        //{
        //    // ���������Ɍ�_���Z�o���āA��_�̕�������
        //    var lookPoint = ray.GetPoint(distance);
        //    transform.LookAt(lookPoint);
        //}

        // �v���C���[�ړ�
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
