using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float survivalTime;
    [SerializeField] private bool isGameOver;

    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!isGameOver) survivalTime += Time.deltaTime;
    }

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("Game Over");
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 30), $"Time: {survivalTime:F1}s");

        if (isGameOver)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "GAME OVER", style);
        }
    }
}
