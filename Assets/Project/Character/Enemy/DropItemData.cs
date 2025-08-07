using UnityEngine;

[System.Serializable]
public class DropItemData
{
    public GameObject itemPrefab;
    public string _itemName;
    public float dropChance = 0.5f; // �h���b�v�m��
    public int minAmount = 1;
    public int maxAmount = 3;
}
