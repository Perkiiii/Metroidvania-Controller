using System.Collections.Generic;
using UnityEngine;

public sealed class CameraTarget : MonoBehaviour
{
    public enum TargetMode
    {
        FollowHero,
        LockZone,
        Free
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
        float s = 0.3f;
        Vector3 p = transform.position;
        Gizmos.DrawLine(p + Vector3.left * s, p + Vector3.right * s);
        Gizmos.DrawLine(p + Vector3.down * s, p + Vector3.up * s);
        Gizmos.DrawWireSphere(p, 0.12f);
    }

    [Header("Config")]
    [SerializeField] private CameraConfig config;

    [Header("Smoothing")]
    [SerializeField] private float dampTimeNormal = 0.30f;
    [SerializeField] private float dampTimeSlow = 0.15f;
    [SerializeField] private float dampTimeSlower = 0.45f;

    [Header("Look-ahead")]
    [SerializeField] private float xLookAhead = 0.16f;
    [SerializeField] private float dashLookAhead = 2.51f;
    [SerializeField] private float fallLookAhead = 1.25f;
    [SerializeField] private float sprintLookAhead = 1.25f;
    [SerializeField] private float specialMoveLookAhead = 2.5f;

    [Header("Vertical Framing")]
    [SerializeField] private float baseVerticalOffset = 1f;
    [SerializeField] private float fastFallVerticalOffset = -1.5f;
    [SerializeField] private float fallCatchAccel = 80f;
    [SerializeField] private float fallCatchMax = 25f;

    public TargetMode Mode { get; private set; } = TargetMode.FollowHero;
    public Vector3 DesiredPosition { get; private set; }
    public Vector2 InferredVelocity => inferredVelocity;
    public float CurrentVerticalOffset => currentVerticalOffset;
    public bool IsRising => inferredVelocity.y > GetRisingThreshold();
    public bool IsFalling => inferredVelocity.y < -GetFallingThreshold();
    public bool IsFastFalling => inferredVelocity.y < GetFastFallThreshold();

    private readonly List<CameraOffsetArea> offsetStack = new List<CameraOffsetArea>();

    private Transform heroTransform;
    private Rect lockRect;

    private Vector3 velocityX;
    private Vector3 velocityY;
    private Vector3 previousHeroPosition;
    private Vector2 inferredVelocity;
    private Vector2 velocityHint;
    private float xOffset;
    private float dashOffset;
    private float externalMoveOffset;
    private float currentVerticalOffset;
    private float fallCatcher;
    private float velocityHintTimer;
    private bool fallStick;

    private float dampTimeX;
    private float dampTimeY;
    private float slowTimer;
    private float detachTimer;
    private int facingDirection = 1;
    private bool stickX;
    private bool stickY;
    private bool ignoreXOffset;

    public void SetConfig(CameraConfig cameraConfig)
    {
        config = cameraConfig;
        ApplyConfig();
    }

    public void SceneInit()
    {
        SceneInit(true);
    }

    public void SceneInit(bool warnIfMissingHero)
    {
        ApplyConfig();

        GameObject hero = GameObject.FindWithTag("Player");
        if (hero == null)
        {
            if (warnIfMissingHero)
            {
                Debug.LogWarning("[CameraTarget] SceneInit: no GameObject tagged 'Player' found.");
            }

            return;
        }

        heroTransform = hero.transform;
        previousHeroPosition = heroTransform.position;
        inferredVelocity = Vector2.zero;
        velocityHint = Vector2.zero;
        velocityHintTimer = 0f;

        Mode = TargetMode.FollowHero;
        lockRect = default;
        dampTimeX = dampTimeNormal;
        dampTimeY = dampTimeNormal;
        xOffset = 0f;
        dashOffset = 0f;
        externalMoveOffset = 0f;
        currentVerticalOffset = baseVerticalOffset;
        fallCatcher = 0f;
        fallStick = false;
        slowTimer = 0f;
        detachTimer = 0f;
        stickX = true;
        stickY = true;
        ignoreXOffset = false;
        offsetStack.Clear();

        transform.position = heroTransform.position;
        DesiredPosition = transform.position;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
    }

