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
	private GameObject PlayingPage;

	[SerializeField]
	private GameObject GameResultWindow;
	[SerializeField]
	private Text GameResultText;

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

	public void UpdateMiddlePage()
	{
		IdlePage.SetActive(GameData.Instance.ArrangeNumberData.CurrentState == ArrangeNumberState.WAITING);
		PlayingPage.SetActive(GameData.Instance.ArrangeNumberData.CurrentState == ArrangeNumberState.PLAYING);

		switch (GameData.Instance.ArrangeNumberData.CurrentState)
		{
			case ArrangeNumberState.WAITING:
				{
					// TODO: 更新設定顯示
				}
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
}
