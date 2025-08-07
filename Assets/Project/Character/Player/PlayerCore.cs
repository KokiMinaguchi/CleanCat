using UnityEngine;

public class PlayerCore : MonoBehaviour
{
    private uint _monney;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ChangeMonney(int  value)
    {
        _monney = (uint)Mathf.Clamp(_monney + value, 0, 99999);
    }
}
