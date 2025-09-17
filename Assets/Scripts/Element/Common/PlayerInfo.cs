using UnityEngine;
using UnityEngine.UI;

public abstract class PlayerInfo : MonoBehaviour
{
	[SerializeField]
	protected Text nameText;
	[SerializeField]
	protected GameObject selfIndicator;

	protected ushort selfUID = 0;

	public virtual void UpdateData(PlayerData playerData)
	{
		if (!GameData.Instance.UserDatas.ContainsKey(playerData.UID))
			return;

		UserData userData = GameData.Instance.UserDatas[playerData.UID];
		nameText.text = userData.Name;

		selfIndicator.SetActive(playerData.UID == GameData.Instance.SelfUID);
		selfUID = playerData.UID;
	}
}
