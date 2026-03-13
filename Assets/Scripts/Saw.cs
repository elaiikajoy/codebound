using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Saw : MonoBehaviour
{
    public float speed = 5f;
    int direction = 10;

    public Transform rightCheck;
    public Transform leftCheck;

    
    void FixedUpdate()
    {
        transform.Translate(Vector2.right * speed * direction * Time.fixedDeltaTime);

        if (Physics2D.Raycast(rightCheck.position, Vector2.down, 5) == false)
        {
            direction = -1;
        }

        if (Physics2D.Raycast(leftCheck.position, Vector2.down, 5) == false)
        {
            direction = 1;
        }
    }
}
