using EmptyKeys.UserInterface.Generated.StoreBlockView_Bindings;
using Microsoft.Build.Utilities;
using Microsoft.IO;
using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Gui;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.Network;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static IngameScript.Program;



namespace IngameScript
{

    partial class Program : MyGridProgram
    {
        // ME TAG
        static string SAM_V2_CONF_HELPER_TAG_NAME = "SV2CH";

        // SAM Values
        static string SAM_TAG_NAME = "SAM";
        static string SAM_CD_TAG = SAM_TAG_NAME + ".";

        // SHIP NAME
        static string SHIP_NAME = "";

        // Use default profile 
        Boolean useDefaultProfile = true;

        // Auto Update Sam Configuration from profile 
        Boolean autoUpdateSamFromProfile = true;

        // Auto Scan Block
        bool autoRescanBlocks = true; 

        // Toggle Remove SAM RC when Merged
        Boolean autoRemoveSamRCWhenMerged = true;

        // Toggle Ship Rename
        Boolean autoRenameShipWhenUnmerged = true;

        ///
        static Dictionary<string, string[]> SAM_CONFIGURATIONS = new Dictionary<string, string[]>
            {
                //Key | short | toogle | default | print gap XD
                { "MaxSpeed", new [] { "ms", "false", "95","1" } },
                { "ConvergingSpeed", new [] { "cs", "false", "60","1" } },
                { "ApproachDistance", new [] { "ad", "false", "500","1" } },
                { "ApproachingSpeed", new [] { "as", "false", "30","1" } },
                { "DockDistance", new [] { "dd", "false", "10","1" } },
                { "DockingSpeed", new [] { "ds", "false", "5","1" } },
                { "UndockDistance", new [] { "ud", "false", "10","1" } },
                { "TaxiingSpeed", new [] { "ts", "false", "5","1" } },
                { "TaxiingDistance", new [] { "td", "false", "10","1" } },
                { "TaxiingPanelDistance", new [] { "tpd", "false", "10","1" } },
                { "NODAMPENERS", new [] { "nd", "true", "false","1" } },
                { "IGNOREGRAVITY", new [] { "ig", "true", "false","1" } }
            };


        // DO NOT EDIT AFTER THIS :)
        // Init
        MyCommandLine _commandLine = new MyCommandLine();
        Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        MyIni _ini = new MyIni();

        SamConfHelperBlocks samConfHelperBlocks = new SamConfHelperBlocks();

        DestinationProfileManager destinationProfileManager = null;

        public Program()
        {

            // Command Lists
            _commands["setUp"] = SetUp;
            _commands["reload"] = LoadBlocks;

            _commands["toggleSamMainConnector"] = ToggleSamMainConnector;
            _commands["tsmc"] = ToggleSamMainConnector;

            _commands["toggleSamMainRc"] = ToggleSamMainRc;
            _commands["tsmrc"] = ToggleSamMainRc;

            _commands["refresh"] = Refresh;

            _commands["loadProfile"] = LoadProfile;


            // Value modifier
            foreach (var entry in SAM_CONFIGURATIONS)
            {
                _commands[entry.Key] = () => SetSamValue(entry.Key);
                _commands[entry.Value[0]] = () => SetSamValue(entry.Key);
            }

            // Set Up 
            SetUp();

            //
            Runtime.UpdateFrequency |= UpdateFrequency.Update100;

        }

        public void Main(string argument, UpdateType updateType)
        {

            // Commands
            if (argument != null && _commandLine.TryParse(argument))
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

            // Auto Refresh - May be use Extra Tick later 
            if ((updateType & UpdateType.Update100) == UpdateType.Update100)
            {
                Refresh();
            }
        }

        public void SetUp()
        {
            Logger.Log("SetUp ... ");

            LoadBlocks();

            Logger.Log("SetUp ... Done");
            Logger.Output(samConfHelperBlocks.samConfHelperLcdLogList);

        }

        // Command 
        public void LoadBlocks()
        {
            var blocks = new List<IMyTerminalBlock>();
            this.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => b.IsSameConstructAs(Me));

            samConfHelperBlocks.ScanBlocks(blocks);
            Boolean result = samConfHelperBlocks.CheckBlocks();
            if (!result)
            {
                Echo(Logger.GetLogText());

            }

            // Init objects
            destinationProfileManager = new DestinationProfileManager(Me);

            // Ship name
            if (SHIP_NAME == "") SHIP_NAME = Me.CubeGrid.CustomName;

        }

        // Command  
        public void ToggleSamMainRc()
        {
            Logger.Log("ToggleSamMainRc : ");
            // Command
            string rcName = _commandLine.Argument(1);
            ChangeMainSamRc(rcName);
        }

        // Command  
        public void ToggleSamMainConnector()
        {
            // Command
            string connectorName = _commandLine.Argument(1);
            ChangeMainSamConnector(connectorName);
        }

