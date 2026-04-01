using UnityEngine;

public class PersistentBackgroundMusic : MonoBehaviour
{
    // Ito ang magiging static reference para siguradong iisa lang ang Background Music sa buong laro
    private static PersistentBackgroundMusic instance;

    void Awake()
    {
        // I-check kung may existing na Background Music at kung hindi ito ang current object
        if (instance != null && instance != this)
        {
            // I-destroy ang bagong kopya para iwas duplicate
            Destroy(gameObject);
            return;
        }

        // Kung wala pa, ito na ang magiging nag-iisang instance
        instance = this;
        
        // Sabihin sa Unity na wag ide-destroy ang GameObject na ito paglipat ng scene
        DontDestroyOnLoad(gameObject);
    }
}
