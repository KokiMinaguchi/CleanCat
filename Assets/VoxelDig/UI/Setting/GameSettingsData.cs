using System;
using UnityEngine;

/// <summary>
/// ゲームの設定値をまとめて持つだけのデータクラス。
/// JsonUtility で JSON に変換して保存するため [Serializable] を付けている。
/// ここに書いた初期値が「デフォルト設定」になる。
/// </summary>
[Serializable]
public class GameSettingsData
{
    // ===== 画面 =====

    // 画面モード（ボーダーレスフルスクリーン／排他フルスクリーン／ウィンドウ）
    // enum は JSON では数値として保存される
    public FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;

    // 解像度。0 は「まだ決まっていない」という意味で、
    // 初回起動時にモニターの解像度で上書きする
    public int resolutionWidth = 0;
    public int resolutionHeight = 0;

    // ===== フレームレート =====

    // 垂直同期。ON のときはモニターのリフレッシュレートに合わせる
    public bool vSync = true;

    // 垂直同期 OFF のときのフレームレート上限（-1 は無制限）
    public int targetFrameRate = 60;

    // ===== 音量（0〜1）=====
    public float masterVolume = 0.8f;
    public float bgmVolume = 0.8f;
    public float seVolume = 0.8f;

    // ===== 操作 =====

    // マウス・カメラ感度の倍率（1.0 が標準）
    public float mouseSensitivity = 1.0f;

    /// <summary>
    /// 自分のコピーを作る。
    /// 設定画面で「編集中の値」と「確定済みの値」を分けるために使う。
    /// （このクラスは数値と bool だけなので浅いコピーで十分）
    /// </summary>
    public GameSettingsData Clone()
    {
        return (GameSettingsData)MemberwiseClone();
    }

    /// <summary>
    /// 読み込んだ値が壊れていたときのために、範囲内に収める。
    /// </summary>
    public void Sanitize()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        bgmVolume = Mathf.Clamp01(bgmVolume);
        seVolume = Mathf.Clamp01(seVolume);
        mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.1f, 10f);

        // 解像度が未設定なら、今のモニターの解像度を使う
        if (resolutionWidth <= 0 || resolutionHeight <= 0)
        {
            resolutionWidth = Screen.currentResolution.width;
            resolutionHeight = Screen.currentResolution.height;
        }
    }
}
