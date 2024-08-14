using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamemodeClientRace : MonoBehaviour
{
    private void Start()
    {
        //initialize the dungeon gen
        GameObject.FindGameObjectWithTag("ClientDungeonGenerator").GetComponent<DungeonGenerator>().GenerateDungeon();
    }
}
