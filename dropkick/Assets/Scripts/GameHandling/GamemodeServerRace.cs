using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamemodeServerRace : MonoBehaviour
{
    [SerializeField] private float raceTime = 60f;
    private Gamemode mode;
    private int score = 3;

    private void Start()
    {
        mode = GetComponent<Gamemode>();
        //initialize the dungeon gen
        NetworkManager.Singleton.GetComponent<DungeonGenerator>().GenerateDungeon();
    }

    private void Update()
    {
        raceTime -= Time.deltaTime;
        if(raceTime <= 0)
        {
            NetworkManager.Singleton.EndGamemode();
        }
    }

    public void FinishLineReached(ushort id)
    {
        mode.AddScore(id, score);
        score--;
        if(score <= 0)
        {
            NetworkManager.Singleton.EndGamemode();
        }
    }
}
