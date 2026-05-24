/// <summary>
/// Oyundaki tüm VFX tiplerini tanımlar.
/// VFXManager bu enum üzerinden doğru particle prefab'ını seçer.
/// </summary>
public enum VFXType
{
    HitFlesh,       // Et/deri isabeti
    HitMetal,       // Metal/zırh isabeti
    UnitDeath,      // Ölüm patlaması
    BattleWin,      // Zafer konfeti/ışık
    ProjectileHit,  // Mermi çarpma
    Spawn,          // Birim spawn efekti
}
