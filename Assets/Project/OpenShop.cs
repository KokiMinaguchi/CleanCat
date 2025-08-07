using UnityEngine;
using UnityEngine.InputSystem;

public class OpenShop : MonoBehaviour
{
    private bool _isOpenShop = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(_isOpenShop && Keyboard.current.fKey.wasPressedThisFrame)
        {
            Debug.Log("OpenShop");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        _isOpenShop = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        _isOpenShop = false;
    }
}
