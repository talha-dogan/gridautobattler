using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// SFX Enum — her ses efekti için bir giriş.
// Inspector'da AudioClip slotları bu enum değerleriyle eşleşir.
// ─────────────────────────────────────────────────────────────────────────────
public enum SoundType
{
    // Silah sesleri
    WeaponShoot,
    WeaponMeleeSwing,

    // Hasar / Hit
    HitFlesh,
    HitMetal,

    // Ölüm
    UnitDeath,

    // Savaş sonucu
    BattleWin,
    BattleLose,

    // Savaş başlangıcı
    BattleStart,
}

// ─────────────────────────────────────────────────────────────────────────────
// SoundEntry — Inspector'da her SoundType için birden fazla clip atanabilir.
// Oynatılacak clip rastgele seçilir (çeşitlilik için).
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class SoundEntry
{
    [Tooltip("Bu ses tipine karşılık gelen ses efektleri (birden fazla olursa rastgele seçilir).")]
    public SoundType soundType;

    [Tooltip("Bu ses tipi için kullanılacak AudioClip listesi.")]
    public AudioClip[] clips;

    [Range(0f, 1f)]
    [Tooltip("Bu ses tipinin varsayılan ses seviyesi.")]
    public float volume = 1f;

    [Range(0.8f, 1.2f)]
    [Tooltip("Pitch varyasyon aralığı (min). Çeşitlilik için hafif rastgele pitch uygulanır.")]
    public float pitchMin = 0.95f;

    [Range(0.8f, 1.2f)]
    [Tooltip("Pitch varyasyon aralığı (max).")]
    public float pitchMax = 1.05f;
}

