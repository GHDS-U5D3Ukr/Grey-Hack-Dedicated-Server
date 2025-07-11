﻿using HarmonyLib;
using UnityEngine;
using TMPro;
using PierToPierPlugin;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine.UI;
using System.Linq;

[HarmonyPatch]
public class BiosMenuPatch
{
    private static GameObject panel;

    private static GameObject hostToggle;
    private static GameObject hostInputPort;
    private static GameObject input;
    private static GameObject inputToggle;
    private static Toggle hostToggleComp;
    private static TMP_InputField hostInputPortComp;
    private static TMP_InputField inputServerComp;
    private static Toggle inputToggleComp;

    private static GameObject CreateToggle(string name, string label, GameObject parent, GameObject template, Vector2 offset)
    {
        GameObject toggle = Object.Instantiate(template);
        toggle.transform.SetParent(parent.transform, false);
        toggle.name = name;
        if (offset != Vector2.zero)
        {
            var rect = toggle.GetComponent<RectTransform>();
            rect.anchoredPosition = offset;
        }
        var toggleComp = toggle.GetComponent<Toggle>();
        toggleComp.isOn = false;
        var text = toggle.GetComponentInChildren<TMP_Text>();
        text.text = label;
        return toggle;
    }

    private static GameObject CreateInputBox(string name, string placeholder, GameObject parent, Vector2 deltaSize, Vector2 offset, Color backColor)
    {
        GameObject inputBox = new GameObject(name);
        inputBox.transform.SetParent(parent.transform, false);
        var rect = inputBox.AddComponent<RectTransform>();
        rect.anchoredPosition = offset;
        var inputComp = inputBox.AddComponent<TMP_InputField>();
        inputComp.richText = false;
        var inputLayout = inputBox.AddComponent<LayoutElement>();
        inputLayout.preferredHeight = 60;
        var inputBackground = new GameObject("Background");
        inputBackground.transform.SetParent(inputBox.transform, false);
        var inputBackRect = inputBackground.AddComponent<RectTransform>();
        inputBackRect.sizeDelta = deltaSize;
        var inputImage = inputBackground.AddComponent<Image>();
        inputImage.color = backColor;
        inputComp.image = inputImage;
        var inputText = new GameObject("InputText");
        inputText.transform.SetParent(inputBox.transform, false);
        var inputTextGUI = inputText.AddComponent<TextMeshProUGUI>();
        inputTextGUI.alignment = TextAlignmentOptions.Center;
        inputComp.textComponent = inputTextGUI;
        var inputPlaceholder = new GameObject("Placeholder");
        inputPlaceholder.transform.SetParent(inputBox.transform, false);
        var inputPlaceText = inputPlaceholder.AddComponent<TextMeshProUGUI>();
        inputPlaceText.alignment = TextAlignmentOptions.Center;
        inputPlaceText.text = placeholder;
        inputComp.placeholder = inputPlaceText;
        return inputBox;
    }

    [HarmonyPatch(typeof(BiosMenu), "Start")]
    class StartPatch
    {
        static void Postfix()
        {
            panel = GameObject.Find("InfoDeletePlayer/Panel");
            GameObject toggle = GameObject.Find("ToggleDeletePlayer");
            if (GameObject.Find("ToggleHostServer") == null)
            {
                hostToggle = CreateToggle("ToggleHostServer", "Host Server", panel, toggle, Vector2.zero);
                hostToggleComp = hostToggle.GetComponent<Toggle>();
                hostToggleComp.onValueChanged.AddListener((isOn) =>
                {
                    DataUtils.hosting = isOn;
                    hostInputPort.gameObject.SetActive(isOn);
                });
                hostToggle.gameObject.SetActive(false);
            }
            if (GameObject.Find("InputPort") == null)
            {
                hostInputPort = CreateInputBox("InputPort", "Port (21200)", panel, new Vector2(200, 30), new Vector2(0, 12), new Color(33f, 0f, 0f, 0.3f));
                hostInputPortComp = hostInputPort.GetComponent<TMP_InputField>();
                hostInputPortComp.characterLimit = 5;
                hostInputPortComp.onValueChanged.AddListener((string value) =>
                {
                    DataUtils.hostPortString = value;
                });
                hostInputPort.gameObject.SetActive(false);
            }
            if (GameObject.Find("ToggleHideServer") == null)
            {
                inputToggle = CreateToggle("ToggleHideServer", "Hide IP", panel, toggle, Vector2.zero);
                inputToggleComp = inputToggle.GetComponent<Toggle>();
                inputToggleComp.onValueChanged.AddListener((isOn) =>
                {
                    ShowHideInput(isOn);
                });
                inputToggle.gameObject.SetActive(false);
            }
            if (GameObject.Find("InputServerIp") == null)
            {
                input = CreateInputBox("InputServerIp", "Enter Host IP", panel, new Vector2(300, 30), new Vector2(0, 12), new Color(33f, 0f, 0f, 0.3f));
                inputServerComp = input.GetComponent<TMP_InputField>();
                inputServerComp.characterLimit = 21;
                inputServerComp.onValueChanged.AddListener((string value) =>
                {
                    DataUtils.host = value;
                });
                input.gameObject.SetActive(false);
            }
            if (DataUtils.publicAddress == null)
            {
                DataUtils.publicAddress = PlayerClient.Singleton.publicAddress;
            }
            if (DataUtils.nightlyAddress == null)
            {
                DataUtils.nightlyAddress = PlayerClient.Singleton.nightlyAddress;
            }
        }

