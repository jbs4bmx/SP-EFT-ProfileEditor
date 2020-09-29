﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SP_EFT_ProfileEditor
{
    public class BackupFile
    {
        public string Path { get; set; }
        public string date { get; set; }
    }

    public class AccentItem
    {
        public string Name { get; set; }
        public string Color { get; set; }
        public string Scheme { get; set; }
    }

    public class GlobalLang
    {
        [JsonProperty("interface")]
        public Dictionary<string, string> Interface { get; set; }
    }

    public class SkillInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public int progress { get; set; }
    }

    public class CharacterHideoutArea
    {
        public int type { get; set; }
        public string name { get; set; }
        public int MaxLevel { get; set; }
        public int CurrentLevel { get; set; }
    }

    public class AreaInfo
    {
        public int type { get; set; }
        public Dictionary<string, object> stages { get; set; }
    }
    /*
    public class Quest
    {
        public string qid { get; set; }
        public string trader { get; set; }
        public string name { get; set; }
        public string status { get; set; }
    }

    public class TraderLocale
    {
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string Nickname { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
    }

    public class QuestLocale
    {
        public string name { get; set; }
        public string description { get; set; }
        public string location { get; set; }
    }
    
    
    
    
    
    public class TraderInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public int CurrentLevel { get; set; }

        public List<LoyaltyLevel> Levels { get; set; }
    }

    public class LoyaltyLevel
    {
        public int level { get; set; }
        public long SalesSum { get; set; }
        public float Standing { get; set; }
    }

    public class CharacterInventory
    {
        public int[,] Stash { get; set; }
        public int Rubles { get; set; }
        public int Euros { get; set; }
        public int Dollars { get; set; }
    }

    public class Item
    {
        [JsonProperty("_id")]
        public string id { get; set; }

        [JsonProperty("_name")]
        public string name { get; set; }

        [JsonProperty("_props")]
        public ItemProps props { get; set; }
    }

    public class ItemProps
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int StackMaxSize { get; set; }
        public int ExtraSizeLeft { get; set; }
        public int ExtraSizeRight { get; set; }
        public int ExtraSizeUp { get; set; }
        public int ExtraSizeDown { get; set; }
    }*/
}