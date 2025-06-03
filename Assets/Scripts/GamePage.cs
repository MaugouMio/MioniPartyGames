using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamePage : MonoBehaviour
{
	public static GamePage Instance { get; private set; }

	[SerializeField]
	private List<PlayerInfo> PlayerList;
	[SerializeField]
	private Button JoinButton;
	[SerializeField]
	private Text JoinButtonText;

	[SerializeField]
	private GameObject IdlePage;

	[SerializeField]
	private GameObject QuestionPage;
	[SerializeField]
	private GameObject PlayerQuestionPage;
	[SerializeField]
	private Text QuestionTargetPlayer;
	[SerializeField]
	private InputField QuestionInput;
	[SerializeField]
	private GameObject SpectateQuestionPage;

	[SerializeField]
	private GameObject GuessingPage;
	[SerializeField]
	private Text GuessingPlayer;
	[SerializeField]
	private Text GuessedText;
	[SerializeField]
	private InputField GuessInput;
	[SerializeField]
	private GameObject GuessConfirmButtons;
	[SerializeField]
	private GameObject VoteButtons;

	[SerializeField]
	private TextList EventList;
	[SerializeField]
	private TextList GuessRecord;
	[SerializeField]
	private TextList CurrentPlayerGuessRecord;
	[SerializeField]
	private Button StartButton;
	[SerializeField]
	private Text StartButtonText;

	[SerializeField]
	private GameObject GameResultWindow;
	[SerializeField]
	private Text GameResultText;

	[SerializeField]
	private Text StartCountdownText;
	[SerializeField]
	private PopupMessage Popup;

	private bool needUpdate = true;
	private IEnumerator countdownCoroutine = null;

	void Awake()
	{
		Instance = this;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		
    }

    // Update is called once per frame
    void Update()
    {
		if (needUpdate)
			UpdateDataReal();

		// if (Input.GetKeyDown(KeyCode.Return))
		// {
			// if (NameWindow.activeSelf)
				// ClickSetName();
			// else
				// ClickConnect();
		// }
    }

	void OnDestroy()
	{
		Instance = null;
	}

	private void UpdateDataReal()
	{
		UpdatePlayerInfo();
		UpdateMiddlePage();
		UpdateEventList();
		UpdateSelfGuessRecord();
		UpdateCurrentPlayerGuessRecord();
		UpdateStartButton();

		needUpdate = false;
	}
	public void UpdateData()
	{
		needUpdate = true;
	}

	public void UpdatePlayerInfo()
	{
		if (GameData.Instance.CurrentState == GameState.WAITING)
		{
			int idx = 0;
			foreach (var player in GameData.Instance.PlayerDatas.Values)
			{
				PlayerInfo obj;
				if (idx >= PlayerList.Count)
				{
					obj = Instantiate(PlayerList[0]);
					obj.transform.SetParent(PlayerList[0].transform.parent, false);
					PlayerList.Add(obj);
				}
				else
				{
					obj = PlayerList[idx];
				}

				obj.gameObject.SetActive(true);
				obj.UpdateData(player);
				idx++;
			}

			// 關閉未使用的物件
			while (idx < PlayerList.Count)
				PlayerList[idx++].gameObject.SetActive(false);
		}

		bool isJoined = GameData.Instance.PlayerDatas.ContainsKey(GameData.Instance.SelfUID);
		JoinButton.interactable = isJoined || GameData.Instance.CurrentState == GameState.WAITING;
		JoinButtonText.text = isJoined ? "離開遊戲" : "加入遊戲";
	}

	public void UpdateMiddlePage()
	{
		IdlePage.SetActive(GameData.Instance.CurrentState == GameState.WAITING);
		QuestionPage.SetActive(GameData.Instance.CurrentState == GameState.PREPARING);
		GuessingPage.SetActive(GameData.Instance.CurrentState == GameState.GUESSING || GameData.Instance.CurrentState == GameState.VOTING);

		switch (GameData.Instance.CurrentState)
		{
			case GameState.PREPARING:
				{
					int selfIndex = GameData.Instance.PlayerOrder.IndexOf(GameData.Instance.SelfUID);
					PlayerQuestionPage.SetActive(selfIndex >= 0);
					SpectateQuestionPage.SetActive(selfIndex < 0);
					if (selfIndex >= 0)
					{
						ushort nextPlayerUID = GameData.Instance.PlayerOrder[(selfIndex + 1) % GameData.Instance.PlayerOrder.Count];
						QuestionTargetPlayer.text = GameData.Instance.UserDatas[nextPlayerUID].Name;
					}
				}
				break;
			case GameState.GUESSING:
				{
					ushort guessingPlayerUID = GameData.Instance.PlayerOrder[GameData.Instance.GuessingPlayerIndex];
					GuessingPlayer.text = GameData.Instance.UserDatas[guessingPlayerUID].Name;

					bool isSelfGuessing = guessingPlayerUID == GameData.Instance.SelfUID;
					GuessedText.gameObject.SetActive(!isSelfGuessing);
					GuessedText.text = "＿＿＿＿＿＿";
					GuessInput.gameObject.SetActive(isSelfGuessing);

					GuessConfirmButtons.SetActive(isSelfGuessing);
					VoteButtons.SetActive(false);
				}
				break;
			case GameState.VOTING:
				{
					ushort guessingPlayerUID = GameData.Instance.PlayerOrder[GameData.Instance.GuessingPlayerIndex];
					GuessingPlayer.text = GameData.Instance.UserDatas[guessingPlayerUID].Name;

					GuessedText.gameObject.SetActive(true);
					GuessedText.text = GameData.Instance.VotingGuess;
					GuessInput.gameObject.SetActive(false);

					GuessConfirmButtons.SetActive(false);
					VoteButtons.SetActive(guessingPlayerUID != GameData.Instance.SelfUID);
				}
				break;
			default:
				break;
		}
	}

	public void UpdateEventList(bool isNewRecord = false)
	{
		EventList.UpdateData(GameData.Instance.EventRecord);
		if (isNewRecord)
			EventList.MoveToLast();
	}

	public void UpdateSelfGuessRecord(bool isNewRecord = false)
	{
		if (!GameData.Instance.PlayerDatas.TryGetValue(GameData.Instance.SelfUID, out PlayerData player))
		{
			GuessRecord.UpdateData(null);
			return;
		}

		GuessRecord.UpdateData(player.GuessHistory);
		if (isNewRecord)
			GuessRecord.MoveToLast();
	}

	public void UpdateCurrentPlayerGuessRecord(bool isNewRecord = false)
	{
		ushort uid = GameData.Instance.PlayerOrder[GameData.Instance.GuessingPlayerIndex];
		if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			GuessRecord.UpdateData(null);
			return;
		}

		CurrentPlayerGuessRecord.UpdateData(player.GuessHistory);
		if (isNewRecord)
			CurrentPlayerGuessRecord.MoveToLast();
	}

	public void UpdateStartButton()
	{
		if (GameData.Instance.CurrentState == GameState.WAITING)
		{
			StartButton.interactable = true;
			StartButtonText.text = GameData.Instance.IsCountingDownStart ? "取消開始" : "開始遊戲";
		}
		else
		{
			StartButton.interactable = false;
		}
	}

	private IEnumerator Countdown(int seconds)
	{
		while (seconds > 0)
		{
			StartCountdownText.text = $"遊戲開始倒數 {seconds} 秒";
			yield return new WaitForSeconds(1f);
			seconds--;
		}
		StartCountdownText.text = "";
		countdownCoroutine = null;
	}

	public void StartCountdown(int seconds)
	{
		// 開始倒數就自動關閉上一場的結果顯示
		GameResultWindow.SetActive(false);

		countdownCoroutine = Countdown(seconds);
		StartCoroutine(countdownCoroutine);
	}

	public void StopCountdown()
	{
		if (countdownCoroutine != null)
		{
			StopCoroutine(countdownCoroutine);
			StartCountdownText.text = "";
			countdownCoroutine = null;
		}
	}

	public void ShowGameResult()
	{
		List<Tuple<ushort, ushort>> resultList = new List<Tuple<ushort, ushort>>();
		foreach (var player in GameData.Instance.PlayerDatas.Values)
			resultList.Add(new Tuple<ushort, ushort>(player.UID, player.SuccessRound));
		// 按照成功回合由低到高排序
		resultList.Sort((a, b) => a.Item2.CompareTo(b.Item2));

		string resultText = "";
		int currentRank = 0;
		for (int i = 0; i < resultList.Count; i++)
		{
			if (resultList[i].Item2 > resultList[currentRank].Item2)
				currentRank = i;

			string playerName = GameData.Instance.UserDatas[resultList[i].Item1].Name;
			string line = $"{currentRank + 1}.\t{playerName} - {resultList[i].Item2} 輪";
			if (resultList[i].Item1 == GameData.Instance.SelfUID)
				line = $"<color=blue>{line}</color>";

			if (i > 0)
				resultText += "\n";
			resultText += line;
		}

		GameResultText.text = resultText;
		GameResultWindow.SetActive(true);
	}

	public void ClickJoinGame()
	{
		bool isJoined = GameData.Instance.PlayerDatas.ContainsKey(GameData.Instance.SelfUID);
		if (isJoined)
			NetManager.Instance.SendLeave();
		else
			NetManager.Instance.SendJoin();
	}

	public void ClickStartGame()
	{
		if (GameData.Instance.IsCountingDownStart)
			NetManager.Instance.SendCancelStart();
		else
			NetManager.Instance.SendStart();
	}

	public void ClickAssignQuestion()
	{
		byte[] encodedQuestion = System.Text.Encoding.UTF8.GetBytes(QuestionInput.text);
		if (encodedQuestion.Length == 0)
		{
			Popup.ShowMessage("問題內容不可為空");
			return;
		}
		if (encodedQuestion.Length > 255)
		{
			Popup.ShowMessage("問題內容過長");
			return;
		}

		NetManager.Instance.SendAssignQuestion(encodedQuestion);
		QuestionInput.text = "";
	}

	public void ClickConfirmGuess()
	{
		byte[] encodedGuess = System.Text.Encoding.UTF8.GetBytes(GuessInput.text);
		if (encodedGuess.Length == 0)
		{
			Popup.ShowMessage("猜測的內容不可為空");
			return;
		}
		if (encodedGuess.Length > 255)
		{
			Popup.ShowMessage("猜測的內容過長");
			return;
		}

		NetManager.Instance.SendGuess(encodedGuess);
		GuessInput.text = "";
	}

	public void ClickVoteOption(int voteOption)
	{
		if (voteOption < 0 || voteOption > 2)
		{
			Debug.LogError($"Invalid vote option: {voteOption}");
			return;
		}
		NetManager.Instance.SendVote((byte)voteOption);
	}

	public void ClickCloseResult()
	{
		GameResultWindow.SetActive(false);
	}
}