        public void ChangeMainSamConnector(string connectorName)
        {

            Echo("ChangeMainSamConnector to " + connectorName);
            if (connectorName == null) return;

            // Get Configured Conenctor in that direction
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            if (samConfHelperBlocks.samConnectors.Count == 1 && samConfHelperBlocks.samMainConnector.CustomName.ToLower().Contains(SAM_V2_CONF_HELPER_TAG_NAME.ToLower() + " " + connectorName.ToLower()))
            {
                return;
            }
            // Don't change connector if ship is connected with actual main
            if (samConfHelperBlocks.samMainConnector.IsConnected) return;

            foreach (IMyShipConnector connector in samConfHelperBlocks.samConnectors)
            {
                if (connector.CustomName.ToLower().Contains(connectorName.ToLower()))
                {
                    if (connector != samConfHelperBlocks.samMainConnector || !connector.CustomData.ToUpper().Contains(SAM_CD_TAG + "MAIN"))
                    {
                        Logger.Log("Switch to Connector " + connector.CustomName);
                        Logger.Log("From connector : " + samConfHelperBlocks.samMainConnector.CustomName);

                        // Change current main connector
                        samConfHelperBlocks.samMainConnector.CustomName = samConfHelperBlocks.samMainConnector.CustomName.Replace(SAM_TAG_NAME + " MAIN", SAM_TAG_NAME);
                        samConfHelperBlocks.samMainConnector.CustomData = samConfHelperBlocks.samMainConnector.CustomData.ToUpper().Replace(SAM_CD_TAG + "MAIN", "");
                        samConfHelperBlocks.samMainConnector = connector;
                        samConfHelperBlocks.samMainConnector.CustomData = SAM_CD_TAG + "\n" + SAM_CD_TAG + "MAIN";

                        // Change profile
                        destinationProfileManager.CurrentDestination.AddProperty("connector", connectorName);
                    }
                }
            }
        }

        public void ChangeMainSamRc(string rcName)
        {
            Echo("ChangeMainSamRc to " + rcName);
            if (rcName == null) return;

            // Get Configured Conenctor in that direction
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            if (samConfHelperBlocks.samRc.CustomName.ToLower().Contains(SAM_V2_CONF_HELPER_TAG_NAME.ToLower() + " " + rcName.ToLower()))
            {
                return;
            }

            foreach (IMyRemoteControl rc in samConfHelperBlocks.remotControllers)
            {
                if (rc.CustomName.ToLower().Contains(rcName.ToLower()))
                {
                    if (rc != samConfHelperBlocks.samRc)
                    {
                        Logger.Log("Switch to RC " + rc.CustomName);
                        Logger.Log("From RC : " + samConfHelperBlocks.samRc.CustomName);

                        // Change current main RC
                        samConfHelperBlocks.samRc.CustomName = samConfHelperBlocks.samRc.CustomName.Replace("[SAM]", "");
                        samConfHelperBlocks.samRc.CustomData = samConfHelperBlocks.samRc.CustomData.ToUpper().Replace("SAM.", "");
                        samConfHelperBlocks.samRc = rc;
                        rc.CustomData = rc.CustomData + "\n" + "SAM.";

                        // Change profile
                        destinationProfileManager.CurrentDestination.AddProperty("rc", rcName);
                    }
                }
            }
        }

        // 
        public void Refresh()
        {
            // Load Blocs ??
             // if(autoRescanBlocks) LoadBlocks();

            // Load profile
            LoadProfile();

            // Check Merged Blocks
            CheckMergeBlocks();

            // Output settings to LCD
            DisplaySamPbConf();

            // Output log
            Logger.Output(samConfHelperBlocks.samConfHelperLcdLogList);

        }

        public void CheckMergeBlocks()
        {

            if (!autoRemoveSamRCWhenMerged || samConfHelperBlocks.samConfHelperMergeBLocks.Count() < 1) return;

            // Merge Blocks STuff
            foreach (var mergeBlock in samConfHelperBlocks.samConfHelperMergeBLocks)
            {
                if (mergeBlock.IsConnected)
                {

                    samConfHelperBlocks.samRc.CustomData = "";
                    samConfHelperBlocks.samPb.samPb.Enabled = false;
                    foreach (var connector in samConfHelperBlocks.samConnectors)
                    {
                        connector.Enabled = true;
                        connector.Connect();
                    }
                }
                else
                {

                    if (autoRenameShipWhenUnmerged && !Me.CubeGrid.CustomName.Equals(SHIP_NAME)) Me.CubeGrid.CustomName = SHIP_NAME;

                    samConfHelperBlocks.samRc.CustomData = SAM_CD_TAG;
                    samConfHelperBlocks.samPb.samPb.Enabled = true;

                }
            }



        }

