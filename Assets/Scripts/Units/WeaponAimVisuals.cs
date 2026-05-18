using UnityEngine;

public class WeaponAimVisuals : MonoBehaviour
{
    [SerializeField] private SpriteRenderer weaponSprite;

    // This method is called by the RangedUnit during its Update
    public void UpdateAimVisuals(Vector3 targetPosition)
    {
        // Calculate the direction from the weapon to the target
        Vector3 direction = targetPosition - transform.position;

        // Calculate the angle in degrees
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Apply the rotation to the Z axis
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Flip the weapon sprite on the Y axis if aiming to the left 
        // to prevent it from appearing upside down
        if (weaponSprite != null)
        {
            weaponSprite.flipY = (angle > 90f || angle < -90f);
        }
    }
}