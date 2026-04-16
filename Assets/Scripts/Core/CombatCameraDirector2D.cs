using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class CombatCameraDirector2D : MonoBehaviour
{
    private enum CameraState
    {
        Normal,
        Dash,
        BossUlt,
        Cutscene
    }

    [Header("Virtual Cameras")]
    [SerializeField] private CinemachineVirtualCamera vcamNormal;
    [SerializeField] private CinemachineVirtualCamera vcamDash;
    [SerializeField] private CinemachineVirtualCamera vcamBossUlt;

    [Header("Targets")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private CinemachineTargetGroup bossTargetGroup;

    [Header("Priority")]
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int dashPriority = 20;
    [SerializeField] private int bossUltPriority = 30;

    [Header("State Switch")]
    [SerializeField] private float switchCooldown = 0.2f;

    [Header("Lens")]
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float dashFov = 78f;
    [SerializeField] private float bossUltFov = 52f;
    [SerializeField] private float normalOrthoSize = 5f;
    [SerializeField] private float dashOrthoSize = 4.5f;
    [SerializeField] private float bossUltOrthoSize = 6f;

    [Header("Dash Feel")]
    [SerializeField] private CinemachineImpulseSource dashImpulseSource;
    [SerializeField] private float dashForwardOffset = 0.35f;
    [SerializeField] private float dashOffsetLerp = 12f;

    [Header("Parry Feel")]
    [SerializeField] private float parryFovBoost = 10f;
    [SerializeField] private float parryOrthoSizeBoost = 1f;
    [SerializeField] private float parryBoostInTime = 0.08f;
    [SerializeField] private float parryBoostOutTime = 0.22f;
    [SerializeField] private float parryHoldTime = 0.08f;

    private CameraState currentState = CameraState.Normal;
    private bool isDashActive;
    private bool isBossUltActive;
    private float lastSwitchTime = -999f;
    private float parryBoostCurrent;
    private float parryBoostHoldUntil = -999f;
    private float lastAppliedParryBoost = float.MinValue;

    private void Awake()
    {
        AutoResolveReferences();
        parryBoostCurrent = 0f;
        parryBoostHoldUntil = -999f;
        lastAppliedParryBoost = float.MinValue;
        ApplyLensProfiles();
        RebuildBossTargetGroup(null);
        ApplyState(CameraState.Normal, true);
    }

    private void OnEnable()
    {
        GameEvents.OnDashStart += HandleDashStart;
        GameEvents.OnDashEnd += HandleDashEnd;
        GameEvents.OnBossUltStart += HandleBossUltStart;
        GameEvents.OnBossUltEnd += HandleBossUltEnd;
        GameEvents.OnPerfectParry += HandlePerfectParry;
    }

    private void OnDisable()
    {
        GameEvents.OnDashStart -= HandleDashStart;
        GameEvents.OnDashEnd -= HandleDashEnd;
        GameEvents.OnBossUltStart -= HandleBossUltStart;
        GameEvents.OnBossUltEnd -= HandleBossUltEnd;
        GameEvents.OnPerfectParry -= HandlePerfectParry;
    }

    private void LateUpdate()
    {
        UpdateDashForwardOffset();
        UpdateParryLensBoost();
    }

    public void ApplySetupFromImporter(
        CinemachineVirtualCamera normal,
        CinemachineVirtualCamera dash,
        CinemachineVirtualCamera bossUlt,
        CinemachineTargetGroup targetGroup,
        Transform player,
        PlayerController2D controller,
        CinemachineImpulseSource impulseSource)
    {
        vcamNormal = normal;
        vcamDash = dash;
        vcamBossUlt = bossUlt;
        bossTargetGroup = targetGroup;
        playerTarget = player;
        playerController = controller;
        dashImpulseSource = impulseSource;

        parryBoostCurrent = 0f;
        parryBoostHoldUntil = -999f;
        lastAppliedParryBoost = float.MinValue;

        ApplyLensProfiles();
        RebuildBossTargetGroup(null);
        ApplyState(CameraState.Normal, true);
    }

    public void InvalidateConfinerCache()
    {
        InvalidateConfinerCache(vcamNormal);
        InvalidateConfinerCache(vcamDash);
        InvalidateConfinerCache(vcamBossUlt);
    }

    private void HandleDashStart()
    {
        isDashActive = true;

        if (dashImpulseSource != null)
        {
            dashImpulseSource.GenerateImpulse();
        }

        if (isBossUltActive)
        {
            return;
        }

        ApplyState(CameraState.Dash, false);
    }

    private void HandleDashEnd()
    {
        isDashActive = false;

        if (isBossUltActive)
        {
            return;
        }

        ApplyState(CameraState.Normal, true);
    }

    private void HandleBossUltStart(Transform bossRoot)
    {
        isBossUltActive = true;
        RebuildBossTargetGroup(bossRoot);
        ApplyState(CameraState.BossUlt, true);
    }

    private void HandleBossUltEnd()
    {
        isBossUltActive = false;
        RebuildBossTargetGroup(null);

        CameraState next = isDashActive ? CameraState.Dash : CameraState.Normal;
        ApplyState(next, true);
    }

    private void HandlePerfectParry()
    {
        parryBoostHoldUntil = Time.unscaledTime + Mathf.Max(0f, parryHoldTime);
    }

    private void ApplyState(CameraState state, bool force)
    {
        if (!force)
        {
            if (state == currentState)
            {
                return;
            }

            if (!CanSwitchTo(state))
            {
                return;
            }
        }

        currentState = state;
        lastSwitchTime = Time.unscaledTime;

        if (vcamNormal != null)
        {
            vcamNormal.Priority = state == CameraState.Normal ? normalPriority : 0;
        }

        if (vcamDash != null)
        {
            vcamDash.Priority = state == CameraState.Dash ? dashPriority : 0;
        }

        if (vcamBossUlt != null)
        {
            vcamBossUlt.Priority = state == CameraState.BossUlt ? bossUltPriority : 0;
        }
    }

    private bool CanSwitchTo(CameraState next)
    {
        if (Time.unscaledTime - lastSwitchTime >= Mathf.Max(0f, switchCooldown))
        {
            return true;
        }

        return GetStatePriority(next) > GetStatePriority(currentState);
    }

    private static int GetStatePriority(CameraState state)
    {
        switch (state)
        {
            case CameraState.Cutscene:
                return 100;
            case CameraState.BossUlt:
                return 80;
            case CameraState.Dash:
                return 60;
            default:
                return 40;
        }
    }

    private void RebuildBossTargetGroup(Transform bossRoot)
    {
        if (bossTargetGroup == null || playerTarget == null)
        {
            return;
        }

        List<CinemachineTargetGroup.Target> targets = new List<CinemachineTargetGroup.Target>(2)
        {
            new CinemachineTargetGroup.Target
            {
                target = playerTarget,
                weight = 1f,
                radius = 2f
            }
        };

        if (bossRoot != null)
        {
            targets.Add(new CinemachineTargetGroup.Target
            {
                target = bossRoot,
                weight = 1.15f,
                radius = 2.5f
            });
        }

        bossTargetGroup.m_Targets = targets.ToArray();
    }

    private void UpdateDashForwardOffset()
    {
        if (vcamDash == null)
        {
            return;
        }

        CinemachineFramingTransposer framing = vcamDash.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing == null)
        {
            return;
        }

        float targetX = 0f;
        if (currentState == CameraState.Dash && playerController != null)
        {
            targetX = dashForwardOffset * playerController.FacingDirection;
        }

        Vector3 currentOffset = framing.m_TrackedObjectOffset;
        currentOffset.x = Mathf.Lerp(currentOffset.x, targetX, Time.deltaTime * Mathf.Max(0.1f, dashOffsetLerp));
        framing.m_TrackedObjectOffset = currentOffset;
    }

    private void UpdateParryLensBoost()
    {
        float target = Time.unscaledTime <= parryBoostHoldUntil ? Mathf.Max(0f, parryFovBoost) : 0f;
        float duration = target > parryBoostCurrent
            ? Mathf.Max(0.01f, parryBoostInTime)
            : Mathf.Max(0.01f, parryBoostOutTime);

        float speed = Mathf.Max(0.001f, Mathf.Max(1f, parryFovBoost)) / duration;
        parryBoostCurrent = Mathf.MoveTowards(parryBoostCurrent, target, speed * Time.unscaledDeltaTime);

        if (Mathf.Abs(parryBoostCurrent - lastAppliedParryBoost) <= 0.001f)
        {
            return;
        }

        lastAppliedParryBoost = parryBoostCurrent;
        ApplyLensProfiles(parryBoostCurrent);
    }

    private void ApplyLensProfiles(float fovBoost = 0f)
    {
        float orthoBoost = Mathf.Max(0f, parryOrthoSizeBoost) * Mathf.Clamp01(fovBoost / Mathf.Max(0.01f, parryFovBoost));
        ApplyLens(vcamNormal, normalFov + fovBoost, normalOrthoSize + orthoBoost);
        ApplyLens(vcamDash, dashFov + fovBoost, dashOrthoSize + orthoBoost);
        ApplyLens(vcamBossUlt, bossUltFov + fovBoost, bossUltOrthoSize + orthoBoost);
    }

    private static void ApplyLens(CinemachineVirtualCamera vcam, float fov, float orthoSize)
    {
        if (vcam == null)
        {
            return;
        }

        LensSettings lens = vcam.m_Lens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Max(0.1f, orthoSize);
        }
        else
        {
            lens.FieldOfView = Mathf.Clamp(fov, 1f, 179f);
        }

        vcam.m_Lens = lens;
    }

    private void AutoResolveReferences()
    {
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }

        if (playerController == null && playerTarget != null)
        {
            playerController = playerTarget.GetComponent<PlayerController2D>();
        }
    }

    private static void InvalidateConfinerCache(CinemachineVirtualCamera vcam)
    {
        if (vcam == null)
        {
            return;
        }

        CinemachineConfiner2D confiner2D = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner2D != null)
        {
            confiner2D.InvalidateCache();
        }

        CinemachineConfiner confiner3D = vcam.GetComponent<CinemachineConfiner>();
        if (confiner3D != null)
        {
            confiner3D.InvalidatePathCache();
        }
    }
}
