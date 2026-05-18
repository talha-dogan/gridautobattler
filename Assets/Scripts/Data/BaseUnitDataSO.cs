using UnityEngine;

// Identity card for the AI to understand roles
public enum UnitType { Melee, Ranged }

public abstract class BaseUnitDataSO : ScriptableObject
{
    [Header("General Info")]
    public string unitName;
    public UnitType unitType; // AI will check this to counter you!
    public GameObject unitPrefab;

    [Header("Combat Stats")]
    public float maxHealth;
    public float attackDamage;
    public float attackRange;
    public float attackCooldown;

    [Header("Movement")]
    public float moveSpeed;

    [Header("Juice Settings")]
    // Visual breathing effects for a more "alive" feel
    public float idleBreathingSpeed = 3f;
    public float moveBreathingSpeed = 8f;
    public float breathingAmplitude = 0.05f;
}