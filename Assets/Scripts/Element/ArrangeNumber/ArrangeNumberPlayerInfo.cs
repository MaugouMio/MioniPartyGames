using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ArrangeNumberPlayerInfo : PlayerInfo
{
	[SerializeField]
	private Text leftNumberText;
	[SerializeField]
	private Image stateMask;

	public override void UpdateData(PlayerData playerData)
	{
		base.UpdateData(playerData);
		if (playerData is not ArrangeNumberPlayerData)
			return;

		ArrangeNumberPlayerData anPlayerData = playerData as ArrangeNumberPlayerData;

		// 更新剩餘數字
		if (anPlayerData.LeftNumbers.Count == 0)
		{
			leftNumberText.text = "<i><color=#00aa00>已出完所有數字</color></i>";
			stateMask.color = new Color(0f, 0f, 0f, 0.7f);
		}
		else
		{
			if (GameData.Instance.IsPlayer())
			{
				leftNumberText.text = $"<i><color=#999999>剩餘數字{anPlayerData.LeftNumbers.Count}個</color></i>";
			}
			else
			{
				var ascendingNumbers = anPlayerData.LeftNumbers.OrderBy(x => x);
				leftNumberText.text = $"<i><color=#999999>持有數字：{string.Join(", ", ascendingNumbers)}</color></i>";
			}
			stateMask.color = Color.clear;
		}
	}
}
