using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Settings panelini yönetir.
/// SFX, Müzik, Grafik Kalitesi, Çözünürlük ve Oyun Verisi Sıfırlama seçeneklerini içerir.
///
/// Save/Load: PlayerPrefs yerine GameSaveService (binary + AES) kullanır.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Panel
    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Panel ───────────────────────────────────────────────")]
    [SerializeField] private GameObject settingsPanel;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — SFX
    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── SFX ─────────────────────────────────────────────────")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Müzik
    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Müzik ───────────────────────────────────────────────")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TextMeshProUGUI musicValueText;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Grafik Kalitesi
    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Grafik Kalitesi ──────────────────────────────────────")]
    [SerializeField] private TMP_Dropdown qualityDropdown;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Çözünürlük
    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Çözünürlük ───────────────────────────────────────────")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Reset
    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Reset ────────────────────────────────────────────────")]
    [SerializeField] private GameObject resetConfirmPanel;

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────
    private Resolution[] _resolutions;
    private int          _currentResolutionIndex;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        LoadSettings();
    }

    private void Start()
    {
        InitResolutionDropdown();
        InitQualityDropdown();
        ApplyLoadedSettings();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public — Panel Aç/Kapat
    // ─────────────────────────────────────────────────────────────────────────

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)      settingsPanel.SetActive(false);
        if (resetConfirmPanel != null)  resetConfirmPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public — SFX
    // ─────────────────────────────────────────────────────────────────────────

    public void OnSFXVolumeChanged(float value)
    {
        GameSaveService.Instance?.SetSFXVolume(value);

        if (sfxValueText != null)
            sfxValueText.text = Mathf.RoundToInt(value * 100f) + "%";

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMasterSFXVolume(value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public — Müzik
    // ─────────────────────────────────────────────────────────────────────────

    public void OnMusicVolumeChanged(float value)
    {
        GameSaveService.Instance?.SetMusicVolume(value);

        if (musicValueText != null)
            musicValueText.text = Mathf.RoundToInt(value * 100f) + "%";

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMasterBGMVolume(value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public — Grafik Kalitesi
    // ─────────────────────────────────────────────────────────────────────────

    public void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index);
        GameSaveService.Instance?.SetQualityLevel(index);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public — Çözünürlük
    // ─────────────────────────────────────────────────────────────────────────

    public void OnResolutionChanged(int index)
    {
        if (_resolutions == null || index >= _resolutions.Length) return;

        Resolution res = _resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        _currentResolutionIndex = index;
        GameSaveService.Instance?.SetResolutionIndex(index);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public — Reset Game Data
    // ─────────────────────────────────────────────────────────────────────────

    public void ShowResetConfirm()
    {
        if (resetConfirmPanel != null) resetConfirmPanel.SetActive(true);
    }

    public void HideResetConfirm()
    {
        if (resetConfirmPanel != null) resetConfirmPanel.SetActive(false);
    }

    public void ConfirmResetGameData()
    {
        // Ayarlar korunarak oyun verisi sıfırlanır
        GameSaveService.Instance?.ResetGameData();

        Debug.Log("[SettingsManager] Oyun verisi sıfırlandı.");

        if (resetConfirmPanel != null) resetConfirmPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Init
    // ─────────────────────────────────────────────────────────────────────────

    private void InitResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        var options    = new List<string>();
        int savedIndex = GameSaveService.Instance != null
            ? GameSaveService.Instance.GetResolutionIndex()
            : -1;

        _currentResolutionIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            Resolution r = _resolutions[i];
            options.Add($"{r.width} x {r.height}  {Mathf.RoundToInt((float)r.refreshRateRatio.value)}Hz");

            if (savedIndex < 0 &&
                r.width  == Screen.currentResolution.width &&
                r.height == Screen.currentResolution.height)
            {
                _currentResolutionIndex = i;
            }
        }

        if (savedIndex >= 0 && savedIndex < _resolutions.Length)
            _currentResolutionIndex = savedIndex;

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = _currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void InitQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        var names = new List<string>(QualitySettings.names);
        qualityDropdown.AddOptions(names);

        int saved = GameSaveService.Instance != null
            ? GameSaveService.Instance.GetQualityLevel()
            : QualitySettings.GetQualityLevel();

        qualityDropdown.value = Mathf.Clamp(saved, 0, names.Count - 1);
        qualityDropdown.RefreshShownValue();
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
    }

    private void LoadSettings()
    {
        if (GameSaveService.Instance == null) return;

        float sfxVol   = GameSaveService.Instance.GetSFXVolume();
        float musicVol = GameSaveService.Instance.GetMusicVolume();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMasterSFXVolume(sfxVol);
            SoundManager.Instance.SetMasterBGMVolume(musicVol);
        }
    }

    private void ApplyLoadedSettings()
    {
        float sfxVol   = GameSaveService.Instance != null ? GameSaveService.Instance.GetSFXVolume()   : 1f;
        float musicVol = GameSaveService.Instance != null ? GameSaveService.Instance.GetMusicVolume() : 1f;

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfxVol);
            if (sfxValueText != null)
                sfxValueText.text = Mathf.RoundToInt(sfxVol * 100f) + "%";
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(musicVol);
            if (musicValueText != null)
                musicValueText.text = Mathf.RoundToInt(musicVol * 100f) + "%";
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
    }
}
