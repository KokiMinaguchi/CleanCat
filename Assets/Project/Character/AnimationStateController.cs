using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationStateController : MonoBehaviour
{
    private Animator _animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetStateToAnimator(Vector2 vector)
    {
        //if (!vector.)
        //{
        //    this._animator.speed = 0.0f;
        //    return;
        //}

        if(vector != Vector2.zero)
        {
            //Debug.Log(vector);
            this._animator.speed = 1.0f;
            this._animator.SetFloat("AxisX", vector.x);
            this._animator.SetFloat("AxisY", vector.y);
            this._animator.SetBool("Running", true);
        }
        else
        {
            this._animator.SetBool("Running", false);
        }
    }
}
