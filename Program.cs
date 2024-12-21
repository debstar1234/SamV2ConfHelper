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
    // Script to change / display SamV2 Conf values
    // SetUp: SAM Ship (obviously)

    partial class Program : MyGridProgram
    {
        // ME TAG
        string SAM_V2_CONF_HELPER_TAG = "[SamV2ConfHelper]";
        string SAM_V2_CONF_HELPER_CD = "SamV2ConfHelper";
        string SAM_VIEWER_LCD_NAME = "SAM_VIEWER_LCD_NAME";
        string SAM_RC_NAME = "SAM_RC_NAME";
        string SAM_PB_NAME = "SAM_PB_NAME";

        // SAM Values
        string SAM_TAG = "SAM";
        string SAM_CD_TAG = "SAM.";
        string SAM_ApproachDistance = "ApproachDistance";
        string SAM_MaxSpeed = "MaxSpeed";



        /*        SAM.ApproachDistance=100
        SAM.ApproachingSpeed=30
        SAM.DockDistance=10
        SAM.UndockDistance=10
        SAM.MaxSpeed=100
        SAM.ConvergingSpeed=30
        SAM.TaxiingDistance=20*/

        Dictionary<string, string> samCdValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        // SAM Values*
        samCdValues = new Dictionary<string, string>()
        {
            { "MaxSpeed",""  },
            { "ApproachDistance", "" },
            { "DockDistance", ""  },
            { "UndockDistance", "" },
            { "ConvergingSpeed", "" },
            { "TaxiingDistance", "" }
        };
 
            // Command Lists
            _commands["initHelperConfiguration"] = InitHelperConfiguration;
            _commands["toggleSamRc"] = ToggleSamRc;
            _commands["getSamPbCd"] = GetSamPbCd;

            // Value modifier
        foreach(var entry in samCdValues)
        {
            _commands[entry.Key] =  () => SetSamValue(entry.Key); 
        }
         


            // Helper CD
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            // Init 
            GetSamPbCd();

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
                    // We have found a command. Invoke it.
                    commandAction();
                }
                else
                {
                    Echo($"Unknown command {command}");
                }
            }
        }

        public Boolean Str_Equals(string s1, string s2)
        {
            return s1 != null && s2!=null && string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
        }

        public void InitHelperConfiguration()
        {
            Echo("InitHelperConfiguration"); 
            string arg = _commandLine.Argument(1);
            Me.CustomData = "";

            if (Str_Equals(arg, "auto")){
                GetTagOrCdBlocks();
            }

            string sb = "";
            sb += SAM_V2_CONF_HELPER_TAG + "\n";
            
            sb += SAM_VIEWER_LCD_NAME + "=" + (samConfHelperLcd != null ? samConfHelperLcd.CustomName : "") + "\n";
            sb += SAM_RC_NAME + "=" + (samRc != null ? samRc.CustomName : "") +  "\n";
            sb += SAM_PB_NAME + "=" + (samPb != null ? samPb.CustomName : "") + "\n";
            Me.CustomData = sb;

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
                        samLoglcd = block as IMyTextPanel;
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
      
            // RC stuff
            var remoteControllerName = _ini.Get(SAM_V2_CONF_HELPER_CD,  SAM_RC_NAME).ToString();
            var remoteController = GridTerminalSystem.GetBlockWithName(remoteControllerName) as IMyRemoteControl;
            remoteController.CustomData = (remoteController != null && toggle ? SAM_CD_TAG : "");

        }

        public void GetSamPbCd()
        {
            Echo("GetSamPbCd : ");

            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            var samPbName = _ini.Get(SAM_V2_CONF_HELPER_CD, SAM_PB_NAME).ToString();
            Echo($"SAM_PB_NAME : '{samPbName}' ");

            var samViewerLcdName = _ini.Get(SAM_V2_CONF_HELPER_CD, SAM_VIEWER_LCD_NAME).ToString();
            Echo($"SAM_VIEWER_LCD_NAME  '{samViewerLcdName}' ");

            samPb = GridTerminalSystem.GetBlockWithName(samPbName) as IMyProgrammableBlock;
            samConfHelperLcd = GridTerminalSystem.GetBlockWithName(samViewerLcdName) as IMyTextPanel;
           
            if (!_ini.TryParse(samPb.CustomData, out result))
                throw new Exception(result.ToString());

            var _textToOutput = "";
            var ApproachDistance = _ini.Get("SAM", "SAM.ApproachDistance").ToString();
            Echo($"ApproachDistance : '{ApproachDistance}' ");
            var MaxSpeed = _ini.Get("SAM", "SAM.MaxSpeed").ToString();
            Echo($"MaxSpeed : '{MaxSpeed}' ");

            foreach (var entry in samCdValues)
            {
                _commands[entry.Key] = () => SetSamValue(entry.Key);
                var samValue = _ini.Get(SAM_TAG, SAM_CD_TAG + entry.Key).ToString();
                _textToOutput += entry.Key + " : " + samValue  + " \n";
            }

            // Append the configured text to the text panel
            Echo("WriteText");
            samConfHelperLcd.WriteText(_textToOutput, false);
        }


        public void MaxSpeed()
        {
            SetSamValue("MaxSpeed");
        }

        public void SetSamValue(string key)
        {
            Echo("SetSamValue : " + key );
            string targetValue = _commandLine.Argument(1);
            Echo($"key : '{targetValue}' ");

            if (targetValue != null)
            {
                MyIniParseResult result;
                if (!_ini.TryParse(samPb.CustomData, out result))
                    throw new Exception(result.ToString());

                _ini.Set(SAM_TAG,SAM_CD_TAG + key, targetValue);
                samPb.CustomData = _ini.ToString();
                GetSamPbCd();
            }
        }



    }
}
