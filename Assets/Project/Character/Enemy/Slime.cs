using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Slime : MonoBehaviour
{
    [SerializeField]
    private string[] _dropItemList;

    [SerializeField]
    private float[] _dropItemWeight;

    private float _totalWeight = 0f;

    [SerializeField]
    private DropItemData[] dropTable;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // �d�݂̑��a�v�Z
        for (var i = 0; i < _dropItemList.Length; i++)
        {
            _totalWeight += _dropItemWeight[i];
        }
    }

    public bool ChooseDropItem(int index)
    {
        // 0�`�d�݂̑��a�͈̗̔͂����l�擾
        var randomPoint = Random.Range(0f, _totalWeight);
        Debug.Log(randomPoint);

        // �����l�����ݗv�f�͈͓̔����`�F�b�N
        if (randomPoint < _dropItemWeight[index])
        {
            return true;
        }

        return false;
    }


    void DropItems()
    {
        foreach (var drop in dropTable)
        {
            if (Random.value <= drop.dropChance)
            {
                int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);

                Debug.Log("�o�����F" + amount);
                Debug.Log("�A�C�e�����F" + drop._itemName);
                //for (int i = 0; i < amount; i++)
                //{
                //    Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0f, 0.5f), 0f);
                //    GameObject item = Instantiate(drop.itemPrefab, transform.position + offset, Quaternion.identity);

                //    // Optionally: ������΂����o
                //    Rigidbody2D rb = item.GetComponent<Rigidbody2D>();
                //    if (rb != null)
                //    {
                //        rb.AddForce(new Vector2(Random.Range(-1f, 1f), Random.Range(1f, 2f)), ForceMode2D.Impulse);
                //    }
                //}
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        DropItems();
        //for(int i = 0; i < _dropItemList.Length;i++)
        //{
        //    var isDrop = ChooseDropItem(i);
        //    if(isDrop == true)
        //    {
        //        Debug.Log(_dropItemList[i]);
        //    }
        //}
    }
}
