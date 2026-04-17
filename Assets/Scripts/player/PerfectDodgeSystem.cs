using System.Collections;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public class PerfectDodgeSystem : MonoBehaviour
{
    // Volume setup (URP):
    // 1) Create a Global Volume and assign a profile.
    // 2) Add Color Adjustments override in that profile.
    // 3) Suggested values:
    //    - Color Filter: deep blue (RGB 0.06, 0.14, 0.42)
    //    - Saturation: -50
    //    - Contrast: +20
    // 4) Assign that Volume component to worldVolumeComponent.
    // This script only drives the Volume weight (0..1) for instant-on and smooth fade-out.

    [Header("Refs")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private MonoBehaviour worldVolumeComponent;

    [Header("Input")]
    [SerializeField] private KeyCode dodgeKey = KeyCode.LeftShift;
    [SerializeField] private bool driveWindowFromDashEvent = true;

    [Header("Timing")]
    [SerializeField] private float perfectWindowSeconds = 0.2f;
    [SerializeField] private float freezeDurationSeconds = 0.5f;
    [SerializeField] private float visualRecoverSeconds = 0.3f;

    [Header("Volume Blend")]
    [SerializeField, Range(0f, 1f)] private float freezeVolumeWeight = 1f;

    private float lastDodgePressedUnscaledTime = float.NegativeInfinity;
    private bool triggerLocked;
    private Coroutine runningRoutine;
    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime;
    private bool timeScaleOverridden;

    private PropertyInfo volumeWeightProperty;
    private FieldInfo volumeWeightField;
    private bool volumeBindingReady;
    private float baseVolumeWeight;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        TryBindVolumeWeightAccessor();
        baseVolumeWeight = GetVolumeWeight();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnBeforeTakeDamage += TryConsumeDamageByPerfectDodge;
        }

        if (driveWindowFromDashEvent)
        {
            GameEvents.OnDashStart += HandleDashStart;
        }
    }

    private void OnDisable()
    {
        if (driveWindowFromDashEvent)
        {
            GameEvents.OnDashStart -= HandleDashStart;
        }

        if (playerHealth != null)
        {
            playerHealth.OnBeforeTakeDamage -= TryConsumeDamageByPerfectDodge;
        }

        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
            runningRoutine = null;
        }

        RestoreTimeScaleImmediately();
        SetVolumeWeight(baseVolumeWeight);
        triggerLocked = false;
    }

    private void Update()
    {
        if (driveWindowFromDashEvent)
        {
            return;
        }

        if (Input.GetKeyDown(dodgeKey))
        {
            lastDodgePressedUnscaledTime = Time.unscaledTime;
        }
    }

    private void HandleDashStart()
    {
        lastDodgePressedUnscaledTime = Time.unscaledTime;
    }

    private bool TryConsumeDamageByPerfectDodge(PlayerHealth.DamageRequest request)
    {
        if (triggerLocked || !enabled || !gameObject.activeInHierarchy)
        {
            return false;
        }

        float elapsed = Time.unscaledTime - lastDodgePressedUnscaledTime;
        bool withinPerfectWindow = elapsed >= 0f && elapsed <= Mathf.Max(0.01f, perfectWindowSeconds);
        if (!withinPerfectWindow)
        {
            return false;
        }

        triggerLocked = true;
        lastDodgePressedUnscaledTime = float.NegativeInfinity;

        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
        }

        runningRoutine = StartCoroutine(PerfectDodgeRoutine());
        return true;
    }

    private IEnumerator PerfectDodgeRoutine()
    {
        SetVolumeWeight(Mathf.Clamp01(freezeVolumeWeight));

        cachedFixedDeltaTime = Time.fixedDeltaTime;
        cachedTimeScale = Time.timeScale;
        timeScaleOverridden = true;
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        float freezeEnd = Time.unscaledTime + Mathf.Max(0f, freezeDurationSeconds);
        while (Time.unscaledTime < freezeEnd)
        {
            yield return null;
        }

        RestoreTimeScaleImmediately();

        float fromWeight = GetVolumeWeight();
        float toWeight = baseVolumeWeight;
        float duration = Mathf.Max(0f, visualRecoverSeconds);

        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetVolumeWeight(Mathf.Lerp(fromWeight, toWeight, t));
                yield return null;
            }
        }

        SetVolumeWeight(toWeight);
        runningRoutine = null;
        triggerLocked = false;
    }

    private void RestoreTimeScaleImmediately()
    {
        if (!timeScaleOverridden)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        if (cachedFixedDeltaTime > 0f)
        {
            Time.fixedDeltaTime = cachedFixedDeltaTime;
        }

        timeScaleOverridden = false;
    }

    private void TryBindVolumeWeightAccessor()
    {
        volumeBindingReady = false;
        volumeWeightProperty = null;
        volumeWeightField = null;

        if (worldVolumeComponent == null)
        {
            return;
        }

        System.Type volumeType = worldVolumeComponent.GetType();
        volumeWeightProperty = volumeType.GetProperty("weight", BindingFlags.Public | BindingFlags.Instance);
        if (volumeWeightProperty != null && volumeWeightProperty.CanWrite && volumeWeightProperty.PropertyType == typeof(float))
        {
            volumeBindingReady = true;
            return;
        }

        volumeWeightField = volumeType.GetField("weight", BindingFlags.Public | BindingFlags.Instance);
        if (volumeWeightField != null && volumeWeightField.FieldType == typeof(float))
        {
            volumeBindingReady = true;
        }
    }

    private float GetVolumeWeight()
    {
        if (!volumeBindingReady || worldVolumeComponent == null)
        {
            return 0f;
        }

        if (volumeWeightProperty != null)
        {
            object value = volumeWeightProperty.GetValue(worldVolumeComponent, null);
            return value is float f ? f : 0f;
        }

        if (volumeWeightField != null)
        {
            object value = volumeWeightField.GetValue(worldVolumeComponent);
            return value is float f ? f : 0f;
        }

        return 0f;
    }

    private void SetVolumeWeight(float value)
    {
        if (!volumeBindingReady || worldVolumeComponent == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(value);
        if (volumeWeightProperty != null)
        {
            volumeWeightProperty.SetValue(worldVolumeComponent, clamped, null);
            return;
        }

        if (volumeWeightField != null)
        {
            volumeWeightField.SetValue(worldVolumeComponent, clamped);
        }
    }
}
