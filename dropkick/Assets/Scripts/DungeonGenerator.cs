using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Riptide;

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private string seed;
    [SerializeField] private bool randomSeed = true;

    [SerializeField] private GameObject room;
    [SerializeField] private GameObject hall;

    [SerializeField] private int dungeonLength = 12;

    [SerializeField] private Vector2 roomSize;
    private Vector2 prevDir = Vector2.zero;
    private Vector2 pos;

    public void SetSeed(string seed) { this.seed = seed; }

    public string StartGenerator()
    {
        pos = Vector2.zero;

        if (randomSeed)
        {
            seed = "";
            for (int i = 0; i < dungeonLength; i++)
                seed += Random.Range(0, 3);
        }

        //generate the dungeon here
        GenerateMainBranch();

        return seed;
    }

    void GenerateMainBranch()
    {
        for (int i = 0; i < dungeonLength; i++)
        {
            GameObject r = Instantiate(room, pos, Quaternion.identity, transform);

            //get the random movement
            Vector2 dir = Vector2.zero;
            dir.y = (int.Parse(seed.Substring(i, 1)) - 1) * roomSize.x;
            if (prevDir.y + dir.y == 0)
                dir.y = 0;
            if (dir.y == 0)
                dir.x = roomSize.x;

            if(hall != null)
            {
                if (i < dungeonLength - 1)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    Instantiate(hall, pos, Quaternion.identity, transform).transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle - 90));
                }
            }

            pos += dir;
            prevDir = dir;
        }
    }

    [MessageHandler((ushort)ServerToClientId.DungeonGenerate, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void DungeonGenerate(Message message)
    {
        string seed = message.GetString();
        NetworkManager.Singleton.clientGen.SetSeed(seed);
        NetworkManager.Singleton.clientGen.StartGenerator();
    }
}