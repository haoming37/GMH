using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Twitch;
using UnityEngine;
using UnityEngine.UI;

namespace TheOtherRoles.Modules
{

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    public class ModUpdaterButton
    {
        private static void Prefix(MainMenuManager __instance)
        {
            if (TheOtherRolesPlugin.DebugMode.Value) FastDestroyableSingleton<EOSManager>.Instance.PlayOffline();
            AssetLoader.LoadAssets();
            CustomHatLoader.LaunchHatFetcher();
            var template = GameObject.Find("ExitGameButton");

            // Discrodボタン
            var buttonDiscord = UnityEngine.Object.Instantiate(template, null);
            buttonDiscord.transform.localPosition = new Vector3(buttonDiscord.transform.localPosition.x, buttonDiscord.transform.localPosition.y + 0.6f, buttonDiscord.transform.localPosition.z);

            var textDiscord = buttonDiscord.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
            __instance.StartCoroutine(Effects.Lerp(0.1f, new System.Action<float>((p) =>
            {
                textDiscord.SetText("Discord");
            })));

            PassiveButton passiveButtonDiscord = buttonDiscord.GetComponent<PassiveButton>();
            SpriteRenderer buttonSpriteDiscord = buttonDiscord.GetComponent<SpriteRenderer>();

            passiveButtonDiscord.OnClick = new Button.ButtonClickedEvent();
            passiveButtonDiscord.OnClick.AddListener((System.Action)(() => Application.OpenURL("https://discord.gg/sTt8EzEpHP")));

            Color discordColor = new Color32(88, 101, 242, byte.MaxValue);
            buttonSpriteDiscord.color = textDiscord.color = discordColor;
            passiveButtonDiscord.OnMouseOut.AddListener((System.Action)delegate
            {
                buttonSpriteDiscord.color = textDiscord.color = discordColor;
            });

            // Twitterボタン
            var buttonTwitter = UnityEngine.Object.Instantiate(template, null);
            buttonTwitter.transform.localPosition = new Vector3(buttonTwitter.transform.localPosition.x, buttonTwitter.transform.localPosition.y + 1.2f, buttonTwitter.transform.localPosition.z);

            var textTwitter = buttonTwitter.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
            __instance.StartCoroutine(Effects.Lerp(0.1f, new System.Action<float>((p) =>
            {
                textTwitter.SetText("Twitter");
            })));

            PassiveButton passiveButtonTwitter = buttonTwitter.GetComponent<PassiveButton>();
            SpriteRenderer buttonSpriteTwitter = buttonTwitter.GetComponent<SpriteRenderer>();

            passiveButtonTwitter.OnClick = new Button.ButtonClickedEvent();
            passiveButtonTwitter.OnClick.AddListener((System.Action)(() => Application.OpenURL("https://twitter.com/haoming_dev")));

            Color twitterColor = new Color32(29, 161, 242, byte.MaxValue);
            buttonSpriteTwitter.color = textTwitter.color = twitterColor;
            passiveButtonTwitter.OnMouseOut.AddListener((System.Action)delegate
            {
                buttonSpriteTwitter.color = textTwitter.color = twitterColor;
            });

            // アップデートボタン
            ModUpdater.LaunchUpdater();
            if (!ModUpdater.hasUpdate) return;
            if (template == null) return;

            var button = UnityEngine.Object.Instantiate(template, null);
            button.transform.localPosition = new Vector3(button.transform.localPosition.x, button.transform.localPosition.y + 0.6f, button.transform.localPosition.z);

            PassiveButton passiveButton = button.GetComponent<PassiveButton>();
            passiveButton.OnClick = new Button.ButtonClickedEvent();
            passiveButton.OnClick.AddListener((UnityEngine.Events.UnityAction)onClick);

            var text = button.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
            __instance.StartCoroutine(Effects.Lerp(0.1f, new System.Action<float>((p) =>
            {
                text.SetText(ModTranslation.getString("updateButton"));
            })));

            TwitchManager man = FastDestroyableSingleton<TwitchManager>.Instance;
            ModUpdater.InfoPopup = UnityEngine.Object.Instantiate<GenericPopup>(man.TwitchPopup);
            ModUpdater.InfoPopup.TextAreaTMP.fontSize *= 0.7f;
            ModUpdater.InfoPopup.TextAreaTMP.enableAutoSizing = false;

            // Discordボタンを上にずらす
            buttonDiscord.transform.localPosition = new Vector3(buttonDiscord.transform.localPosition.x, buttonDiscord.transform.localPosition.y + 0.6f, buttonDiscord.transform.localPosition.z);
            buttonTwitter.transform.localPosition = new Vector3(buttonTwitter.transform.localPosition.x, buttonTwitter.transform.localPosition.y + 0.6f, buttonTwitter.transform.localPosition.z);

            void onClick()
            {
                ModUpdater.ExecuteUpdate();
                button.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(AnnouncementPopUp), nameof(AnnouncementPopUp.UpdateAnnounceText))]
    public static class Announcement
    {
        public static bool Prefix(AnnouncementPopUp __instance)
        {
            var text = __instance.AnnounceTextMeshPro;
            text.text = ModUpdater.announcement;
            return false;
        }
    }

    public class ModUpdater
    {
        public static bool running = false;
        public static bool hasUpdate = false;
        public static string updateURI = null;
        private static Task updateTask = null;
        public static string announcement = "";
        public static GenericPopup InfoPopup;

        public static void LaunchUpdater()
        {
            if (running) return;
            running = true;
            checkForUpdate().GetAwaiter().GetResult();
            clearOldVersions();
            if (hasUpdate || TheOtherRolesPlugin.ShowPopUpVersion.Value != TheOtherRolesPlugin.VersionString)
            {
                FastDestroyableSingleton<MainMenuManager>.Instance.Announcement.gameObject.SetActive(true);
                TheOtherRolesPlugin.ShowPopUpVersion.Value = TheOtherRolesPlugin.VersionString;
            }
        }

        public static void ExecuteUpdate()
        {
            string info = ModTranslation.getString("updatePleaseWait");
            ModUpdater.InfoPopup.Show(info); // Show originally
            if (updateTask == null)
            {
                if (updateURI != null)
                {
                    updateTask = downloadUpdate();
                }
                else
                {
                    info = ModTranslation.getString("updateManually");
                }
            }
            else
            {
                info = ModTranslation.getString("updateInProgress");
            }
            ModUpdater.InfoPopup.StartCoroutine(Effects.Lerp(0.01f, new System.Action<float>((p) => { ModUpdater.setPopupText(info); })));
        }

        public static void clearOldVersions()
        {
            try
            {
                DirectoryInfo d = new(Path.GetDirectoryName(Application.dataPath) + @"\BepInEx\plugins");
                string[] files = d.GetFiles("*.old").Select(x => x.FullName).ToArray(); // Getting old versions
                foreach (string f in files)
                    File.Delete(f);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("Exception occurred when clearing old versions:\n" + e);
            }
        }

        public static async Task<bool> checkForUpdate()
        {
            try
            {
                HttpClient http = new();
                http.DefaultRequestHeaders.Add("User-Agent", "TheOtherRoles Updater");
                var response = await http.GetAsync(new System.Uri("https://api.github.com/repos/haoming37/TheOtherRoles-GM-Haoming/releases/latest"), HttpCompletionOption.ResponseContentRead);
                if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                {
                    System.Console.WriteLine("Server returned no data: " + response.StatusCode.ToString());
                    return false;
                }
                string json = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(json);

                string tagname = data["tag_name"]?.ToString();
                if (tagname == null)
                {
                    return false; // Something went wrong
                }

                string changeLog = data["body"]?.ToString();
                if (changeLog != null) announcement = changeLog;
                // check version
                System.Version ver = System.Version.Parse(tagname.Replace("v", ""));
                int diff = TheOtherRolesPlugin.Version.CompareTo(ver);
                if (diff < 0)
                { // Update required
                    hasUpdate = true;
                    announcement = string.Format(ModTranslation.getString("announcementUpdate"), ver, announcement);

                    JToken assets = data["assets"];
                    if (!assets.HasValues)
                        return false;

                    for (JToken current = assets.First; current != null; current = current.Next)
                    {
                        string browser_download_url = current["browser_download_url"]?.ToString();
                        if (browser_download_url != null && current["content_type"] != null)
                        {
                            if ((current["content_type"].ToString().Equals("application/x-msdownload")
                                || current["content_type"].ToString().Equals("application/x-dosexec"))
                                && browser_download_url.EndsWith(".dll"))
                            {
                                updateURI = browser_download_url;
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    announcement = string.Format(ModTranslation.getString("announcementChangelog"), ver, announcement);
                }
            }
            catch (System.Exception ex)
            {
                TheOtherRolesPlugin.Instance.Log.LogError(ex.ToString());
                System.Console.WriteLine(ex);
            }
            return false;
        }

        public static async Task<bool> downloadUpdate()
        {
            try
            {
                HttpClient http = new();
                http.DefaultRequestHeaders.Add("User-Agent", "TheOtherRoles Updater");
                var response = await http.GetAsync(new System.Uri(updateURI), HttpCompletionOption.ResponseContentRead);
                if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                {
                    System.Console.WriteLine("Server returned no data: " + response.StatusCode.ToString());
                    return false;
                }
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                System.UriBuilder uri = new(codeBase);
                string fullname = System.Uri.UnescapeDataString(uri.Path);
                if (File.Exists(fullname + ".old")) // Clear old file in case it wasnt;
                    File.Delete(fullname + ".old");

                File.Move(fullname, fullname + ".old"); // rename current executable to old

                using (var responseStream = await response.Content.ReadAsStreamAsync())
                {
                    using var fileStream = File.Create(fullname);
                    // probably want to have proper name here
                    responseStream.CopyTo(fileStream);
                }
                showPopup(ModTranslation.getString("updateRestart"));
                return true;
            }
            catch (System.Exception ex)
            {
                TheOtherRolesPlugin.Instance.Log.LogError(ex.ToString());
                System.Console.WriteLine(ex);
            }
            showPopup(ModTranslation.getString("updateFailed"));
            return false;
        }
        private static void showPopup(string message)
        {
            setPopupText(message);
            InfoPopup.gameObject.SetActive(true);
        }

        public static void setPopupText(string message)
        {
            if (InfoPopup == null)
                return;
            if (InfoPopup.TextAreaTMP != null)
            {
                InfoPopup.TextAreaTMP.text = message;
            }
        }
    }
}
