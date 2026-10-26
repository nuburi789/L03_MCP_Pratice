using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private float interval = 3f;
    [SerializeField] private float height = 10f;

    private const float Range = 4.5f;
    private const float Lifetime = 15f;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < interval) return;
        timer -= interval;
        Spawn();
    }

    private void Spawn()
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Obstacle Sphere";
        sphere.transform.position = new Vector3(Random.Range(-Range, Range), height, Random.Range(-Range, Range));

        var rb = sphere.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        sphere.AddComponent<Obstacle>();
        Destroy(sphere, Lifetime);
    }
}
