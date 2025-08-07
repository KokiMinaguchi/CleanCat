using UnityEngine;

public class GetItem : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        // ƒAƒCƒeƒ€Žæ“¾
        if(other.gameObject.name == "Trush")
        {
            Destroy(other.gameObject);
        }

        if(other.gameObject.tag == "Treasure")
        {
            Destroy(other.gameObject);
        }
    }
}
