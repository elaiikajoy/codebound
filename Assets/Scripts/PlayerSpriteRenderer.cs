using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpriteRenderer : MonoBehaviour
{
   private SpriteRenderer spriteRenderer;
   private Movement movement;

   public Sprite idleSprite;
   public Sprite runSprite;
   public Sprite jumpSprite;
   public Sprite fallSprite;

   private void Awake()
   {
       spriteRenderer = GetComponent<SpriteRenderer>();
       movement = GetComponentInParent<Movement>();
   }

   private void LateUpdate()
   {
       spriteRenderer.flipX = movement.velocity.x < 0f;
   }
}
