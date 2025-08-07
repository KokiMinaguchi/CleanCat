using System.Collections.Generic;
using UnityEngine;

public class ItemDrop : MonoBehaviour
{
    [SerializeField]
    private string[] _dropItemList;

    // 各要素の重みリスト
    [SerializeField]
    private float[] _weights;

    private WeightedChooser _weightedChooser;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _weightedChooser = new WeightedChooser(_weights);
    }

    // test
    private void OnTriggerEnter2D(Collider2D collision)
    {
        var index = _weightedChooser.Choose();
        Debug.Log(_dropItemList[index]);
    }
}
