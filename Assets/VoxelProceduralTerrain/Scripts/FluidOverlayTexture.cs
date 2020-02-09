using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidOverlayTexture : MonoBehaviour
{
    private RectTransform rectTransform;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Screen.width);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Screen.height);
    }
}
