using UnityEngine;

public class ScrollingTexture : MonoBehaviour
{
    public float scrollSpeedX = 0.5f;
    public float scrollSpeedY = 0.5f;
    
    private Renderer rend;
    private Vector2 offset;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        offset.x += Time.deltaTime * scrollSpeedX;
        offset.y += Time.deltaTime * scrollSpeedY;
        
        rend.material.mainTextureOffset = offset;
    }
}