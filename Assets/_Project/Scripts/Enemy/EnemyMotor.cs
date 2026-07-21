using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyMotor : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D col;
    [SerializeField] private EnemyConfig config;

    public float FacingDirection { get; private set; } = 1f;
    public bool IsGrounded { get; private set; }
    public bool ExternalVelocityActive { get; private set; }

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
        if (col == null)
        {
            col = GetComponent<Collider2D>();
        }
        
        FacingDirection = Mathf.Sign(transform.localScale.x);
    }

    public void Initialize(EnemyConfig enemyConfig, Rigidbody2D rb)
    {
        config = enemyConfig;
        body = rb;
    }

    public void MoveHorizontal(float direction, float speed)
    {
        if (ExternalVelocityActive)
        {
            return;
        }

        if (body != null)
        {
            body.linearVelocity = new Vector2(direction * speed, body.linearVelocity.y);
        }

        if (Mathf.Abs(direction) > 0.01f)
        {
            float newFacing = Mathf.Sign(direction);
            if (!Mathf.Approximately(newFacing, FacingDirection))
            {
                FacingDirection = newFacing;
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * FacingDirection;
                transform.localScale = scale;
            }
        }
    }

    public void ApplyExternalVelocity(Vector2 velocity)
    {
        ExternalVelocityActive = true;
        if (body != null)
        {
            body.linearVelocity = velocity;
        }
    }

    public void ClearExternalVelocity()
    {
        ExternalVelocityActive = false;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    public void Flip()
    {
        FacingDirection = -FacingDirection;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * FacingDirection;
        transform.localScale = scale;
    }

    public void StopHorizontal()
    {
        if (ExternalVelocityActive)
        {
            return;
        }

        if (body != null)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    private void FixedUpdate()
    {
        if (config == null)
        {
            return;
        }

        Vector2 origin;
        float distance;

        if (col != null)
        {
            origin = new Vector2(col.bounds.center.x, col.bounds.min.y);
            distance = 0.1f;
        }
        else
        {
            origin = transform.position;
            distance = 0.5f;
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, distance, config.terrainLayers);
        IsGrounded = hit.collider != null;

        #if UNITY_EDITOR
        Debug.DrawRay(origin, Vector2.down * distance, IsGrounded ? Color.red : Color.green);
        #endif
    }
}
