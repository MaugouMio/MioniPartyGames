using UnityEngine;
using UnityEngine.UI;

public class PlayerInfo : MonoBehaviour
{
	[SerializeField]
	private Text nameText;
	[SerializeField]
	private Text questionText;
	[SerializeField]
	private Text voteText;
	[SerializeField]
	private Image stateMask;
	[SerializeField]
	private GameObject selfIndicator;

	public void UpdateData(PlayerData playerData)
	{
		if (!GameData.Instance.UserDatas.ContainsKey(playerData.UID))
			return;

		UserData userData = GameData.Instance.UserDatas[playerData.UID];
		nameText.text = userData.Name;
		questionText.text = playerData.Question;

		if (GameData.Instance.CurrentState == GameState.WAITING)
		{
			stateMask.color = Color.clear;
		}
		else
		{
			bool isProcessingPlayer = playerData.UID == GameData.Instance.PlayerOrder[GameData.Instance.GuessingPlayerIndex];
			if (playerData.SuccessRound > 0)
				stateMask.color = new Color(0f, 0f, 0f, 0.7f);
			else if (isProcessingPlayer && (GameData.Instance.CurrentState == GameState.GUESSING || GameData.Instance.CurrentState == GameState.VOTING))
				stateMask.color = new Color(1f, 1f, 90f / 255f, 0.5f);
			else
				stateMask.color = Color.clear;
		}

		if (GameData.Instance.CurrentState == GameState.VOTING && GameData.Instance.Votes.TryGetValue(playerData.UID, out byte vote))
		{
			switch (vote)
			{
				case 0:
					voteText.text = "<color=#00ba00>✔</color>";
					break;
				case 1:
					voteText.text = "<color=#ba0000>✘</color>";
					break;
				case 2:
					voteText.text = "<color=#baba00>∆</color>";
					break;
			}
		}
		else
		{
			voteText.text = "";
		}

		selfIndicator.SetActive(playerData.UID == GameData.Instance.SelfUID);
	}
}
