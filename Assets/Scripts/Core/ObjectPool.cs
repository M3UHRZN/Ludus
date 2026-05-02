using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Component
{
    private readonly Queue<T>  _pool = new();
    private readonly T         _prefab;
    private readonly Transform _parent; // optional — keeps hierarchy clean

    public ObjectPool(T prefab, int initialSize, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < initialSize; i++)
            Return(CreateNew());
    }

    /// <summary>Get an object from the pool; instantiates a new one if empty.</summary>
    public T Get()
    {
        T obj = _pool.Count > 0 ? _pool.Dequeue() : CreateNew();
        obj.gameObject.SetActive(true);
        return obj;
    }

    /// <summary>Return an object back to the pool.</summary>
    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        if (_parent != null) obj.transform.SetParent(_parent);
        _pool.Enqueue(obj);
    }

    private T CreateNew() =>
        Object.Instantiate(_prefab, _parent);
}
