using TMPro;
using UnityEngine;

/// <summary>
/// Bir TextMeshProUGUI bileşenini otomatik olarak lokalize eder.
/// Dil değiştiğinde metni otomatik günceller.
///
/// Kullanım:
///   1. TextMeshProUGUI olan GameObject'e bu bileşeni ekle.
///   2. Inspector'dan localizationKey alanını doldur (örn: "ui.start_button").
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("LocalizationKeys sabitlerinden bir anahtar girin.")]
    [SerializeField] private string localizationKey;

    private TextMeshProUGUI _text;

    private void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= Refresh;
    }

    /// <summary>Metni mevcut dile göre günceller.</summary>
    public void Refresh()
    {
        if (_text == null || string.IsNullOrEmpty(localizationKey)) return;
        _text.text = LocalizationManager.Get(localizationKey);
    }

    /// <summary>Çalışma zamanında key'i değiştirip metni günceller.</summary>
    public void SetKey(string key)
    {
        localizationKey = key;
        Refresh();
    }
}
