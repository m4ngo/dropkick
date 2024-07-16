using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteOutline : MonoBehaviour
{
    public float outlineThickness;
    SpriteRenderer outline;
    private SpriteRenderer rend;

    private void Awake()
    {
        rend = GetComponent<SpriteRenderer>();

        outline = new GameObject().AddComponent<SpriteRenderer>();
        outline.transform.SetParent(transform.parent);
        outline.sprite = rend.sprite;
        outline.sortingLayerName = "Player";
        outline.sortingOrder = -1;
        outline.color = Color.black;
    }
    
    private void LateUpdate()
    {
        outline.transform.localScale = new Vector2(rend.transform.localScale.x + outlineThickness * 2, rend.transform.localScale.y + outlineThickness * 2);
        outline.transform.localPosition = rend.transform.localPosition;
        outline.transform.localRotation = rend.transform.localRotation;
    }
}
