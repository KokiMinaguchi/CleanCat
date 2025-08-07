using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SleepingAnimals
{
    /// <summary>
    /// �ėp�{�^�����g�p�����x�[�X�{�^���N���X
    /// </summary>
    [RequireComponent(typeof(CustomButton))]
    public class BaseButton : MonoBehaviour
    {
        protected CustomButton _button;

        // Start is called before the first frame update
        protected virtual void Start()
        {
            _button = GetComponent<CustomButton>();
        }
    }
}