        private static void ShowHideInput(bool isOn)
        {
            if (isOn)
                inputServerComp.inputType = TMP_InputField.InputType.Password;
            if (!isOn)
                inputServerComp.inputType = TMP_InputField.InputType.Standard;
            inputServerComp.ForceLabelUpdate();
        }
    }

    [HarmonyPatch(typeof(BiosMenu), "Configure")]
    class ConfigurePatch
    {
        static void Postfix(ref BiosMenu __instance)
        {
            GameObject biosUtil = GameObject.Find("MainMenu/MainPanel/MidPanel/PanelRealSpecs/Adorno/TextMeshPro Text");
            if (biosUtil != null)
            {
                var tmp = biosUtil.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = "System Hardware... [OK]\r\nUnsafe pointer access... [OK]\r\nLoading p2ppatched Bios... [OK]\r\nSystem ready to connect!";
            }
        }
    }

    [HarmonyPatch(typeof(BiosMenu), "ResetWipeOptions")]
    class ResetWipeOptionsPatch
    {
        static void Prefix()
        {
            inputServerComp.text = "";
            inputToggleComp.isOn = false;
            hostToggleComp.isOn = false;
            hostInputPortComp.text = "";
        }
    }

    [HarmonyPatch(typeof(BiosMenu), "OnStartGame")]
    class OnStartGamePatch
    {
        static bool Prefix(BiosMenu __instance)
        {
            FieldInfo fieldInfo = AccessTools.Field(typeof(BiosMenu), "currentGameMode");
            var currentGameMode = (GameMode)fieldInfo.GetValue(__instance);
            if (currentGameMode == GameMode.SinglePlayer && DataUtils.hosting)
            {
                if (string.IsNullOrWhiteSpace(DataUtils.hostPortString))
                    return true;
                if (!int.TryParse(DataUtils.hostPortString, out int port) || port < 0 || port > 65535)
                {
                    __instance.panelsOptions[6].SetActive(true);
                    __instance.connect_text.text = $"Error: Invalid host port, please check your input and try again.";
                    __instance.objBackButtonConn.SetActive(true);
                    return false;
                }
                DataUtils.hostPort = port;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BiosMenu), "ConnectServer")]
    class ConnectServerPatch
    {
        static bool Prefix(BiosMenu __instance, ref IEnumerator __result)
        {
            FieldInfo fieldInfo = AccessTools.Field(typeof(BiosMenu), "currentGameMode");
            var currentGameMode = (GameMode)fieldInfo.GetValue(__instance);
            if (currentGameMode == GameMode.Multiplayer && !string.IsNullOrEmpty(DataUtils.host))
            {
                if (DataUtils.TryParseHost(DataUtils.host, out string ip, out int port, out string error))
                {
                    DataUtils.host = "";
                    DataUtils.server = ip;
                    DataUtils.port = port;
                }
                else
                {
                    __instance.StopCoroutine("DrawDots");
                    __instance.connect_text.text = $"Error: {error}, please check your input and try again.";
                    __instance.objBackButtonConn.SetActive(true);
                    __result = CleanRes();
                    return false;
                }
            }
            else if (currentGameMode == GameMode.Multiplayer)
            {
                DataUtils.server = "";
                DataUtils.port = 21200;
            }
            return true;
        }
        static IEnumerator CleanRes()
        {
            yield break;
        }
    }

    [HarmonyPatch(typeof(BiosMenu), "OnSelectOnlineMode")]
    class OnSelectOnlineModePatch
    {
        static void Prefix()
        {
            if (hostToggle != null)
                hostToggle.SetActive(false);
            if (input != null)
                input.SetActive(true);
            if (inputToggle != null)
                inputToggle.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(BiosMenu), "OnSelectSinglePlayer")]
    class OnSelectSinglePlayerPatch
    {
        static void Prefix()
        {
            if (input != null)
                input.SetActive(false);
            if (inputToggle != null)
                inputToggle.SetActive(false);
            if (hostToggle != null)
                hostToggle.SetActive(true);
            bool flag = false;
            foreach (KeyValuePair<string, PluginInfo> kvp in Chainloader.PluginInfos)
            {
                PluginInfo info = kvp.Value;
                if (info.Metadata.Name.ToLower() == "saveselectplugin")
                {
                    flag = true;
                    break;
                }
            }
            if (flag)
            {
                var panelType = AccessTools.TypeByName("SaveSelectPlugin.SavePanel");
                var parent = (Component)GameObject.FindObjectsOfType(typeof(MonoBehaviour), true).FirstOrDefault(x => x.GetType() == panelType);
                var toggle = parent.transform.Find("ScrollView/Viewport/Content/SaveTemplate/Top/Wipe").gameObject;
                if (parent != null && hostToggle.transform.parent.name != parent.transform.name)
                {
                    Plugin.Logger.LogInfo("SaveSelectPlugin found, relocating Host button...");
                    hostToggle = CreateToggle("ToggleHostServer", "Host Server", parent.gameObject, toggle, new Vector2(420, -30));
                    hostToggle.GetComponent<RectTransform>().sizeDelta = new Vector2(113, 20);
                    hostToggleComp = hostToggle.GetComponent<Toggle>();
                    hostToggleComp.onValueChanged.AddListener((isOn) =>
                    {
                        DataUtils.hosting = isOn;
                        hostInputPort.gameObject.SetActive(isOn);
                    });
                }
                else if (parent == null)
                    Plugin.Logger.LogError("Couldn't find SavePanel!");
            }
        }
    }

    private enum GameMode
    {
        Multiplayer,
        SinglePlayer
    }
}