        public void DisplaySamPbConf()
        {
            var _textToOutput = "";
            Animation.Run();
            _textToOutput += "SamV2 Helper " + Animation.Rotator() + '\n';

            // SAM Settings
            var samSettingsText = samConfHelperBlocks.samPb.samPb.CustomData.Replace(SAM_CD_TAG, "");

            // Lcd Pretty printing
            StringBuilder sb = new StringBuilder();
            sb.Append("X");
            if (samConfHelperBlocks.samConfHelperLcdList != null && samConfHelperBlocks.samConfHelperLcdList.Count > 0)
            {
                foreach (var textSurface in samConfHelperBlocks.samConfHelperLcdList)
                {
                    int maxLineLength = (int)(23 / textSurface.FontSize);
                    var _textToOutputFormated = PrettyPrint(samSettingsText, maxLineLength);

                    // Output per lcd 
                    textSurface.WriteText(_textToOutput, false);
                    textSurface.WriteText(_textToOutputFormated, true);
                }
            }
            else
            {
                Echo(_textToOutput);
                Echo(samSettingsText);
            }
            // Reset output text
            _textToOutput = "";

            // Main Connector
            if (samConfHelperBlocks.samMainConnector != null)
            {
                _textToOutput += "\uD83E : " + samConfHelperBlocks.samMainConnector.CustomName + "\n";
            }

            // RC
            if (samConfHelperBlocks.samRc != null)
            {
                _textToOutput += "RC : " + samConfHelperBlocks.samRc.CustomName + "\n";
            }

            // Destination
            _textToOutput += Animation.Destination() + ":" + samConfHelperBlocks.logLcd.GetDestination();

            // Final Output
            if (samConfHelperBlocks.samConfHelperLcdList != null && samConfHelperBlocks.samConfHelperLcdList.Count > 0)
            {
                foreach (var textSurface in samConfHelperBlocks.samConfHelperLcdList)
                {
                    textSurface.WriteText(_textToOutput, true);
                }
            }
            else
            {
                Echo("_textToOutput");
            }

        }

