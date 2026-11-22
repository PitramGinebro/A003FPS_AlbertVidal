using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista del hitmarker. No escucha eventos: <see cref="UIManager"/> llama a <see cref="Show"/> cuando el jugador acierta.
/// En rojo y algo más grande si el impacto mata.
/// </summary>
public class HitMarkerUI : MonoBehaviour
{
    [SerializeField] private Image marker;
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private float popScale = 1.35f;
    [SerializeField] private float killScale = 1.6f;
    [SerializeField] private Color hitColor = Color.white;
    [SerializeField] private Color killColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 0.6f;

    private float shownAt = -10f;
    private float startScale = 1f;
    private AudioSource audioSource;

    private void Awake()
    {
        if (marker == null) marker = GetComponent<Image>();
        if (hitSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        SetAlpha(0f);
    }

    public void Show(bool kill)
    {
        if (marker == null) return;
        marker.color = kill ? killColor : hitColor;
        startScale = kill ? killScale : popScale;
        shownAt = Time.time;
        if (audioSource != null) audioSource.PlayOneShot(hitSound, hitSoundVolume);
    }

    private void Update()
    {
        float t = (Time.time - shownAt) / duration;
        if (t >= 1f) { SetAlpha(0f); return; }
        marker.rectTransform.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, Mathf.Clamp01(t * 3f));
        SetAlpha(1f - t * t);
    }

    private void SetAlpha(float a)
    {
        if (marker == null) return;
        Color c = marker.color; c.a = a; marker.color = c;
    }
}