    public void SnapToHero()
    {
        if (heroTransform == null)
        {
            return;
        }

        transform.position = heroTransform.position;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        xOffset = 0f;
        dashOffset = 0f;
        externalMoveOffset = 0f;
        currentVerticalOffset = baseVerticalOffset;
        DesiredPosition = transform.position;
        fallCatcher = 0f;
        fallStick = false;
        stickX = true;
        stickY = true;
        previousHeroPosition = heroTransform.position;
        inferredVelocity = Vector2.zero;
        velocityHint = Vector2.zero;
        velocityHintTimer = 0f;
    }

    public void SetSlowDamp()
    {
        slowTimer = config != null ? config.slowTime : 0.5f;
        dampTimeX = dampTimeSlow;
        dampTimeY = dampTimeSlow;
        stickX = false;
        stickY = false;
    }

    public void SetShortSlowDamp()
    {
        slowTimer = config != null ? config.shortSlowTime : 0.25f;
        dampTimeX = dampTimeSlow;
        dampTimeY = dampTimeSlow;
        stickX = false;
        stickY = false;
    }

    public void ShortDetach()
    {
        detachTimer = config != null ? config.shortSlowTime : 0.25f;
        stickX = false;
        stickY = false;
    }

    public void EnterLockZone(Rect rect, float currentJumpDistance)
    {
        Mode = TargetMode.LockZone;
        lockRect = rect;

        float largeJumpDistance = config != null ? config.lockLargeJumpDistance : 9f;
        slowTimer = config != null ? config.slowTime : 0.5f;
        dampTimeX = currentJumpDistance > largeJumpDistance ? dampTimeSlower : dampTimeSlow;
        dampTimeY = currentJumpDistance > largeJumpDistance ? dampTimeSlower : dampTimeSlow;
        stickX = false;
        stickY = false;
    }

    public void ExitLockZone()
    {
        Mode = TargetMode.FollowHero;
        lockRect = default;
        SetSlowDamp();
    }

    public void StartFreeMode(bool keepHorizontalOffset)
    {
        Mode = TargetMode.Free;
        ignoreXOffset = !keepHorizontalOffset;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
    }

    public void EndFreeMode()
    {
        Mode = TargetMode.FollowHero;
        ignoreXOffset = false;
        SetShortSlowDamp();
    }

    public void FreezeInPlace()
    {
        Mode = TargetMode.Free;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
    }

    public void SetFacingDirection(int direction)
    {
        if (direction != 0)
        {
            facingDirection = direction > 0 ? 1 : -1;
        }
    }

    public void SetVelocityHint(Vector2 velocity)
    {
        velocityHint = velocity;
        velocityHintTimer = 0.1f;
    }

    public void SetDashActive(bool active, int direction)
    {
        if (active)
        {
            SetFacingDirection(direction);
            dashOffset = facingDirection * dashLookAhead;
        }
    }

    public void SetSprintActive(bool active, int direction)
    {
        externalMoveOffset = active ? Mathf.Sign(direction == 0 ? facingDirection : direction) * sprintLookAhead : 0f;
    }

    public void SetSpecialMoveLookAhead(float direction)
    {
        externalMoveOffset = Mathf.Approximately(direction, 0f) ? 0f : Mathf.Sign(direction) * specialMoveLookAhead;
    }

    public void AddOffsetArea(CameraOffsetArea area)
    {
        if (area != null && !offsetStack.Contains(area))
        {
            offsetStack.Add(area);
            SetShortSlowDamp();
        }
    }

    public void RemoveOffsetArea(CameraOffsetArea area)
    {
        if (offsetStack.Remove(area))
        {
            SetShortSlowDamp();
        }
    }

    public Vector2 GetCameraOffset()
    {
        Vector2 combined = Vector2.zero;
        for (int i = offsetStack.Count - 1; i >= 0; i--)
        {
            CameraOffsetArea area = offsetStack[i];
            if (area != null && area.isActiveAndEnabled)
            {
                combined += area.Offset;
                continue;
            }

            offsetStack.RemoveAt(i);
        }

        float maxMagnitude = config != null ? config.maxCombinedOffsetAreaMagnitude : 6f;
        return maxMagnitude > 0f ? Vector2.ClampMagnitude(combined, maxMagnitude) : combined;
    }

