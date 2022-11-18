using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using TheOtherRoles.Modules;
using TheOtherRoles;
using Newtonsoft.Json.Linq;

using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;
namespace TheOtherRoles.Objects
{
    public sealed class HaomingMenu : MonoBehaviour
    {
        public static GameObject prefab;
        public static GameObject menuObj;
        private static GameObject content;
        private Button closeButton;
        private Button loadSettingsButton;
        private TMP_Dropdown dropdown;
        private TextMeshProUGUI log;
        private string logText;
        private string title;
        private string fileName;

        private static int selected = 0;
        public static GameObject menuPrefab;
        public static GameObject loadSettingsPrefab;

        public void Awake()
        {
            if (prefab == null)
            {
                prefab = this.gameObject;
                this.gameObject.SetActive(false);
                return;
            }
            else
            {
                this.gameObject.SetActive(true);
            }

            if (menuObj) GameObject.Destroy(menuObj);
            PlayerControl.LocalPlayer.moveable = false;
            PlayerControl.LocalPlayer.NetTransform.Halt();
            if (menuObj != null)
            {
                GameObject.Destroy(menuObj);
            }

            menuObj = GameObject.Instantiate(menuPrefab, this.transform);

            var buttons = menuObj.GetComponentsInChildren<Button>();

            // Closeボタン有効化
            closeButton = buttons.FirstOrDefault(x => x.name == "CloseButton");
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener((UnityAction)close);
            closeButton.GetComponentInChildren<Text>().text = string.Empty;


            // LoadSettingsButton有効化
            loadSettingsButton = buttons.FirstOrDefault(x => x.name == "LoadSettingsButton");
            loadSettingsButton.onClick = new Button.ButtonClickedEvent();
            loadSettingsButton.onClick.AddListener((UnityAction)showloadSettingsMenu);
            loadSettingsButton.GetComponentInChildren<TextMeshProUGUI>().text = "Regulations";

            // メニュー表示ボタン有効化
            menuObj.SetActive(true);
            showloadSettingsMenu();
        }

        private void FixedUpdate()
        {
            PlayerControl.LocalPlayer.moveable = false;
            if (Input.GetKey(KeyCode.Escape))
            {
                close();
            }
        }

        public void OnEnable()
        {
            this.enabled = true;
        }

        public void OnDisable()
        {
            if (menuObj) menuObj.SetActive(false);
        }

        public void OnDestroy()
        {
            PlayerControl.LocalPlayer.moveable = true;
        }

        void showloadSettingsMenu()
        {
            title = "";
            fileName = "";

            string filePath = Path.GetDirectoryName(Application.dataPath) + @"\Regulations\";
            bool exists = System.IO.Directory.Exists(filePath);
            if (!exists) System.IO.Directory.CreateDirectory(filePath);

            if (content) GameObject.Destroy(content);
            content = GameObject.Instantiate(loadSettingsPrefab, menuObj.transform);
            content.SetActive(true);

            var buttons = content.GetComponentsInChildren<Button>();
            var saveButton = buttons.FirstOrDefault(x => x.name == "SaveButton");
            saveButton.GetComponentInChildren<TextMeshProUGUI>().text = "Save";
            saveButton.onClick = new Button.ButtonClickedEvent();
            saveButton.onClick.AddListener((UnityAction)save);
            var loadButton = buttons.FirstOrDefault(x => x.name == "LoadButton");
            loadButton.GetComponentInChildren<TextMeshProUGUI>().text = "Load";
            loadButton.onClick = new Button.ButtonClickedEvent();
            loadButton.onClick.AddListener((UnityAction)load);

            dropdown = content.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(x => x.name == "Dropdown");
            dropdown.ClearOptions();
            var optionDataList = new Il2CppSystem.Collections.Generic.List<TMP_Dropdown.OptionData>();
            List<string> optionList = new();
            var fileList = getFileList();
            foreach (var file in fileList)
            {
                var optionData = new TMP_Dropdown.OptionData();
                optionData.text = getTitleFromFile(file);
                optionDataList.Add(optionData);
            }
            dropdown.AddOptions(optionDataList);
            dropdown.value = selected;
            dropdown.RefreshShownValue();
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            dropdown.onValueChanged.AddListener((UnityAction<int>)onValueChanged);


            var inputFields = content.GetComponentsInChildren<TMP_InputField>();
            var titleField = inputFields.FirstOrDefault(x => x.name == "TitleInputField");
            titleField.onValueChanged = new TMP_InputField.OnChangeEvent();
            titleField.onValueChanged.AddListener((UnityAction<String>)onTitleChanged);
            var fileNameField = inputFields.FirstOrDefault(x => x.name == "FileNameInputField");
            fileNameField.onValueChanged = new TMP_InputField.OnChangeEvent();
            fileNameField.onValueChanged.AddListener((UnityAction<String>)onFileNameChanged);

            var scrollView = content.GetComponentsInChildren<ScrollRect>().FirstOrDefault(x => x.name == "Scroll View");
            log = scrollView.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.name == "Log");
            log.text = logText;
        }

