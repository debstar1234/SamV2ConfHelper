using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
 
    partial class Program : MyGridProgram
    {
        // ME TAG
        string SAM_V2_CONF_HELPER_TAG = "[SamV2ConfHelper]";
        string SAM_V2_CONF_HELPER_CD = "SamV2ConfHelper";
        string SAM_VIEWER_LCD_NAME = "SAM_VIEWER_LCD_NAME";
        string SAM_RC_NAME = "SAM_RC_NAME";
        string SAM_PB_NAME = "SAM_PB_NAME";
        string SAM_LOG_LCD_NAME = "SAM_LOG_LCD_NAME";

        // SAM Values
        string SAM_TAG  = "[SAM]";
        string SAM_TAG_NAME = "SAM";
        string SAM_CD_TAG = "SAM.";

        Dictionary<string, string> samCdValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      
>>>>>>>>> Temporary merge branch 2
        // Init
        MyCommandLine _commandLine = new MyCommandLine();
        Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        MyIni _ini = new MyIni();

        // SAM CONF HELPER 
        IMyTextPanel samConfHelperLcd = null ;

        // Sam BLocks
        IMyRemoteControl samRc = null;
        IMyTextPanel samLoglcd = null;
        IMyProgrammableBlock samPb = null;

        public Program()
        {
            // SAM Values : long name / short name
            samCdValues = new Dictionary<string, string>()
            {
                { "MaxSpeed","ms"  },
                { "ApproachDistance", "ad" },
                { "DockDistance", "dd"  },
                { "UndockDistance", "ud" },
                { "ConvergingSpeed", "cs" },
                { "TaxiingDistance", "td" },
                { "TaxiingPanelDistance", "tpd" }
             };

            // SAM Toggles : long name / short name
            samCdToggles = new Dictionary<string, string>()
            {
                { "NODAMPENERS", "nd" },
                { "IGNOREGRAVITY", "ig" }
            };

            // Command Lists
            _commands["initHelperConfiguration"] = InitHelperConfiguration;
            _commands["reloadBlockSources"] = ReloadBlockSources;
            _commands["rbs"] = ReloadBlockSources;

            _commands["toggleSamRc"] = ToggleSamRc;
            _commands["getSamPbCd"] = GetSamPbCd;
            _commands["loadDestinationProfile"] = LoadDestinationProfile;


            // Value modifier
            foreach (var entry in samCdValues)
            {
                _commands[entry.Key] =  () => SetSamValue(entry.Key);
                _commands[entry.Value] = () => SetSamValue(entry.Key);
            }
            // Value toggles
            foreach (var entry in samCdToggles)
            {
                _commands[entry.Key] = () => SetSamToggle(entry.Key);
                _commands[entry.Value] = () => SetSamToggle(entry.Key);
            }

            // Init
            if(Me.CustomData == "")
            {
                InitHelperConfiguration();
            }
            ReloadBlockSources();
 
  
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (_commandLine.TryParse(argument))
            {
                Action commandAction;
                string command = _commandLine.Argument(0);

                if (command == null) 
                {
                    Echo("No command specified");
                }
                else if (_commands.TryGetValue(command, out commandAction))
                {
                    commandAction();
                }
                else
                {
                    Echo($"Unknown command {command}");
                }
            }
        }

        public void InitHelperConfiguration()
        {
            Echo("InitHelperConfiguration");
             // GetTagOrCdBlocks();

            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            _ini.AddSection(SAM_V2_CONF_HELPER_CD);
            _ini.Set(SAM_V2_CONF_HELPER_CD, SAM_VIEWER_LCD_NAME, (samConfHelperLcd != null ? samConfHelperLcd.CustomName : ""));
            _ini.Set(SAM_V2_CONF_HELPER_CD, SAM_RC_NAME ,  (samRc != null ? samRc.CustomName : "") ) ;
            _ini.Set(SAM_V2_CONF_HELPER_CD, SAM_PB_NAME , (samPb != null ? samPb.CustomName : "") );
            _ini.Set(SAM_V2_CONF_HELPER_CD, SAM_LOG_LCD_NAME, (samLoglcd != null ? samLoglcd.CustomName : "") ) ;
            _ini.SetComment(SAM_V2_CONF_HELPER_CD, SAM_LOG_LCD_NAME,"End of Conf");

            Me.CustomData = _ini.ToString();

        }

        public  void GetTagOrCdBlocks()
        {
            Echo("GetSamBlocks : ");
            // Get the grid this programmable block is on
            var grid = Me.CubeGrid;

            // Get all blocks in the programmable block's grid
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block.CubeGrid == grid);


            // Iterate through all blocks and find the ones with the specified tag
            foreach (var block in blocks)
            {
                if (isTagOrCdlockOf(block,SAM_TAG,SAM_CD_TAG))
                {
                    if (block is IMyRemoteControl)
                    {
                        samRc = block as IMyRemoteControl;
                    }
                    else if (block is IMyTextPanel)
                    {
                        if (block.CustomName.Contains("LOG") || block.CustomData.Contains("SAM.LOG")) ;
                        {
                            samLoglcd = block as IMyTextPanel;
                        }
                       
                    }
                    else if (block is IMyProgrammableBlock)
                    {
                        samPb = block as IMyProgrammableBlock;
                    }
                }
                if (isTagOrCdlockOf(block, SAM_V2_CONF_HELPER_TAG, SAM_V2_CONF_HELPER_CD))
                {
                    if (block is IMyTextPanel)
                    {
                        samConfHelperLcd = block as IMyTextPanel;
                    }
                }
            }
        }

        public Boolean isTagOrCdlockOf(IMyTerminalBlock block, string tag, string cd)
        {
            return block != null && block.CustomName.Contains(tag) ||(block.CustomData != null &&  block.CustomData.Contains(cd));
        }

        public void ToggleSamRc()
        {
            // Command
            string status = _commandLine.Argument(1);
            Boolean toggle = Str_Equals(status, "on");

            // CD parse
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            // RC stuff
            var remoteControllerName = _ini.Get(SAM_V2_CONF_HELPER_CD, SAM_RC_NAME).ToString();
            var remoteController = GridTerminalSystem.GetBlockWithName(remoteControllerName) as IMyRemoteControl;
            remoteController.CustomData = (remoteController != null && toggle ? SAM_CD_TAG : "");
        }

        public void GetSamPbCd()
        {
            Echo("GetSamPbCd : ");

            if(samPb == null || samConfHelperLcd == null || samLoglcd == null )
                ReloadBlockSources();

            _ini.Clear();
            MyIniParseResult result;
            if (!_ini.TryParse(samPb.CustomData, out result))
                throw new Exception(result.ToString());

            var _textToOutput = "";
            foreach (var entry in samCdValues)
            {
                var samValue = _ini.Get(SAM_TAG_NAME, SAM_CD_TAG + entry.Key).ToString();
                _textToOutput += entry.Key + " : " + samValue + " \n";
            }

            // Destination
            _textToOutput += "Going to : " + GetDestination();

            samConfHelperLcd.WriteText(_textToOutput, false);
        }

        private void ReloadBlockSources()
        {
            MyIniParseResult result;
    
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            var samPbName = _ini.Get(SAM_V2_CONF_HELPER_CD, SAM_PB_NAME).ToString();
            var samViewerLcdName = _ini.Get(SAM_V2_CONF_HELPER_CD, SAM_VIEWER_LCD_NAME).ToString();
            var samLogLcdName = _ini.Get(SAM_V2_CONF_HELPER_CD, SAM_LOG_LCD_NAME).ToString();

            samPb = GridTerminalSystem.GetBlockWithName(samPbName) as IMyProgrammableBlock;
            samConfHelperLcd = GridTerminalSystem.GetBlockWithName(samViewerLcdName) as IMyTextPanel;
            samLoglcd = GridTerminalSystem.GetBlockWithName(samLogLcdName) as IMyTextPanel;

            if (samPb == null) Echo("No SAM PB found");
            if (samConfHelperLcd == null) Echo("No SAM CONF HELPER LCD found");
            if (samLoglcd == null) Echo("No SAM LOG LCD found");

        }

        public void SetSamValue(string key)
        {
            string targetValue = _commandLine.Argument(1);
            if (targetValue != null)
            {
                SetSamTargetValue(SAM_CD_TAG + key, targetValue);
            }
        }

        private void SetSamTargetValue(string key, string targetValue)
        {
            if (targetValue != null)
            {
                MyIniParseResult result;
                if (!_ini.TryParse(samPb.CustomData, out result))
                    throw new Exception(result.ToString());
                _ini.Set(SAM_TAG_NAME,  key, targetValue);
                samPb.CustomData = _ini.ToString();
                GetSamPbCd();
            }
        }

        public void SetSamToggle(string key)
