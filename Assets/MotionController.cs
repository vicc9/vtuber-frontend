using UnityEngine;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.Expression;

/// <summary>
/// 控制 Live2D 動作與表情，由 StreamerClient 呼叫
/// </summary>
public class MotionController : MonoBehaviour
{
    // ── 動作 AnimationClip ──
    [Header("動作 Clips（從 Project 拖入）")]
    public AnimationClip clipWave;    // hiyori_m01
    public AnimationClip clipNod;     // hiyori_m02
    public AnimationClip clipThink;   // hiyori_m05

    // ── Live2D Controllers ──
    [Header("Live2D Controllers（自動偵測，可留空）")]
    public CubismMotionController motionController;
    public CubismExpressionController expressionController;

    // ── 表情 Parameters ──
    private CubismParameter _paramCheek;
    private CubismParameter _paramEyeLSmile;
    private CubismParameter _paramEyeRSmile;
    private CubismParameter _paramBrowLY;
    private CubismParameter _paramBrowRY;

    // 表情目標值（平滑插值用）
    private float _targetCheek;
    private float _targetEyeSmile;
    private float _targetBrowY;

    // 目前實際值
    private float _currentCheek;
    private float _currentEyeSmile;
    private float _currentBrowY;

    private const float LerpSpeed = 5f;

    void Awake()
    {
        // 自動尋找 Controllers
        if (motionController == null)
            motionController = GetComponent<CubismMotionController>();
        if (expressionController == null)
            expressionController = GetComponent<CubismExpressionController>();

        // 取得 CubismModel 的 Parameters
        var model = GetComponent<CubismModel>();
        if (model != null)
        {
            _paramCheek     = model.Parameters.FindById("ParamCheek");
            _paramEyeLSmile = model.Parameters.FindById("ParamEyeLSmile");
            _paramEyeRSmile = model.Parameters.FindById("ParamEyeRSmile");
            _paramBrowLY    = model.Parameters.FindById("ParamBrowLY");
            _paramBrowRY    = model.Parameters.FindById("ParamBrowRY");
        }

        SetNeutral();
    }

    void LateUpdate()
    {
        // 平滑插值，讓表情變化自然
        _currentCheek    = Mathf.Lerp(_currentCheek,    _targetCheek,    Time.deltaTime * LerpSpeed);
        _currentEyeSmile = Mathf.Lerp(_currentEyeSmile, _targetEyeSmile, Time.deltaTime * LerpSpeed);
        _currentBrowY    = Mathf.Lerp(_currentBrowY,    _targetBrowY,    Time.deltaTime * LerpSpeed);

        if (_paramCheek     != null) _paramCheek.Value     = _currentCheek;
        if (_paramEyeLSmile != null) _paramEyeLSmile.Value = _currentEyeSmile;
        if (_paramEyeRSmile != null) _paramEyeRSmile.Value = _currentEyeSmile;
        if (_paramBrowLY    != null) _paramBrowLY.Value    = _currentBrowY;
        if (_paramBrowRY    != null) _paramBrowRY.Value    = _currentBrowY;
    }

    // ────────────────────────────────
    // 公開方法：由 StreamerClient 呼叫
    // ────────────────────────────────

    /// <summary>播放動作 AnimationClip</summary>
    public void PlayMotion(string actionName)
    {
        if (motionController == null)
        {
            Debug.LogWarning("⚠ MotionController: CubismMotionController 未找到");
            return;
        }

        AnimationClip clip = actionName switch
        {
            "Wave"  => clipWave,
            "Nod"   => clipNod,
            "Think" => clipThink,
            _       => null
        };

        if (clip == null)
        {
            Debug.Log($"🎬 動作 [{actionName}] 無對應 clip，略過");
            return;
        }

        motionController.PlayAnimation(
            clip,
            layerIndex: 0,
            priority: 2,        // PriorityNormal
            isLoop: false,
            speed: 1.0f
        );
        Debug.Log($"🎬 播放動作: {actionName} → {clip.name}");
    }

    /// <summary>設定表情（平滑過渡）</summary>
    public void PlayExpression(string emotionName)
    {
        switch (emotionName)
        {
            case "joy":
                _targetCheek    = 1f;
                _targetEyeSmile = 1f;
                _targetBrowY    = 1f;   // 眉毛上揚
                break;
            case "anger":
                _targetCheek    = 0f;
                _targetEyeSmile = 0f;
                _targetBrowY    = -1f;  // 眉毛下壓
                break;
            case "sadness":
                _targetCheek    = 0f;
                _targetEyeSmile = 0f;
                _targetBrowY    = -0.5f;
                break;
            default: // neutral
                SetNeutral();
                break;
        }
        Debug.Log($"😊 設定表情: {emotionName}");
    }

    private void SetNeutral()
    {
        _targetCheek    = 0f;
        _targetEyeSmile = 0f;
        _targetBrowY    = 0f;
    }
}