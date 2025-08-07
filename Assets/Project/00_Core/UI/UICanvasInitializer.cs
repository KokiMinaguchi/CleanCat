using UnityEngine;
using UnityEngine.UI;

public class UICanvasInitializer : MonoBehaviour
{
    private CanvasScaler _scaler;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        _scaler.referenceResolution = new Vector2(1920f, 1080f);
    }
}
