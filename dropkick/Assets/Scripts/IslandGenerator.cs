using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandGenerator : MonoBehaviour
{

    [Header("Generation Settings")]
    [SerializeField] private GameObject[] tiles;
    [SerializeField] private Vector3 arenaBounds;
    [SerializeField] private float stepSize = 1.0f;
    [SerializeField] private float tileVariance = 0.5f;

    private void Start()
    {
        GenerateArena();
    }

    public void GenerateArena()
    {
        Random.InitState(NetworkManager.Singleton.seed);
        Vector3 curPos = new Vector3(-arenaBounds.x, 0, -arenaBounds.z);
        for (int x = 0; x < Mathf.Abs(arenaBounds.x * 2 / stepSize); x++)
        {
            for (int z = 0; z < Mathf.Abs(arenaBounds.z * 2 / stepSize); z++)
            {
                int randTile = Random.Range(0, tiles.Length);
                Vector3 pos = curPos + new Vector3(Random.Range(-tileVariance, tileVariance), 0, Random.Range(-tileVariance, tileVariance));
                Instantiate(tiles[randTile], pos + new Vector3(0, 0.05f * Random.Range(-1f, 1f), 0), Quaternion.Euler(0, Random.Range(-15f, 15f), 0), transform);
                curPos.z += stepSize;
            }
            curPos.x += stepSize;
            curPos.z = -arenaBounds.z;
        }
    }
}