>>>>>>>>> Temporary merge branch 2
        public void SetSamValue(string key)
        {
            string targetValue = _commandLine.Argument(1);
            SetSamToggleValue(key, targetValue);
        }

        private void SetSamToggleValue(string key, string targetValue)
        {
            if (targetValue != null)
            {
                Echo("SetSamToggleValue : " + key + " to " + targetValue );
                Boolean toggle = Str_Equals("on", targetValue);
                MyIniParseResult result;
                if (!_ini.TryParse(samPb.CustomData, out result))
                    throw new Exception(result.ToString());
                if (toggle)
                {
                    _ini.Set(SAM_TAG, SAM_CD_TAG + key, targetValue);
                }
                else
                {
                    _ini.Delete(SAM_TAG, SAM_CD_TAG + key);
                }
                samPb.CustomData = _ini.ToString();
                GetSamPbCd();
            }
        }

        public string GetDestination()
        {
            if (samLoglcd == null)
                return ""; 

            string lcdText = samLoglcd.GetText();
            string[] lines = lcdText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string searchText = "I: Navigating to ";
            string destination = null ;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(searchText))
                {
                    destination = lines[i].Replace(searchText, "");
                    break;
                }
            }
            return destination;
        }

        public void LoadDestinationProfile()
        {
            Echo("LoadDestinationProfile");
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            var destination = GetDestination();
            if (destination == null )
            {
                Echo("No destination found");
                return;
            }
            Echo("Destination found : " + destination );
            
            // search Profile
            List<string> sectionList = new List<string>();
            _ini.GetSections(sectionList);

            foreach(var section in sectionList)
            {
                if (destination.Contains(section))
                {
                    Echo("Destination profile found : " + destination);
                    List<MyIniKey> profileKeys = new List<MyIniKey>();
                    _ini.GetKeys(section, profileKeys);
                    var profileSamValues = new Dictionary<string, string>();
                    foreach (var myIniKey in profileKeys)
                    {
                        Echo("myIniKey profile found : " + myIniKey.Name);
                        profileSamValues.Add(myIniKey.Name, _ini.Get(section, myIniKey.Name).ToString() );
                    }
                    foreach(var cdValue in profileSamValues)
                    {
                        Echo("profileSamValues cdValue found : " + cdValue.Key + " for " + cdValue.Value);
                        SetSamTargetValue(cdValue.Key, cdValue.Value);
                    }
                }
            }
  
        }

 ///

        public Boolean Str_Equals(string s1, string s2)
        {
            return s1 != null && s2 != null && string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
        }


        class SamConfiguration
        {
            public void update()
            {

            }

            public void read()
            {

            }

            public void write()
            {
                
            }

        }

        class ConnectorConfiguration : SamConfiguration
        {
            string name { get; set; }
        }
    }
}
