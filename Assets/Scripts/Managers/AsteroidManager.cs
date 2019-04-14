﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Utilities;
using Events;
using JetBrains.Annotations;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Responsible for spawning and keeping track of asteroids.
/// </summary>
public class AsteroidManager : MonoBehaviour, IPoolObserver<Asteroid>, IEnumerable<Asteroid>
{
    [Header("Setup")]
    public GameObject ParentContainer;
    public AsteroidData AsteroidData;
    private Dictionary<int, ObjectPool<Asteroid>> _pools;

    public AsteroidManager()
    {
        _pools = new Dictionary<int, ObjectPool<Asteroid>>();
    }

    public int ActiveCount => _pools.Sum(p => p.Value.ActiveCount);

    void Awake()
    {
        Game.Events.OnBulletAsteroidCollision.Register(OnBulletAsteroidCollision);
    }

    private void OnBulletAsteroidCollision((Asteroid Asteroid, Bullet Bullet) obj)
    {
        SpawnChildAsteroids(obj.Asteroid);
        AsteroidExplosionEffects(obj.Asteroid);
    }

    private void AsteroidExplosionEffects(Asteroid asteroid)
    {
        if(!AsteroidData.AsteroidEffects.Any())
            throw new InvalidOperationException("Asteroid data has no effects assigned");

        var randomEffect = AsteroidData.AsteroidEffects[UnityEngine.Random.Range(0, AsteroidData.AsteroidEffects.Count)];
        var effect = Instantiate(randomEffect.Prefab, asteroid.transform.position, Quaternion.identity);
        effect.transform.localScale = asteroid.transform.localScale / asteroid.Type.Size;        
    }

    //private void OnBulletAsteroidCollision(CollisionArgs<Asteroid, Bullet> args)
    //{
    //    SpawnChildAsteroids(args.Source);
    //}

    public Asteroid SpawnAsteroid(int maxSize = int.MaxValue, Vector3 position = default, Vector3 velocity = default, Quaternion rotation = default)
    {
        var prefab = GetRandomAsteroidPrefab();
        var pool = GetPoolForPrefab(prefab);

        var pos = GetRandomPosition(position);
        var rot = UnityEngine.Random.rotation;
        var asteroid = pool.Spawn(pos, rot);

        var type = GetRandomAsteroidType(maxSize);
        asteroid.Type = type;
        asteroid.transform.localScale = Vector3.one * type.Size;
        asteroid.MovementSpeed = GetAsteroidMovementSpeed() / type.Size;
        asteroid.AngularVelocity = CreateAsteroidRotationalVelocity();
        asteroid.MoveDirection = GetAsteroidMoveDirection(velocity).Forward;

        Game.Wrap.TryMoveInsideBounds(asteroid.gameObject);

        return asteroid;
    }

    private GameObject GetRandomAsteroidPrefab()
    {
        var randomIndex = UnityEngine.Random.Range(0, AsteroidData.AsteroidPrefabs.Count);
        var prefab = AsteroidData.AsteroidPrefabs[randomIndex];
        return prefab;
    }

    private ObjectPool<Asteroid> GetPoolForPrefab(GameObject prefab)
    {
        var prefabId = prefab.GetInstanceID();
        if (!_pools.ContainsKey(prefabId))
        {
            var pool = new ObjectPool<Asteroid>(prefab, ParentContainer, AsteroidData.StartingPoolSize, this);
            _pools[prefabId] = pool;
            return pool;
        }
        return _pools[prefabId];
    }

    private Vector3 GetRandomPosition(Vector3 position)
    {
        if (position == Vector3.zero)
        {
            position = Game.Wrap.RandomPointInBounds();
        }
        position.y = Game.Wrap.Bounds.center.y;
        return position;
    }

    private AsteroidType GetRandomAsteroidType(int maxSize)
    {
        AsteroidType[] types = AsteroidData.AsteroidTypes.Where(t => t.Size <= maxSize).ToArray();
        var randomIndex = UnityEngine.Random.Range(0, types.Length);
        if (randomIndex < 0 || randomIndex >= types.Length)
        {
            Debug.Log($"Invalid range {randomIndex} range=0-{types.Length - 1}");
        }
        var type = types.ElementAtOrDefault(randomIndex);
        return type;
    }

    public Asteroid SpawnAsteroid(Asteroid parent)
    {
        return SpawnAsteroid(parent.Type.Size-1, parent.transform.position, parent.MoveDirection);
    }

    private float GetAsteroidMovementSpeed()
    {
        return UnityEngine.Random.Range(AsteroidData.MinMoveSpeed, AsteroidData.MaxMoveSpeed);
    }

    private Quaternion CreateAsteroidRotationalVelocity()
    {
        var x = UnityEngine.Random.Range(AsteroidData.MinRotationSpeed, AsteroidData.MaxRotationSpeed);
        var y = UnityEngine.Random.Range(AsteroidData.MinRotationSpeed, AsteroidData.MaxRotationSpeed);
        var z = UnityEngine.Random.Range(AsteroidData.MinRotationSpeed, AsteroidData.MaxRotationSpeed);
        return Quaternion.Euler(x, y, z);
    }

    private (Vector3 Forward, Vector3 Up) GetAsteroidMoveDirection(Vector3 parentVelocity = default)
    {
        const float inheritParentMomentumFraction = 0.35f;

        Quaternion yAxisRotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0,360), Vector3.up);

        // Point children vaguely in the same direction of their parent.
        var rot = parentVelocity != default 
            ? Quaternion.Slerp(Quaternion.LookRotation(parentVelocity), yAxisRotation, inheritParentMomentumFraction) 
            : yAxisRotation;

        Vector3 forward = rot * Vector3.forward;
        Vector3 up = rot * Vector3.up;
        return (forward.normalized, up.normalized);
    }

    void IPoolObserver<Asteroid>.OnItemCreated(IObjectPool<Asteroid> pool, Asteroid asteroid)
    {

    }

    void IPoolObserver<Asteroid>.OnItemSpawned(IObjectPool<Asteroid> pool, Asteroid asteroid)
    {

    }

    void IPoolObserver<Asteroid>.OnItemDespawned(IObjectPool<Asteroid> pool, Asteroid asteroid)
    {
        //SpawnChildAsteroids(asteroid);
    }

    private void SpawnChildAsteroids(Asteroid asteroid)
    {
        if (asteroid.Type.Size > 1)
        {
            for (int i = 0; i < asteroid.Type.Children; i++)
            {
                SpawnAsteroid(asteroid);
            }
        }
    }

    public IEnumerator<Asteroid> GetEnumerator()
    {
        foreach (var pool in _pools.Values)
        {                        
            var active = pool.ToActiveArray();
            for (int j = 0; j < active.Length; j++)
            {
                yield return active[j];
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Clear()
    {
        foreach (var pool in _pools)
        {
            foreach (var item in pool.Value.ToActiveArray())
            {
                item.Despawn();
            }
        }
    }
}



