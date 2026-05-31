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
        // 1. Reset all base game data while keeping settings intact
        GameSaveService.Instance?.ResetGameData();

        // 2. Define startup defaults (500 Gold, Level 1)
        int startupGold = 500;
        int startingLevelIndex = 0; // 0-based index for Level 1

        // 3. Apply changes to PlayerPrefs (Simulate 'First Time' state)
        PlayerPrefs.SetInt("PlayerGold", startupGold);
        PlayerPrefs.SetInt("IsFirstTimePlaying", 1);
        
        // Replace "CurrentLevel" with the exact key your system uses if it's different
        PlayerPrefs.SetInt("CurrentLevel", startingLevelIndex); 
        PlayerPrefs.Save();

        // 4. Sync with GameSaveService if it's active
        if (GameSaveService.Instance != null)
        {
            GameSaveService.Instance.SetGold(startupGold);
            GameSaveService.Instance.SetUnlockedPawnCount(1); // Reset inventory slots to 1
            
            // If your GameSaveService has a SetLevel or SetCurrentLevel method, call it here:
            // GameSaveService.Instance.SetCurrentLevel(startingLevelIndex);
        }

        // 5. Broadcast the gold update so the UI instantly reflects the 500 startup money
        GameEvents.SetGold(startupGold);

        // 6. Force reload the scene to cleanly start from Level 1
        // Note: Make sure UnityEngine.SceneManagement is added at the top if you use this
        if (Application.isPlaying)
        {
            LevelManager lm = FindAnyObjectByType<LevelManager>();
            if (lm != null)
            {
                // Optionally trigger a level reload through LevelManager if available
                var loadMethod = typeof(LevelManager).GetMethod("LoadLevel", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadMethod?.Invoke(lm, new object[] { startingLevelIndex });
            }
        }

        Debug.Log("[SettingsManager] Game data completely reset. Injected 500 startup gold and reverted to Level 1.");

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
