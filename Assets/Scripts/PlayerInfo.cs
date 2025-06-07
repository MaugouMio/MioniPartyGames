using UnityEngine;
using UnityEngine.UI;

public class PlayerInfo : MonoBehaviour
{
	[SerializeField]
	private Button selfButton;
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

	private ushort selfUID = 0;

	public void UpdateData(PlayerData playerData)
	{
		selfButton.enabled = GameData.Instance.CurrentState == GameState.GUESSING || GameData.Instance.CurrentState == GameState.VOTING;

		if (!GameData.Instance.UserDatas.ContainsKey(playerData.UID))
			return;

		UserData userData = GameData.Instance.UserDatas[playerData.UID];
		nameText.text = userData.Name;

		if (playerData.Question == "")
			questionText.text = "<i><color=#999999>等待其他玩家出題</color></i>";
		else if (playerData.QuestionLocked)
			questionText.text = playerData.Question;
		else
			questionText.text = $"<i><color=#00aa00>{playerData.Question}</color></i>";

		if (GameData.Instance.CurrentState == GameState.WAITING)
		{
			stateMask.color = Color.clear;
		}
		else
		{
			bool isProcessingPlayer = playerData.UID == GameData.Instance.GetCurrentPlayerUID();
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
				case 1:
					voteText.text = "<color=#00ba00>✔</color>";
					break;
				case 2:
					voteText.text = "<color=#ba0000>✘</color>";
					break;
				case 0:
					voteText.text = "<color=#baba00>∆</color>";
					break;
			}
		}
		else
		{
			voteText.text = "";
		}

		selfIndicator.SetActive(playerData.UID == GameData.Instance.SelfUID);
		selfUID = playerData.UID;
	}

	public void OnClicked()
	{
		if (GamePage.Instance != null)
			GamePage.Instance.ShowPlayerHistoryRecord(selfUID);
	}
}