    public void Tick()
    {
        if (heroTransform == null)
        {
            SceneInit(false);
            if (heroTransform == null)
            {
                return;
            }
        }

        if (Time.timeScale <= Mathf.Epsilon)
        {
            previousHeroPosition = heroTransform.position;
            inferredVelocity = Vector2.zero;
            return;
        }

        UpdateInferredMotion();

        if (Mode == TargetMode.Free)
        {
            DesiredPosition = transform.position;
            previousHeroPosition = heroTransform.position;
            return;
        }

        UpdateDampTime();
        UpdateLookAhead();
        UpdateDashOffset();
        UpdateVerticalOffset();
        UpdateFallCatcher();
        SmoothToDestination();
        previousHeroPosition = heroTransform.position;
    }

    private void UpdateDampTime()
    {
        float dt = Time.deltaTime;
        if (slowTimer > 0f)
        {
            slowTimer -= dt;
            return;
        }

        float step = 0.007f;
        dampTimeX = Mathf.MoveTowards(dampTimeX, dampTimeNormal, step);
        dampTimeY = Mathf.MoveTowards(dampTimeY, dampTimeNormal, step);

        if (detachTimer > 0f)
        {
            detachTimer -= dt;
        }
    }

    private void UpdateLookAhead()
    {
        if (ignoreXOffset)
        {
            float cancelSpeed = config != null ? config.lookAheadMoveSpeed : 6f;
            xOffset = Mathf.MoveTowards(xOffset, 0f, cancelSpeed * Time.deltaTime);
            return;
        }

        float lead = IsFalling ? fallLookAhead : xLookAhead;
        float movingThreshold = config != null ? config.facingVelocityThreshold * 3f : 0.15f;
        bool movingHorizontally = Mathf.Abs(inferredVelocity.x) > movingThreshold;

        float target;
        float speed;

        if (movingHorizontally)
        {
            target = Mathf.Sign(inferredVelocity.x) * lead;
            speed = config != null ? config.lookAheadMoveSpeed : 6f;
        }
        else
        {
            float multiplier = config != null ? config.stationaryLookAheadMultiplier : 0.3f;
            target = facingDirection * lead * multiplier;
            speed = config != null ? config.lookAheadIdleMoveSpeed : 1.5f;
        }

        xOffset = Mathf.MoveTowards(xOffset, target, speed * Time.deltaTime);
    }

    private void UpdateDashOffset()
    {
        float dashThreshold = config != null ? config.dashVelocityThreshold : 5f;
        if (Mathf.Abs(inferredVelocity.x) > dashThreshold)
        {
            dashOffset = Mathf.Sign(inferredVelocity.x) * dashLookAhead;
        }
        else
        {
            dashOffset = Mathf.MoveTowards(dashOffset, 0f, dashLookAhead * 4f * Time.deltaTime);
        }
    }

    private void UpdateVerticalOffset()
    {
        float targetOffset = baseVerticalOffset;

        if (IsFastFalling)
        {
            targetOffset = fastFallVerticalOffset;
        }
        else if (IsFalling)
        {
            targetOffset = Mathf.Lerp(baseVerticalOffset, fastFallVerticalOffset, 0.45f);
        }

        float speed = config != null ? config.verticalOffsetMoveSpeed : 5f;
        currentVerticalOffset = Mathf.MoveTowards(currentVerticalOffset, targetOffset, speed * Time.deltaTime);
    }

    private void UpdateFallCatcher()
    {
        bool falling = IsFalling;
        if (!falling)
        {
            fallCatcher = 0f;
            fallStick = false;
            return;
        }

        if (!fallStick && transform.position.y > heroTransform.position.y + 0.1f)
        {
            fallCatcher = Mathf.Min(fallCatchMax, fallCatcher + fallCatchAccel * Time.deltaTime);
            Vector3 p = transform.position;
            p.y -= fallCatcher * Time.deltaTime;
            transform.position = p;

            if (transform.position.y <= heroTransform.position.y + 0.1f)
            {
                fallStick = true;
            }
        }

        if (fallStick)
        {
            fallCatcher = 0f;
            Vector3 p = transform.position;
            p.y = heroTransform.position.y + 0.1f;
            transform.position = p;
        }
    }

