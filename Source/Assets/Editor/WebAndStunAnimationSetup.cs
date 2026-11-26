using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tự gắn các hình đã vẽ + cắt sẵn vào Animator (làm giống công cụ lộn né):
//  - Nhân vật: dáng bắn tơ cho cả tơ đạn R lẫn tơ kéo Q: đứng đất (webshoot/neushooti.png) và đang nhảy (webshoot/neushootiair.png), mỗi cái 5 khung;
//    dáng bay theo tơ kéo (webflying, 1 khung) suốt lúc isWebPullFlying bật;
//    clip player_jump (1 khung lúc vừa bấm nhảy) đang trỏ vào ảnh đã bị xóa -> đổi sang ảnh rising.
//  - Quỷ gối: dáng bị trói (strappedpillow.png, 5 khung): chạy 1 lần rồi đứng ở khung cuối suốt lúc bị trói (HiepSi.cs lo phần còn lại).
// Mở Unity là tự chạy 1 lần cho phần nào chưa có. Muốn làm lại: menu Brand NEU day > Gắn animation bắn tơ, bay tơ, Quỷ gối bị trói.
[InitializeOnLoad]
public static class WebAndStunAnimationSetup
{
    private const string playerControllerPath = "Assets/Characters/Player/AC_Player.controller";
    private const string pillowControllerPath = "Assets/Characters/Enemies/HiepSi/HiepSiAnimatorController.controller";

    private const string webShootSheet = "Assets/Art/NEUNEU/webshoot/neushooti.png";
    private const string webShootAirSheet = "Assets/Art/NEUNEU/webshoot/neushootiair.png";
    private const string webFlySheet = "Assets/Art/NEUNEU/webflying/neuwebflying-1.png.png";
    private const string risingSheet = "Assets/Art/NEUNEU/rising/neurising 1.png";
    private const string strappedSheet = "Assets/Art/Pillow/strappedpillow.png";

    private const string webCastClipPath = "Assets/Characters/Player/player_webcast.anim";
    private const string webCastAirClipPath = "Assets/Characters/Player/player_webcast_air.anim";
    private const string webFlyClipPath = "Assets/Characters/Player/player_webpull_fly.anim";
    private const string jumpClipPath = "Assets/Characters/Player/player_jump.anim";
    private const string stunnedClipPath = "Assets/Characters/Enemies/HiepSi/HiepSi_stunned.anim";

    private const string webCastState = "player_webcast";
    private const string webCastAirState = "player_webcast_air";
    private const string webFlyState = "player_webpull_fly";
    private const string stunnedState = "HiepSi_stunned";

    // tên tham số Animator, phải trùng AnimationStrings (lớp đó là internal của phần game nên script Editor không đọc được)
    private const string webCastParam = "webCast";
    private const string webFlyParam = "isWebPullFlying";
    private const string isStunnedParam = "isStunned";
    private const string isAliveParam = "isAlive";
    private const string isGroundedParam = "isGrounded";
    private const string jumpParam = "jump";

    // 5 khung trong 0.2 giây = đúng thời gian đứng khựng khi bắn tơ dưới đất (webCastLockDuration)
    private const float webCastFrameRate = 25f;
    private const float webFlyFrameRate = 10f;
    // 5 khung ~0.4 giây: tơ quấn + bung tơ, xong đứng ở khung cuối (trói mặc định 2 giây)
    private const float stunnedFrameRate = 12f;

