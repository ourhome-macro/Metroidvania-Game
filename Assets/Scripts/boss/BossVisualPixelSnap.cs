using UnityEngine;

[DisallowMultipleComponent]
public class BossVisualPixelSnap : MonoBehaviour
{
    [SerializeField] private float pixelsPerUnit = 128f;
    [SerializeField] private bool snapX = true;
    [SerializeField] private bool snapY = true;
    [SerializeField] private bool snapZ;

    private void LateUpdate()
    {
        float ppu = ResolvePixelsPerUnit();
        if (ppu <= 0f)
        {
            return;
        }

        float unit = 1f / ppu;
        Vector3 position = transform.position;

        if (snapX)
        {
            position.x = Mathf.Round(position.x / unit) * unit;
        }

        if (snapY)
        {
            position.y = Mathf.Round(position.y / unit) * unit;
        }

        if (snapZ)
        {
            position.z = Mathf.Round(position.z / unit) * unit;
        }

        transform.position = position;
    }

    private float ResolvePixelsPerUnit()
    {
        if (pixelsPerUnit > 0f)
        {
            return pixelsPerUnit;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null && sr.sprite.pixelsPerUnit > 0f)
        {
            return sr.sprite.pixelsPerUnit;
        }

        return 100f;
    }
}
