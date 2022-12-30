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
    [SerializeField] private GameObject[] roomLayouts;

    [SerializeField] private GameObject startRoom;
    [SerializeField] private GameObject endRoom;
    [SerializeField] private GameObject checkpointRoom;

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
            for (int i = 0; i < dungeonLength - 2; i++)
                seed += Random.Range(0, roomLayouts.Length);
        }

        //generate the dungeon here
        GenerateMainBranch();

        return seed;
    }

    void GenerateMainBranch()
    {
        for (int i = 0; i < dungeonLength; i++)
        {
            //GameObject r = Instantiate(room, pos, Quaternion.identity, transform);

            //get the random movement
            Vector2 dir = Vector2.zero;
            dir.y = (int.Parse(seed.Substring(i, 1)) - 1) * roomSize.x;
            if (prevDir.y + dir.y == 0)
                dir.y = 0;
            if (dir.y == 0)
                dir.x = roomSize.x;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (i == 0)
                Instantiate(startRoom, pos, Quaternion.identity, transform);
            else if (i == dungeonLength - 1)
                Instantiate(endRoom, pos, Quaternion.identity, transform);
            else if (i == (dungeonLength - 1 )/2)
                Instantiate(checkpointRoom, pos, Quaternion.identity, transform);
            else
                Instantiate(roomLayouts[int.Parse(seed.Substring(dungeonLength + i - 1, 1))], pos, Quaternion.identity, transform).transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle + 90));
            
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