using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Characters
{
    public string CharacterName;
    public Sprite characterSprite;
    public int cost; // Cost to buy this character (0 = free)

    // Backend character ID — must match the character "id" field in /characters/available.
    // Example: "default", "ninja", "wizard"
    // Set this in the Inspector for each character in CharacterDatabase.
    public string characterId;

    // Animator Controller for this character's animations.
    // Assign the character's .controller asset here in the CharacterDatabase ScriptableObject.
    // If left empty, the player's existing Animator Controller will not be changed.
    public RuntimeAnimatorController animatorController;
}
