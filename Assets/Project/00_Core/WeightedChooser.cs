using UnityEngine;

/// <summary>
/// �d�ݕt���m�����I�N���X
/// </summary>
public class WeightedChooser
{
    // �e�v�f�̏d�݃��X�g
    private float[] _weights;

    // �d�݂̑��a�i���������Ɍv�Z�����j
    private float _totalWeight;

    /// <summary>
    /// �R���X�g���N�^
    /// </summary>
    /// <param name="weights">�d�݃��X�g</param>
    public WeightedChooser(float[] weights)
    {
        _weights = weights;

        // �d�݂̑��a�v�Z
        for (var i = 0; i < _weights.Length; i++)
        {
            _totalWeight += _weights[i];
        }
    }

    /// <summary>
    /// �d�ݕt���̊m�����I�����{����
    /// </summary>
    /// <returns>�I�����ꂽ�v�f�̃C���f�b�N�X</returns>
    public int Choose()
    {
        // 0�`�d�݂̑��a�͈̗̔͂����l�擾
        var randomPoint = Random.Range(0, _totalWeight);
        //Debug.Log(randomPoint);
        // �����l��������v�f��擪���珇�ɑI��
        var currentWeight = 0f;
        for (var i = 0; i < _weights.Length; i++)
        {
            // ���ݗv�f�܂ł̏d�݂̑��a�����߂�
            currentWeight += _weights[i];

            // �����l�����ݗv�f�͈͓̔����`�F�b�N
            if (randomPoint < currentWeight)
            {
                return i;
            }
        }

        // �����l���d�݂̑��a�ȏ�Ȃ疖���v�f�Ƃ���
        return _weights.Length - 1;
    }
}