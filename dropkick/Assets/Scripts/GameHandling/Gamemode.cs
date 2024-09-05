using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Gamemode : MonoBehaviour
{
    public Dictionary<ushort, int> scores = new Dictionary<ushort, int>();
    [SerializeField] private Vector3[] spawnPos;

    private void Start() {
        foreach(ushort id in ServerPlayer.List.Keys){
            scores.Add(id, 0);
        }

        //reset all player positions
        int i = 0;
        foreach (ServerPlayer p in ServerPlayer.List.Values)
        {
            p.transform.position = spawnPos[i];
            i++;
            if(i >= spawnPos.Length)
            {
                i = 0;
            }
            PlayerMovement move = p.GetComponent<PlayerMovement>();
            move.Freeze(false);
            move.SendResync();
        }
    }

    public void AddScore(ushort id, int score)
    {
        if (!scores.ContainsKey(id)) return;
        scores[id] += score;
    }

    public List<ushort> EndGame()
    {
        var sortedPlayers = scores.OrderBy(x => -x.Value).ToDictionary(x => x.Key, x => x.Value).Keys.ToList();
        List<ushort> tags = new List<ushort>();
        int count = 0;
        foreach(ushort s in sortedPlayers)
        {
            if (count >= 3) break;
            if (scores[s] <= 0) continue;
            tags.Add(s);
            count++;
        }
        return tags;
    }
}
