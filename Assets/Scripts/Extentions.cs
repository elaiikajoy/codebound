using UnityEngine;

public static class Extentions
{
    private static LayerMask layerMask = LayerMask.GetMask("Default");
    public static bool Raycast(this Rigidbody2D rigidbody, Vector2 direction)
    {
        if (rigidbody.isKinematic)
        {
            return false;
        }

        float radius = 0.375f;
        float distance = 0.25f;

        RaycastHit2D hit = Physics2D.CircleCast(rigidbody.position, radius, direction.normalized, distance, layerMask);
        return hit.collider != null && hit.rigidbody != rigidbody;
    }

    public static bool DoTest(this Transform transform, Transform other, Vector2 direction)
    {
        Vector2 directionToOther = other.position - transform.position;
        return Vector2.Dot(directionToOther.normalized, direction) > 0.5f;
    }
}
