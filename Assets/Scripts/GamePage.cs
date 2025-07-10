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
	private List<UserInfo> UserList;
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
	private Toggle TempLeaveToggle;
	[SerializeField]
	private GameObject GuessPageTopButtonGroup;
	[SerializeField]
	private GameObject IdleCheckButton;
	[SerializeField]
	private Text IdleCheckButtonText;
	[SerializeField]
	private GameObject GiveUpButton;
	[SerializeField]
	private Text GuessingPlayer;
	[SerializeField]
	private Text GuessingQuestionText;
	[SerializeField]
	private Text GuessedText;
	[SerializeField]
	private InputField GuessInput;
	[SerializeField]
	private GameObject GuessConfirmButtons;
	[SerializeField]
	private GameObject PassGuessButton;
	[SerializeField]
	private GameObject VoteButtons;
	[SerializeField]
	private Slider VolumeSlider;
	[SerializeField]
	private Text VolumeText;

	[SerializeField]
	private TextList EventList;
	[SerializeField]
	private TextList ChatList;
	[SerializeField]
	private Toggle HiddleChatToggle;
	[SerializeField]
	private Text HiddleChatToggleText;
	[SerializeField]
	private InputField ChatInput;
	[SerializeField]
	private TextList GuessRecord;
	[SerializeField]
	private Text CurrentPlayerGuessRecordTitle;
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
	private PopupImage ImagePopup;
	[SerializeField]
	private PopupMessage MessagePopup;

	[SerializeField]
	private AudioSource SFXPlayer;

	private bool needUpdate = true;
	private IEnumerator countdownCoroutine = null;
	private ushort checkingHistoryUID = 0;
	private IEnumerator idleCheckCoroutine = null;

	void Awake()
	{
		Instance = this;
		GuessPageTopButtonGroup.SetActive(false);
		IdleCheckButton.SetActive(false);
		GameResultWindow.SetActive(false);
		VolumeSlider.value = PlayerPrefs.GetFloat("SoundVolume", 0.5f);
		VolumeText.text = ((int)(VolumeSlider.value * 100)).ToString();
	}

    // Update is called once per frame
    void Update()
    {
		if (needUpdate)
			UpdateDataReal();
	}

	void OnDestroy()
	{
		Instance = null;
		PlayerPrefs.SetFloat("SoundVolume", VolumeSlider.value);
	}

	private void UpdateDataReal()
	{
		UpdatePlayerInfo();
		UpdateUserInfo();
		UpdateMiddlePage();
		UpdateChatList();
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
		int idx = 0;
		if (GameData.Instance.CurrentState == GameState.WAITING)
		{
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
		}
		else
		{
			foreach (var uid in GameData.Instance.PlayerOrder)
			{
				if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
					continue;

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
				PlayerList[idx].UpdateData(player);
				idx++;
			}
		}
		// 關閉未使用的物件
		while (idx < PlayerList.Count)
			PlayerList[idx++].gameObject.SetActive(false);

		bool isJoined = GameData.Instance.PlayerDatas.ContainsKey(GameData.Instance.SelfUID);
		JoinButton.interactable = isJoined || GameData.Instance.CurrentState == GameState.WAITING;
		JoinButtonText.text = isJoined ? "離開遊戲" : "加入遊戲";
	}

	public void UpdateUserInfo()
	{
		int idx = 0;
		foreach (var user in GameData.Instance.UserDatas.Values)
		{
			// 還沒取名字的忽略不顯示
			if (user.Name == "")
				continue;

			UserInfo obj;
			if (idx >= UserList.Count)
			{
				obj = Instantiate(UserList[0]);
				obj.transform.SetParent(UserList[0].transform.parent, false);
				UserList.Add(obj);
			}
			else
			{
				obj = UserList[idx];
			}
			obj.gameObject.SetActive(true);
			obj.UpdateData(user);
			idx++;
		}
		// 關閉未使用的物件
		while (idx < UserList.Count)
			UserList[idx++].gameObject.SetActive(false);
	}

	private void SetGuessingPlayerBaseInfo()
	{
		ushort guessingPlayerUID = GameData.Instance.GetCurrentPlayerUID();
		bool isSelfGuessing = guessingPlayerUID == GameData.Instance.SelfUID;

		GuessingPlayer.text = $"<color=yellow>{GameData.Instance.UserDatas[guessingPlayerUID].Name}</color> 的回合";
		GuessingQuestionText.gameObject.SetActive(!isSelfGuessing);
		GuessingQuestionText.text = GameData.Instance.PlayerDatas[guessingPlayerUID].Question;
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
					SetGuessingPlayerBaseInfo();
					bool isSelfGuessing = GameData.Instance.GetCurrentPlayerUID() == GameData.Instance.SelfUID;

					GuessPageTopButtonGroup.SetActive(isSelfGuessing);
					GiveUpButton.SetActive(isSelfGuessing);
					GuessedText.gameObject.SetActive(!isSelfGuessing);
					GuessedText.text = "＿＿＿＿＿＿";
					GuessInput.gameObject.SetActive(isSelfGuessing);

					GuessConfirmButtons.SetActive(isSelfGuessing);
					if (isSelfGuessing)  // 剩自己還沒猜出來就不用想跳過了
						PassGuessButton.SetActive(!GameData.Instance.IsOthersAllGuessed());
					VoteButtons.SetActive(false);
				}
				break;
			case GameState.VOTING:
				{
					SetGuessingPlayerBaseInfo();
					bool isSelfGuessing = GameData.Instance.GetCurrentPlayerUID() == GameData.Instance.SelfUID;

					GuessPageTopButtonGroup.SetActive(!isSelfGuessing);
					GiveUpButton.SetActive(false);
					GuessedText.gameObject.SetActive(true);
					GuessedText.text = GameData.Instance.VotingGuess;
					GuessInput.gameObject.SetActive(false);

					GuessConfirmButtons.SetActive(false);
					VoteButtons.SetActive(!isSelfGuessing);
				}
				break;
			default:
				break;
		}
	}

	public void UpdateChatList(bool isNewMessage = false)
	{
		ChatList.UpdateData(GameData.Instance.ChatRecord);
		if (isNewMessage)
			ChatList.MoveToLast();
	}

	public void UpdateEventList(bool isNewRecord = false)
	{
		EventList.UpdateData(GameData.Instance.EventRecord);
		if (isNewRecord)
			EventList.MoveToLast();
	}

	public void UpdateSelfGuessRecord(bool isNewRecord = false)
	{
		if (GameData.Instance.CurrentState == GameState.WAITING)
			return;

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
		if (GameData.Instance.CurrentState == GameState.WAITING)
			return;
		
		ushort uid = checkingHistoryUID > 0 ? checkingHistoryUID : GameData.Instance.GetCurrentPlayerUID();
		if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			CurrentPlayerGuessRecord.UpdateData(null);
			return;
		}

		CurrentPlayerGuessRecordTitle.text = $"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color>的猜測記錄";
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
			StartButtonText.text = "遊戲中";
			StartButton.interactable = false;
		}
	}

	private IEnumerator Countdown(int seconds)
	{
		while (seconds > 0)
		{
			StartCountdownText.text = $"遊戲開始倒數 {seconds} 秒";
			PlaySound("clock");
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

	public void ResetTempLeaveToggle()
	{
		TempLeaveToggle.isOn = false;
	}

	public void ShowPopupImage(string filename)
	{
		if (ImagePopup != null)
			ImagePopup.ShowImage(filename);
		else
			Debug.LogWarning("PopupImage is not assigned.");
	}

	public void ShowPopupMessage(string message)
	{
		if (MessagePopup != null)
			MessagePopup.ShowMessage(message);
		else
			Debug.LogWarning("PopupMessage is not assigned.");
	}

	public void PlaySound(string name)
	{
		if (SFXPlayer != null)
		{
			AudioClip clip = Resources.Load<AudioClip>($"Sounds/{name}");
			if (clip != null)
				SFXPlayer.PlayOneShot(clip);
			else
				Debug.LogWarning($"Audio clip '{name}' not found.");
		}
		else
		{
			Debug.LogWarning("SFXPlayer is not assigned.");
		}
	}

	public void ShowPlayerHistoryRecord(ushort uid)
	{
		if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			ShowPopupMessage("玩家資料不存在");
			return;
		}

		checkingHistoryUID = uid;
		UpdateCurrentPlayerGuessRecord();
	}

	private void PassOperation()
	{
		if (GameData.Instance.CurrentState == GameState.GUESSING)
			NetManager.Instance.SendGuess(new byte[0]);  // 表示跳過猜測
		else if (GameData.Instance.CurrentState == GameState.VOTING)
			NetManager.Instance.SendVote(0);  // 直接投棄權
	}

	private IEnumerator IdleCheck(int seconds)
	{
		while (seconds > 0)
		{
			IdleCheckButtonText.text = $"閒置倒數 {seconds} 秒\n(點擊長考)";
			yield return new WaitForSeconds(1f);
			seconds--;
		}
		idleCheckCoroutine = null;
		IdleCheckButton.SetActive(false);

		PassOperation();
	}

	public void StartIdleCheck()
	{
		// 輪到自己猜題，但其他玩家都已經猜過了，不需要閒置檢查
		if (GameData.Instance.CurrentState == GameState.GUESSING && GameData.Instance.IsOthersAllGuessed())
			return;

		// 有勾暫離時直接跳過不用等閒置
		if (TempLeaveToggle.isOn)
		{
			ShowPopupMessage("暫離模式啟動中，自動跳過操作");
			PassOperation();
			return;
		}

		IdleCheckButton.SetActive(true);
		idleCheckCoroutine = IdleCheck(20);
		StartCoroutine(idleCheckCoroutine);
	}

	public void StopIdleCheck()
	{
		if (idleCheckCoroutine != null)
		{
			StopCoroutine(idleCheckCoroutine);
			idleCheckCoroutine = null;
			IdleCheckButton.SetActive(false);
		}
	}

	public void ShowGameResult()
	{
		List<Tuple<ushort, ushort>> resultList = new List<Tuple<ushort, ushort>>();
		foreach (var player in GameData.Instance.PlayerDatas.Values)
			resultList.Add(new Tuple<ushort, ushort>(player.UID, (ushort)player.SuccessRound));
		// 按照成功回合由低到高排序
		resultList.Sort((a, b) => a.Item2.CompareTo(b.Item2));

		string resultText = "";
		int currentRank = 0;
		for (int i = 0; i < resultList.Count; i++)
		{
			if (resultList[i].Item2 > resultList[currentRank].Item2)
				currentRank = i;

			string playerName = GameData.Instance.UserDatas[resultList[i].Item1].Name;
			string line = "";
			if ((short)resultList[i].Item2 > 0)
				line = $"{currentRank + 1}.\t{playerName} - {resultList[i].Item2} 輪";
			else
				line = $"{currentRank + 1}.\t{playerName} - 投降";

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
		else if (!GameData.Instance.PlayerDatas.ContainsKey(GameData.Instance.SelfUID))
			ShowPopupMessage("須先加入遊戲才能進行操作");
		else if (GameData.Instance.PlayerDatas.Count < 2)
			ShowPopupMessage("至少需要兩名玩家才能開始遊戲");
		else
			NetManager.Instance.SendStart();
	}

	// mode = 0:輸入框 1:展示按鈕 2:鎖定按鈕
	public void ClickAssignQuestion(int mode)
	{
		byte[] encodedQuestion = System.Text.Encoding.UTF8.GetBytes(QuestionInput.text);
		if (encodedQuestion.Length == 0)
		{
			ShowPopupMessage("問題內容不可為空");
			return;
		}
		if (encodedQuestion.Length > 255)
		{
			ShowPopupMessage("問題內容過長");
			return;
		}

		// 輸入框 enter 同時按住左 shift 視同鎖定
		if (mode == 0 && Input.GetKey(KeyCode.LeftShift))
			mode = 2;

		NetManager.Instance.SendAssignQuestion(encodedQuestion, mode == 2);
		if (mode == 2)
			QuestionInput.text = "";
	}

	public void ClickGiveUp()
	{
		NetManager.Instance.SendGiveUp();
		StopIdleCheck();
	}

	public void ClickConfirmGuess()
	{
		byte[] encodedGuess = System.Text.Encoding.UTF8.GetBytes(GuessInput.text);
		if (encodedGuess.Length == 0)
		{
			ShowPopupMessage("猜測的內容不可為空");
			return;
		}
		if (encodedGuess.Length > 255)
		{
			ShowPopupMessage("猜測的內容過長");
			return;
		}

		NetManager.Instance.SendGuess(encodedGuess);
		GuessInput.text = "";
		StopIdleCheck();
	}

	public void ClickPassGuess()
	{
		NetManager.Instance.SendGuess(new byte[0]);  // 表示跳過猜測
		StopIdleCheck();
	}

	public void ClickVoteOption(int voteOption)
	{
		if (voteOption < 0 || voteOption > 2)
		{
			Debug.LogError($"Invalid vote option: {voteOption}");
			return;
		}
		NetManager.Instance.SendVote((byte)voteOption);
		StopIdleCheck();
	}

	public void ClickCloseResult()
	{
		GameResultWindow.SetActive(false);
	}

	public void ClickHiddenChat(bool isHidden)
	{
		HiddleChatToggleText.text = isHidden ? "密電" : "文本";
	}

	public void ClickSendChat()
	{
		string processedMessage = ChatInput.text.Trim();
		if (processedMessage == "")
			return;

		byte[] encodedMessage = System.Text.Encoding.UTF8.GetBytes(processedMessage);
		if (encodedMessage.Length > 255)
		{
			ShowPopupMessage("訊息內容過長");
			return;
		}

		NetManager.Instance.SendChatMessage(encodedMessage, HiddleChatToggle.isOn);
		ChatInput.text = "";
		ChatInput.ActivateInputField();
	}

	public void OnVolumeChanged()
	{
		if (SFXPlayer != null)
		{
			SFXPlayer.volume = VolumeSlider.value;
			VolumeText.text = ((int)(VolumeSlider.value * 100)).ToString();
		}
		else
		{
			Debug.LogWarning("SFXPlayer is not assigned.");
		}
	}
}
