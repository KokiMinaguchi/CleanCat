using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定画面の UI を操作するクラス。
/// UI で変更した値はすぐには確定せず、「適用」を押したときに保存する。
/// （音量だけは聞きながら調整できるように、動かした瞬間に試し聴きで反映する）
/// </summary>
public class SettingsMenuUI : MonoBehaviour
{
    [Header("パネル")]
    // 設定画面全体（開閉で表示・非表示を切り替える）
    [SerializeField] private GameObject panelRoot;

    [Header("画面")]
    [SerializeField] private TMP_Dropdown screenModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("フレームレート")]
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private TMP_Dropdown frameRateDropdown;

    [Header("音量（スライダーの範囲は 0〜1 にする）")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider seVolumeSlider;

    [Header("操作（スライダーの範囲は 0.1〜5 くらいがおすすめ）")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText; // 「1.0」のように数値を表示（無くてもOK）

    [Header("ボタン")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button closeButton;

    // 画面モードの選択肢（ドロップダウンの並び順と対応させる）
    private static readonly FullScreenMode[] ScreenModes =
    {
        FullScreenMode.FullScreenWindow,     // ボーダーレスフルスクリーン
        FullScreenMode.ExclusiveFullScreen,  // 排他フルスクリーン（Windows のみ）
        FullScreenMode.Windowed,             // ウィンドウ
    };

    private static readonly string[] ScreenModeLabels =
    {
        "フルスクリーン（ボーダーレス）",
        "フルスクリーン（排他）",
        "ウィンドウ",
    };

    // フレームレート上限の選択肢（-1 は無制限）
    private static readonly int[] FrameRates = { 30, 60, 120, 144, -1 };

    // 選べる解像度の一覧（モニターに合わせて実行時に作る）
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    // 編集中の設定（「適用」を押すまでは確定しない）
    private GameSettingsData editingData;

    private void Awake()
    {
        SetupStaticOptions();
        RegisterListeners();

        // 最初は閉じておく
        panelRoot.SetActive(false);
    }

    // ==============================
    // 開く・閉じる
    // ==============================

    /// <summary>設定画面を開く。ポーズメニューのボタンなどから呼ぶ。</summary>
    public void Open()
    {
        // 確定済みの設定をコピーして編集を始める
        editingData = GameSettingsManager.Instance.Current.Clone();

        BuildResolutionOptions();
        RefreshUI();

        panelRoot.SetActive(true);
    }

    /// <summary>
    /// 設定画面を閉じる。適用していない変更は捨てる。
    /// </summary>
    public void Close()
    {
        // 試し聴きで変えた音量を、確定済みの値に戻す
        GameSettingsManager.Instance.ApplyVolumes(GameSettingsManager.Instance.Current);

        panelRoot.SetActive(false);
    }

    // ==============================
    // 初期化
    // ==============================

    /// <summary>内容が変わらないドロップダウンの選択肢を作る。</summary>
    private void SetupStaticOptions()
    {
        // 画面モード
        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(new List<string>(ScreenModeLabels));

        // フレームレート
        var frameRateLabels = new List<string>();
        foreach (int fps in FrameRates)
        {
            frameRateLabels.Add(fps < 0 ? "無制限" : $"{fps} FPS");
        }
        frameRateDropdown.ClearOptions();
        frameRateDropdown.AddOptions(frameRateLabels);
    }

    /// <summary>モニターが対応している解像度から、ドロップダウンの選択肢を作る。</summary>
    private void BuildResolutionOptions()
    {
        resolutions.Clear();

        // Screen.resolutions には同じ解像度がリフレッシュレート違いで複数入っているので、重複を除く
        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(size))
            {
                resolutions.Add(size);
            }
        }

        // 保存されている解像度が一覧に無い場合も選べるように追加しておく
        var saved = new Vector2Int(editingData.resolutionWidth, editingData.resolutionHeight);
        if (!resolutions.Contains(saved))
        {
            resolutions.Add(saved);
        }

        // 小さい順に並べる
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        var labels = new List<string>();
        foreach (Vector2Int size in resolutions)
        {
            labels.Add($"{size.x} × {size.y}");
        }
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
    }

    /// <summary>UI が操作されたときに呼ばれる処理を登録する。</summary>
    private void RegisterListeners()
    {
        // ラムダ式（=> の部分）で「値が変わったら editingData に書き込む」処理を登録している
        screenModeDropdown.onValueChanged.AddListener(index =>
        {
            editingData.fullScreenMode = ScreenModes[index];
        });

        resolutionDropdown.onValueChanged.AddListener(index =>
        {
            editingData.resolutionWidth = resolutions[index].x;
            editingData.resolutionHeight = resolutions[index].y;
        });

        vSyncToggle.onValueChanged.AddListener(isOn =>
        {
            editingData.vSync = isOn;
            // 垂直同期 ON のときはフレームレート上限は使わないので、触れないようにする
            frameRateDropdown.interactable = !isOn;
        });

        frameRateDropdown.onValueChanged.AddListener(index =>
        {
            editingData.targetFrameRate = FrameRates[index];
        });

        // 音量は動かした瞬間に試し聴きできるよう、すぐ反映する（保存はまだしない）
        masterVolumeSlider.onValueChanged.AddListener(value =>
        {
            editingData.masterVolume = value;
            GameSettingsManager.Instance.ApplyVolumes(editingData);
        });

        bgmVolumeSlider.onValueChanged.AddListener(value =>
        {
            editingData.bgmVolume = value;
            GameSettingsManager.Instance.ApplyVolumes(editingData);
        });

        seVolumeSlider.onValueChanged.AddListener(value =>
        {
            editingData.seVolume = value;
            GameSettingsManager.Instance.ApplyVolumes(editingData);
        });

        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            editingData.mouseSensitivity = value;
            UpdateSensitivityText();
        });

        applyButton.onClick.AddListener(OnApplyClicked);
        resetButton.onClick.AddListener(OnResetClicked);
        closeButton.onClick.AddListener(Close);
    }

    // ==============================
    // 表示の更新
    // ==============================

    /// <summary>editingData の内容を UI に表示する。</summary>
    private void RefreshUI()
    {
        // SetValueWithoutNotify を使うと、値を変えても onValueChanged が呼ばれない。
        // （ここで呼ばれると、表示を更新しただけなのに処理が走ってしまうため）

        screenModeDropdown.SetValueWithoutNotify(
            Mathf.Max(0, System.Array.IndexOf(ScreenModes, editingData.fullScreenMode)));

        var savedSize = new Vector2Int(editingData.resolutionWidth, editingData.resolutionHeight);
        resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, resolutions.IndexOf(savedSize)));

        vSyncToggle.SetIsOnWithoutNotify(editingData.vSync);
        frameRateDropdown.SetValueWithoutNotify(
            Mathf.Max(0, System.Array.IndexOf(FrameRates, editingData.targetFrameRate)));
        frameRateDropdown.interactable = !editingData.vSync;

        masterVolumeSlider.SetValueWithoutNotify(editingData.masterVolume);
        bgmVolumeSlider.SetValueWithoutNotify(editingData.bgmVolume);
        seVolumeSlider.SetValueWithoutNotify(editingData.seVolume);

        sensitivitySlider.SetValueWithoutNotify(editingData.mouseSensitivity);
        UpdateSensitivityText();
    }

    /// <summary>感度の数値表示を更新する。</summary>
    private void UpdateSensitivityText()
    {
        if (sensitivityValueText != null)
        {
            // F1 = 小数第1位まで表示
            sensitivityValueText.text = editingData.mouseSensitivity.ToString("F1");
        }
    }

    // ==============================
    // ボタン
    // ==============================

    /// <summary>「適用」：編集中の設定を確定して保存する。</summary>
    private void OnApplyClicked()
    {
        GameSettingsManager.Instance.ApplyAndSave(editingData);
    }

    /// <summary>
    /// 「初期設定に戻す」：UI の値をデフォルトに戻す。
    /// 確定するには「適用」を押す必要がある。
    /// </summary>
    private void OnResetClicked()
    {
        editingData = GameSettingsManager.Instance.CreateDefault();

        BuildResolutionOptions();
        RefreshUI();

        // 音量は試し聴きとしてすぐ反映
        GameSettingsManager.Instance.ApplyVolumes(editingData);
    }
}