        private string PrettyPrint(string buffer, int maxLineLength)
        {
            string[] lines = buffer.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            string output = "";

            // Iterate through the key-value pairs and print them
            foreach (var line in lines)
            {
                string key = line;
                string value = "";
                if (line.Contains("="))
                {
                    string[] keyValue = line.Split(new[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                    key = keyValue[0].Trim();
                    if (keyValue.Length > 1) value = keyValue[1].Trim();
                }

                int currentLength = key.Length + value.Length;

                string spaces = new string('.', maxLineLength - currentLength);
                // Create a formatted string with left-aligned keys and right-aligned values

                string formattedLine = ($"{key} {spaces} {value}");
                output += formattedLine + "\n";
            }
            return output;
        }

        public void SetSamValue(string key)
        {
            string targetValue = _commandLine.Argument(1);
            if (targetValue != null)
            {
                SetSamTargetValue(key, targetValue);
            }
        }

        private void SetSamTargetValue(string key, string targetValue)
        {
            Echo("SetSamTargetValue " + key + " - " + targetValue);
            if (destinationProfileManager.CurrentDestination != null)
            {
                // Increement +
                if (targetValue != null)
                {
                    string value = destinationProfileManager.CurrentDestination.properties[key];
                    double doubleValue;

                    Echo("current " + key + " - " + targetValue);

                    if (double.TryParse(value, out doubleValue))
                    {
                        targetValue = targetValue.Trim();
                        double increement = 0;

                        Echo("current doubleValue " + key + " - " + doubleValue);

                        if (targetValue.StartsWith("+"))
                        {
                            double.TryParse(targetValue.Substring(1), out increement);
                            doubleValue += increement;
                            Echo("current + " + key + " - " + doubleValue);
                            targetValue = "" + doubleValue;
                        }
                        if (targetValue.StartsWith("-"))
                        {
                            double.TryParse(targetValue.Substring(1), out increement);
                            doubleValue -= increement;
                            Echo("current - " + key + " - " + doubleValue);
                            targetValue = "" + doubleValue;
                        }
                       
                    }
                }

                Echo("CurrentDestination- " + key + " - " + targetValue);

                destinationProfileManager.CurrentDestination.AddProperty(key, targetValue);
            }
            else
            {
                samConfHelperBlocks.samPb.SetSamPbProperty(key, targetValue);
            }

            samConfHelperBlocks.samPb.WriteSamPbPropertiesToPb();
        }

        public void LoadProfile()
        {
            
            // Get destination / profile From command line
            string destination = _commandLine.Argument(1);

            // Not a Manual Command
            if (!autoUpdateSamFromProfile && destination  == null ) return;

            // Or else Get from SAM Log
            if (destination == null)
            {
                destination = samConfHelperBlocks.logLcd.GetDestination();
            }

            // Check if profile exists
            DestinationProfile destinationProfile = destinationProfileManager.LoadDestinationProfile(destination);

            if (destinationProfile == null && useDefaultProfile)
            {
                destinationProfile = destinationProfileManager.LoadDestinationProfile("DEFAULT");
            }
            if (destinationProfile == null) { return; }

            destinationProfileManager.applyCurrentDestinationProfileToSamPb(samConfHelperBlocks.samPb);

            // Change connector if needed
            if (destinationProfile.properties.ContainsKey("connector"))
            {
                ChangeMainSamConnector(destinationProfile.properties["connector"]);
            }

            // Change Remote controller if needed
            if (destinationProfile.properties.ContainsKey("rc"))
            {
                ChangeMainSamRc(destinationProfile.properties["rc"]);
            }

        }

        public Boolean Str_Equals(string s1, string s2)
        {
            return s1 != null && s2 != null && string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        /// 
        class SamConfHelperBlocks
        {
            // SAM CONF HELPER 
            public List<IMyTextSurface> samConfHelperLcdList = new List<IMyTextSurface>();
            public List<IMyTextSurface> samConfHelperLcdLogList = new List<IMyTextSurface>();
            public List<IMyShipConnector> samConnectors = new List<IMyShipConnector>();
            public List<IMyRemoteControl> remotControllers = new List<IMyRemoteControl>();
            public List<IMyShipMergeBlock> samConfHelperMergeBLocks = new List<IMyShipMergeBlock>();
            public Dictionary<String, String> samConfProperties = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            // Sam BLocks
            public IMyRemoteControl samRc = null;
            public IMyTextPanel samLoglcd = null;
            public Cockpit samCockpit = null;
            public LogLcd logLcd = null;

            public SamPb samPb = null;
            public IMyShipConnector samMainConnector = null;

            public void ScanBlocks(List<IMyTerminalBlock> blocks)
            {
                this.Clear();

                string outputLog = "";

                // Iterate through all blocks and find the ones with the specified tag
                foreach (var block in blocks)
                {
                    if (isTagOrCdBlockOf(block, SAM_TAG_NAME, SAM_CD_TAG))
                    {
                        if (block is IMyRemoteControl)
                        {
                            samRc = block as IMyRemoteControl;
                            outputLog = "Found SAM RC ... " + "\n" + outputLog;
                        }
                        else if (block is IMyTextPanel)
                        {
                            if (blockHasNameOrCd(block, "LOG"))
                            {
                                samLoglcd = block as IMyTextPanel;
                                outputLog = "Found SAM LOG LCD ... " + "\n" + outputLog;
                            }
                        }
                        else if (block is IMyCockpit)
                        {
                            if (blockHasNameOrCd(block, "LOG"))
                            {
                                samCockpit = new Cockpit(SAM_TAG_NAME, block as IMyCockpit);
                                outputLog = "Found SAM LOG COCKPIT ... " + "\n" + outputLog;
                            }
                        }
                        else if (block is IMyProgrammableBlock && !blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME) && block.IsWorking)
                        {
                            samPb = new SamPb(block as IMyProgrammableBlock);
                            outputLog = "Found SAM PB ... " + "\n" + outputLog;
                        }
                        else if (block is IMyShipConnector)
                        {
                            if (blockHasNameOrCd(block, "MAIN"))
                            {
                                samMainConnector = block as IMyShipConnector;
                                outputLog = "Found SAM MAIN Connector ... " + "\n" + outputLog;
                            }

                            if (samMainConnector == null)
                            {
                                samMainConnector = block as IMyShipConnector;
                                outputLog = "Found SAM Connector ... " + "\n" + outputLog;
                            }

                        }
                    }

                    //
                    if (isTagOrCdBlockOf(block, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_TAG_NAME))
                    {
                        if (block is IMyTextPanel)
                        {
                            if (blockHasNameOrCd(block, "log"))
                            {
                                (block as IMyTextSurface).ContentType = ContentType.TEXT_AND_IMAGE;
                                (block as IMyTextSurface).Font = "Monospace";

                                samConfHelperLcdLogList.Add(block as IMyTextSurface);
                                outputLog = "Found SV2CH LOG LCD ... " + "\n" + outputLog;
                            }
                            else
                            {
                                (block as IMyTextSurface).ContentType = ContentType.TEXT_AND_IMAGE;
                                samConfHelperLcdList.Add(block as IMyTextPanel);
                                (block as IMyTextSurface).Font = "Monospace";
                                outputLog = "Found SV2CH LCD ... " + "\n" + outputLog;
                            }

                        }
                        if (block is IMyCockpit)
                        {
                            Cockpit cockpit = new Cockpit(SAM_V2_CONF_HELPER_TAG_NAME, block as IMyCockpit);
                            if (cockpit.getLogTextSurface() != null)
                            {
                                cockpit.getLogTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                                cockpit.getLogTextSurface().Font = "Monospace";

                                samConfHelperLcdLogList.Add(cockpit.getLogTextSurface());

                                outputLog = "Found SV2CH LOG COCKPIT LCD ... " + "\n" + outputLog;
                            }
                            if (cockpit.getConfTextSurface() != null)
                            {
                                cockpit.getConfTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                                samConfHelperLcdList.Add(cockpit.getConfTextSurface());
                                cockpit.getConfTextSurface().Font = "Monospace";
                                outputLog = "Found SV2CH COCKPIT LCD ... " + "\n" + outputLog;
                            }

                        }

                        if (block is IMyRemoteControl)
                        {
                            if (blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME))
                            {
                                remotControllers.Add(block as IMyRemoteControl);
                                outputLog = "Found SV2CH RC ... " + "\n" + outputLog;
                            }
                        }

                        if (block is IMyShipConnector)
                        {
                            if (blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME))
                            {
                                samConnectors.Add(block as IMyShipConnector);
                                outputLog = "Found SV2CH Connector ... " + "\n" + outputLog;
                            }
                        }

                        if (block is IMyShipMergeBlock)
                        {
                            if (blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME))
                            {
                                samConfHelperMergeBLocks.Add(block as IMyShipMergeBlock);
                                outputLog = "Found SV2CH Merge Blocks ... " + "\n" + outputLog;
                            }
                        }

                    }
                }
                logLcd = new LogLcd(samLoglcd, samCockpit);

                Logger.Log(outputLog);
                Logger.Log("LoadBlocks ... Done");

            }