    static WebAndStunAnimationSetup()
    {
        // chờ Unity nạp xong asset rồi mới làm, đang Play thì thôi
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            AnimatorController player = AssetDatabase.LoadAssetAtPath<AnimatorController>(playerControllerPath);
            if (player != null && FindState(player.layers[0].stateMachine, webCastAirState) == null) SetupPlayer(false);

            AnimatorController pillow = AssetDatabase.LoadAssetAtPath<AnimatorController>(pillowControllerPath);
            if (pillow != null && FindState(pillow.layers[0].stateMachine, stunnedState) == null) SetupPillow(false);
        };
    }

    [MenuItem("Brand NEU day/Gắn animation bắn tơ, bay tơ, Quỷ gối bị trói")]
    public static void SetupAll()
    {
        SetupPlayer(true);
        SetupPillow(true);
    }

    // ===================== nhân vật =====================

    private static void SetupPlayer(bool fromMenu)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(playerControllerPath);
        if (controller == null)
        {
            Debug.LogError("Gắn bắn tơ: không thấy " + playerControllerPath);
            return;
        }

        Sprite[] shootSprites = LoadSprites(webShootSheet);
        Sprite[] shootAirSprites = LoadSprites(webShootAirSheet);
        Sprite[] flySprites = LoadSprites(webFlySheet);
        if (shootSprites.Length == 0 || shootAirSprites.Length == 0 || flySprites.Length == 0)
        {
            Debug.LogError("Gắn bắn tơ: thiếu khung hình trong " + webShootSheet + ", " + webShootAirSheet + " hoặc " + webFlySheet
                           + " (ảnh phải được cắt thành sprite).");
            return;
        }

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine groundStates = FindChildMachine(root, "GroundStates");
        AnimatorStateMachine airStates = FindChildMachine(root, "AirStates");
        if (groundStates == null || airStates == null)
        {
            Debug.LogError("Gắn bắn tơ: Animator thiếu nhóm GroundStates hoặc AirStates.");
            return;
        }
        AnimatorState jumpState = FindState(airStates, "player_jump");

        AnimationClip castClip = CreateOrUpdateClip(webCastClipPath, shootSprites, webCastFrameRate, false);
        AnimationClip castAirClip = CreateOrUpdateClip(webCastAirClipPath, shootAirSprites, webCastFrameRate, false);
        AnimationClip flyClip = CreateOrUpdateClip(webFlyClipPath, flySprites, webFlyFrameRate, true);

        AddParameterIfMissing(controller, webCastParam, AnimatorControllerParameterType.Trigger);
        AddParameterIfMissing(controller, webFlyParam, AnimatorControllerParameterType.Bool);

        // --- bắn tơ: dáng đứng đất và dáng đang nhảy ---
        AnimatorState cast = GetOrAddState(root, webCastState, new Vector3(250f, -220f, 0f));
        cast.motion = castClip;
        cast.writeDefaultValues = true;
        cast.speed = 1f;

        AnimatorState castAir = GetOrAddState(root, webCastAirState, new Vector3(250f, -360f, 0f));
        castAir.motion = castAirClip;
        castAir.writeDefaultValues = true;
        castAir.speed = 1f;

        AnimatorState fly = GetOrAddState(root, webFlyState, new Vector3(250f, -290f, 0f));
        fly.motion = flyClip;
        fly.writeDefaultValues = true;
        fly.speed = 1f;

        // mũi tên Any State của 3 state này: xóa rồi tạo lại theo đúng thứ tự (bản cũ chỉ có 1 dáng bắn chung).
        // Thêm vào cuối danh sách nên mũi tên chết / trúng đòn / lộn né có sẵn vẫn được xét trước.
        // Bắn tơ phải đứng trước bay tơ: bấm Q trúng tường thì khung đầu vào dáng bắn (ăn luôn trigger webCast), khung sau sang dáng bay.
        RemoveAnyStateTransitions(root, cast);
        RemoveAnyStateTransitions(root, castAir);
        RemoveAnyStateTransitions(root, fly);
        AddCastEntry(root, cast, AnimatorConditionMode.If);
        AddCastEntry(root, castAir, AnimatorConditionMode.IfNot);

        AnimatorStateTransition flyEnter = root.AddAnyStateTransition(fly);
        flyEnter.AddCondition(AnimatorConditionMode.If, 0f, webFlyParam);
        flyEnter.AddCondition(AnimatorConditionMode.If, 0f, isAliveParam);
        flyEnter.hasExitTime = false;
        flyEnter.duration = 0f;
        flyEnter.canTransitionToSelf = false; // bắt buộc: không thì mỗi khung hình lại vào lại từ đầu

        // bắn dưới đất: nhảy ngay trong lúc khựng thì vào dáng nhảy luôn (mũi tên này đầu danh sách), không thì chạy hết dáng bắn
        ClearTransitions(cast);
        AddJumpExit(cast, jumpState);
        AddExitByGround(cast, groundStates, AnimatorConditionMode.If, true);
        AddExitByGround(cast, airStates, AnimatorConditionMode.IfNot, true);

        // bắn trên không: chạm đất là thôi dáng bắn ngay (khỏi trượt dáng bay trên mặt đất), bật tường thì vào dáng nhảy
        ClearTransitions(castAir);
        AddJumpExit(castAir, jumpState);
        AddExitByGround(castAir, groundStates, AnimatorConditionMode.If, false);
        AddExitByGround(castAir, airStates, AnimatorConditionMode.IfNot, true);

        // bay theo tơ kéo: tới nơi (bool tắt) thì về dưới đất / trên không
        ClearTransitions(fly);
        AnimatorStateTransition flyToGround = AddExitByGround(fly, groundStates, AnimatorConditionMode.If, false);
        flyToGround.AddCondition(AnimatorConditionMode.IfNot, 0f, webFlyParam);
        AnimatorStateTransition flyToAir = AddExitByGround(fly, airStates, AnimatorConditionMode.IfNot, false);
        flyToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, webFlyParam);

        // --- player_jump đang trỏ vào ảnh đã xóa (khung trống lúc vừa nhảy) -> dùng ảnh rising ---
        FixJumpClip();

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Gắn bắn tơ: xong - state " + webCastState + " (" + shootSprites.Length + " khung), " + webCastAirState
                  + " (" + shootAirSprites.Length + " khung) và " + webFlyState
                  + " trong " + playerControllerPath + "." + (fromMenu ? " (chạy lại từ menu)" : ""));
    }

    private static void FixJumpClip()
    {
        AnimationClip jumpClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(jumpClipPath);
        Sprite[] rising = LoadSprites(risingSheet);
        if (jumpClip == null || rising.Length == 0) return;

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(jumpClip, binding);
        bool broken = keys == null || keys.Length == 0;
        if (!broken)
        {
            foreach (ObjectReferenceKeyframe key in keys)
            {
                if (key.value == null) broken = true;
            }
        }
        if (!broken) return; // đã tự gắn hình khác thì để nguyên

        AnimationUtility.SetObjectReferenceCurve(jumpClip, binding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = rising[0] }
        });
        EditorUtility.SetDirty(jumpClip);
    }

    // ===================== Quỷ gối =====================

    private static void SetupPillow(bool fromMenu)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(pillowControllerPath);
        if (controller == null)
        {
            Debug.LogError("Gắn dáng bị trói: không thấy " + pillowControllerPath);
            return;
        }

        Sprite[] sprites = LoadSprites(strappedSheet);
        if (sprites.Length == 0)
        {
            Debug.LogError("Gắn dáng bị trói: không thấy khung hình nào trong " + strappedSheet + " (ảnh phải được cắt thành sprite).");
            return;
        }

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine move = FindChildMachine(root, "Move");
        if (move == null)
        {
            Debug.LogError("Gắn dáng bị trói: Animator Quỷ gối thiếu nhóm Move - không biết hết trói thì về đâu.");
            return;
        }

        // không lặp: chạy hết khung tơ bung ra rồi đứng yên ở khung cuối (dáng bị trói)
        AnimationClip clip = CreateOrUpdateClip(stunnedClipPath, sprites, stunnedFrameRate, false);
        AddParameterIfMissing(controller, isStunnedParam, AnimatorControllerParameterType.Bool);

        AnimatorState stunned = GetOrAddState(root, stunnedState, new Vector3(510f, 240f, 0f));
        stunned.motion = clip;
        stunned.writeDefaultValues = true;
        stunned.speed = 1f;

        // Any State -> bị trói (để sau mũi tên chết và trúng đòn có sẵn: bị trói vẫn nghe tiếng đánh, chết vẫn ưu tiên)
        if (!HasAnyStateTransition(root, stunned))
        {
            AnimatorStateTransition enter = root.AddAnyStateTransition(stunned);
            enter.AddCondition(AnimatorConditionMode.If, 0f, isStunnedParam);
            enter.AddCondition(AnimatorConditionMode.If, 0f, isAliveParam);
            enter.hasExitTime = false;
            enter.duration = 0f;
            enter.canTransitionToSelf = false; // bắt buộc: không thì mỗi khung hình lại bung tơ lại từ đầu
        }

        // hết trói -> về đi tuần (nhóm Move); đang có người chơi trong tầm thì nhóm Move tự chuyển sang đánh như cũ
        ClearTransitions(stunned);
        AnimatorStateTransition exit = stunned.AddTransition(move);
        exit.AddCondition(AnimatorConditionMode.IfNot, 0f, isStunnedParam);
        exit.hasExitTime = false;
        exit.duration = 0f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Gắn dáng bị trói: xong - clip " + stunnedClipPath + " (" + sprites.Length + " khung), state " + stunnedState
                  + " trong " + pillowControllerPath + "." + (fromMenu ? " (chạy lại từ menu)" : ""));
    }

    // Any State -> dáng bắn tơ: trigger webCast (code gửi cả lúc bấm R lẫn Q), đứng đất / đang nhảy, còn sống
    private static void AddCastEntry(AnimatorStateMachine root, AnimatorState target, AnimatorConditionMode groundedMode)
    {
        AnimatorStateTransition enter = root.AddAnyStateTransition(target);
        enter.AddCondition(AnimatorConditionMode.If, 0f, webCastParam);
        enter.AddCondition(groundedMode, 0f, isGroundedParam);
        enter.AddCondition(AnimatorConditionMode.If, 0f, isAliveParam);
        enter.hasExitTime = false;
        enter.duration = 0f;
        enter.canTransitionToSelf = true; // bắn liên tiếp thì dáng chạy lại từ đầu
    }

    private static void AddJumpExit(AnimatorState from, AnimatorState jumpState)
    {
        if (jumpState == null) return;
        AnimatorStateTransition toJump = from.AddTransition(jumpState);
        toJump.AddCondition(AnimatorConditionMode.If, 0f, jumpParam);
        toJump.hasExitTime = false;
        toJump.duration = 0f;
    }

    private static void RemoveAnyStateTransitions(AnimatorStateMachine root, AnimatorState target)
    {
        foreach (AnimatorStateTransition transition in root.anyStateTransitions)
        {
            if (transition.destinationState == target) root.RemoveAnyStateTransition(transition);
        }
    }

    // ===================== dùng chung =====================

    private static AnimatorStateTransition AddExitByGround(AnimatorState from, AnimatorStateMachine to, AnimatorConditionMode groundedMode, bool waitClipEnd)
    {
        AnimatorStateTransition exit = from.AddTransition(to);
        exit.AddCondition(groundedMode, 0f, isGroundedParam);
        exit.hasExitTime = waitClipEnd;
        exit.exitTime = 1f; // chạy hết clip mới ra
        exit.duration = 0f;
        return exit;
    }

    private static void ClearTransitions(AnimatorState state)
    {
        foreach (AnimatorStateTransition old in state.transitions)
        {
            state.RemoveTransition(old);
        }
    }

    private static bool HasAnyStateTransition(AnimatorStateMachine root, AnimatorState target)
    {
        foreach (AnimatorStateTransition transition in root.anyStateTransitions)
        {
            if (transition.destinationState == target) return true;
        }
        return false;
    }

    private static AnimatorState GetOrAddState(AnimatorStateMachine root, string name, Vector3 position)
    {
        AnimatorState state = FindState(root, name);
        return state != null ? state : root.AddState(name, position);
    }

    private static Sprite[] LoadSprites(string sheetPath)
    {
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null) sprites.Add(sprite);
        }
        sprites.Sort((a, b) => a.rect.x.CompareTo(b.rect.x)); // trái sang phải đúng thứ tự khung
        return sprites.ToArray();
    }

    private static AnimationClip CreateOrUpdateClip(string clipPath, Sprite[] sprites, float frameRate, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();

        clip.frameRate = frameRate;

        // thêm 1 khóa cuối lặp lại khung cuối để khung cuối cũng được hiện đủ 1 nhịp
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = sprites[i] };
        }
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / frameRate, value = sprites[sprites.Length - 1] };

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew) AssetDatabase.CreateAsset(clip, clipPath);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    private static AnimatorStateMachine FindChildMachine(AnimatorStateMachine root, string name)
    {
        foreach (ChildAnimatorStateMachine child in root.stateMachines)
        {
            if (child.stateMachine.name == name) return child.stateMachine;
        }
        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == name) return child.state;
        }
        return null;
    }
}
