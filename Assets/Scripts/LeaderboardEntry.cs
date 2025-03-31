using UnityEngine;
using UnityEngine.UI;

public class LeaderboardEntry : MonoBehaviour {
    [SerializeField]
    private Text playerNameText;
    [SerializeField]
    private Text scoreText;
    [SerializeField]
    private Text killsText;

    public void SetStats(string playerName, int score, int kills) {
        playerNameText.text = playerName;
        scoreText.text = "Score: " + score;
        killsText.text = "Kills: " + kills;
    }
} 