    private void SmoothToDestination()
    {
        Vector2 offset = GetCameraOffset();
        float heroX = heroTransform.position.x + offset.x;
        float heroY = heroTransform.position.y + offset.y;
        float z = transform.position.z;

        float destX = heroX + xOffset + dashOffset + externalMoveOffset;
        float destY = heroY;

        if (Mode == TargetMode.LockZone)
        {
            destX = Mathf.Clamp(destX, lockRect.xMin, lockRect.xMax);
            destY = Mathf.Clamp(destY, lockRect.yMin, lockRect.yMax);
        }

        DesiredPosition = new Vector3(destX, destY, z);

        float currentX = transform.position.x;
        float currentY = transform.position.y;
        float snapDistance = config != null ? config.snapDistance : 0.15f;
        float facingThreshold = config != null ? config.facingVelocityThreshold : 0.05f;
        float verticalThreshold = Mathf.Max(GetRisingThreshold(), GetFallingThreshold());
        bool horizontalSettled = Mathf.Abs(inferredVelocity.x) <= facingThreshold && Mathf.Abs(dashOffset) <= 0.01f;
        bool verticalSettled = Mathf.Abs(inferredVelocity.y) <= verticalThreshold;

        stickX = horizontalSettled && Mathf.Abs(currentX - destX) <= snapDistance;
        stickY = verticalSettled && Mathf.Abs(currentY - destY) <= snapDistance;

        Vector3 current = new Vector3(currentX, currentY, z);
        Vector3 smoothX = Vector3.SmoothDamp(current, new Vector3(destX, currentY, z), ref velocityX, dampTimeX);
        Vector3 smoothY = Vector3.SmoothDamp(current, new Vector3(currentX, destY, z), ref velocityY, dampTimeY);

        float finalX = stickX ? destX : smoothX.x;
        float finalY = stickY && !fallStick ? destY : smoothY.y;

        transform.position = new Vector3(finalX, finalY, z);
    }

    private void UpdateInferredMotion()
    {
        float dt = Time.deltaTime;
        Vector3 current = heroTransform.position;

        inferredVelocity = dt > Mathf.Epsilon ? (current - previousHeroPosition) / dt : Vector2.zero;
        if (velocityHintTimer > 0f)
        {
            velocityHintTimer -= dt;
            inferredVelocity = velocityHint;
        }

        float facingThreshold = config != null ? config.facingVelocityThreshold : 0.05f;
        if (Mathf.Abs(inferredVelocity.x) > facingThreshold)
        {
            facingDirection = inferredVelocity.x > 0f ? 1 : -1;
        }
    }

    private float GetFallingThreshold()
    {
        return config != null ? config.fallingVelocityThreshold : 0.25f;
    }

    private float GetRisingThreshold()
    {
        return config != null ? config.risingVelocityThreshold : 0.25f;
    }

    private float GetFastFallThreshold()
    {
        return config != null ? config.fastFallVelocityThreshold : -8f;
    }

    private void ApplyConfig()
    {
        if (config == null)
        {
            return;
        }

        dampTimeNormal = config.targetDampTimeNormal;
        dampTimeSlow = config.targetDampTimeSlow;
        dampTimeSlower = config.targetDampTimeSlower;
        xLookAhead = config.xLookAhead;
        dashLookAhead = config.dashLookAhead;
        fallLookAhead = config.fallLookAhead;
        sprintLookAhead = config.sprintLookAhead;
        specialMoveLookAhead = config.specialMoveLookAhead;
        baseVerticalOffset = config.baseVerticalOffset;
        fastFallVerticalOffset = config.fastFallVerticalOffset;
        fallCatchAccel = config.fallCatchAccel;
        fallCatchMax = config.fallCatchMax;
    }
}
