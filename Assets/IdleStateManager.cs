// Assets/Scripts/IdleStateManager.cs
// 管理 AI 待機狀態的 UI 表現。
// 實際計時由 Python 後端控制，這裡只負責接收 stage 並更新表情。

using UnityEngine;

public class IdleStateManager : MonoBehaviour
{
    [Header("References")]
    public MotionController motionController;
    public UIManager        uiManager;

    /// <summary>
    /// 由 StreamerClient 收到 is_idle=true 時呼叫
    /// </summary>
    public void OnIdleResponse(string stage, string emotion, string action)
    {
        Debug.Log($"[IdleState] stage={stage}, emotion={emotion}, action={action}");
        ApplyIdleEmotion(stage);
        // 動作由 StreamerClient 統一在語音播放時觸發
    }

    void ApplyIdleEmotion(string stage)
    {
        switch (stage)
        {
            case "first":
                // 好奇：眉毛上揚 + 微笑
                motionController.SetExpression("curious");
                break;
            case "bored":
                // 無聊：眉毛下垂
                motionController.SetExpression("sad");
                break;
            case "sleepy":
                // 睏：眼睛半閉
                motionController.SetExpression("sleepy");
                break;
            default:
                motionController.SetExpression("neutral");
                break;
        }
    }
}