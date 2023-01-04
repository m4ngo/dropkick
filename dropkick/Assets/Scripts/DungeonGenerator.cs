using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Riptide;

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private string seed;
    [SerializeField] private bool randomSeed = true;

    [SerializeField] private GameObject checkpoint;
    [SerializeField] private GameObject endRoom;
    [SerializeField] private GameObject[] leftRightRooms;
    [SerializeField] private GameObject[] leftUpRooms;
    [SerializeField] private GameObject[] leftDownRooms;
    [SerializeField] private GameObject[] upDownRooms;
    [SerializeField] private GameObject[] upRightRooms;
    [SerializeField] private GameObject[] downUpRooms;
    [SerializeField] private GameObject[] downRightRooms;

    [SerializeField] private int dungeonLength = 12;

    [SerializeField] private Vector2 roomSize;
    [SerializeField] private List<Vector2> dirs = new List<Vector2>();
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
            seed += "0";
            for (int i = 0; i < dungeonLength - 1; i++)
                //seed += i == dungeonLength / 2 ? 2 : Random.Range(0, leftRightRooms.Length);
                seed += Random.Range(1, leftRightRooms.Length);
        }

        //generate the dungeon here
        GenerateMainBranch();

        return seed;
    }

    void GenerateMainBranch()
    {
        dirs.Clear();

        for (int i = 0; i < dungeonLength; i++)
        {
            Vector2 dir = Vector2.zero;
            dir.y = (int.Parse(seed.Substring(i, 1)) - 1) * roomSize.x;

            if (i > 0)
            {
                if (dirs[i - 1].y + dir.y == 0)
                    dir.y = 0;
            }

            if (dir.y == 0)
                dir.x = roomSize.x;

            dirs.Add(dir);
        }

        for (int i = 0; i < dungeonLength - 1; i++)
        {
            Vector2 dir = dirs[i];
            Vector2 prevDir = dirs[i <= 0 ? 0 : i - 1];

            //AAAAA THIS CODE IS DOGSHIT
            //hehehehe code dupliction go BRRRR

            if (prevDir.x > 0)
            {
                if (dir.y > 0)
                    Instantiate(leftUpRooms[int.Parse(seed.Substring(dungeonLength + i, 1))], pos, Quaternion.identity, transform);
                else if (dir.y < 0)
                    Instantiate(leftDownRooms[int.Parse(seed.Substring(dungeonLength + i, 1))], pos, Quaternion.identity, transform);
                else
                    Instantiate(leftRightRooms[int.Parse(seed.Substring(dungeonLength + i, 1))], pos, Quaternion.identity, transform);
            }
            else if (prevDir.y > 0)
            {
                if (dir.y > 0)
                    Instantiate(downUpRooms[int.Parse(seed.Substring(dungeonLength + i, 1))], pos, Quaternion.identity, transform);
                else
                    Instantiate(downRightRooms[int.Parse(seed.Substring(dungeonLength + i, 1))], pos, Quaternion.identity, transform);
            }
            else
            {
                if (dir.y < 0)
                    Instantiate(upDownRooms[int.Parse(seed.Substring(dungeonLength + i, 1))], pos, Quaternion.identity, transform);
                else
                    Instantiate(upRightRooms[int.Parse(seed.Substring(dungeonLength + i, 1))], pos, Quaternion.identity, transform);
            }

            if(i == dungeonLength / 2)
                Instantiate(checkpoint, pos, Quaternion.identity, transform);

            pos += dir;
        }

        Instantiate(endRoom, pos, Quaternion.identity, transform);
    }

    [MessageHandler((ushort)ServerToClientId.DungeonGenerate, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void DungeonGenerate(Message message)
    {
        string seed = message.GetString();
        NetworkManager.Singleton.clientGen.SetSeed(seed);
        NetworkManager.Singleton.clientGen.StartGenerator();
    }
}