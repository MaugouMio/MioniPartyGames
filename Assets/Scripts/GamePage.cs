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
	private Text StartCountdownText;

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

	public void UpdateEventList()
	{
		EventList.UpdateData(GameData.Instance.EventRecord);
	}

	public void UpdateSelfGuessRecord()
	{
		if (!GameData.Instance.PlayerDatas.TryGetValue(GameData.Instance.SelfUID, out PlayerData player))
		{
			GuessRecord.UpdateData(null);
			return;
		}

		GuessRecord.UpdateData(player.GuessHistory);
	}

	public void UpdateCurrentPlayerGuessRecord()
	{
		ushort uid = GameData.Instance.PlayerOrder[GameData.Instance.GuessingPlayerIndex];
		if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			GuessRecord.UpdateData(null);
			return;
		}

		CurrentPlayerGuessRecord.UpdateData(player.GuessHistory);
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
			// TODO: 提示問題不可為空
			return;
		}
		if (encodedQuestion.Length > 255)
		{
			// TODO: 提示問題過長
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
			// TODO: 提示猜測內容不可為空
			return;
		}
		if (encodedGuess.Length > 255)
		{
			// TODO: 提示猜測內容過長
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
}