// ─────────────────────────────────────────────────────────────────────────────
// SoundManager
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Merkezi ses yöneticisi.
///
/// Özellikler:
///   • AudioSource Pool — her seste yeni bir AudioSource oluşturmak yerine
///     önceden oluşturulmuş bir havuzdan kaynak alır, bitince geri verir.
///   • SoundEntry sistemi — Inspector'dan her SoundType için birden fazla clip
///     atanabilir; oynatılacak clip rastgele seçilir.
///   • Arka plan müziği — ayrı bir AudioSource üzerinde döngüsel olarak çalar,
///     sahne geçişlerinde yumuşak fade in/out destekler.
///   • GameEvents entegrasyonu — BattleWin, BattleLose, BattleStart olaylarını
///     dinler ve otomatik olarak ilgili sesleri çalar.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — SFX
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── SFX Kütüphanesi ───────────────────────────────────────")]
    [Tooltip("Her SoundType için ses efektlerini buraya atayın.")]
    [SerializeField] private SoundEntry[] soundLibrary;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Arka Plan Müziği
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── Arka Plan Müziği ─────────────────────────────────────")]
    [Tooltip("Savaş sırasında çalacak arka plan müziği.")]
    [SerializeField] private AudioClip battleBGM;

    [Tooltip("Menü / bekleme ekranında çalacak arka plan müziği.")]
    [SerializeField] private AudioClip menuBGM;

    [Range(0f, 1f)]
    [Tooltip("Arka plan müziğinin ses seviyesi.")]
    [SerializeField] private float bgmVolume = 0.4f;

    [Tooltip("Müzik geçişlerinde fade süresi (saniye).")]
    [SerializeField] private float bgmFadeDuration = 1.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Pool Ayarları
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── AudioSource Pool Ayarları ──────────────────────────")]
    [Tooltip("Başlangıçta oluşturulacak AudioSource sayısı.")]
    [SerializeField] private int initialPoolSize = 10;

    [Tooltip("Pool dolduğunda ek olarak oluşturulabilecek maksimum AudioSource sayısı.")]
    [SerializeField] private int maxPoolSize = 20;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — Genel Ses Kontrolleri
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── Genel Ses Kontrolleri ──────────────────────────────")]
    [Range(0f, 1f)]
    [Tooltip("Tüm SFX'lerin ana ses seviyesi çarpanı.")]
    [SerializeField] private float masterSFXVolume = 1f;

    [Range(0f, 1f)]
    [Tooltip("Arka plan müziğinin ana ses seviyesi çarpanı.")]
    [SerializeField] private float masterBGMVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Pool
    // ─────────────────────────────────────────────────────────────────────────

    private readonly Queue<AudioSource> _pool = new Queue<AudioSource>();
    private readonly List<AudioSource>  _active = new List<AudioSource>();
    private Transform _poolContainer;

    // ─────────────────────────────────────────────────────────────────────────
    // Private — BGM
    // ─────────────────────────────────────────────────────────────────────────

    private AudioSource _bgmSource;
    private Coroutine   _bgmFadeCoroutine;

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Lookup
    // ─────────────────────────────────────────────────────────────────────────

    private readonly Dictionary<SoundType, SoundEntry> _soundMap =
        new Dictionary<SoundType, SoundEntry>();

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSoundMap();
        InitPool();
        InitBGMSource();
    }

    private void OnEnable()
    {
        GameEvents.OnBattleStarted += OnBattleStarted;
        GameEvents.OnLevelWin      += OnLevelWin;
        GameEvents.OnLevelLose     += OnLevelLose;
        GameEvents.OnUnitDied      += OnUnitDied;
    }

    private void OnDisable()
    {
        GameEvents.OnBattleStarted -= OnBattleStarted;
        GameEvents.OnLevelWin      -= OnLevelWin;
        GameEvents.OnLevelLose     -= OnLevelLose;
        GameEvents.OnUnitDied      -= OnUnitDied;
    }

    private void Update()
    {
        // Pool'a geri dön: çalmayı bitirmiş aktif kaynakları serbest bırak
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            AudioSource src = _active[i];
            if (src == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            if (!src.isPlaying)
            {
                _active.RemoveAt(i);
                ReturnToPool(src);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — SFX
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Belirtilen ses tipini çalar.
    /// </summary>
    public void PlaySound(SoundType type)
    {
        if (!_soundMap.TryGetValue(type, out SoundEntry entry)) return;
        if (entry.clips == null || entry.clips.Length == 0) return;

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (clip == null) return;

        AudioSource src = GetFromPool();
        if (src == null) return;

        src.clip        = clip;
        src.volume      = entry.volume * masterSFXVolume;
        src.pitch       = Random.Range(entry.pitchMin, entry.pitchMax);
        src.spatialBlend = 0f; // 2D ses
        src.Play();

        _active.Add(src);
    }

    /// <summary>
    /// Belirtilen ses tipini dünya konumunda 3D olarak çalar.
    /// </summary>
    public void PlaySoundAtPosition(SoundType type, Vector3 worldPosition)
    {
        if (!_soundMap.TryGetValue(type, out SoundEntry entry)) return;
        if (entry.clips == null || entry.clips.Length == 0) return;

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (clip == null) return;

        AudioSource src = GetFromPool();
        if (src == null) return;

        src.transform.position = worldPosition;
        src.clip               = clip;
        src.volume             = entry.volume * masterSFXVolume;
        src.pitch              = Random.Range(entry.pitchMin, entry.pitchMax);
        src.spatialBlend       = 1f; // 3D ses
        src.Play();

        _active.Add(src);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — BGM
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Arka plan müziğini çalar. Zaten aynı clip çalıyorsa hiçbir şey yapmaz.
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

        if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
        _bgmFadeCoroutine = StartCoroutine(FadeBGM(clip));
    }

    /// <summary>
    /// Arka plan müziğini durdurur (fade out ile).
    /// </summary>
    public void StopBGM()
    {
        if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
        _bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
    }

    /// <summary>
    /// Savaş BGM'ini çalar.
    /// </summary>
    public void PlayBattleBGM()
    {
        if (battleBGM != null) PlayBGM(battleBGM);
    }

    /// <summary>
    /// Menü BGM'ini çalar.
    /// </summary>
    public void PlayMenuBGM()
    {
        if (menuBGM != null) PlayBGM(menuBGM);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Volume Kontrolleri
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Ana SFX ses seviyesini ayarlar (0-1).</summary>
    public void SetMasterSFXVolume(float volume)
    {
        masterSFXVolume = Mathf.Clamp01(volume);
    }

    /// <summary>Ana BGM ses seviyesini ayarlar (0-1).</summary>
    public void SetMasterBGMVolume(float volume)
    {
        masterBGMVolume = Mathf.Clamp01(volume);
        if (_bgmSource != null)
            _bgmSource.volume = bgmVolume * masterBGMVolume;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GameEvents Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnBattleStarted()
    {
        PlaySound(SoundType.BattleStart);
        PlayBattleBGM();
    }

    private void OnLevelWin(string message)
    {
        StopBGM();
        PlaySound(SoundType.BattleWin);
    }

    private void OnLevelLose(string message)
    {
        StopBGM();
        PlaySound(SoundType.BattleLose);
    }

    private void OnUnitDied(BaseUnit unit)
    {
        if (unit == null) return;
        PlaySoundAtPosition(SoundType.UnitDeath, unit.transform.position);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pool — Dahili
    // ─────────────────────────────────────────────────────────────────────────

    private void InitPool()
    {
        _poolContainer = new GameObject("AudioSourcePool").transform;
        _poolContainer.SetParent(transform);

        for (int i = 0; i < initialPoolSize; i++)
            _pool.Enqueue(CreateAudioSource());
    }

    private AudioSource CreateAudioSource()
    {
        GameObject go = new GameObject("PooledAudioSource");
        go.transform.SetParent(_poolContainer);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        return src;
    }

    private AudioSource GetFromPool()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        // Pool boşsa ve maksimum boyuta ulaşılmadıysa yeni kaynak oluştur
        if (_active.Count < maxPoolSize)
        {
            AudioSource newSrc = CreateAudioSource();
            return newSrc;
        }

        // Maksimum boyuta ulaşıldı — en eski aktif kaynağı zorla geri al
        if (_active.Count > 0)
        {
            AudioSource oldest = _active[0];
            _active.RemoveAt(0);
            oldest.Stop();
            return oldest;
        }

        return null;
    }

    private void ReturnToPool(AudioSource src)
    {
        if (src == null) return;
        src.clip  = null;
        src.pitch = 1f;
        src.transform.position = Vector3.zero;
        _pool.Enqueue(src);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BGM — Dahili
    // ─────────────────────────────────────────────────────────────────────────

    private void InitBGMSource()
    {
        GameObject bgmGO = new GameObject("BGMSource");
        bgmGO.transform.SetParent(transform);
        _bgmSource             = bgmGO.AddComponent<AudioSource>();
        _bgmSource.loop        = true;
        _bgmSource.playOnAwake = false;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.volume      = bgmVolume * masterBGMVolume;
    }

    private IEnumerator FadeBGM(AudioClip newClip)
    {
        // Fade out mevcut müzik
        if (_bgmSource.isPlaying)
        {
            float startVol = _bgmSource.volume;
            float elapsed  = 0f;
            while (elapsed < bgmFadeDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / (bgmFadeDuration * 0.5f));
                yield return null;
            }
            _bgmSource.Stop();
        }

        // Yeni müziği başlat ve fade in
        _bgmSource.clip   = newClip;
        _bgmSource.volume = 0f;
        _bgmSource.Play();

        float fadeInElapsed = 0f;
        float targetVol     = bgmVolume * masterBGMVolume;
        while (fadeInElapsed < bgmFadeDuration * 0.5f)
        {
            fadeInElapsed += Time.deltaTime;
            _bgmSource.volume = Mathf.Lerp(0f, targetVol, fadeInElapsed / (bgmFadeDuration * 0.5f));
            yield return null;
        }

        _bgmSource.volume = targetVol;
        _bgmFadeCoroutine = null;
    }

    private IEnumerator FadeOutBGM()
    {
        float startVol = _bgmSource.volume;
        float elapsed  = 0f;
        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.deltaTime;
            _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / bgmFadeDuration);
            yield return null;
        }
        _bgmSource.Stop();
        _bgmSource.clip   = null;
        _bgmFadeCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lookup — Dahili
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildSoundMap()
    {
        _soundMap.Clear();
        if (soundLibrary == null) return;

        foreach (SoundEntry entry in soundLibrary)
        {
            if (!_soundMap.ContainsKey(entry.soundType))
                _soundMap[entry.soundType] = entry;
            else
                Debug.LogWarning($"[SoundManager] '{entry.soundType}' için mükerrer giriş var. İlk giriş kullanılacak.");
        }
    }
}
