using UnityEngine;

[CreateAssetMenu(fileName = "NewDamageTextData", menuName = "AutoBattler/Juice/Damage Text Data")]
public class DamageTextDataSO : ScriptableObject
{
    [Header("Visual Settings")]
    public GameObject textPrefab;
    public Color textColor = Color.white;
    public float floatSpeed = 2f;
    public float fadeDuration = 1f;

    [Header("Positioning")]
    // Karakterin merkezinden ne kadar yukarıda (kafada) doğacağını belirler
    public float spawnHeightOffset = 1.5f;
    public Vector2 randomOffsetRange = new Vector2(0.5f, 0.5f);
}