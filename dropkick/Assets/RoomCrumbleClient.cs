using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomCrumbleClient : MonoBehaviour
{
    public MeshRenderer rend;
    public Material[] materials;
    private int remaining = 4;

    public void Crumble()
    {
        remaining--;
        if(remaining <= 0)
        {
            Destroy(gameObject);
        }
        rend.material = materials[remaining - 1];
    }
}
