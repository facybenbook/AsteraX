﻿using System.Collections;
using System.Collections.Generic;
using Events;
using UnityEngine;

public class Asteroid : MonoBehaviour, IPoolable<Asteroid>
{
    public float MovementSpeed;
    public Vector3 MoveDirection;
    public Quaternion AngularVelocity;
    public AsteroidType Type;
    private IObjectPool<Asteroid> _pool;
    private Collider _collider;
    private Rigidbody _rigidBody;
    private bool _isSpawned;

    public Bounds Bounds => _collider.bounds;

    void Update()
    {

        //transform.position += MoveDirection * MovementSpeed * Time.deltaTime;
        //transform.rotation = AngularVelocity * transform.rotation;

        _rigidBody.MoveRotation(AngularVelocity * _rigidBody.rotation);
        _rigidBody.MovePosition(_rigidBody.position + MoveDirection * MovementSpeed * Time.deltaTime);

        Debug.DrawLine(transform.position, transform.position + MoveDirection.normalized * 3f);
    }

    public void Awake()
    {
        _collider = GetComponent<Collider>();
        _rigidBody = GetComponent<Rigidbody>();
    }

    public void OnSpawned(IObjectPool<Asteroid> pool)
    {
        _pool = pool;
    }

    public void OnDespawned()
    {

    }

    public void Despawn()
    {
        _pool.Despawn(this);
    }

    public void OnCollisionEnter(Collision collision)
    {
        var bullet = collision.transform.GetComponent<Bullet>();
        if (bullet != null && bullet.IsValid)
        {
            // The bullet needs to be disabled before spawning child asteroids
            // or any children spawned will immediately collide with it.
            bullet.Despawn();
            Despawn();

            Game.Events.OnBulletAsteroidCollision.Raise((this, bullet));
        }
    }
}
