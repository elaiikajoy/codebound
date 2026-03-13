using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SideScrolling : MonoBehaviour
{
    private Transform player;
    public float maxScrollPosition = 50f; // Adjust this per level

    private void Awake()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("No GameObject with 'Player' tag found!");
            return;
        }
        player = playerObj.transform;
    }

    private void LateUpdate()
    {
        Vector3 cameraPosition = transform.position;
        cameraPosition.x = Mathf.Max(player.position.x, cameraPosition.x);
        cameraPosition.x = Mathf.Min(cameraPosition.x, maxScrollPosition);
        transform.position = cameraPosition;
    }
}