        void onTitleChanged(string value)
        {
            title = value;
        }

        void onFileNameChanged(string value)
        {
            fileName = value;
        }

        void onValueChanged(int value)
        {
            selected = value;
            showloadSettingsMenu();
        }
        void close()
        {
            GameObject.Destroy(this.gameObject);
        }
        void load()
        {
            var fileList = getFileList();
            Regulation.load(fileList[selected]);
            sendLog($"{fileList[selected]} is loaded");
        }

        void sendLog(string s)
        {
            logText = s + "\n" + log.text;
            log.text = logText;
        }

        void save()
        {
            string filePath = Path.GetDirectoryName(Application.dataPath) + @"\Regulations\";
            bool exists = System.IO.Directory.Exists(filePath);
            if (!exists) System.IO.Directory.CreateDirectory(filePath);
            if (fileName == null || fileName == string.Empty)
            {
                sendLog($"FileName can not be empty");
                return;
            }

            if (title == null || title == string.Empty)
            {
                sendLog($"Title can not be empty");
                return;
            }
            if (!Regex.IsMatch(fileName, @".*\.json"))
            {
                fileName += ".json";
            }
            filePath += fileName;
            Regulation.save(filePath, title);
            sendLog($"{title} is saved to {filePath}");
            showloadSettingsMenu();
        }

        List<string> getFileList()
        {
            string filePath = Path.GetDirectoryName(Application.dataPath) + @"\Regulations\";
            var fileList = Directory.GetFiles(filePath, "*.json").ToList();
            return fileList;
        }

        string getTitleFromFile(string file)
        {
            string json = File.ReadAllText(file);
            JToken jobj = JObject.Parse(json)["title"];
            return jobj != null ? jobj.ToString() : file;
        }

        class Regulation
        {
            public static void save(string filePath, string title)
            {
                var value = new Dictionary<string, object>();
                value.Add("title", title);

                // AmongUsオプション保存
                value.Add("Map", (int)PlayerControl.GameOptions.MapId);
                value.Add("NumImpostors", PlayerControl.GameOptions.NumImpostors);
                value.Add("ConfirmEjection", PlayerControl.GameOptions.ConfirmImpostor ? 1 : 0);
                value.Add("EmergencyMeetings", PlayerControl.GameOptions.NumEmergencyMeetings);
                value.Add("EmergencyCooldown", PlayerControl.GameOptions.EmergencyCooldown);
                value.Add("DiscussionTime", PlayerControl.GameOptions.DiscussionTime);
                value.Add("VotingTime", PlayerControl.GameOptions.VotingTime);
                value.Add("AnonymousVotes", PlayerControl.GameOptions.AnonymousVotes ? 1 : 0);
                value.Add("PlayerSpeed", (int)(PlayerControl.GameOptions.PlayerSpeedMod / 0.25));
                value.Add("CrewmateVision", (int)(PlayerControl.GameOptions.CrewLightMod / 0.25));
                value.Add("ImpostorVision", (int)(PlayerControl.GameOptions.ImpostorLightMod / 0.25));
                value.Add("KillCooldown", (int)(PlayerControl.GameOptions.KillCooldown / 2.5));
                value.Add("KillDistance", PlayerControl.GameOptions.KillDistance);
                value.Add("VisualTask", PlayerControl.GameOptions.VisualTasks ? 1 : 0);
                value.Add("TaskBarUpdates", (int)PlayerControl.GameOptions.TaskBarMode);
                value.Add("CommonTasks", PlayerControl.GameOptions.NumCommonTasks);
                value.Add("LongTasks", PlayerControl.GameOptions.NumLongTasks);
                value.Add("ShortTasks", PlayerControl.GameOptions.NumShortTasks);

                // MODオプション保存
                var mod_options = new List<object>();
                foreach (var option in CustomOption.options)
                {
                    if (option.id != -1)
                    {
                        var item = new Dictionary<string, object>();
                        item.Add("id", option.id);
                        item.Add("value", option.selection);
                        mod_options.Add(item);
                    }
                }
                value.Add("mod_options", mod_options);

                // json変換
                string data = Helpers.SerializeObject(value);

                File.WriteAllText(filePath, data);
                string fileDir = Path.GetDirectoryName(Application.dataPath) + @"\Regulations\";
                System.Diagnostics.Process.Start(fileDir);
            }

