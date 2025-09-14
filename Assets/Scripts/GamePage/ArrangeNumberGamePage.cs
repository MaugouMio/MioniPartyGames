using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ArrangeNumberGamePage : GamePage
{
	public static ArrangeNumberGamePage Instance { get; private set; }

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

	protected override void Awake()
	{
		base.Awake();

		Instance = this;
		GameResultWindow.SetActive(false);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Instance = null;
	}

	protected override void UpdateDataReal()
	{
		base.UpdateDataReal();

		UpdateMiddlePage();
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
				{
					// TODO: 更新遊戲中資訊
				}
				break;
			default:
				break;
		}
	}

	public void UpdateSettings()
	{
		MaxNumberSettingText.text = GameData.Instance.ArrangeNumberData.MaxNumber.ToString();

		if (GameData.Instance.ArrangeNumberData.NumberGroupCount > 0)
			NumberGroupCountSettingText.text = GameData.Instance.ArrangeNumberData.NumberGroupCount.ToString();
		else
			NumberGroupCountSettingText.text = "∞";

		NumberPerPlayerSettingText.text = GameData.Instance.ArrangeNumberData.NumberPerPlayer.ToString();
	}

	public override void StartCountdown(int seconds)
	{
		base.StartCountdown(seconds);

		// 開始倒數就自動關閉上一場的結果顯示
		ClickCloseResult();
	}

	public void ShowGameResult()
	{
		//GameResultText.text = resultText;
		GameResultWindow.SetActive(true);
	}

	public void ClickCloseResult()
	{
		GameResultWindow.SetActive(false);
	}

	protected override ushort GetHideChatUID()
	{
		return 0;
	}

	public void PressSetMaxNumber(bool isAdd)
	{
		int modify = isAdd ? MAX_NUMBER_STEP : -MAX_NUMBER_STEP;
		ushort newMaxNumber = (ushort)Mathf.Clamp(GameData.Instance.ArrangeNumberData.MaxNumber + modify, MAX_NUMBER_MIN_VALUE, MAX_NUMBER_MAX_VALUE);
		if (newMaxNumber == GameData.Instance.ArrangeNumberData.MaxNumber)
			return;
		
		// 設定假數值然後直接更新顯示，結束長按會觸發點擊事件送出真正的設定
		GameData.Instance.ArrangeNumberData.MaxNumber = newMaxNumber;
		UpdateMiddlePage();
	}
	public void ClickSetMaxNumber(bool isAdd)
	{
		int modify = isAdd ? MAX_NUMBER_STEP : -MAX_NUMBER_STEP;
		ushort newMaxNumber = (ushort)Mathf.Clamp(GameData.Instance.ArrangeNumberData.MaxNumber + modify, MAX_NUMBER_MIN_VALUE, MAX_NUMBER_MAX_VALUE);
		if (newMaxNumber == GameData.Instance.ArrangeNumberData.MaxNumber)
			return;

		NetManager.Instance.SendSetMaxNumber(newMaxNumber);
	}

	public void PressSetNumberGroupCount(bool isAdd)
	{
		int modify = isAdd ? NUMBER_GROUP_COUNT_STEP : -NUMBER_GROUP_COUNT_STEP;
		byte newCount = (byte)Mathf.Clamp(GameData.Instance.ArrangeNumberData.NumberGroupCount + modify, NUMBER_GROUP_COUNT_MIN_VALUE, NUMBER_GROUP_COUNT_MAX_VALUE);
		if (newCount == GameData.Instance.ArrangeNumberData.NumberGroupCount)
			return;

		// 設定假數值然後直接更新顯示，結束長按會觸發點擊事件送出真正的設定
		GameData.Instance.ArrangeNumberData.NumberGroupCount = newCount;
		UpdateMiddlePage();
	}
	public void ClickSetNumberGroupCount(bool isAdd)
	{
		int modify = isAdd ? NUMBER_GROUP_COUNT_STEP : -NUMBER_GROUP_COUNT_STEP;
		byte newCount = (byte)Mathf.Clamp(GameData.Instance.ArrangeNumberData.NumberGroupCount + modify, NUMBER_GROUP_COUNT_MIN_VALUE, NUMBER_GROUP_COUNT_MAX_VALUE);
		if (newCount == GameData.Instance.ArrangeNumberData.NumberGroupCount)
			return;

		NetManager.Instance.SendSetNumberGroupCount(newCount);
	}

	public void PressSetNumberPerPlayer(bool isAdd)
	{
		int modify = isAdd ? NUMBER_PER_PLAYER_STEP : -NUMBER_PER_PLAYER_STEP;
		byte newCount = (byte)Mathf.Clamp(GameData.Instance.ArrangeNumberData.NumberPerPlayer + modify, NUMBER_PER_PLAYER_MIN_VALUE, NUMBER_PER_PLAYER_MAX_VALUE);
		if (newCount == GameData.Instance.ArrangeNumberData.NumberPerPlayer)
			return;

		// 設定假數值然後直接更新顯示，結束長按會觸發點擊事件送出真正的設定
		GameData.Instance.ArrangeNumberData.NumberPerPlayer = newCount;
		UpdateMiddlePage();
	}
	public void ClickSetNumberPerPlayer(bool isAdd)
	{
		int modify = isAdd ? NUMBER_PER_PLAYER_STEP : -NUMBER_PER_PLAYER_STEP;
		byte newCount = (byte)Mathf.Clamp(GameData.Instance.ArrangeNumberData.NumberPerPlayer + modify, NUMBER_PER_PLAYER_MIN_VALUE, NUMBER_PER_PLAYER_MAX_VALUE);
		if (newCount == GameData.Instance.ArrangeNumberData.NumberPerPlayer)
			return;

		NetManager.Instance.SendSetNumberPerPlayer(newCount);
	}
}
