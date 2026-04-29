using UnityEngine;
using TMPro;
using System;

public class PlayerUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI promptText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void updateText(String promptMessage)
    {
        ApplyPromptStyle();
        promptText.text = promptMessage;
    }

    private void ApplyPromptStyle()
    {
        if (promptText == null)
            return;

        promptText.enableWordWrapping = true;
        promptText.overflowMode = TextOverflowModes.Overflow;
        promptText.enableAutoSizing = true;
        promptText.fontSizeMin = 14f;
        promptText.fontSizeMax = Mathf.Max(promptText.fontSize, 24f);
    }
}
