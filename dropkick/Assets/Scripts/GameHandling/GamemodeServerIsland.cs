using Riptide;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GamemodeServerIsland : MonoBehaviour
{
    [Header("Player Tile Proximity Detection")]
    [SerializeField] private LayerMask tileMask;
    [SerializeField] private float tileRadius;

    [Header("Tile")]
    [SerializeField] private float tileCrumbleSpeed = 0.75f;
    private float curCrumble = 1f;
    private Gamemode mode;
    private List<ushort> deathOrder = new List<ushort>();

    private List<GameObject> tiles = new List<GameObject>();
    private List<int> counts = new List<int>();

    private bool init = false;

    void CrumbleTile(int who)
    {
        counts[who]--;
        if (counts[who] <= 0)
        {
            Destroy(tiles[who]);
            tiles.RemoveAt(who);
            counts.RemoveAt(who);
        }
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.CrumbleTile);
        message.AddInt(who);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void Start()
    {
        mode = GetComponent<Gamemode>();
    }

    private void Update()
    {
        if(!init)
        {
            foreach (Transform child in transform)
            {
                tiles.Add(child.gameObject);
                counts.Add(4);
            }
            init = true;
        }

        //check if players are dead
        bool allFrozen = true;
        foreach (ServerPlayer p in ServerPlayer.List.Values)
        {
            PlayerMovement m = p.GetComponent<PlayerMovement>();
            if (m.deathTimer > 0)
            {
                m.Freeze(true);
                deathOrder.Add(p.Id);
            }
            if (!m.freeze)
            {
                allFrozen = false;
            }
        }

        //crumble tiles
        curCrumble -= Time.deltaTime;
        if (curCrumble <= 0)
        {
            int rand = Random.Range(0, transform.childCount);
            CrumbleTile(rand);
            curCrumble = tileCrumbleSpeed;
        }

        //end game
        if (allFrozen)
        {
            int s = 3;
            for (int i = deathOrder.Count - 1; i >= 0; i--)
            {
                if (s <= 0)
                {
                    break;
                }
                mode.AddScore(deathOrder[i], s);
                s--;
            }

            NetworkManager.Singleton.EndGamemode();
        }
    }
}