            public Boolean CheckBlocks()
            {
                // Minimum
                if (samPb == null) { Logger.Log("No SAM PB found"); return false; }
                if (samLoglcd == null || samCockpit == null) { Logger.Log("No SAM LOG LCD Or Cockpit found"); return false; }

                if (samRc == null) { Logger.Log("No SAM RC found"); return false; }
                if (samMainConnector == null) { Logger.Log("No SAM MAIN Connector found"); return false; }

                //
                if (samConfHelperLcdLogList == null || samConfHelperLcdLogList.Count < 1) Logger.Log("No SV2CH LCD found");
                if (samConfHelperLcdList == null || samConfHelperLcdList.Count < 1) Logger.Log("No SV2CH LCD found");

                return true;
            }

            public void Clear()
            {
                samConfHelperLcdLogList.Clear();
                samConfHelperLcdList.Clear();
                samConnectors.Clear();
                remotControllers.Clear();
                samConfHelperMergeBLocks.Clear();
                samConfProperties.Clear();
            }

            static bool blockHasNameOrCd(IMyTerminalBlock block, string test)
            {
                if (block.CustomName.ToLower().Contains(test.ToLower()) || block.CustomData.ToLower().Contains(test.ToLower())) { return true; }
                return false;
            }

            public static Boolean isTagOrCdBlockOf(IMyTerminalBlock block, string tag, string cd)
            {
                return block != null && (block.CustomName.ToLower().Contains("[" + tag.ToLower()) || isBlockForCd(block, cd.ToLower()));
            }

