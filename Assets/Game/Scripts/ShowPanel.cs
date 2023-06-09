using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using xasset;

public class ShowPanel : MonoBehaviour
{
    public GameObject canvas;
    private GameObject uiPanel;
    private AssetRequest request;

    public void Switch()
    {
        if (uiPanel == null)
        {
            //                        Assets/Game/Prefabs/Panel.prefab
            request = Asset.Load("Assets/Game/Prefabs/Panel.prefab", typeof(GameObject));
            var prefab = Asset.Get<GameObject>("Assets/Game/Prefabs/Panel.prefab");
            uiPanel = Instantiate<GameObject>(prefab);
            uiPanel.transform.parent = canvas.transform;
            return;
        }
        uiPanel.SetActive(!uiPanel.activeSelf);
    }
    
}
