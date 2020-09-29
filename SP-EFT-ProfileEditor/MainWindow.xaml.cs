﻿using System;
using System.Collections.Generic;
using System.IO;
using MahApps.Metro.Controls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Newtonsoft.Json;
using MahApps.Metro.Controls.Dialogs;
using ControlzEx.Theming;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Diagnostics;

namespace SP_EFT_ProfileEditor
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainData Lang = new MainData();
        private BackgroundWorker LoadDataWorker;
        private ProgressDialogController progressDialog;
        private string lastdata = null;
        private List<BackupFile> backups;
        private List<SkillInfo> commonSkills;
        private List<CharacterHideoutArea> HideoutAreas;
        private GlobalLang globalLang;

        private Dictionary<string, Item> itemsDB;
        private static string moneyRub = "5449016a4bdc2d6f028b456f";
        private static string moneyDol = "5696686a4bdc2da3298b456a";
        private static string moneyEur = "569668774bdc2da2298b4568";

        private Dictionary<string, string> Langs = new Dictionary<string, string>
        {
            ["en"] = "English",
            ["ru"] = "Русский",
            ["fr"] = "Français",
            ["ge"] = "Deutsch "
        };

        private FolderBrowserDialog folderBrowserDialogSPT;
        
        //https://dev.offline-tarkov.com/sp-tarkov/server/src/branch/development/project/core/util/utility.js - generate id's

        public MainWindow()
        {
            InitializeComponent();
            infotab_Side.ItemsSource = new List<string> { "Bear", "Usec" };
            QuestsStatusesBox.ItemsSource = new List<string> { "Locked", "AvailableForStart", "Started", "Fail", "AvailableForFinish", "Success" };
            QuestsStatusesBox.SelectedIndex = 0;
            LoadDataWorker = new BackgroundWorker();
            LoadDataWorker.DoWork += LoadDataWorker_DoWork;
            LoadDataWorker.RunWorkerCompleted += LoadDataWorker_RunWorkerCompleted;
        }

        private void LoadDataWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //
            if (HideoutAreas != null)
                hideoutGrid.ItemsSource = HideoutAreas;
            if (commonSkills != null)
                skillsGrid.ItemsSource = commonSkills;
            if (backups != null)
                backupsGrid.ItemsSource = backups;
            progressDialog.CloseAsync();
        }

        private void LoadDataWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            globalLang = JsonConvert.DeserializeObject<GlobalLang>(File.ReadAllText(Path.Combine(Lang.options.EftServerPath, "db", "locales", "global_" + Lang.options.Language + ".json")));
            //
            HideoutAreas = new List<CharacterHideoutArea>();
            var areas = JsonConvert.DeserializeObject<List<AreaInfo>>(File.ReadAllText(Path.Combine(Lang.options.EftServerPath, "db", "hideout", "areas.json")));
            foreach (var area in areas)
                HideoutAreas.Add(new CharacterHideoutArea { type = area.type, name = globalLang.Interface[$"hideout_area_{area.type}_name"], MaxLevel = area.stages.Count - 1, CurrentLevel = Lang.Character.Hideout.Areas.Where(x => x.Type == area.type).FirstOrDefault().Level });
            commonSkills = new List<SkillInfo>();
            foreach (var skill in Lang.Character.Skills.Common)
            {
                if (globalLang.Interface.ContainsKey(skill.Id))
                    commonSkills.Add(new SkillInfo { progress = (int)skill.Progress, name = globalLang.Interface[skill.Id], id = skill.Id });
            }
            LoadBackups();
            itemsDB = new Dictionary<string, Item>();
            itemsDB = JsonConvert.DeserializeObject<Dictionary<string, Item>>(File.ReadAllText(Path.Combine(Lang.options.EftServerPath, "db", "templates", "items.json")));
            GenerateInventory();
        }

        private void LoadBackups()
        {
            backups = new List<BackupFile>();
            foreach (var bk in Directory.GetFiles(Path.Combine(Lang.options.EftServerPath, "user\\profiles", Lang.options.DefaultProfile)).Where(x => x.Contains("backup")).OrderByDescending(i => i))
            {
                try { backups.Add(new BackupFile { Path = bk, date = DateTime.ParseExact(Path.GetFileNameWithoutExtension(bk).Remove(0, 17), "dd-MM-yyyy-HH-mm", CultureInfo.InvariantCulture, DateTimeStyles.None).ToString("dd.MM.yyyy HH:mm") }); }
                catch (Exception ex)
                { ExtMethods.Log($"LoadBackups | {ex.GetType().Name}: {ex.Message}"); }
            }
            if (!LoadDataWorker.IsBusy)
            {
                backupsGrid.ItemsSource = null;
                backupsGrid.ItemsSource = backups;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bool readyToLoad = false;
            Lang = MainData.Load();
            if (string.IsNullOrWhiteSpace(Lang.options.Language) || string.IsNullOrWhiteSpace(Lang.options.EftServerPath)
                || !Directory.Exists(Lang.options.EftServerPath) || !ExtMethods.PathIsEftServerBase(Lang.options.EftServerPath)
                || string.IsNullOrWhiteSpace(Lang.options.DefaultProfile) || !Directory.Exists(Path.Combine(Lang.options.EftServerPath, "user\\profiles", Lang.options.DefaultProfile)) || !File.Exists(Path.Combine(Lang.options.EftServerPath, "user\\profiles", Lang.options.DefaultProfile, "character.json")))
                SettingsBorder.Visibility = Visibility.Visible;
            else
                readyToLoad = true;
            langSelectBox.ItemsSource = Langs;
            langSelectBox.SelectedItem = new KeyValuePair<string, string>(Lang.options.Language, Langs[Lang.options.Language]);
            if (!string.IsNullOrEmpty(Lang.options.ColorScheme))
            {
                ThemeManager.Current.ChangeTheme(this, Lang.options.ColorScheme);
            }
            foreach (var style in ThemeManager.Current.Themes.OrderBy(x => x.DisplayName))
            {
                var newItem = new AccentItem { Name = style.DisplayName, Color = style.PrimaryAccentColor.ToString(), Scheme = style.Name };
                StyleChoicer.Items.Add(newItem);
                if (ThemeManager.Current.DetectTheme(this).DisplayName == newItem.Name) StyleChoicer.SelectedItem = newItem;
            }
            DataContext = Lang;
            CheckPockets();
            if (readyToLoad)
            {
                lastdata = Lang.options.EftServerPath + Lang.options.Language + Lang.Character.Aid;
                LoadData();
            }
        }

        private async void LoadData()
        {
            progressDialog = await this.ShowProgressAsync(Lang.locale["progressdialog_title"], Lang.locale["progressdialog_caption"]);
            progressDialog.SetIndeterminate();
            LoadDataWorker.RunWorkerAsync();
        }

        private void langSelectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;
            Lang.options.Language = langSelectBox.SelectedValue.ToString();
            SaveAndReload();
        }

        private void SaveAndReload()
        {
            Lang.SaveOptions();
            Lang = MainData.Load();
            DataContext = Lang;
            CheckPockets();
        }

        private void CheckPockets()
        {
            if (Lang.Character == null) return;
            if (Lang.Character.Inventory.Items.Where(x => x.Tpl == "557ffd194bdc2d28148b457f").Count() > 0) 
                BigPocketsSwitcher.IsOn = false;
            if (Lang.Character.Inventory.Items.Where(x => x.Tpl == "5af99e9186f7747c447120b8").Count() > 0)
                BigPocketsSwitcher.IsOn = true;
        }

        private async void serverSelect_Click(object sender, RoutedEventArgs e)
        {
            folderBrowserDialogSPT = new FolderBrowserDialog
            {
                Description = Lang.locale["server_select"],
                RootFolder = Environment.SpecialFolder.MyComputer,
                ShowNewFolderButton = false
            };
            bool pathOK = false;
            do
            {
                if (!string.IsNullOrWhiteSpace(Lang.options.EftServerPath) && Directory.Exists(Lang.options.EftServerPath))
                    folderBrowserDialogSPT.SelectedPath = Lang.options.EftServerPath;
                if (folderBrowserDialogSPT.ShowDialog() != System.Windows.Forms.DialogResult.OK) pathOK = false;
                if (ExtMethods.PathIsEftServerBase(folderBrowserDialogSPT.SelectedPath)) pathOK = true;
            } while (
                !pathOK &&
                (await this.ShowMessageAsync(Lang.locale["invalid_server_location_caption"], Lang.locale["invalid_server_location_text"], MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { DefaultButtonFocus = MessageDialogResult.Affirmative, AffirmativeButtonText = Lang.locale["button_yes"], NegativeButtonText = Lang.locale["button_no"] }) == MessageDialogResult.Affirmative)
            );

            if (pathOK)
            {
                Lang.options.EftServerPath = folderBrowserDialogSPT.SelectedPath;
                SaveAndReload();
            }
        }

        private void profileSelectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;
            Lang.options.DefaultProfile = profileSelectBox.SelectedValue.ToString();
            SaveAndReload();
        }

        private void StyleChoicer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;
            var selectedAccent = StyleChoicer.SelectedItem as AccentItem;
            if (selectedAccent.Name == ThemeManager.Current.DetectTheme(this).DisplayName) return;
            ThemeManager.Current.ChangeTheme(this, selectedAccent.Scheme);
            Lang.options.ColorScheme = selectedAccent.Scheme;
            SaveAndReload();
        }

        private void TabSettingsClose_Click(object sender, RoutedEventArgs e)
        {
            SettingsBorder.Visibility = Visibility.Collapsed;
            if (lastdata != Lang.options.EftServerPath + Lang.options.Language + Lang.Character.Aid)
            {
                LoadData();
                lastdata = Lang.options.EftServerPath + Lang.options.Language + Lang.Character.Aid;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e) => SettingsBorder.Visibility = Visibility.Visible;

        private void infotab_Side_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;
            if (!infotab_Voice.Items.Contains(infotab_Voice.SelectedItem))
                infotab_Voice.SelectedIndex = 0;
        }

        private void infotab_Level_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            Lang.Character.Info.Level = Convert.ToInt32(textBox.Text);
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {/*
            if (!IsLoaded)
                return;
            var comboBox = sender as System.Windows.Controls.ComboBox;
            Lang.Character.Quests.Where(x => x.Qid == ((Quest)comboBox.DataContext).qid).FirstOrDefault().Status = comboBox.SelectedItem.ToString();*/
        }

        private void merchantLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {/*
            if (!IsLoaded)
                return;
            var comboBox = sender as System.Windows.Controls.ComboBox;
            LoyaltyLevel level = (LoyaltyLevel)comboBox.SelectedItem;
            Lang.Character.TraderStandings[((TraderInfo)comboBox.DataContext).id].CurrentLevel = level.level;
            if (Lang.Character.TraderStandings[((TraderInfo)comboBox.DataContext).id].CurrentSalesSum < level.SalesSum) Lang.Character.TraderStandings[((TraderInfo)comboBox.DataContext).id].CurrentSalesSum = level.SalesSum;
            if (Lang.Character.TraderStandings[((TraderInfo)comboBox.DataContext).id].CurrentStanding < level.Standing) Lang.Character.TraderStandings[((TraderInfo)comboBox.DataContext).id].CurrentStanding = level.Standing;*/
        }

        private void hideoutarea_Level_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;
            var slider = sender as Slider;
            Lang.Character.Hideout.Areas.Where(x => x.Type == ((CharacterHideoutArea)slider.DataContext).type).FirstOrDefault().Level = (int)slider.Value;
        }

        private void commonskill_exp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;
            var slider = sender as Slider;
            Lang.Character.Skills.Common.Where(x => x.Id == ((SkillInfo)slider.DataContext).id).FirstOrDefault().Progress = (float)slider.Value;
        }

        private void masteringskill_exp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {/*
            if (!IsLoaded)
                return;
            var slider = sender as Slider;
            Lang.Character.Skills.Mastering.Where(x => x.Id == ((SkillInfo)slider.DataContext).id).FirstOrDefault().Progress = (int)slider.Value;*/
        }

        private void BigPocketsSwitcher_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;
            if (Lang.Character.Inventory.Items == null) return;
            if (Lang.Character.Inventory.Items.Where(x => x.Tpl == "557ffd194bdc2d28148b457f" || x.Tpl == "5af99e9186f7747c447120b8").Count() > 0)
                Lang.Character.Inventory.Items.Where(x => x.Tpl == "557ffd194bdc2d28148b457f" || x.Tpl == "5af99e9186f7747c447120b8").FirstOrDefault().Tpl = BigPocketsSwitcher.IsOn ? "5af99e9186f7747c447120b8" : "557ffd194bdc2d28148b457f";
        }

        private void QuestsStatusesButton_Click(object sender, RoutedEventArgs e)
        {/*
            if (Lang.Character.Quests == null) return;
            foreach (var q in Lang.Character.Quests)
                q.Status = QuestsStatusesBox.SelectedItem.ToString();
            LoadData();*/
        }

        private async void ResetProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (await this.ShowMessageAsync(Lang.locale["reloadprofiledialog_title"], Lang.locale["reloadprofiledialog_caption"], MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { DefaultButtonFocus = MessageDialogResult.Affirmative, AffirmativeButtonText = Lang.locale["button_yes"], NegativeButtonText = Lang.locale["button_no"] }) == MessageDialogResult.Affirmative)
            {
                SaveAndReload();
                LoadData();
            }
        }

        private void HideoutMaximumButton_Click(object sender, RoutedEventArgs e)
        {
            if (HideoutAreas == null) return;
            foreach (var a in Lang.Character.Hideout.Areas)
                a.Level = HideoutAreas.Where(x => x.type == a.Type).FirstOrDefault().MaxLevel;
            LoadData();
        }

        private void SkillsExpButton_Click(object sender, RoutedEventArgs e)
        {
            if (commonSkills == null) return;
            foreach (var exp in Lang.Character.Skills.Common)
                exp.Progress = (float)allskill_exp.Value;
            LoadData();
        }

        private void MasteringsExpButton_Click(object sender, RoutedEventArgs e)
        {/*
            if (masteringSkills == null) return;
            foreach (var ms in Lang.Character.Skills.Mastering)
                ms.Progress = (int)allmastering_exp.Value;
            LoadData();*/
        }

        private void MerchantsMaximumButton_Click(object sender, RoutedEventArgs e)
        {/*
            if (traderInfos == null) return;
            foreach (var tr in Lang.Character.TraderStandings)
            {
                var max = tr.Value.LoyaltyLevels.Last();
                tr.Value.CurrentLevel = Int32.Parse(max.Key) + 1;
                if (tr.Value.CurrentSalesSum < max.Value.MinSalesSum + 1000) tr.Value.CurrentSalesSum = max.Value.MinSalesSum + 1000;
                if (tr.Value.CurrentStanding < max.Value.MinStanding + 0.01f) tr.Value.CurrentStanding = max.Value.MinStanding + 0.01f;
            }
            LoadData();*/
        }

        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            JsonSerializerSettings seriSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            };
            try
            {
                string profilepath = Path.Combine(Lang.options.EftServerPath, "user\\profiles", Lang.options.DefaultProfile, "character.json");
                JObject jobject = JObject.Parse(File.ReadAllText(profilepath));
                jobject.SelectToken("Info")["Nickname"] = Lang.Character.Info.Nickname;
                jobject.SelectToken("Info")["LowerNickname"] = Lang.Character.Info.Nickname.ToLower();
                jobject.SelectToken("Info")["Side"] = Lang.Character.Info.Side;
                jobject.SelectToken("Info")["Voice"] = Lang.Character.Info.Voice;
                jobject.SelectToken("Info")["Level"] = Lang.Character.Info.Level;
                jobject.SelectToken("Info")["Experience"] = Lang.Character.Info.Experience;
                for (int index = 0; index < Lang.Character.Inventory.Items.Count(); ++index)
                {
                    var probe = jobject.SelectToken("Inventory").SelectToken("items")[index].ToObject<Character.Character_Inventory.Character_Inventory_Item>();
                    if (probe.Tpl == "557ffd194bdc2d28148b457f" || probe.Tpl == "5af99e9186f7747c447120b8")
                    {
                        jobject.SelectToken("Inventory").SelectToken("items")[index]["_tpl"] = BigPocketsSwitcher.IsOn ? "5af99e9186f7747c447120b8" : "557ffd194bdc2d28148b457f";
                    }
                }
                for (int index = 0; index < Lang.Character.Skills.Common.Count(); ++index)
                {
                    var probe = jobject.SelectToken("Skills").SelectToken("Common")[index].ToObject<Character.Character_Skills.Character_Skill>();
                    jobject.SelectToken("Skills").SelectToken("Common")[index]["Progress"] = Lang.Character.Skills.Common.Where(x => x.Id == probe.Id).FirstOrDefault().Progress;
                }
                for (int index = 0; index < Lang.Character.Hideout.Areas.Count(); ++index)
                {
                    var probe = jobject.SelectToken("Hideout").SelectToken("Areas")[index].ToObject<Character.Character_Hideout_Areas>();
                    jobject.SelectToken("Hideout").SelectToken("Areas")[index]["level"] = Lang.Character.Hideout.Areas.Where(x => x.Type == probe.Type).FirstOrDefault().Level;
                }
                /*
                foreach (var tr in Lang.Character.TraderStandings)
                {
                    jobject.SelectToken("TraderStandings").SelectToken(tr.Key)["currentLevel"] = Lang.Character.TraderStandings[tr.Key].CurrentLevel;
                    jobject.SelectToken("TraderStandings").SelectToken(tr.Key)["currentSalesSum"] = Lang.Character.TraderStandings[tr.Key].CurrentSalesSum;
                    jobject.SelectToken("TraderStandings").SelectToken(tr.Key)["currentStanding"] = Lang.Character.TraderStandings[tr.Key].CurrentStanding;
                    if (Lang.Character.TraderStandings[tr.Key].CurrentLevel > 1)
                        jobject.SelectToken("TraderStandings").SelectToken(tr.Key)["display"] = true;
                }
                if (Lang.Character.Quests.Count() > 0)
                {
                    for (int index = 0; index < Lang.Character.Quests.Count(); ++index)
                    {
                        var probe = jobject.SelectToken("Quests")[index].ToObject<Character.Character_Quests>();
                        jobject.SelectToken("Quests")[index]["status"] = Lang.Character.Quests.Where(x => x.Qid == probe.Qid).FirstOrDefault().Status;
                    }
                }
                
                
                for (int index = 0; index < Lang.Character.Skills.Mastering.Count(); ++index)
                {
                    var probe = jobject.SelectToken("Skills").SelectToken("Mastering")[index].ToObject<Character.Character_Skills.Character_Skill>();
                    jobject.SelectToken("Skills").SelectToken("Mastering")[index]["Progress"] = Lang.Character.Skills.Mastering.Where(x => x.Id == probe.Id).FirstOrDefault().Progress;
                }*/
                DateTime now = DateTime.Now;
                string backupfile = $"{profilepath.Replace("character.json", "character")}-backup-{now:dd-MM-yyyy-HH-mm}.json";
                File.Copy(profilepath, backupfile, true);
                string json = JsonConvert.SerializeObject(jobject, seriSettings);
                File.WriteAllText(profilepath, json);
                await this.ShowMessageAsync(Lang.locale["saveprofiledialog_title"], Lang.locale["saveprofiledialog_caption"], MessageDialogStyle.Affirmative, new MetroDialogSettings { AffirmativeButtonText = Lang.locale["saveprofiledialog_ok"] });
            }
            catch (Exception ex)
            {
                ExtMethods.Log($"SaveProfileButton_Click | {ex.GetType().Name}: {ex.Message}");
                await this.ShowMessageAsync(Lang.locale["invalid_server_location_caption"], $"{ex.GetType().Name}: {ex.Message}", MessageDialogStyle.Affirmative, new MetroDialogSettings { AffirmativeButtonText = Lang.locale["saveprofiledialog_ok"] });
            }
            LoadBackups();
        }

        private async void backupRemove_Click(object sender, RoutedEventArgs e)
        {
            if (await this.ShowMessageAsync(Lang.locale["removebackupdialog_title"], Lang.locale["removebackupdialog_caption"], MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { DefaultButtonFocus = MessageDialogResult.Affirmative, AffirmativeButtonText = Lang.locale["button_yes"], NegativeButtonText = Lang.locale["button_no"] }) == MessageDialogResult.Affirmative)
            {
                try
                {
                    var button = sender as System.Windows.Controls.Button;
                    File.Delete(((BackupFile)button.DataContext).Path);
                }
                catch (Exception ex)
                {
                    ExtMethods.Log($"backupRemove_Click | {ex.GetType().Name}: {ex.Message}");
                    await this.ShowMessageAsync(Lang.locale["invalid_server_location_caption"], $"{ex.GetType().Name}: {ex.Message}", MessageDialogStyle.Affirmative, new MetroDialogSettings { AffirmativeButtonText = Lang.locale["saveprofiledialog_ok"] });
                }                
                LoadBackups();
            }
        }

        private async void backupRestore_Click(object sender, RoutedEventArgs e)
        {
            if (await this.ShowMessageAsync(Lang.locale["restorebackupdialog_title"], Lang.locale["restorebackupdialog_caption"], MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { DefaultButtonFocus = MessageDialogResult.Affirmative, AffirmativeButtonText = Lang.locale["button_yes"], NegativeButtonText = Lang.locale["button_no"] }) == MessageDialogResult.Affirmative)
            {
                try
                {
                    var button = sender as System.Windows.Controls.Button;
                    File.Copy(((BackupFile)button.DataContext).Path, Path.Combine(Lang.options.EftServerPath, "user\\profiles", Lang.options.DefaultProfile, "character.json"), true);
                    File.Delete(((BackupFile)button.DataContext).Path);
                }
                catch (Exception ex)
                {
                    ExtMethods.Log($"backupRestore_Click | {ex.GetType().Name}: {ex.Message}");
                    await this.ShowMessageAsync(Lang.locale["invalid_server_location_caption"], $"{ex.GetType().Name}: {ex.Message}", MessageDialogStyle.Affirmative, new MetroDialogSettings { AffirmativeButtonText = Lang.locale["saveprofiledialog_ok"] });
                } 
                SaveAndReload();
                LoadData();
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void GenerateInventory()
        {
            CharacterInventory characterInventory = new CharacterInventory();
            var ProfileStash = Lang.Character.Inventory.Items.Where(x => x.Id == Lang.Character.Inventory.Stash).FirstOrDefault();
            var stashInfo = itemsDB[ProfileStash.Tpl].props.Grids.First();
            //Debug.Print($"profile stash size is {stashInfo.gridProps.cellsH} X {stashInfo.gridProps.cellsV}");
            characterInventory.Stash = new int[stashInfo.gridProps.cellsV, stashInfo.gridProps.cellsH];
            foreach (var item in Lang.Character.Inventory.Items)
            {
                if (item.Tpl == moneyRub) characterInventory.Rubles += item.Upd.StackObjectsCount ?? 0;
                if (item.Tpl == moneyEur) characterInventory.Euros += item.Upd.StackObjectsCount ?? 0;
                if (item.Tpl == moneyDol) characterInventory.Dollars += item.Upd.StackObjectsCount ?? 0;
                if (item.Location == null || item.ParentId != Lang.Character.Inventory.Stash) continue;
                var tmpSize = GetTmpSize(itemsDB[item.Tpl], item);
                var iW = tmpSize.Key; // x
                var iH = tmpSize.Value; // y
                var fH = item.Location.R == "Vertical" ? iW : iH;
                var fW = item.Location.R == "Vertical" ? iH : iW;
                //var fillTo = item.Location.X + fW;
                for (int i2 = item.Location.Y; i2 < fH + item.Location.Y; i2++)
                    for (int i = item.Location.X; i < fW + item.Location.X; i++)
                        characterInventory.Stash[i2, i] = 1;
            }
            int freeSlots = 0;
            foreach (var slot in characterInventory.Stash)
                if (slot == 0) freeSlots++;
            Debug.Print($"we have {freeSlots} free slots in stash");
            Debug.Print($"we have {characterInventory.Rubles} rubles");
            Debug.Print($"we have {characterInventory.Euros} euros");
            Debug.Print($"we have {characterInventory.Dollars} dollars");
            using (var sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "/inventory.txt"))
            {
                for (int i = 0; i < 68; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        sw.Write(characterInventory.Stash[i, j]);
                    }
                    sw.Write("\n");
                }
                sw.Flush();
                sw.Close();
            }
        }

        //return int[vert, horiz]
        private KeyValuePair<int, int> GetTmpSize(Item tmpitem, Character.Character_Inventory.Character_Inventory_Item rootitem)
        {
            var FoldableWeapon = tmpitem.props.Foldable;
            var FoldedSlot = tmpitem.props.FoldedSlot;

            int SizeUp = 0;
            int SizeDown = 0;
            int SizeLeft = 0;
            int SizeRight = 0;

            int ForcedUp = 0;
            int ForcedDown = 0;
            int ForcedLeft = 0;
            int ForcedRight = 0;
            int outX = tmpitem.props.Width;
            int outY = tmpitem.props.Height;

            bool rootFolded = (rootitem.Upd != null) && (rootitem.Upd.Foldable != null) && (rootitem.Upd.Foldable.Folded);
            if (FoldableWeapon && string.IsNullOrEmpty(FoldedSlot) && rootFolded)
                outX -= tmpitem.props.SizeReduceRight;

            if (tmpitem.parent != "5448e53e4bdc2d60728b4567" && tmpitem.parent != "566168634bdc2d144c8b456c" && tmpitem.parent != "5795f317245977243854e041")
            {
                foreach (var item in Lang.Character.Inventory.Items.Where(x => x.ParentId == tmpitem.id))
                {
                    if (!item.SlotId.Contains("mod_")) continue;
                    var itm = itemsDB[item.Tpl];
                    bool childFoldable = itm.props.Foldable;
                    bool childFolded = (item.Upd != null) && (item.Upd.Foldable != null) && item.Upd.Foldable.Folded;
                    if (FoldableWeapon && FoldedSlot == item.SlotId && (rootFolded || childFolded))
                        continue;
                    else if (childFoldable && rootFolded && childFolded)
                        continue;
                    // Calculating child ExtraSize
                    if (itm.props.ExtraSizeForceAdd)
                    {
                        ForcedUp += itm.props.ExtraSizeUp;
                        ForcedDown += itm.props.ExtraSizeDown;
                        ForcedLeft += itm.props.ExtraSizeLeft;
                        ForcedRight += itm.props.ExtraSizeRight;
                    }
                    else
                    {
                        SizeUp = (SizeUp < itm.props.ExtraSizeUp) ? itm.props.ExtraSizeUp : SizeUp;
                        SizeDown = (SizeDown < itm.props.ExtraSizeDown) ? itm.props.ExtraSizeDown : SizeDown;
                        SizeLeft = (SizeLeft < itm.props.ExtraSizeLeft) ? itm.props.ExtraSizeLeft : SizeLeft;
                        SizeRight = (SizeRight < itm.props.ExtraSizeRight) ? itm.props.ExtraSizeRight : SizeRight;
                    }
                }
            }
            var Vert = outY + SizeUp + SizeDown + ForcedUp + ForcedDown;
            var Horiz = outX + SizeLeft + SizeRight + ForcedLeft + ForcedRight;
            //return new int[outX + SizeLeft + SizeRight + ForcedLeft + ForcedRight, outY + SizeUp + SizeDown + ForcedUp + ForcedDown];
            return new KeyValuePair<int, int>(Vert, Horiz);
        }
    }
}