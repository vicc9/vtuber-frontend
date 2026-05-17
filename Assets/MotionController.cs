using UnityEngine;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.Expression;

/// <summary>
/// 控制 Live2D 動作與表情，由 StreamerClient / IdleStateManager 呼叫。
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
    public CubismMotionController     motionController;
    public CubismExpressionController expressionController;

    // ── 表情 Parameters ──
    private CubismParameter _paramCheek;
    private CubismParameter _paramEyeLSmile;
    private CubismParameter _paramEyeRSmile;
    private CubismParameter _paramBrowLY;
    private CubismParameter _paramBrowRY;
    private CubismParameter _paramEyeLOpen;   // 新增：眼睛開合（sleepy 用）
    private CubismParameter _paramEyeROpen;

    // ── 目標值（平滑插值）──
    private float _targetCheek;
    private float _targetEyeSmile;
    private float _targetBrowY;
    private float _targetEyeLOpen = 1f;
    private float _targetEyeROpen = 1f;

    // ── 目前實際值 ──
    private float _currentCheek;
    private float _currentEyeSmile;
    private float _currentBrowY;
    private float _currentEyeLOpen = 1f;
    private float _currentEyeROpen = 1f;

    private const float LerpSpeed = 5f;

    // ─────────────────────────────────────────
    void Awake()
    {
        if (motionController == null)
            motionController = GetComponent<CubismMotionController>();
        if (expressionController == null)
            expressionController = GetComponent<CubismExpressionController>();

        var model = GetComponent<CubismModel>();
        if (model != null)
        {
            _paramCheek     = model.Parameters.FindById("ParamCheek");
            _paramEyeLSmile = model.Parameters.FindById("ParamEyeLSmile");
            _paramEyeRSmile = model.Parameters.FindById("ParamEyeRSmile");
            _paramBrowLY    = model.Parameters.FindById("ParamBrowLY");
            _paramBrowRY    = model.Parameters.FindById("ParamBrowRY");
            _paramEyeLOpen  = model.Parameters.FindById("ParamEyeLOpen");
            _paramEyeROpen  = model.Parameters.FindById("ParamEyeROpen");
        }

        SetNeutral();
    }

    // ─────────────────────────────────────────
    void LateUpdate()
    {
        _currentCheek     = Mathf.Lerp(_currentCheek,     _targetCheek,     Time.deltaTime * LerpSpeed);
        _currentEyeSmile  = Mathf.Lerp(_currentEyeSmile,  _targetEyeSmile,  Time.deltaTime * LerpSpeed);
        _currentBrowY     = Mathf.Lerp(_currentBrowY,     _targetBrowY,     Time.deltaTime * LerpSpeed);
        _currentEyeLOpen  = Mathf.Lerp(_currentEyeLOpen,  _targetEyeLOpen,  Time.deltaTime * LerpSpeed);
        _currentEyeROpen  = Mathf.Lerp(_currentEyeROpen,  _targetEyeROpen,  Time.deltaTime * LerpSpeed);

        if (_paramCheek     != null) _paramCheek.Value     = _currentCheek;
        if (_paramEyeLSmile != null) _paramEyeLSmile.Value = _currentEyeSmile;
        if (_paramEyeRSmile != null) _paramEyeRSmile.Value = _currentEyeSmile;
        if (_paramBrowLY    != null) _paramBrowLY.Value    = _currentBrowY;
        if (_paramBrowRY    != null) _paramBrowRY.Value    = _currentBrowY;
        if (_paramEyeLOpen  != null) _paramEyeLOpen.Value  = _currentEyeLOpen;
        if (_paramEyeROpen  != null) _paramEyeROpen.Value  = _currentEyeROpen;
    }

    // ─────────────────────────────────────────
    // 公開方法
    // ─────────────────────────────────────────

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
            priority:   2,
            isLoop:     false,
            speed:      1.0f
        );
        Debug.Log($"🎬 播放動作: {actionName} → {clip.name}");
    }

    /// <summary>
    /// 設定表情（平滑過渡）。
    /// 支援：joy / anger / sadness / neutral / curious / sleepy
    /// 同時接受舊版名稱：happiness（→ joy）
    /// </summary>
    public void PlayExpression(string emotionName) => SetExpression(emotionName);

    /// <summary>SetExpression 與 PlayExpression 功能相同，供 IdleStateManager 呼叫</summary>
    public void SetExpression(string emotionName)
    {
        ResetTargets();

        switch (emotionName.ToLower())
        {
            case "joy":
            case "happiness":   // 相容舊版
                _targetCheek    = 1f;
                _targetEyeSmile = 1f;
                _targetBrowY    = 1f;
                break;

            case "anger":
                _targetCheek    = 0f;
                _targetEyeSmile = 0f;
                _targetBrowY    = -1f;
                break;

            case "sadness":
                _targetCheek    = 0f;
                _targetEyeSmile = 0f;
                _targetBrowY    = -0.5f;
                break;

            case "curious":     // 好奇：眉毛上揚 + 微微笑
                _targetBrowY    =  0.8f;
                _targetEyeSmile =  0.3f;
                break;

            case "sleepy":      // 睏：眼睛半閉 + 眉毛略下
                _targetEyeLOpen = 0.3f;
                _targetEyeROpen = 0.3f;
                _targetBrowY    = -0.3f;
                break;

            case "surprised":   // 驚訝：眉毛大幅上揚
                _targetBrowY    = 1f;
                _targetEyeLOpen = 1f;
                _targetEyeROpen = 1f;
                break;

            default:            // neutral
                break;          // 已在 ResetTargets 重置為預設值
        }

        Debug.Log($"😊 設定表情: {emotionName}");
    }

    /// <summary>外部直接控制嘴巴開合（LipSync 補充用）</summary>
    public void SetLipSync(float value)
    {
        // StreamerClient 的 LateUpdate 已直接寫 _mouthOpenY，
        // 此方法保留供其他腳本擴充使用。
    }

    // ─────────────────────────────────────────
    // 私有輔助
    // ─────────────────────────────────────────

    void ResetTargets()
    {
        _targetCheek    = 0f;
        _targetEyeSmile = 0f;
        _targetBrowY    = 0f;
        _targetEyeLOpen = 1f;   // 眼睛預設全開
        _targetEyeROpen = 1f;
    }

    void SetNeutral()
    {
        ResetTargets();
        // 把目前值也同步，避免啟動時插值過長
        _currentCheek    = 0f;
        _currentEyeSmile = 0f;
        _currentBrowY    = 0f;
        _currentEyeLOpen = 1f;
        _currentEyeROpen = 1f;
    }
}
