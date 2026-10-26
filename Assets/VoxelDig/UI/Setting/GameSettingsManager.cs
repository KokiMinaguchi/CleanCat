using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 設定の「読み込み・保存・反映」を担当するクラス。
/// 最初のシーン（タイトルなど）に1つだけ置く。
/// シーンをまたいでも消えないので、どこからでも Instance で使える。
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    // どこからでもアクセスできるようにするための static 参照（シングルトン）
    public static GameSettingsManager Instance { get; private set; }

    [Header("オーディオ")]
    // 音量を変えるための AudioMixer（Master / BGM / SE のグループを作っておく）
    [SerializeField] private AudioMixer audioMixer;

    // AudioMixer で「Expose」したパラメーター名。Mixer 側の名前と完全に一致させること
    [SerializeField] private string masterVolumeParam = "Master";
    [SerializeField] private string bgmVolumeParam = "BGM";
    [SerializeField] private string seVolumeParam = "SE";

    // 保存ファイル名
    private const string SaveFileName = "settings.json";

    /// <summary>現在確定している設定（読み取り専用で使うこと）</summary>
    public GameSettingsData Current { get; private set; }

    /// <summary>
    /// 設定が適用されたときに呼ばれるイベント。
    /// 感度など、他のスクリプトで設定を使う場合はこれを購読する。
    /// </summary>
    public event Action<GameSettingsData> OnSettingsApplied;

    // 保存先のフルパス（PC だと AppData/LocalLow/会社名/製品名/ の中）
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        // すでに別の Manager があれば、自分は不要なので消える
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // シーンを切り替えても消さない

        Load();
    }

    private void Start()
    {
        // 注意：AudioMixer.SetFloat は Awake で呼ぶと反映されないことがあるため、
        // 反映は Start で行う
        ApplyAll(Current);
        OnSettingsApplied?.Invoke(Current);
    }

    // ==============================
    // 読み込み・保存
    // ==============================

    /// <summary>ファイルから設定を読み込む。ファイルが無い／壊れている場合はデフォルト値。</summary>
    public void Load()
    {
        GameSettingsData data = null;

        try
        {
            if (File.Exists(SaveFilePath))
            {
                string json = File.ReadAllText(SaveFilePath);
                data = JsonUtility.FromJson<GameSettingsData>(json);
            }
        }
        catch (Exception e)
        {
            // 読み込みに失敗してもゲームは止めず、デフォルト値で続行する
            Debug.LogWarning($"設定の読み込みに失敗しました。デフォルト値を使います: {e.Message}");
        }

        // 読み込めなかったら new してデフォルト値にする
        Current = data ?? new GameSettingsData();
        Current.Sanitize();
    }

    /// <summary>現在の設定をファイルに保存する。</summary>
    public void Save()
    {
        try
        {
            // true にすると改行付きの読みやすい JSON になる
            string json = JsonUtility.ToJson(Current, true);
            File.WriteAllText(SaveFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"設定の保存に失敗しました: {e.Message}");
        }
    }

    /// <summary>
    /// 新しい設定を確定して、反映・保存する。
    /// 設定画面の「適用」ボタンから呼ぶ。
    /// </summary>
    public void ApplyAndSave(GameSettingsData newData)
    {
        // 渡されたデータをそのまま使うと、UI 側で編集したときに勝手に変わってしまうのでコピーする
        Current = newData.Clone();
        Current.Sanitize();

        ApplyAll(Current);
        Save();

        OnSettingsApplied?.Invoke(Current);
    }

    /// <summary>デフォルト設定のデータを新しく作って返す（まだ反映はしない）。</summary>
    public GameSettingsData CreateDefault()
    {
        var data = new GameSettingsData();
        data.Sanitize();
        return data;
    }

    // ==============================
    // 反映
    // ==============================

    /// <summary>すべての設定をゲームに反映する。</summary>
    public void ApplyAll(GameSettingsData data)
    {
        ApplyScreen(data);
        ApplyFrameRate(data);
        ApplyVolumes(data);
    }

    /// <summary>画面モードと解像度を反映する。</summary>
    public void ApplyScreen(GameSettingsData data)
    {
        // ※エディター上では解像度は変わらない。ビルドして確認すること
        Screen.SetResolution(data.resolutionWidth, data.resolutionHeight, data.fullScreenMode);
    }

    /// <summary>垂直同期とフレームレート上限を反映する。</summary>
    public void ApplyFrameRate(GameSettingsData data)
    {
        if (data.vSync)
        {
            // 垂直同期 ON：モニターに合わせるので targetFrameRate は使わない
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
        else
        {
            // 垂直同期 OFF：指定したフレームレートを上限にする（-1 は無制限）
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = data.targetFrameRate;
        }
    }

    /// <summary>
    /// 音量を反映する。
    /// 設定画面でスライダーを動かしたときの「試し聴き」にも使うので public にしている。
    /// </summary>
    public void ApplyVolumes(GameSettingsData data)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioMixer が設定されていません。");
            return;
        }

        audioMixer.SetFloat(masterVolumeParam, LinearToDecibel(data.masterVolume));
        audioMixer.SetFloat(bgmVolumeParam, LinearToDecibel(data.bgmVolume));
        audioMixer.SetFloat(seVolumeParam, LinearToDecibel(data.seVolume));
    }

    /// <summary>
    /// 0〜1 の音量を AudioMixer 用のデシベル（-80〜0dB）に変換する。
    /// 人の耳は音量を対数で感じるので、そのまま線形に変えると
    /// 「スライダーの下の方だけで音量が急に変わる」感じになってしまう。
    /// </summary>
    public static float LinearToDecibel(float volume)
    {
        // 0 だと Log10 が計算できないので、ごく小さい値にしておく（= ほぼ無音の -80dB）
        return Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;
    }
}
