using Microsoft.Build.Utilities;
using Microsoft.IO;
using ParallelTasks;
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
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
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
        string SAM_V2_CONF_HELPER_TAG_NAME = "SV2CH";

        // SAM Values
        string SAM_TAG_NAME = "SAM";
        string SAM_CD_TAG = "SAM.";

        Dictionary<string, string> samCdValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> samCdDefaultValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string> samCdToggles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        List<IMyShipConnector> samConnectors = new List<IMyShipConnector>();

        // Init
        MyCommandLine _commandLine = new MyCommandLine();
        Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        MyIni _ini = new MyIni();

        // SAM CONF HELPER 
        List<IMyTextSurface> samConfHelperLcdList = new List<IMyTextSurface>();
        List<IMyTextSurface> samConfHelperLcdLogList = new List<IMyTextSurface>();


        // Sam BLocks
        IMyRemoteControl samRc = null;
        IMyTextPanel samLoglcd = null;
        Cockpit samCockpit = null;


        SamPb samPb = null;
        IMyShipConnector samMainConnector = null;

        DestinationProfileManager destinationProfileManager = new DestinationProfileManager();

        public Program()
        {
            // SAM Values : long name / short name
            samCdValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MaxSpeed","ms"  },
                { "ConvergingSpeed", "cs" },
                { "ApproachDistance", "ad" },
                { "ApproachingSpeed", "as" },
                { "DockDistance", "dd"  },
                { "DockingSpeed", "ds"  },
                { "UndockDistance", "ud" },
                { "TaxiingSpeed", "ts" },
                { "TaxiingDistance", "td" },
                { "TaxiingPanelDistance", "tpd" }
             };
            samCdDefaultValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MaxSpeed","95"  },
                { "ConvergingSpeed", "60" },
                { "ApproachDistance", "500" },
                { "ApproachingSpeed", "30" },
                { "DockDistance", "10"  },
                { "DockingSpeed", "5"  },
                { "UndockDistance", "5" },
                { "TaxiingSpeed", "5" },
                { "TaxiingDistance", "10" },
                { "TaxiingPanelDistance", "10" }
             };

            // SAM Toggles : long name / short name
            samCdToggles = new Dictionary<string, string>()
            {
                { "NODAMPENERS", "nd" },
                { "IGNOREGRAVITY", "ig" }
            };

            // Command Lists
            _commands["initHelperConfiguration"] = InitHelperConfiguration;
            _commands["init"] = InitHelperConfiguration;
            _commands["reload"] = LoadBlocks;


            _commands["toggleSamMainConnector"] = ToggleSamMainConnector;
            _commands["tsmc"] = ToggleSamMainConnector;

            _commands["toggleSamRc"] = ToggleSamRc;
            _commands["refresh"] = refresh;

            _commands["loadDestinationProfile"] = LoadDestinationProfile;
            _commands["loadProfile"] = LoadDestinationProfile;


            // Value modifier
            foreach (var entry in samCdValues)
            {
                _commands[entry.Key] = () => SetSamValue(entry.Key);
                _commands[entry.Value] = () => SetSamValue(entry.Key);
            }
            // Value toggles
            foreach (var entry in samCdToggles)
            {
                _commands[entry.Key] = () => SetSamValue(entry.Key);
                _commands[entry.Value] = () => SetSamValue(entry.Key);
            }

            // Init
            InitHelperConfiguration();

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

            // Auto Refresh
            if ((updateType & UpdateType.Update100) == UpdateType.Update100)
            {
                refresh();
            }
        }

        public void InitHelperConfiguration()
        {
            Logger.Log("Loading ... ");
            Logger.Log("InitHelperConfiguration ... ");
            LoadBlocks();
            checkBlocks();
            generateDefaultProfile();
            Logger.Log("InitHelperConfiguration ... Done");
            Echo("samConfHelperLcdLogList count " + samConfHelperLcdLogList.Count);
            Echo("Logger count " + Logger.logText.Count);
            Logger.Output(samConfHelperLcdLogList);

        }
        public void generateDefaultProfile()
        {
            if (Me.CustomData == null || Me.CustomData == "")
            {
                // generate default
                Me.CustomData += "[DEFAULT] \n";
                foreach (var samProperty in samCdDefaultValues)
                {
                    Me.CustomData += $"{samProperty.Key}={samProperty.Value} \n";
                }
            }
        }

        public void checkBlocks()
        {
            // Minimum
            if (samPb == null) Logger.Log("No SAM PB found");
            if (samLoglcd == null) Logger.Log("No SAM LOG LCD found");
            if (samCockpit == null) Logger.Log("No SAM LOG Cockpit found");
            if (samRc == null) Logger.Log("No SAM RC found");
            if (samMainConnector == null) Logger.Log("No SAM MAIN Connector found");

            //
            if (samConfHelperLcdLogList == null || samConfHelperLcdLogList.Count < 1) Logger.Log("No SV2CH LCD found");
            if (samConfHelperLcdList == null || samConfHelperLcdList.Count < 1) Logger.Log("No SV2CH LCD found");

        }

        public void LoadBlocks()
        {
            Logger.Log("LoadBlocks ... ");
            // Get the grid this programmable block is on
            var grid = Me.CubeGrid;

            // Get all blocks in the programmable block's grid
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block.CubeGrid == grid);

            string outputLog = "";

            // Clear list
            samConfHelperLcdLogList.Clear();
            samConfHelperLcdList.Clear();


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
                    else if (block is IMyProgrammableBlock && !blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME))
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

                        if (blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME))
                        {
                            samConnectors.Add(block as IMyShipConnector);
                            outputLog = "Found SAM LEFT Connector ... " + "\n" + outputLog;
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
                            samConfHelperLcdLogList.Add(block as IMyTextSurface);
                            outputLog = "Found SV2CH LOG LCD ... " + "\n" + outputLog;
                        }
                        else
                        {
                            (block as IMyTextSurface).ContentType = ContentType.TEXT_AND_IMAGE;
                            samConfHelperLcdList.Add(block as IMyTextPanel);
                            outputLog = "Found SV2CH LCD ... " + "\n" + outputLog;
                        }

                    }
                    if (block is IMyCockpit)
                    {
                        Cockpit cockpit = new Cockpit(SAM_V2_CONF_HELPER_TAG_NAME, block as IMyCockpit);
                        if (cockpit.getLogTextSurface() != null)
                        {
                            cockpit.getLogTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                            samConfHelperLcdLogList.Add(cockpit.getLogTextSurface());
                            Echo("Found IMyCockpit SV2CH LOG COCKPIT LCD ... ");
                            outputLog = "Found SV2CH LOG COCKPIT LCD ... " + "\n" + outputLog;
                        }
                        if (cockpit.getConfTextSurface() != null)
                        {
                            cockpit.getConfTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                            samConfHelperLcdList.Add(cockpit.getConfTextSurface());
                            Echo("Found IMyCockpit SV2CH CONF  COCKPIT LCD ... ");
                            outputLog = "Found SV2CH COCKPIT LCD ... " + "\n" + outputLog;
                        }

                    }
                }
            }

            Logger.Log(outputLog);
            Logger.Log("LoadBlocks ... Done");
        }

        bool blockHasNameOrCd(IMyTerminalBlock block, string test)
        {
            if (block.CustomName.ToLower().Contains(test.ToLower()) || block.CustomData.ToLower().Contains(test.ToLower())) { return true; }
            return false;
        }

        public Boolean isTagOrCdBlockOf(IMyTerminalBlock block, string tag, string cd)
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

        public void ToggleSamRc()
        {
            Logger.Log("ToggleSamRc : ");
            // Command
            string status = _commandLine.Argument(1);
            Boolean toggle = Str_Equals(status, "on");

            if (toggle && samRc != null)
            {
                samRc.CustomData = (samRc != null && toggle ? SAM_CD_TAG : ""); ;
            }
            else
            {
                samRc.CustomData = "SAM.";
            }
        }

        public void ToggleSamMainConnector()
        {
            // Command
            string direction = _commandLine.Argument(1);
            ChangeMainConnector(direction);
        }

        public void ChangeMainConnector(string connectorName)
        {
            if (connectorName == null) return;

            // Get Configured Conenctor in that direction
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            if (samMainConnector.CustomName.ToLower().Contains(SAM_V2_CONF_HELPER_TAG_NAME.ToLower() + " " + connectorName.ToLower()))
            {
                return;
            }
            // Don'not change connector if ship is connected with actual main
            if (samMainConnector.IsConnected) return;

            foreach (IMyShipConnector connector in samConnectors)
            {
                if (connector.CustomName.ToLower().Contains(connectorName.ToLower()))
                {
                    if (connector != samMainConnector)
                    {
                        Logger.Log("Switch to Connector " + connector.CustomName);
                        Logger.Log("From connector : " + samMainConnector.CustomName);

                        // Change current main connector
                        samMainConnector.CustomData = samMainConnector.CustomData.ToUpper().Replace("SAM.MAIN", "");
                        samMainConnector = connector;
                        connector.CustomData = connector.CustomData + "\n" + "SAM." + "\n" + "SAM.MAIN";
                    }
                }
            }
        }

        public void refresh()
        {
            // Load profile
            LoadDestinationProfile();

            // 
            DisplaySamPbConf();

            // Output log
            Logger.Output(samConfHelperLcdLogList);

        }

        public void DisplaySamPbConf()
        {
            var _textToOutput = "";

            // Current Profile
            Animation.Run();
            _textToOutput += "SamV2 Helper " + Animation.Rotator() + '\n';

            // SAM Settings
            var samSettingsText = samPb.samPb.CustomData.Replace("SAM.", "");

            // Lcd Pretty printing
            StringBuilder sb = new StringBuilder();
            sb.Append("X");
            foreach (var textSurface in samConfHelperLcdList)
            {
                int maxLineLength = (int)(textSurface.SurfaceSize.X / (textSurface.MeasureStringInPixels(sb, textSurface.Font, 0.75f).X));
                var _textToOutputFormated = PrettyPrint(samSettingsText, maxLineLength);

                // Output per lcd 
                textSurface.WriteText(_textToOutput, false);
                textSurface.WriteText(_textToOutputFormated, true);
            }
            _textToOutput = "";

            // Main Connector
            _textToOutput += "Using : " + samMainConnector.CustomName + "\n";

            // Destination
            var samLogText = "";
            if (samLoglcd != null) samLogText = samLoglcd.GetText();
            if (samCockpit != null) samLogText = samCockpit.getLogTextSurface().GetText();

            _textToOutput += "Going to : " + SamLogParser.GetDestination(samLogText);

            // Output
            foreach (var textSurface in samConfHelperLcdList)
            {
                textSurface.WriteText(_textToOutput, true);
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

                //  string spaces = new string(' ', maxLineLength - currentLength);
                // Create a formatted string with left-aligned keys and right-aligned values

                string formattedLine = ($"{key} : {value}");
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
            samPb.SetSamPbProperty(key, targetValue);
            samPb.WriteSamPbPropertiesToPb();
            DisplaySamPbConf();
        }

        public void LoadDestinationProfile()
        {
            // From command line
            string destination = _commandLine.Argument(1);

            // Or else From the next navigation point
            if (destination == null)
            {
                // Look for destination if exists
                var samLogText = "";
                if (samLoglcd != null) samLogText = samLoglcd.GetText();
                if (samCockpit != null) samLogText = samCockpit.getLogTextSurface().GetText();

                destination = SamLogParser.GetDestination(samLogText);
            }

            if (destination == null) { return; }

            // Check if profile exists
            DestinationProfile destinationProfile = destinationProfileManager.LoadDestinationProfile(Me.CustomData, destination);

            if (destinationProfile == null) { return; }

            // Apply Destination Profile
            foreach (var key in destinationProfile.properties.Keys.ToList())
            {
                if (samCdValues.ContainsKey(key) || samCdToggles.ContainsKey(key))
                {
                    samPb.SetSamPbProperty(key.ToString(), destinationProfile.properties[key]);
                }
            }

            samPb.WriteSamPbPropertiesToPb();

            // Change connector if needed
            if (destinationProfile.properties.ContainsKey("connector"))
            {
                ChangeMainConnector(destinationProfile.properties["connector"]);
            }
            DisplaySamPbConf();
        }

        public Boolean Str_Equals(string s1, string s2)
        {
            return s1 != null && s2 != null && string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>

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

            static void Clear() { logText.Clear(); }

            public static void Log(string log) { logText.Add(log); }

            public static void Output(List<IMyTextSurface> textSurfaceList)
            {

                if (textSurfaceList != null)
                {
                    foreach (IMyTextSurface textSurface in textSurfaceList)
                    {


                        string outputString = textSurface.GetText();
                        string[] lines = outputString.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 20) outputString = "";

                        foreach (string line in logText)
                        {
                            outputString = line + "\n" + outputString;
                        }

                        textSurface.WriteText(outputString, false);
                    }
                    Clear();
                }
            }
        }

        internal class SamLogParser
        {
            public static string GetDestination(string lcdText)
            {
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
            string initialCustomData;
            MyIni _ini = new MyIni();

            public Boolean HasChanged(string newCustomData)
            {
                return initialCustomData == null || !initialCustomData.Equals(newCustomData);
            }

            public DestinationProfile LoadDestinationProfile(string customData, string destination)
            {
                if (customData == null || destination == null) return null;
                if (HasChanged(customData))
                {
                    parseDestinationProfile(customData);
                }
                this.CurrentDestination = this.GetDestinationProfile(destination);
                return CurrentDestination;
            }

            public void parseDestinationProfile(string customData)
            {
                this.initialCustomData = customData;

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
            ConfigurationType configurationType { set; get; }

            public string Name { set; get; }
            public string Value { set; get; }

            public SamProperty(ConfigurationType configurationType, string name, string value)
            {
                this.configurationType = configurationType;
                this.Name = Name;
                this.Name = Value;
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
                    if (line.ToLower().Equals("sam."))
                    {
                        continue;
                    }
                    if (line.Contains("="))
                    {
                        string[] keyValue = line.Split(new[] { "=" }, StringSplitOptions.None);
                        string key = keyValue[0];
                        key = key.Replace("SAM.", "");
                        this.SetSamPbProperty(key, keyValue[1]);
                    }
                }
            }

            public void SetSamPbProperty(string key, string value)
            {
                if (samProperties.ContainsKey(key))
                {
                    SamProperty samProperty = samProperties[key];
                    samProperty.Value = value;
                }
                else
                {
                    SamProperty samProperty = new SamProperty(ConfigurationType.CD, key, value);
                    samProperties.Add(key, samProperty);
                }
            }

            public void WriteSamPbPropertiesToPb()
            {
                // Current CD
                string[] lines = samPb.CustomData.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                // New CD
                string newSamPbCustomData = "";
                List<string> listRemainingKeys = this.samProperties.Keys.ToList();

                foreach (var line in lines)
                {
                    string newLine = line;
                    if (newLine.Equals("SAM."))
                    {
                        newSamPbCustomData += newLine + "\n";
                        continue;
                    }
                    if (line.Contains("="))
                    {
                        string[] keyValue = line.Split(new[] { "=" }, StringSplitOptions.None);
                        string key = keyValue[0];
                        key = key.Replace("SAM.", "");
                        if (this.samProperties.ContainsKey(key))
                        {
                            newLine = "SAM." + key + "=" + this.samProperties[key].Value;
                            newSamPbCustomData += newLine + "\n";
                            listRemainingKeys.Remove(key);
                            continue;
                        }
                        else
                        {
                            this.samProperties.Remove(key);
                        }
                    }
                    if (line.StartsWith("SAM.") && !this.samProperties.ContainsKey(line.Replace("SAM.", "")))
                    {
                        continue;
                    }
                    newSamPbCustomData += newLine + "\n";
                }
                foreach (var key in listRemainingKeys)
                {
                    if (samProperties[key] != null)
                    {
                        var newLine = "SAM." + key + "=" + samProperties[key].Value + "\n";
                        newSamPbCustomData += newLine + "\n";
                    }
                    else
                    {
                        var newLine = "SAM." + key + "X \n";
                        newSamPbCustomData += newLine + "\n";
                    }
                }

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
            private static int rotatorCount = 0;
            private static int debugRotatorCount = 0;
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

            public static void DebugRun()
            {
                if (++debugRotatorCount > ROTATOR.Length - 1)
                {
                    debugRotatorCount = 0;
                }
            }

            public static string DebugRotator()
            {
                return ROTATOR[debugRotatorCount];
            }
        }

        private class BlockProfile
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
    }
}

