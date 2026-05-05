// Assets/Scripts/Core/ObjectPoolTester.cs
using UnityEngine;

public class ObjectPoolTester : MonoBehaviour
{
    [SerializeField] private BaseItem _itemPrefab;   
    [SerializeField] private int      _poolSize = 5;

    private ObjectPool<BaseItem> _pool;

    private void Start()
    {
        _pool = new ObjectPool<BaseItem>(_itemPrefab, _poolSize, this.transform);
        Debug.Log($"[Pool] Initialized with {_poolSize} objects.");
    }

    private void Update()
    {
        // G tuşu → havuzdan al
        if (Input.GetKeyDown(KeyCode.G))
        {
            BaseItem item = _pool.Get();
            Debug.Log($"[Pool] Got: {item.ItemName}");
        }

        // R tuşu → ilk aktif objeyi iade et
        if (Input.GetKeyDown(KeyCode.R))
        {
            BaseItem child = GetComponentInChildren<BaseItem>();
            if (child != null)
            {
                _pool.Return(child);
                Debug.Log($"[Pool] Returned: {child.ItemName}");
            }
        }
    }
}