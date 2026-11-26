using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadeRemoveBehavior : StateMachineBehaviour
{
    [TenHienThi("Thời gian mờ")] public float fadeTime = 0.5f;
    [TenHienThi("Chờ trước khi mờ")] public float fadeDelay = 0.0f;
    private float timeElapsed = 0f;
    private float fadeDelayElapsed = 0f;
    
    SpriteRenderer spriteRenderer;
    GameObject objToRemove;
    Color startColor;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timeElapsed = 0f;
        fadeDelayElapsed = 0f;
        spriteRenderer = animator.GetComponent<SpriteRenderer>();
        startColor = spriteRenderer.color;
        objToRemove = animator.gameObject;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (fadeDelay > fadeDelayElapsed)
        {
            fadeDelayElapsed += Time.deltaTime;
        }
        else
        {
            timeElapsed += Time.deltaTime;
            
            if (timeElapsed > fadeTime)
            {
                Destroy(objToRemove);
            }
            else
            {
                float newAlpha = startColor.a * (1 - (timeElapsed / fadeTime));
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, newAlpha);
            }
        }
    }
}