using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ArrangeNumberGamePage : GamePage
{
	public static new ArrangeNumberGamePage Instance
	{
		get
		{
			if (GamePage.Instance is not ArrangeNumberGamePage)
				return null;
			return GamePage.Instance as ArrangeNumberGamePage;
		}
		private set { }
	}

	[SerializeField]
	private GameObject IdlePage;
	[SerializeField]
	private Text MaxNumberSettingText;
	[SerializeField]
	private Text NumberGroupCountSettingText;
	[SerializeField]
	private Text NumberPerPlayerSettingText;

	[SerializeField]
	private GameObject PlayingPage;
	[SerializeField]
	private GameObject UrgentPlayersObject;
	[SerializeField]
	private TextList UrgentPlayersList;
	[SerializeField]
	private Text CurrentNumberText;
	[SerializeField]
	private Text LastPlayerText;
	[SerializeField]
	private GameObject buttonList;
	[SerializeField]
	private GameObject UrgentButton;
	[SerializeField]
	private GameObject NotUrgentButton;

	[SerializeField]
	private TextList SelfNumberList;

	[SerializeField]
	private GIFController ExplosionGIF;
	[SerializeField]
	private GameObject GameResultWindow;
	[SerializeField]
	private Text GameResultText;

	private const int MAX_NUMBER_STEP = 10;
	private const int MAX_NUMBER_MIN_VALUE = 10;
	private const int MAX_NUMBER_MAX_VALUE = 1000;

	private const int NUMBER_GROUP_COUNT_STEP = 1;
	private const int NUMBER_GROUP_COUNT_MIN_VALUE = 0;
	private const int NUMBER_GROUP_COUNT_MAX_VALUE = 50;

	private const int NUMBER_PER_PLAYER_STEP = 1;
	private const int NUMBER_PER_PLAYER_MIN_VALUE = 1;
	private const int NUMBER_PER_PLAYER_MAX_VALUE = 20;

	private ushort tempMaxNumber;
	private byte tempNumberGroupCount;
	private byte tempNumberPerPlayer;

	protected override void Awake()
	{
		base.Awake();

		GameResultWindow.SetActive(false);
		ExplosionGIF.gameObject.SetActive(false);
		ExplosionGIF.onPlayEnd = () => OpenResultWindow(false);

		SyncSettingTempValues();
	}

	protected override void UpdateDataReal()
	{
		base.UpdateDataReal();

		UpdateMiddlePage();
		UpdateSelfNumberList();
	}

	private void UpdateMiddlePage()
	{
		IdlePage.SetActive(GameData.Instance.ArrangeNumberData.CurrentState == ArrangeNumberState.WAITING);
		PlayingPage.SetActive(GameData.Instance.ArrangeNumberData.CurrentState == ArrangeNumberState.PLAYING);

		switch (GameData.Instance.ArrangeNumberData.CurrentState)
		{
			case ArrangeNumberState.WAITING:
				UpdateSettings();
				break;
			case ArrangeNumberState.PLAYING:
				UpdatePlayingInfo();
				break;
			default:
				break;
		}
	}

	private void UpdateSelfNumberList()
	{
		if (GameData.Instance.PlayerDatas.TryGetValue(GameData.Instance.SelfUID, out PlayerData selfPlayer))
		{
			if (selfPlayer is ArrangeNumberPlayerData selfANPlayer)
			{
				var stringList = selfANPlayer.LeftNumbers.ConvertAll(x => x.ToString());
				stringList.Reverse();  // 反轉順序讓小的數字在上面
				SelfNumberList.UpdateData(stringList);
				return;
			}
		}
		SelfNumberList.UpdateData(null);
	}

	public void SyncSettingTempValues()
	{
		tempMaxNumber = GameData.Instance.ArrangeNumberData.MaxNumber;
		tempNumberGroupCount = GameData.Instance.ArrangeNumberData.NumberGroupCount;
		tempNumberPerPlayer = GameData.Instance.ArrangeNumberData.NumberPerPlayer;
	}

	public void UpdateSettings()
	{
		MaxNumberSettingText.text = tempMaxNumber.ToString();

		if (tempNumberGroupCount > 0)
			NumberGroupCountSettingText.text = tempNumberGroupCount.ToString();
		else
			NumberGroupCountSettingText.text = "∞";

		NumberPerPlayerSettingText.text = tempNumberPerPlayer.ToString();
	}

	private void UpdatePlayingInfo()
	{
		CurrentNumberText.text = GameData.Instance.ArrangeNumberData.CurrentNumber.ToString();
		LastPlayerText.text = GameData.Instance.ArrangeNumberData.GetLastPlayerName();

		List<string> urgentList = new List<string>();
		foreach (var player in GameData.Instance.PlayerDatas.Values)
		{
			if (player is ArrangeNumberPlayerData anPlayer && anPlayer.IsUrgent)
				urgentList.Add(GameData.Instance.UserDatas[anPlayer.UID].Name);
		}
		UrgentPlayersObject.SetActive(urgentList.Count > 0);
		UrgentPlayersList.UpdateData(urgentList);

		if (GameData.Instance.PlayerDatas.TryGetValue(GameData.Instance.SelfUID, out PlayerData selfPlayer))
		{
			if (selfPlayer is ArrangeNumberPlayerData selfANPlayer)
			{
				buttonList.SetActive(true);
				UrgentButton.SetActive(!selfANPlayer.IsUrgent);
				NotUrgentButton.SetActive(selfANPlayer.IsUrgent);
				return;
			}
		}
		buttonList.SetActive(false);
	}

	public override void StartCountdown(int seconds)
	{
		base.StartCountdown(seconds);

		// 開始倒數時同步假數值，這時候已經不能設定了
		SyncSettingTempValues();
		UpdateSettings();

		// 開始倒數就自動關閉上一場的結果顯示
		ClickCloseResult();
	}

	private void OpenResultWindow(bool isSuccess)
	{
		GameResultText.text = isSuccess ? "<color=green>成功</color>" : "<color=red>失敗</color>";
		GameResultWindow.SetActive(true);
	}

	public override void ShowGameResult()
	{
		if (GameData.Instance.ArrangeNumberData.IsAllNumbersPosed())
		{
			OpenResultWindow(true);
			PlaySound("end");
		}
		else
		{
			ExplosionGIF.gameObject.SetActive(true);
			PlaySound("explosion");
		}
	}

	public void ClickCloseResult()
	{
		GameResultWindow.SetActive(false);
	}

	protected override ushort GetHideChatUID()
	{
		return 0;
	}

	private bool ClickSettingCheck(bool showHint = true)
	{
		if (!GameData.Instance.IsPlayer())
		{
			if (showHint)
				ShowPopupMessage("請先成為玩家再更改設定");
			return false;
		}
		if (IsCountingDownStart())
		{
			if (showHint)
				ShowPopupMessage("遊戲開始倒數中，無法更改設定");
			return false;
		}
		return true;
	}

	public void PressSetMaxNumber(bool isAdd)
	{
		if (!ClickSettingCheck())
			return;

		int modify = isAdd ? MAX_NUMBER_STEP : -MAX_NUMBER_STEP;
		ushort newMaxNumber = (ushort)Mathf.Clamp(tempMaxNumber + modify, MAX_NUMBER_MIN_VALUE, MAX_NUMBER_MAX_VALUE);
		if (newMaxNumber == tempMaxNumber)
			return;

		// 設定假數值然後直接更新顯示，結束長按會觸發點擊事件送出真正的設定
		tempMaxNumber = newMaxNumber;
		UpdateMiddlePage();
	}
	public void ClickSetMaxNumber(bool isAdd)
	{
		if (!ClickSettingCheck(false))
			return;
		if (tempMaxNumber == GameData.Instance.ArrangeNumberData.MaxNumber)
			return;

		NetManager.Instance.SendSetMaxNumber(tempMaxNumber);
	}

	public void PressSetNumberGroupCount(bool isAdd)
	{
		if (!ClickSettingCheck())
			return;

		int modify = isAdd ? NUMBER_GROUP_COUNT_STEP : -NUMBER_GROUP_COUNT_STEP;
		byte newCount = (byte)Mathf.Clamp(tempNumberGroupCount + modify, NUMBER_GROUP_COUNT_MIN_VALUE, NUMBER_GROUP_COUNT_MAX_VALUE);
		if (newCount == tempNumberGroupCount)
			return;

		// 設定假數值然後直接更新顯示，結束長按會觸發點擊事件送出真正的設定
		tempNumberGroupCount = newCount;
		UpdateMiddlePage();
	}
	public void ClickSetNumberGroupCount(bool isAdd)
	{
		if (!ClickSettingCheck(false))
			return;
		if (tempNumberGroupCount == GameData.Instance.ArrangeNumberData.NumberGroupCount)
			return;

		NetManager.Instance.SendSetNumberGroupCount(tempNumberGroupCount);
	}

	public void PressSetNumberPerPlayer(bool isAdd)
	{
		if (!ClickSettingCheck())
			return;

		int modify = isAdd ? NUMBER_PER_PLAYER_STEP : -NUMBER_PER_PLAYER_STEP;
		byte newCount = (byte)Mathf.Clamp(tempNumberPerPlayer + modify, NUMBER_PER_PLAYER_MIN_VALUE, NUMBER_PER_PLAYER_MAX_VALUE);
		if (newCount == tempNumberPerPlayer)
			return;

		// 設定假數值然後直接更新顯示，結束長按會觸發點擊事件送出真正的設定
		tempNumberPerPlayer = newCount;
		UpdateMiddlePage();
	}
	public void ClickSetNumberPerPlayer(bool isAdd)
	{
		if (!ClickSettingCheck(false))
			return;
		if (tempNumberPerPlayer == GameData.Instance.ArrangeNumberData.NumberPerPlayer)
			return;

		NetManager.Instance.SendSetNumberPerPlayer(tempNumberPerPlayer);
	}

	protected override bool CheckCanStartGame()
	{
		int totalNumberCount = GameData.Instance.ArrangeNumberData.MaxNumber * GameData.Instance.ArrangeNumberData.NumberGroupCount;
		int needNumberCount = GameData.Instance.PlayerDatas.Count * GameData.Instance.ArrangeNumberData.NumberPerPlayer;
		if (needNumberCount > totalNumberCount)
		{
			ShowPopupMessage("可分配的數字數量不足，請調整設定");
			return false;
		}
		return true;
	}

	public void ClickSetUrgent(bool isUrgent)
	{
		NetManager.Instance.SendSetUrgent(isUrgent);
	}

	public void ClickPoseNumber()
	{
		NetManager.Instance.SendPoseNumber();
	}
}