            public static void load(string file)
            {
                string json = File.ReadAllText(file);
                JToken jobj = JObject.Parse(json);
                string title = jobj["title"].ToString();

                PlayerControl.GameOptions.MapId = (byte)int.Parse(jobj["Map"].ToString());
                PlayerControl.GameOptions.NumImpostors = int.Parse(jobj["NumImpostors"].ToString());
                PlayerControl.GameOptions.ConfirmImpostor = jobj["ConfirmEjection"].ToString() == "1" ? true : false;
                PlayerControl.GameOptions.NumEmergencyMeetings = int.Parse(jobj["EmergencyMeetings"].ToString());
                PlayerControl.GameOptions.EmergencyCooldown = int.Parse(jobj["EmergencyCooldown"].ToString());
                PlayerControl.GameOptions.DiscussionTime = int.Parse(jobj["DiscussionTime"].ToString());
                PlayerControl.GameOptions.VotingTime = int.Parse(jobj["VotingTime"].ToString());
                PlayerControl.GameOptions.AnonymousVotes = jobj["AnonymousVotes"].ToString() == "1" ? true : false;
                PlayerControl.GameOptions.PlayerSpeedMod = int.Parse(jobj["PlayerSpeed"].ToString()) * 0.25f;
                PlayerControl.GameOptions.CrewLightMod = int.Parse(jobj["CrewmateVision"].ToString()) * 0.25f;
                PlayerControl.GameOptions.ImpostorLightMod = int.Parse(jobj["ImpostorVision"].ToString()) * 0.25f;
                PlayerControl.GameOptions.KillCooldown = int.Parse(jobj["KillCooldown"].ToString()) * 2.5f;
                PlayerControl.GameOptions.KillDistance = int.Parse(jobj["KillDistance"].ToString());
                PlayerControl.GameOptions.VisualTasks = jobj["VisualTask"].ToString() == "1" ? true : false;
                PlayerControl.GameOptions.TaskBarMode = (TaskBarMode)int.Parse(jobj["TaskBarUpdates"].ToString());
                PlayerControl.GameOptions.NumCommonTasks = int.Parse(jobj["CommonTasks"].ToString());
                PlayerControl.GameOptions.NumLongTasks = int.Parse(jobj["LongTasks"].ToString());
                PlayerControl.GameOptions.NumShortTasks = int.Parse(jobj["ShortTasks"].ToString());

                jobj = jobj["mod_options"];
                for (JToken current = jobj.First; current != null; current = current.Next)
                {
                    int id = int.Parse(current["id"].ToString());
                    int value = int.Parse(current["value"].ToString());
                    CustomOption.options.FirstOrDefault(x => x.id == id)?.updateSelection(value);
                }
            }
        }

        [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickDown))]
        class PassiveButtonReveiceClickDown
        {
            public static bool Prefix(PassiveButton __instance)
            {
                if (HaomingMenu.menuObj) return false;
                return true;

            }
        }
        [HarmonyPatch(typeof(UiElement), nameof(UiElement.ReceiveMouseOver))]
        class UiElementReceiveMouseOver
        {
            public static bool Prefix(PassiveButton __instance)
            {
                if (HaomingMenu.menuObj) return false;
                return true;

            }
        }

        [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
        class LobbyBehaviourStartPatch
        {
            public static void Postfix(LobbyBehaviour __instance)
            {
                var panel = GameObject.Find("Lobby(Clone)/SmallBox/Panel");
                var leftBox = GameObject.Find("Lobby(Clone)/Leftbox");
                var newPanel = GameObject.Instantiate(panel, leftBox.transform);
                var console = newPanel.GetComponentInChildren<OptionsConsole>();
                var obj = new GameObject("HaomingMenu");
                obj.AddComponent<HaomingMenu>();
                console.MenuPrefab = HaomingMenu.prefab;
                leftBox.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            }
        }
    }
}
