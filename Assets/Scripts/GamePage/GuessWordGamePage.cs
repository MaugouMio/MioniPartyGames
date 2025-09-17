using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GuessWordGamePage : GamePage
{
	public static new GuessWordGamePage Instance
	{
		get {
			if (GamePage.Instance is not GuessWordGamePage)
				return null;
			return GamePage.Instance as GuessWordGamePage;
		}
		private set {}
	}

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
	private Toggle HiddleChatToggle;
	[SerializeField]
	private Text HiddleChatToggleText;
	[SerializeField]
	private TextList GuessRecord;
	[SerializeField]
	private Text CurrentPlayerGuessRecordTitle;
	[SerializeField]
	private TextList CurrentPlayerGuessRecord;

	[SerializeField]
	private GameObject GameResultWindow;
	[SerializeField]
	private Text GameResultText;

	private int assignQuestionFrame = 0;
	private ushort checkingHistoryUID = 0;
	private IEnumerator idleCheckCoroutine = null;
	private static GuessWordGamePage instance;

	protected override void Awake()
	{
		base.Awake();

		GuessPageTopButtonGroup.SetActive(false);
		IdleCheckButton.SetActive(false);
		GameResultWindow.SetActive(false);
	}

    // Update is called once per frame
    protected override void Update()
    {
		base.Update();

		// 出題時沒有選擇輸入框直接按 Enter 視同鎖定 (非網頁版輸入框 enter 當下會馬上觸發這邊，所以要擋同 frame)
		if (Time.frameCount != assignQuestionFrame && GameData.Instance.GuessWordData.CurrentState == GuessWordState.PREPARING && Input.GetKeyDown(KeyCode.Return))
			ClickAssignQuestion(2);
	}

	protected override void UpdateDataReal()
	{
		base.UpdateDataReal();

		UpdateMiddlePage();
		UpdateSelfGuessRecord();
		UpdateCurrentPlayerGuessRecord();
	}

	protected override IEnumerable<PlayerData> GetDisplayPlayerList()
	{
		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.WAITING)
		{
			foreach (var player in GameData.Instance.PlayerDatas.Values)
				yield return player;
		}
		else
		{
			foreach (var uid in GameData.Instance.GuessWordData.PlayerOrder)
			{
				if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
					continue;
				yield return player;
			}
		}
	}

	private void SetGuessingPlayerBaseInfo()
	{
		ushort guessingPlayerUID = GameData.Instance.GuessWordData.GetCurrentPlayerUID();
		bool isSelfGuessing = guessingPlayerUID == GameData.Instance.SelfUID;
		GuessWordPlayerData player = GameData.Instance.PlayerDatas[guessingPlayerUID] as GuessWordPlayerData;

		GuessingPlayer.text = $"<color=yellow>{GameData.Instance.UserDatas[guessingPlayerUID].Name}</color> 的回合";
		GuessingQuestionText.gameObject.SetActive(!isSelfGuessing);
		GuessingQuestionText.text = player.Question;
	}

	public void UpdateMiddlePage()
	{
		IdlePage.SetActive(GameData.Instance.GuessWordData.CurrentState == GuessWordState.WAITING);
		QuestionPage.SetActive(GameData.Instance.GuessWordData.CurrentState == GuessWordState.PREPARING);
		GuessingPage.SetActive(GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING || GameData.Instance.GuessWordData.CurrentState == GuessWordState.VOTING);

		switch (GameData.Instance.GuessWordData.CurrentState)
		{
			case GuessWordState.PREPARING:
				{
					int selfIndex = GameData.Instance.GuessWordData.PlayerOrder.IndexOf(GameData.Instance.SelfUID);
					PlayerQuestionPage.SetActive(selfIndex >= 0);
					SpectateQuestionPage.SetActive(selfIndex < 0);
					if (selfIndex >= 0)
					{
						ushort nextPlayerUID = GameData.Instance.GuessWordData.PlayerOrder[(selfIndex + 1) % GameData.Instance.GuessWordData.PlayerOrder.Count];
						QuestionTargetPlayer.text = GameData.Instance.UserDatas[nextPlayerUID].Name;
					}
				}
				break;
			case GuessWordState.GUESSING:
				{
					SetGuessingPlayerBaseInfo();
					bool isSelfGuessing = GameData.Instance.GuessWordData.GetCurrentPlayerUID() == GameData.Instance.SelfUID;

					GuessPageTopButtonGroup.SetActive(isSelfGuessing);
					GiveUpButton.SetActive(isSelfGuessing);
					GuessedText.gameObject.SetActive(!isSelfGuessing);
					GuessedText.text = "＿＿＿＿＿＿";
					GuessInput.gameObject.SetActive(isSelfGuessing);

					GuessConfirmButtons.SetActive(isSelfGuessing);
					if (isSelfGuessing)  // 剩自己還沒猜出來就不用想跳過了
						PassGuessButton.SetActive(!GameData.Instance.GuessWordData.IsOthersAllGuessed());
					VoteButtons.SetActive(false);
				}
				break;
			case GuessWordState.VOTING:
				{
					SetGuessingPlayerBaseInfo();
					bool isSelfGuessing = GameData.Instance.GuessWordData.GetCurrentPlayerUID() == GameData.Instance.SelfUID;
					bool needVote = !isSelfGuessing && GameData.Instance.IsPlayer();

					GuessPageTopButtonGroup.SetActive(needVote);
					GiveUpButton.SetActive(false);
					GuessedText.gameObject.SetActive(true);
					GuessedText.text = GameData.Instance.GuessWordData.VotingGuess;
					GuessInput.gameObject.SetActive(false);

					GuessConfirmButtons.SetActive(false);
					VoteButtons.SetActive(needVote);
				}
				break;
			default:
				break;
		}
	}

	public void UpdateSelfGuessRecord(bool isNewRecord = false)
	{
		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.WAITING)
			return;

		if (!GameData.Instance.PlayerDatas.TryGetValue(GameData.Instance.SelfUID, out PlayerData player))
		{
			GuessRecord.UpdateData(null);
			return;
		}

		GuessWordPlayerData gwPlayer = player as GuessWordPlayerData;
		GuessRecord.UpdateData(gwPlayer.GuessHistory);
		if (isNewRecord)
			GuessRecord.MoveToLast();
	}

	public void UpdateCurrentPlayerGuessRecord(bool isNewRecord = false)
	{
		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.WAITING)
			return;
		
		ushort uid = checkingHistoryUID > 0 ? checkingHistoryUID : GameData.Instance.GuessWordData.GetCurrentPlayerUID();
		if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			CurrentPlayerGuessRecord.UpdateData(null);
			return;
		}

		GuessWordPlayerData gwPlayer = player as GuessWordPlayerData;
		CurrentPlayerGuessRecordTitle.text = $"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color>的猜測記錄";
		CurrentPlayerGuessRecord.UpdateData(gwPlayer.GuessHistory);
		if (isNewRecord)
			CurrentPlayerGuessRecord.MoveToLast();
	}

	public override void StartCountdown(int seconds)
	{
		base.StartCountdown(seconds);

		// 開始倒數就自動關閉上一場的結果顯示
		ClickCloseResult();
	}

	public override void OnStartGame()
	{
		base.OnStartGame();

		TempLeaveToggle.isOn = false;
	}

	public void ShowPlayerHistoryRecord(ushort uid)
	{
		if (!GameData.Instance.PlayerDatas.ContainsKey(uid))
		{
			ShowPopupMessage("玩家資料不存在");
			return;
		}

		checkingHistoryUID = uid;
		UpdateCurrentPlayerGuessRecord();
	}

	private void PassOperation()
	{
		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING)
			NetManager.Instance.SendGuess(new byte[0]);  // 表示跳過猜測
		else if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.VOTING)
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
		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING && GameData.Instance.GuessWordData.IsOthersAllGuessed())
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

	public override void ShowGameResult()
	{
		base.ShowGameResult();

		List<Tuple<ushort, ushort>> resultList = new List<Tuple<ushort, ushort>>();
		foreach (GuessWordPlayerData player in GameData.Instance.PlayerDatas.Values)
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
		PlaySound("end");
	}

	// mode = 0:輸入框 1:展示按鈕 2:鎖定按鈕
	public void ClickAssignQuestion(int mode)
	{
		// 避免重複觸發
		assignQuestionFrame = Time.frameCount;

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

	protected override ushort GetHideChatUID()
	{
		return HiddleChatToggle.isOn ? GameData.Instance.GuessWordData.GetCurrentPlayerUID() : (ushort)0;
	}
}
