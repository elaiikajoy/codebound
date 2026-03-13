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
}
