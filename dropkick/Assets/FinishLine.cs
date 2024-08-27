using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ServerPlayer"))
        {
            transform.parent.GetComponent<GamemodeServerRace>().FinishLineReached(other.GetComponent<ServerPlayer>().Id);
            other.GetComponent<PlayerMovement>().Freeze(true);
        }
    }
}
