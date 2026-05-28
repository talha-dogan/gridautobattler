using UnityEngine;

// Identity card for the AI to understand roles
public enum UnitType { Melee, Ranged }

public abstract class BaseUnitDataSO : ScriptableObject
{
    [Header("General Info")]
    public string unitName;
    public UnitType unitType;
    public GameObject unitPrefab;

    [Header("Combat Stats")]
    public float maxHealth;
    public float attackDamage;
    public float attackRange;
    public float attackCooldown;

    [Header("Movement")]
    public float moveSpeed;

    [Header("Stat Progression (optional)")]
    [Tooltip("Eğer atanırsa, Combat Stats değerleri curve'den hesaplanır. " +
             "Boş bırakılırsa sabit Combat Stats kullanılır.")]
    public StatProgressionSO statProgression;

    [Tooltip("Bu unit'in hangi max level'da değerlendirildiği.")]
    public int maxLevel = 20;

    [Header("Juice Settings")]
    public float idleBreathingSpeed = 3f;
    public float moveBreathingSpeed = 8f;
    public float breathingAmplitude = 0.05f;
}