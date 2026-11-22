using UnityEngine;

/// <summary>
/// Punto único de la UI del juego. Escucha los eventos del gameplay (armas, munición, daño)
/// y se los pasa a las vistas: <see cref="AmmoHUD"/> y <see cref="HitMarkerUI"/>.
/// Las vistas no se suscriben a nada por su cuenta; solo pintan lo que les manda el UIManager.
/// </summary>
[DefaultExecutionOrder(-50)]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Gameplay")]
    [SerializeField] private WeaponManager weaponManager;

    [Header("Vistas")]
    [SerializeField] private AmmoHUD ammoHUD;
    [SerializeField] private HitMarkerUI hitMarker;

    public AmmoHUD AmmoHUD => ammoHUD;
    public HitMarkerUI HitMarker => hitMarker;
    public Weapon CurrentWeapon => current;

    private Canvas canvas;
    private Weapon current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UIManager] Hay más de un UIManager en la escena; se desactiva el duplicado.", this);
            enabled = false;
            return;
        }
        Instance = this;

        canvas = GetComponent<Canvas>();
        if (weaponManager == null) weaponManager = FindAnyObjectByType<WeaponManager>();
        if (ammoHUD == null) ammoHUD = GetComponentInChildren<AmmoHUD>(true);
        if (hitMarker == null) hitMarker = GetComponentInChildren<HitMarkerUI>(true);
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        if (weaponManager != null)
        {
            weaponManager.ActiveWeaponChanged += HandleActiveWeaponChanged;
            weaponManager.AmmoChanged += HandleAmmoChanged;
        }
        Health.AnyDamaged += HandleAnyDamaged;
        HandleActiveWeaponChanged(weaponManager != null ? weaponManager.ActiveWeapon : null);
    }

    private void OnDisable()
    {
        if (Instance != this) return;
        if (weaponManager != null)
        {
            weaponManager.ActiveWeaponChanged -= HandleActiveWeaponChanged;
            weaponManager.AmmoChanged -= HandleAmmoChanged;
        }
        Health.AnyDamaged -= HandleAnyDamaged;
        HandleActiveWeaponChanged(null);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------- API pública ----------------

    /// <summary>Muestra u oculta todo el HUD (menús de pausa, cinemáticas...).</summary>
    public void SetHUDVisible(bool visible)
    {
        if (canvas != null) canvas.enabled = visible;
    }

    /// <summary>Fuerza un hitmarker (p. ej. para daño que no pase por Health).</summary>
    public void ShowHitMarker(bool kill = false)
    {
        if (hitMarker != null) hitMarker.Show(kill);
    }

    // ---------------- Eventos del gameplay ----------------

    private void HandleActiveWeaponChanged(Weapon weapon)
    {
        if (current != null) current.StateChanged -= HandleWeaponStateChanged;
        current = weapon;
        if (current != null) current.StateChanged += HandleWeaponStateChanged;
        if (ammoHUD != null) ammoHUD.SetWeapon(current);
    }

    private void HandleAmmoChanged(Weapon weapon)
    {
        if (weapon == current && ammoHUD != null) ammoHUD.OnAmmoChanged(weapon);
    }

    private void HandleWeaponStateChanged(Weapon.StateId previous, Weapon.StateId next)
    {
        if (ammoHUD != null) ammoHUD.OnWeaponStateChanged(previous, next);
    }

    private void HandleAnyDamaged(Health target, DamageInfo damage)
    {
        // Solo los impactos del jugador
        if (weaponManager == null || damage.Instigator != weaponManager.gameObject) return;
        ShowHitMarker(target.IsDead);
    }
}
