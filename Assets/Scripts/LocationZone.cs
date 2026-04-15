using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LocationZone : MonoBehaviour
{
    [Tooltip("ID локации, которой принадлежит эта зона")]
    public string LocationID;
    
    [Tooltip("Радиус действия зоны (для отладки)")]
    public float DebugRadius = 1000f;
    
    private WorldSystemManager _worldManager;
    private bool _isInside = false;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        _worldManager = FindObjectOfType<WorldSystemManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && _worldManager != null && _worldManager.CurrentLocationID != LocationID)
        {
            Debug.Log($"[LocationZone] ✅ Игрок ВОШЁЛ в зону локации '{LocationID}'");
            _worldManager.ForceSetLocation(LocationID);
            _isInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[LocationZone] ⚠️ Игрок ВЫШЕЛ из зоны локации '{LocationID}'");
            _isInside = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        Gizmos.color = _isInside ? Color.green : new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, DebugRadius);
    }
}