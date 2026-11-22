using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista del HUD de munición: nombre del arma, cargador, reserva y fila de balitas.
/// No escucha eventos: la controla <see cref="UIManager"/>.
/// </summary>
public class AmmoHUD : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text weaponNameText;
    [SerializeField] private Text magazineText;
    [SerializeField] private Text reserveText;
    [SerializeField] private Text statusText;
    [SerializeField] private RectTransform pipContainer;
    [SerializeField] private RectTransform reloadFill;

    [Header("Estilo")]
    [SerializeField, Range(0f, 1f)] private float lowAmmoFraction = 0.25f;
    [SerializeField] private Color normalColor = new Color(0.94f, 0.92f, 0.86f);
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.33f, 0.2f);
    [SerializeField] private Color accentColor = new Color(1f, 0.64f, 0.16f);
    [SerializeField, Range(0f, 1f)] private float spentAlpha = 0.16f;
    [SerializeField] private int maxPips = 40;
    [SerializeField] private Vector2 pipSize = new Vector2(5f, 22f);
    [SerializeField] private float pipTilt = -18f;
    [SerializeField] private float punchScale = 1.18f;
    [SerializeField] private float punchTime = 0.12f;

    private readonly List<Image> pips = new List<Image>();
    private int pipsBuiltFor = -1;
    private int roundsPerPip = 1;
    private Weapon current;
    private int lastMagazine = -1;
    private float punchUntil;
    private bool reloading;
    private float reloadStart, reloadDuration;

    // ---------------- Llamadas del UIManager ----------------

    public void SetWeapon(Weapon weapon)
    {
        current = weapon;
        lastMagazine = current != null ? current.Magazine : -1;
        reloading = current != null && current.State == Weapon.StateId.Reloading;
        Refresh();
    }

    public void OnAmmoChanged(Weapon weapon)
    {
        if (weapon != current || current == null) return;
        if (current.Magazine < lastMagazine) punchUntil = Time.time + punchTime;
        lastMagazine = current.Magazine;
        Refresh();
    }

    public void OnWeaponStateChanged(Weapon.StateId previous, Weapon.StateId next)
    {
        if (current == null) return;
        if (next == Weapon.StateId.Reloading)
        {
            reloading = true;
            reloadStart = Time.time;
            reloadDuration = Mathf.Max(0.05f, current.Magazine == 0 ? current.Data.reloadEmptyTime : current.Data.reloadTime);
        }
        else if (previous == Weapon.StateId.Reloading)
        {
            reloading = false;
        }
        Refresh();
    }

    // ---------------- Pintado ----------------

    private bool IsLow => current != null && current.Magazine <= Mathf.CeilToInt(current.Data.magazineSize * lowAmmoFraction);

    private void Refresh()
    {
        bool hasWeapon = current != null;
        if (panel != null) panel.SetActive(hasWeapon);
        if (!hasWeapon)
        {
            if (statusText != null) statusText.gameObject.SetActive(false);
            return;
        }

        WeaponData data = current.Data;
        int mag = current.Magazine;

        if (weaponNameText != null) weaponNameText.text = data.displayName.ToUpperInvariant();
        magazineText.text = mag.ToString("00");
        magazineText.color = IsLow ? lowAmmoColor : normalColor;
        reserveText.text = data.infiniteReserveAmmo ? "∞" : current.Reserve.ToString("000");

        BuildPips(data.magazineSize);
        PaintPips(0f);

        if (reloadFill != null) reloadFill.gameObject.SetActive(reloading);

        if (statusText != null)
        {
            bool empty = !reloading && mag == 0 && !current.CanReload;
            statusText.gameObject.SetActive(empty);
            if (empty) statusText.text = "SIN MUNICIÓN";
        }
    }

    private void BuildPips(int magazineSize)
    {
        if (pipContainer == null || pipsBuiltFor == magazineSize) return;
        pipsBuiltFor = magazineSize;
        roundsPerPip = Mathf.Max(1, Mathf.CeilToInt(magazineSize / (float)Mathf.Max(1, maxPips)));
        int count = Mathf.CeilToInt(magazineSize / (float)roundsPerPip);

        foreach (Transform child in pipContainer) Destroy(child.gameObject);
        pips.Clear();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(pipContainer, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = pipSize;
            rt.localRotation = Quaternion.Euler(0f, 0f, pipTilt);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            pips.Add(img);
        }
    }

    /// <param name="reloadProgress">0..1 mientras se recarga: rellena las balas que faltan.</param>
    private void PaintPips(float reloadProgress)
    {
        if (pips.Count == 0 || current == null) return;
        int lit = Mathf.CeilToInt(current.Magazine / (float)roundsPerPip);
        int previewTarget = lit;
        if (reloading)
        {
            int canAdd = current.Data.infiniteReserveAmmo ? current.Data.magazineSize : current.Magazine + current.Reserve;
            int full = Mathf.CeilToInt(Mathf.Min(current.Data.magazineSize, canAdd) / (float)roundsPerPip);
            previewTarget = lit + Mathf.FloorToInt((full - lit) * reloadProgress);
        }

        Color live = IsLow && !reloading ? lowAmmoColor : normalColor;
        Color spent = normalColor; spent.a = spentAlpha;
        Color refill = accentColor; refill.a = 0.85f;
        int firstLit = pips.Count - lit;
        int firstPreview = pips.Count - previewTarget;
        for (int i = 0; i < pips.Count; i++)
        {
            if (i >= firstLit) pips[i].color = live;
            else if (i >= firstPreview) pips[i].color = refill;
            else pips[i].color = spent;
        }
    }

    private void Update()
    {
        if (current == null) return;

        float k = Mathf.Clamp01((punchUntil - Time.time) / Mathf.Max(0.001f, punchTime));
        magazineText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, k * k);

        if (reloading)
        {
            float p = Mathf.Clamp01((Time.time - reloadStart) / reloadDuration);
            if (reloadFill != null)
            {
                reloadFill.anchorMax = new Vector2(p, reloadFill.anchorMax.y);
                reloadFill.offsetMax = new Vector2(0f, reloadFill.offsetMax.y);
            }
            PaintPips(p);
        }
    }
}
