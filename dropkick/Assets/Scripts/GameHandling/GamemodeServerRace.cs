using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GamemodeServerRace : MonoBehaviour
{
    [SerializeField] private float raceTime = 60f;
    private Gamemode mode;
    private DungeonGenerator gen;
    private int score = 3;
    private int total = 0;

    private void Start()
    {
        mode = GetComponent<Gamemode>();
        gen = GetComponent<DungeonGenerator>();
        //initialize the dungeon gen
        gen.GenerateDungeon();
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
        total++;
        if(score <= 0 || total == ServerPlayer.List.Count)
        {
            NetworkManager.Singleton.EndGamemode();
        }
    }
}
