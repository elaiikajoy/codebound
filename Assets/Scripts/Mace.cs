using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class Mace : MonoBehaviour
{
    public float speed = 5f;
    public float range = 10f;

    float startingY;
    int direction = 1;
    void Start()
    {
        startingY = transform.position.y;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.Translate(Vector2.up * speed * Time.deltaTime * direction);

        if(transform.position.y < startingY || transform.position.y > startingY + range)
        
            direction *= -1;
        
    }
}
