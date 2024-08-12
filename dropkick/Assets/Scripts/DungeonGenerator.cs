using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Riptide;

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private int seed;
    [SerializeField] private GameObject checkpoint;
    [SerializeField] private GameObject[] rooms;
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
        //GenerateMainBranch();

        return seed;
    }

    void GenerateMainBranch()
    {
        Vector2 prevDir = Vector2.zero;
        float factor = Random.Range(0.8f, 2.0f);
        int count = Random.Range(1, 4);
        int roomType = 0;

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

            Vector3 yOffset = new Vector3(0,-0.01f * roomType,0);
            Instantiate(rooms[roomType], pos+yOffset, Quaternion.Euler(0, Random.Range(0, 90), 0), transform);
            //create checkpoints halfway through
            if(i % (dungeonLength / 2) == 0 || i == dungeonLength - 1)
                Instantiate(checkpoint, pos, Quaternion.identity, transform);
            
            pos += new Vector3(dir.x * factor, 0, dir.y * factor);

            count--;
            if(count <= 0){
                factor = Random.Range(0.5f, 1.5f);
                count = Random.Range(1, 4);
                roomType = Random.Range(0, rooms.Length);
            }
            
            prevDir = dir;
        }
    }

    [MessageHandler((ushort)ServerToClientId.InitializeGamemode, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void DungeonGenerate(Message message)
    {
        int seed = message.GetInt();
        NetworkManager.Singleton.clientGen.SetSeed(seed);
        NetworkManager.Singleton.clientGen.StartGenerator(false);
        UIManager.Singleton.GameStarted();
    }
}