            private static bool isBlockForCd(IMyTerminalBlock block, string cd)
            {
                if (block.CustomData == null) return false;
                foreach (var line in block.CustomData.Split('\n').ToList())
                {
                    if (line != null && line.ToLower().Equals(cd.ToLower(), StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        class SamConnectorManager
        {
            public List<SamConnector> samConnectorList = new List<SamConnector>();

            public void addConnector(IMyShipConnector connector)
            {
                SamConnector SamConnector = new SamConnector(connector);

            }

            public void clear()
            {
                this.samConnectorList.Clear();

            }
        }

        class SamConnector
        {
            IMyShipConnector connector;
            public SamConnector(IMyShipConnector connector)
            {
                this.connector = connector;
            }

        }

        internal class CustomName
        {
            string tag;
            string pattern;
            public string customName { set; get; }

            public Dictionary<string, string> properties = new Dictionary<string, string>();

            public CustomName(string tag, string customName)
            {
                this.tag = tag.ToUpper();
                this.customName = customName;
                this.pattern = $"[{tag} ".ToUpper();

            }

            public void parseCustonName()
            {
                // Clear Properties
                properties.Clear();

                // Find the start of the tag
                int startIndex = customName.ToUpper().IndexOf(pattern);
                if (startIndex == -1) return;

                // Find the end of the tag
                startIndex += pattern.Length;
                int endIndex = customName.IndexOf(']', startIndex);
                if (endIndex == -1) return;

                // Extract the tag
                string tagText = customName.Substring(startIndex, endIndex - startIndex);

                // Retrieve only the content
                tagText = tagText.Replace(pattern, "").Replace("]", "").Trim();

                string[] tagtextKeyValues = tagText.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyValuePair in tagtextKeyValues)
                {
                    string key = "";
                    string value = "";

                    // key value 
                    if (keyValuePair.Contains("="))
                    {
                        string[] keyValue = keyValuePair.Split(new[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                        if (keyValue.Length > 0) key = keyValue[0];
                        if (keyValue.Length > 1) value = keyValue[1];
                    }
                    else key = keyValuePair;
                    properties[key] = value;
                }
            }

            public void updateCustomName()
            {
                string newTagText = $"[{tag}";
                foreach (var property in properties)
                {
                    newTagText += $" {property.Key}={property.Value}";
                }

                // Define the pattern to search for
                int startIndex = customName.ToUpper().IndexOf(pattern.ToUpper());
                if (startIndex != -1)
                {
                    int endIndex = customName.IndexOf(']', startIndex);
                    if (endIndex != -1)
                    {
                        // Extract the value
                        string tagText = customName.Substring(startIndex, endIndex - startIndex).ToUpper();

                        tagText = tagText.Replace(pattern, "").Replace("]", "");
                        string[] tagtextKeyValues = tagText.Trim().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                        newTagText += "]";

                        // Replace the existing value
                        customName = customName.Remove(startIndex, endIndex - startIndex + 1);
                        customName = customName.Insert(startIndex, newTagText.ToUpper());
                    }
                }
                else
                {
                    // If the key does not exist, append it
                    customName += " " + newTagText;
                }

            }

            public void UpdateProperty(string key, string value)
            {
                this.properties[key] = value;
            }
            public void RemoveProperty(string key)
            {
                if (properties.ContainsKey(key)) this.properties.Remove(key);
            }

            public string GetProperty(string key)
            {
                if (this.properties.ContainsKey(key)) return this.properties[key];
                return null;
            }
        }



        public static class Logger
        {
            public static List<string> logText = new List<string>();

            public static void Log(string log) { logText.Add(log); }

            public static void Output(List<IMyTextSurface> textSurfaceList)
            {

                if (textSurfaceList != null && textSurfaceList.Count > 0)
                {
                    string outputString = GetLogText();

                    foreach (IMyTextSurface textSurface in textSurfaceList)
                    {
                        textSurface.WriteText(outputString, false);
                    }

                    if (logText.Count > 30)
                    {
                        logText.RemoveRange(29, logText.Count() - 1);
                    }
                }
            }

            public static string GetLogText()
            {
                string outputString = "";

                foreach (string line in logText)
                {
                    outputString = line + "\n" + outputString;
                }

                return outputString;
            }
        }

        internal class LogLcd
        {
            public IMyTextPanel samLogLcd { set; get; }
            public Cockpit samLogCockpit { set; get; }

            public LogLcd(IMyTextPanel samLoglcd, Cockpit samLogCockpit)
            {
                this.samLogLcd = samLoglcd;
                this.samLogCockpit = samLogCockpit;
            }

            public string GetDestination()
            {
                string lcdText = null;

                if (samLogLcd != null) lcdText = samLogLcd.GetText();
                if (samLogCockpit != null) lcdText = samLogCockpit.getLogTextSurface().GetText();

                if (lcdText == null)
                    return "";

                string[] lines = lcdText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                string searchText = "I: Navigating to ";
                string destination = null;
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
        }

        internal class DestinationProfile
        {
            string destination { get; set; }
            public Dictionary<string, string> properties { get; }

            public DestinationProfile(string destination)
            {
                this.destination = destination;
                this.properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            public void AddProperty(string key, string value)
            {
                if (this.properties.ContainsKey(key))
                {
                    this.properties[key] = value;
                }
                else
                {
                    this.properties.Add(key, value);
                }
            }

        }

        internal class DestinationProfileManager
        {
            public Dictionary<String, DestinationProfile> destinationProfileList = new Dictionary<string, DestinationProfile>();
            public DestinationProfile CurrentDestination;
            public IMyProgrammableBlock samV2HelperPb;
            string initialCustomData;
            MyIni _ini = new MyIni();

            public DestinationProfileManager(IMyProgrammableBlock samV2HelperPb)
            {
                this.samV2HelperPb = samV2HelperPb;
                this.initialCustomData = samV2HelperPb.CustomData;
                GenerateDefaultProfile("DEFAULT");
                parseDestinationProfile();
            }

            public void GenerateDefaultProfile(string profileName)
            {
                if (samV2HelperPb.CustomData == null || samV2HelperPb.CustomData == "")
                {
                    // generate default
                    samV2HelperPb.CustomData += $"[{profileName}] \n";
                    foreach (var samProperty in SAM_CONFIGURATIONS)
                    {
                        samV2HelperPb.CustomData += $"{samProperty.Key}={samProperty.Value[2]} \n";
                    }
                }

                if (!samV2HelperPb.CustomName.Contains("[" + SAM_V2_CONF_HELPER_TAG_NAME))
                {
                    samV2HelperPb.CustomName += " " + "[" + SAM_V2_CONF_HELPER_TAG_NAME + "]";
                }
                this.initialCustomData = samV2HelperPb.CustomData;
            }

            public Boolean HasChanged()
            {
                return initialCustomData == null || !initialCustomData.Equals(this.samV2HelperPb.CustomData);
            }

            public DestinationProfile LoadDestinationProfile(string destination)
            {
                if (this.samV2HelperPb.CustomData == null || destination == null) return null;

                if (HasChanged())
                {
                    parseDestinationProfile();
                }
                this.CurrentDestination = this.GetDestinationProfile(destination);
                return CurrentDestination;
            }

            public void applyCurrentDestinationProfileToSamPb(SamPb samPb)
            {
                foreach (var key in CurrentDestination.properties.Keys.ToList())
                {
                    if (SAM_CONFIGURATIONS.ContainsKey(key))
                    {
                        samPb.SetSamPbProperty(key.ToString(), CurrentDestination.properties[key]);
                    }
                }
                samPb.WriteSamPbPropertiesToPb();
            }

            public void parseDestinationProfile()
            {
                this.initialCustomData = this.samV2HelperPb.CustomData;

                MyIniParseResult result;
                if (!_ini.TryParse(initialCustomData, out result))
                    throw new Exception(result.ToString());

                // search Profile
                List<string> sectionList = new List<string>();
                _ini.GetSections(sectionList);

                foreach (var section in sectionList)
                {
                    DestinationProfile destinationProfile = new DestinationProfile(section);

                    List<MyIniKey> profileKeys = new List<MyIniKey>();
                    _ini.GetKeys(section, profileKeys);

                    foreach (var myIniKey in profileKeys)
                    {
                        destinationProfile.AddProperty(myIniKey.Name, _ini.Get(section, myIniKey.Name).ToString());
                    }

                    if (this.destinationProfileList.ContainsKey(section))
                    {
                        this.destinationProfileList[section] = destinationProfile;
                    }
                    else
                    {
                        this.destinationProfileList.Add(section, destinationProfile);
                    }

                }
            }

            public DestinationProfile GetDestinationProfile(string destination)
            {
                foreach (var key in destinationProfileList.Keys.ToList())
                {
                    if (destination.ToLower().Contains(key.ToLower()))
                    {
                        return destinationProfileList.GetValueOrDefault(key);
                    }
                }
                return null;
            }

        }

        internal class SamProperty
        {
            ConfigurationType ConfigurationType { set; get; }

            public string Name { set; get; }
            public string Value { set; get; }

            public SamProperty(ConfigurationType ConfigurationType, string Name, string Value)
            {
                this.ConfigurationType = ConfigurationType;
                this.Name = Name;
                this.Value = Value;
            }
        }



        internal enum ConfigurationType { TAG, CD }


        internal class SamPb
        {
            public IMyProgrammableBlock samPb { set; get; }

            Dictionary<string, SamProperty> samProperties = new Dictionary<string, SamProperty>(StringComparer.OrdinalIgnoreCase);

            public SamPb(IMyProgrammableBlock samPb)
            {
                this.samPb = samPb;
                LoadSamPbProperties();

            }

            public void LoadSamPbProperties()
            {
                if (samPb == null) return;
                string customName = samPb.CustomName;
                LoadSamPbPropertiesFromTag();
                LoadSamPbPropertiesFromCd();
            }

            public void LoadSamPbPropertiesFromTag()
            {
            }

            public void LoadSamPbPropertiesFromCd()
            {
                string customData = samPb.CustomData;
                string[] lines = customData.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // SAM TAG
                    if (line.ToLower().Trim().Equals(SAM_CD_TAG.ToLower())) continue;
                    Logger.Log("line " + line);
                    // SAM Values
                    if (line.ToLower().StartsWith(SAM_CD_TAG.ToLower()))
                    {
                        // Key Value Tag
                        if (line.Contains("="))
                        {
                            string[] keyValue = line.Split(new[] { "=" }, StringSplitOptions.None);
                            string key = keyValue[0];
                            key = key.ToLower().Replace(SAM_CD_TAG.ToLower(), "");
                            if (SAM_CONFIGURATIONS.ContainsKey(key) && SAM_CONFIGURATIONS[key].Equals("false"))
                            {
                                this.SetSamPbProperty(key, keyValue[1]);
                            }

                        }
                        // Exclusive Tag
                        else
                        {
                            string key = line.ToLower().Replace(SAM_CD_TAG.ToLower(), "");
                            if (SAM_CONFIGURATIONS.ContainsKey(key) && SAM_CONFIGURATIONS[key].Equals("true"))
                            {
                                this.SetSamPbProperty(key, "true");
                            }
                        }
                    }
                }

            }

            public void SetSamPbProperty(string key, string value)
            {
                if (samProperties.ContainsKey(key.Trim()))
                {
                    SamProperty samProperty = samProperties[key];
                    samProperty.Value = value.Trim().ToLower();
                }
                else
                {
                    SamProperty samProperty = new SamProperty(ConfigurationType.CD, key.Trim(), value.Trim().ToLower());
                    samProperties.Add(key.Trim(), samProperty);
                }
            }

            public void WriteSamPbPropertiesToPb()
            {
                // Current CD
                string[] lines = samPb.CustomData.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                // New CD
                string newSamPbCustomData = "SAM. \n";

                foreach (var samPropery in this.samProperties)
                {
                    // Exclusive tags
                    if ("true".Equals(samPropery.Value.Value.ToLower()))
                    {
                        newSamPbCustomData += SAM_CD_TAG + samPropery.Key + "\n";
                        continue;
                    }
                    // Key Value Tag
                    if (!"false".Equals(samPropery.Value.Value.ToLower()))
                    {
                        newSamPbCustomData += SAM_CD_TAG + samPropery.Key + "=" + samPropery.Value.Value + " \n";
                    }
                }


                /*List<string> listRemainingKeys = this.samProperties.Keys.ToList();

                foreach (var line in lines)
                {
                    string newLine = line;
                    // Skip Line
                    if (newLine.Trim().Equals(SAM_CD_TAG))
                    {
                        continue;
                    }
                    // Key Value  Line
                    if (line.Contains("="))
                    {
                        string[] keyValue = line.Split(new[] { "=" }, StringSplitOptions.None);
                        string key = keyValue[0];
                        key = key.Replace(SAM_CD_TAG, "");
                        if (this.samProperties.ContainsKey(key))
                        {
                            // Check if empty ==> then default values
                            string value = this.samProperties[key].Value;
                            if ("false".Equals(SAM_CONFIGURATIONS[key][1]))
                            {
                                newLine = SAM_CD_TAG + key + "=" + value;
                            }
                            newSamPbCustomData += newLine + "\n";
                            listRemainingKeys.Remove(key);
                        }
                        continue;
                    }
                    // Key exclusive tag line
                    else
                    {
                        string key = line.Replace("SAM.", "");
                        if (line.StartsWith(SAM_CD_TAG) && this.samProperties.ContainsKey(key))
                        {
                            if ("true".Equals(this.samProperties[key].Value))
                            {
                                newSamPbCustomData +=  key +  "\n";
                            }
                            listRemainingKeys.Remove(key);
                        }
                    }
                }

                foreach (var key in listRemainingKeys)
                {
                    if (SAM_CONFIGURATIONS.ContainsKey(key))
                    {
                        // Exclusive tag
                        if (samProperties[key].Equals("true"))
                        {
                            newSamPbCustomData += SAM_CD_TAG + key + "\n";
                        } else
                        {
                            // Key value 
                            if (!samProperties[key].Equals("false"))
                            {
                                var newLine = SAM_CD_TAG + key + "=" + samProperties[key].Value;
                                newSamPbCustomData += newLine + "\n";
                            }
                        }
                    }
                }*/

                this.samPb.CustomData = newSamPbCustomData;

            }
        }

        internal class Cockpit
        {
            IMyCockpit myCockpit;
            int screenLogIndex = -1;
            int screenConfIndex = -1;
            public CustomName customName;
            string tag;

            public Cockpit(string tag, IMyCockpit myCockpit)
            {
                this.myCockpit = myCockpit;
                this.tag = tag;
                customName = new CustomName(tag, myCockpit.CustomName.ToUpper());
                customName.parseCustonName();
                for (int index = 0; index < 10; index++)
                {
                    string value = customName.GetProperty("PANEL" + index);
                    if (value != null && value.ToUpper() == "LOG")
                    {
                        screenLogIndex = index;
                    }
                    if (value != null && value == "")
                    {
                        screenConfIndex = index;
                    }
                }
            }

            public IMyTextSurface getLogTextSurface() { return screenLogIndex > -1 ? this.myCockpit.GetSurface(screenLogIndex) : null; }

            public IMyTextSurface getConfTextSurface() { return screenConfIndex > -1 ? this.myCockpit.GetSurface(screenConfIndex) : null; }


            public void parseCustomNameOrCd()
            {

            }
        }

        internal static class Animation
        { // Animation
            private static string[] ROTATOR = new string[] { "|", "/", "-", "\\" };
            private static string[] DESTINATION = new string[] { ">--", "->-", "-->", "---" };
            private static int rotatorCount = 0;

            public static void Run()
            {
                if (++rotatorCount > ROTATOR.Length - 1)
                {
                    rotatorCount = 0;
                }
            }

            public static string Rotator()
            {
                return ROTATOR[rotatorCount];
            }

            public static string Destination()
            {
                return DESTINATION[rotatorCount];
            }

        }



        internal class BlockProfile
        { // BlockProfile
            public string[] tags;
            public string[] exclusiveTags;
            public string[] pbAttributes;
            public BlockProfile(ref string[] tags, ref string[] exclusiveTags, ref string[] pbAttributes)
            {
                this.tags = tags;
                this.exclusiveTags = exclusiveTags;
                this.pbAttributes = pbAttributes;
            }

        }

        /// <summary>
        /// Simulate a timer
        /// </summary>

        internal class SamConfHelperTimer
        {
            public double timerDuration { set; get; }
            private DateTime timerStartTime;
            public SamConfHelperTimer(double timerDuration)
            {
                this.timerDuration = timerDuration;

            }

            public void start()
            {
                timerStartTime = DateTime.Now;
            }

            public Boolean isFinished()
            {
                return (DateTime.Now - timerStartTime).TotalSeconds >= timerDuration;
            }
        }

    }
}

