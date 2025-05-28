using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class SceneRaycaster : Editor
{
	static SceneRaycaster()
	{
		SceneView.duringSceneGui += OnScene;
	}

	static void OnScene(SceneView scene)
	{
		Event e = Event.current;

		// shift + right_click
		if (e.type == EventType.MouseDown && e.button == 1 && e.shift)
		{
			List<GameObject> hitList = new List<GameObject>();
			GameObject selectedObj = selectedObj = HandleUtility.PickGameObject(e.mousePosition, false, hitList.ToArray());
			while (selectedObj != null)
			{
				hitList.Add(selectedObj);
				selectedObj = HandleUtility.PickGameObject(e.mousePosition, false, hitList.ToArray());
			}

			GenericMenu menu = new GenericMenu();
			menu.allowDuplicateNames = true;
			for (int i = 0; i < hitList.Count; i++)
			{
				string itemName = hitList[i].name;
				Canvas parentCanvas = null;
				if (hitList[i].transform.parent != null)
					parentCanvas = hitList[i].transform.parent.GetComponentInParent<Canvas>();

				while (parentCanvas != null)
				{
					itemName = parentCanvas.gameObject.name + "/" + itemName;
					if (parentCanvas.transform.parent != null)
						parentCanvas = parentCanvas.transform.parent.GetComponentInParent<Canvas>();
					else
						parentCanvas = null;
				}
				menu.AddItem(new GUIContent(itemName), false, SelectObject, hitList[i]);
			}
			menu.ShowAsContext();
			e.Use();
		}
	}

	static void SelectObject(object obj)
	{
		Selection.activeGameObject = (GameObject)obj;
	}
}