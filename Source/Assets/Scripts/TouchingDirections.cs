using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TouchingDirections : MonoBehaviour
{
    [TenHienThi("Bộ lọc")] public ContactFilter2D castFilter;
    [TenHienThi("Dò đất")] public float groundDistance = 0.05f;
    [TenHienThi("Dò tường")] public float wallDistance = 0.2f;
    [TenHienThi("Dò trần")] public float ceilingDistance = 0.05f;
    CapsuleCollider2D touchingCol;
    Animator animator;
    
    RaycastHit2D[] groundHits = new RaycastHit2D[5];
    RaycastHit2D[] wallHits = new RaycastHit2D[5];
    RaycastHit2D[] ceilingHits = new RaycastHit2D[5];

   [SerializeField]
    [TenHienThi("Chạm đất")] private bool _isGrounded; 

   
    public bool isGrounded 
    {
        get
        {
            return _isGrounded;
        }
        private set 
        {
            _isGrounded = value;
            animator.SetBool(AnimationStrings.isGrounded, value);
        }
    }
     [SerializeField]
    [TenHienThi("Chạm tường")] private bool _isOnWall; 

   
    public bool isOnWall 
    {
        get
        {
            return _isOnWall;
        }
        private set 
        {
            _isOnWall = value;
            animator.SetBool(AnimationStrings.isOnWall, value);
        }
    }
     [SerializeField]
    [TenHienThi("Chạm trần")] private bool _isOnCeiling; 
    private Vector2 wallCheckDirection => gameObject.transform.localScale.x > 0 ? Vector2.right : Vector2.left;
   
    public bool isOnCeiling 
    {
        get
        {
            return _isOnCeiling;
        }
        private set 
        {
            _isOnCeiling = value;
            animator.SetBool(AnimationStrings.isOnCeiling, value);
        }
    }

    private void Awake()
    {
        touchingCol = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        isGrounded = touchingCol.Cast(Vector2.down, castFilter, groundHits, groundDistance) > 0;
        isOnWall = touchingCol.Cast(wallCheckDirection, castFilter, wallHits, wallDistance) > 0;
        isOnCeiling = touchingCol.Cast(Vector2.up, castFilter, ceilingHits, ceilingDistance) > 0;
    }
}
