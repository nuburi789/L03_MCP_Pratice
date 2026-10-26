using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField] private float speed = 90f;

    private void Update()
    {
        transform.Rotate(0f, speed * Time.deltaTime, 0f);
    }
}
