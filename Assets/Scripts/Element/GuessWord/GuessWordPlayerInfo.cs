using UnityEngine;
using UnityEngine.UI;

public class GuessWordPlayerInfo : PlayerInfo
{
	[SerializeField]
	private Button selfButton;
	[SerializeField]
	private Text questionText;
	[SerializeField]
	private Text voteText;
	[SerializeField]
	private Image stateMask;

	public override void UpdateData(PlayerData playerData)
	{
		base.UpdateData(playerData);
		if (playerData is not GuessWordPlayerData)
			return;

		GuessWordPlayerData gwPlayerData = (GuessWordPlayerData)playerData;

		selfButton.enabled = GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING || GameData.Instance.GuessWordData.CurrentState == GuessWordState.VOTING;

		if (gwPlayerData.Question == "")
			questionText.text = "<i><color=#999999>等待其他玩家出題</color></i>";
		else if (gwPlayerData.QuestionLocked)
			questionText.text = gwPlayerData.Question;
		else
			questionText.text = $"<i><color=#00aa00>{gwPlayerData.Question}</color></i>";

		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.WAITING)
		{
			stateMask.color = Color.clear;
		}
		else
		{
			bool isProcessingPlayer = gwPlayerData.UID == GameData.Instance.GetCurrentPlayerUID();
			if (gwPlayerData.SuccessRound != 0)
				stateMask.color = new Color(0f, 0f, 0f, 0.7f);
			else if (isProcessingPlayer &&
					(GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING ||
					GameData.Instance.GuessWordData.CurrentState == GuessWordState.VOTING))
				stateMask.color = new Color(1f, 1f, 90f / 255f, 0.5f);
			else
				stateMask.color = Color.clear;
		}

		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.VOTING &&
			GameData.Instance.GuessWordData.Votes.TryGetValue(gwPlayerData.UID, out byte vote))
		{
			switch (vote)
			{
				case 1:
					voteText.text = "<color=#00ba00>✓</color>";
					break;
				case 2:
					voteText.text = "<color=#ba0000>×</color>";
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
	}

	public void OnClicked()
	{
		if (GuessWordGamePage.Instance != null)
			GuessWordGamePage.Instance.ShowPlayerHistoryRecord(selfUID);
	}
}
