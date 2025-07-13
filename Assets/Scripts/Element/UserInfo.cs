using UnityEngine;
using UnityEngine.UI;

public class UserInfo : MonoBehaviour
{
	[SerializeField]
	private Text nameText;

	public void UpdateData(UserData userData)
	{
		if (userData.UID == GameData.Instance.SelfUID)
			nameText.text = $"<color=blue>{userData.Name}</color>";
		else
			nameText.text = userData.Name;
	}
}
