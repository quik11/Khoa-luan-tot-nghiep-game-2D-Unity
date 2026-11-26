using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    internal class AnimationStrings
{
    internal static string isMoving = "isMoving";
    internal static string isRunning = "isRunning";
    internal static string isGrounded = "isGrounded";

    internal static string yVelocity = "yVelocity";

    internal static string jump = "jump";
    internal static string isOnWall = "isOnWall";
    internal static string isOnCeiling = "isOnCeiling";
    internal static string attackTrigger = "attack";
    internal static string canMove = "canMove";
    internal static string hasTarget = "hasTarget";
    internal static string isAlive = "isAlive";
    internal static string hitTrigger = "hit";
    internal static string lockVelocity = "lockVelocity";
    internal static string attackCooldown = "attackCooldown";
    internal static string rangedAttackTrigger = "rangedAttack";
    internal static string webCastTrigger = "webCast"; // chưa có state nào nhận - đợi bạn vẽ animation xong rồi tự nối trigger này vào Animator
    internal static string heavyAttackState = "GroundAttack.player_attack4"; // đòn mạnh phím I tạm dùng lại đòn 4 cũ (đã tách khỏi combo), vẽ animation riêng thì đổi tên state ở đây
    internal static string dodgeTrigger = "dodge"; // chưa có state nào nhận - đợi bạn vẽ animation né tránh xong rồi tự nối trigger này vào Animator
    internal static string isWebPullFlying = "isWebPullFlying"; // bật suốt lúc đang bay theo tơ kéo - nối vào state "bay bằng tơ" trong Animator
    internal static string isStunned = "isStunned"; // Quỷ gối đang bị trói - Animator giữ dáng bị trói suốt lúc bool này bật
    internal static string hiepSiStunnedState = "HiepSi_stunned"; // state dáng bị trói của Quỷ gối (công cụ Editor tự tạo)
    internal static string hiepSiAttackState = "HiepSi_attack"; // đòn đánh 2 nhát của Quỷ gối (nháy đỏ báo trước từng nhát)
    internal static string flyingEyeAttackState = "FlyingEye_Attack"; // cắn + quật của quái bay
}
