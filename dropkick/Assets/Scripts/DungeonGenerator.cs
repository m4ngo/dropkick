using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Riptide;

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private int seed;
    [SerializeField] private GameObject checkpoint;
    [SerializeField] private GameObject room;
    [SerializeField] private int dungeonLength = 12;

    [SerializeField] private Vector2 roomSize;
    private Vector3 pos;

    public void SetSeed(int seed) { this.seed = seed; }

    public int StartGenerator(bool isServer)
    {
        pos = Vector3.zero;
        if(isServer)
            seed = Random.Range(0, 214748364);
        Random.InitState(seed);

        //generate the dungeon here
        GenerateMainBranch();

        return seed;
    }

    void GenerateMainBranch()
    {
        Vector2 prevDir = Vector2.zero;
        for (int i = 0; i < dungeonLength; i++)
        {
            Vector2 dir = Vector2.zero;
            if(Random.Range(0,2) == 1){
                dir.x = roomSize.x;
            }
            else{
                dir.y = roomSize.y * (Random.Range(0,2)==1?1:-1);
                if(dir.y + prevDir.y == 0){
                    dir.y *= -1f;
                }
            }
            
            Instantiate(room, pos, Quaternion.identity, transform);
            //create checkpoints halfway through
            if(i % (dungeonLength / 3) == 0 || i == dungeonLength - 1)
                Instantiate(checkpoint, pos, Quaternion.identity, transform);

            pos += new Vector3(dir.x, 0, dir.y);
            prevDir = dir;
        }
    }

    [MessageHandler((ushort)ServerToClientId.DungeonGenerate, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void DungeonGenerate(Message message)
    {
        int seed = message.GetInt();
        NetworkManager.Singleton.clientGen.SetSeed(seed);
        NetworkManager.Singleton.clientGen.StartGenerator(false);
    }
}