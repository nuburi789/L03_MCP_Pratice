using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector3 input;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        input = Vector3.zero;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.isPressed) input.z += 1f;
        if (kb.sKey.isPressed) input.z -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        input = Vector3.ClampMagnitude(input, 1f);
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        rb.MovePosition(rb.position + input * moveSpeed * Time.fixedDeltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckObstacle(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        CheckObstacle(collision);
    }

    private void CheckObstacle(Collision collision)
    {
        if (collision.gameObject.GetComponent<Obstacle>() != null && GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }
}
