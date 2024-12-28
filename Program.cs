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
        static string SAM_CD_TAG = "SAM.";

        // 
        static Boolean useDefaultProfile = true;

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
        List<IMyShipConnector> samConnectors = new List<IMyShipConnector>();
        List<IMyRemoteControl> remotControllers = new List<IMyRemoteControl>();

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
           
            // Command Lists
            _commands["initHelperConfiguration"] = InitHelperConfiguration;
            _commands["init"] = InitHelperConfiguration;
            _commands["reload"] = LoadBlocks;


            _commands["toggleSamMainConnector"] = ToggleSamMainConnector;
            _commands["tsmc"] = ToggleSamMainConnector;

            _commands["toggleSamMainRc"] = ToggleSamMainRc;
            _commands["tsmrc"] = ToggleSamMainRc;

            _commands["changeRcDirection"] = ChangeRcDirection;
            _commands["crcd"] = ChangeRcDirection;

            _commands["refresh"] = Refresh;

            _commands["loadDestinationProfile"] = LoadDestinationProfile;
            _commands["loadProfile"] = LoadDestinationProfile;


            // Value modifier
            foreach (var entry in SAM_CONFIGURATIONS)
            {
                _commands[entry.Key] = () => SetSamValue(entry.Key);
                _commands[entry.Value[0]] = () => SetSamValue(entry.Key);
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
                Refresh();
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
            Logger.Output(samConfHelperLcdLogList);

        }
        public void generateDefaultProfile()
        {
            if (Me.CustomData == null || Me.CustomData == "")
            {
                // generate default
                Me.CustomData += "[DEFAULT] \n";
                foreach (var samProperty in SAM_CONFIGURATIONS)
                {
                    Me.CustomData += $"{samProperty.Key}={samProperty.Value[2]} \n";
                }
            }

            if (! Me.CustomName.Contains("[" + SAM_V2_CONF_HELPER_TAG_NAME))
            {
                Me.CustomName += " " + "[" +  SAM_V2_CONF_HELPER_TAG_NAME + "]";
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
   
            this.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => b.IsSameConstructAs(Me));

            string outputLog = "";

            // Clear list
            samConfHelperLcdLogList.Clear();
            samConfHelperLcdList.Clear();
            samConnectors.Clear();


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
                    else if (block is IMyProgrammableBlock && !blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME) && block.IsWorking )
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

                        if (samMainConnector == null )
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
                    if (block is IMyCockpit )
                    {
                        Cockpit cockpit = new Cockpit(SAM_V2_CONF_HELPER_TAG_NAME, block as IMyCockpit);
                        if (cockpit.getLogTextSurface() != null)
                        {
                            cockpit.getLogTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                            cockpit.getLogTextSurface().Font = "Monospace";
                   
                            samConfHelperLcdLogList.Add(cockpit.getLogTextSurface());
                            Echo("Found SV2CH LOG COCKPIT LCD ... ");
                            outputLog = "Found SV2CH LOG COCKPIT LCD ... " + "\n" + outputLog;
                        }
                        if (cockpit.getConfTextSurface() != null)
                        {
                            cockpit.getConfTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                            samConfHelperLcdList.Add(cockpit.getConfTextSurface());
                            cockpit.getConfTextSurface().Font = "Monospace";

                            Echo("Found SV2CH CONF  COCKPIT LCD ... ");
                            outputLog = "Found SV2CH COCKPIT LCD ... " + "\n" + outputLog;
                        }

                    }

                    if(block is IMyRemoteControl)  {
                        if (blockHasNameOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME))
                        {
                            remotControllers.Add(block as IMyRemoteControl);
                            outputLog = "Found SV2CH RC ... " + "\n" + outputLog;
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

        public void ToggleSamMainRc()
        {
            Logger.Log("ToggleSamMainRc : ");
            // Command
            string rcName = _commandLine.Argument(1);
            ChangeMainSamRc(rcName);    
        }

        public void ChangeRcDirection()
        {
             
        }

        public void ToggleSamMainConnector()
        {
            // Command
            string connectorName = _commandLine.Argument(1);
            ChangeMainSamConnector(connectorName);
        }

        public void ChangeMainSamConnector(string connectorName)
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
            // Don't change connector if ship is connected with actual main
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
                        samMainConnector.CustomName = samMainConnector.CustomName.Replace("SAM MAIN", "SAM");
                        samMainConnector.CustomData = samMainConnector.CustomData.ToUpper().Replace("SAM.MAIN", "");
                        samMainConnector = connector;
                        connector.CustomData = connector.CustomData + "\n" + "SAM.MAIN";

                        // Change profile
                        destinationProfileManager.CurrentDestination.AddProperty("connector", connectorName);
                    }
                }
            }
        }

        public void ChangeMainSamRc(string rcName)
        {
            if (rcName == null) return;

            // Get Configured Conenctor in that direction
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            if (samRc.CustomName.ToLower().Contains(SAM_V2_CONF_HELPER_TAG_NAME.ToLower() + " " + rcName.ToLower()))
            {
                return;
            }
          
            foreach (IMyRemoteControl rc in remotControllers)
            {
                if (rc.CustomName.ToLower().Contains(rcName.ToLower()))
                {
                    if (rc != samRc)
                    {
                        Logger.Log("Switch to RC " + rc.CustomName);
                        Logger.Log("From RC : " + samRc.CustomName);

                        // Change current main RC
                        samRc.CustomName = samRc.CustomName.Replace("[SAM]", "");
                        samRc.CustomData = samRc.CustomData.ToUpper().Replace("SAM.", "");
                        samRc = rc;
                        rc.CustomData = rc.CustomData + "\n" + "SAM.";

                        // Change profile
                        destinationProfileManager.CurrentDestination.AddProperty("rc", rcName);
                    }
                }
            }
        }

        public void Refresh()
        {
            // Load Blocs ??
            // LoadBlocks();

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
            if (samPb.samPb == null) Echo("No SAM PB Found ");
            var samSettingsText = samPb.samPb.CustomData.Replace("SAM.", "");

            // Lcd Pretty printing
            StringBuilder sb = new StringBuilder();
            sb.Append("X");
            if(samConfHelperLcdList != null && samConfHelperLcdList.Count > 0)
            {
                foreach (var textSurface in samConfHelperLcdList)
                {
                    int maxLineLength = (int)(23 / textSurface.FontSize);
                    var _textToOutputFormated = PrettyPrint(samSettingsText, maxLineLength);

                    // Output per lcd 
                    textSurface.WriteText(_textToOutput, false);
                    textSurface.WriteText(_textToOutputFormated, true);
                }
            }
            _textToOutput = "";
            if (samMainConnector == null) Echo("No SAM Connector Found ");
            // Main Connector
            if (samMainConnector != null)
            {
                _textToOutput += "Connector : " + samMainConnector.CustomName + "\n";
            }else
            {
                _textToOutput += "No SAM Connector Found " + "\n";
            }

            // RC
            if (samRc != null)
            {
                _textToOutput += "RC : " + samRc.CustomName + "\n";
            }
            else
            {
                _textToOutput += "No RC Found " + "\n";
            }
 

            // Destination
            var samLogText = "";
            if (samLoglcd != null) samLogText = samLoglcd.GetText();
            if (samCockpit != null) samLogText = samCockpit.getLogTextSurface().GetText();
           
            _textToOutput += "Going to : " + SamLogParser.GetDestination(samLogText);

            // Output
            if (samConfHelperLcdList != null && samConfHelperLcdList.Count > 0)
            {
                foreach (var textSurface in samConfHelperLcdList)
                {
                    textSurface.WriteText(_textToOutput, true);
                }
            } else
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
            if( destinationProfileManager.CurrentDestination != null)
            {
                destinationProfileManager.CurrentDestination.AddProperty(key, targetValue);
            }
            else
            {
                samPb.SetSamPbProperty(key, targetValue);
            }

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

            // Check if profile exists
            
            DestinationProfile destinationProfile = destinationProfileManager.LoadDestinationProfile(Me.CustomData, destination);

            if (destinationProfile == null && useDefaultProfile)
            {
                destinationProfile = destinationProfileManager.LoadDestinationProfile(Me.CustomData, "DEFAULT");
            }

            if (destinationProfile == null ) { return; }


            // Apply Destination Profile
            foreach (var key in destinationProfile.properties.Keys.ToList())
            {
                if (SAM_CONFIGURATIONS.ContainsKey(key))
                {
                    samPb.SetSamPbProperty(key.ToString(), destinationProfile.properties[key]);
                }
            }

            samPb.WriteSamPbPropertiesToPb();

            // Change connector if needed
            if (destinationProfile.properties.ContainsKey("connector"))
            {
                ChangeMainSamConnector(destinationProfile.properties["connector"]);
            }

            // Chage Remot controller if needed
            if (destinationProfile.properties.ContainsKey("rc"))
            {
                ChangeMainSamRc(destinationProfile.properties["rc"]);
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

            public static void Log(string log) { logText.Add(log); }

            public static void Output(List<IMyTextSurface> textSurfaceList)
            {

                if (textSurfaceList != null && textSurfaceList.Count > 0 )
                {

                    string outputString = "";
                 
                    foreach (string line in logText)
                    {
                        outputString = line + "\n" + outputString;
                    }

                    foreach (IMyTextSurface textSurface in textSurfaceList)
                    {
                        textSurface.WriteText(outputString, false);
                    }

                    if (logText.Count > 30)
                    {
                        logText.RemoveRange(29,logText.Count()-1 );
                    }
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
        public   void Debug(string text)
        {
            Echo("d :" + text);
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
                            if (SAM_CONFIGURATIONS.ContainsKey(key) && SAM_CONFIGURATIONS[key].Equals("false")){
                                this.SetSamPbProperty(key, keyValue[1]);
                            }
                           
                        }
                        // Exclusive Tag
                        else
                        {
                            string key = line.ToLower().Replace(SAM_CD_TAG.ToLower(), "");
                            if (SAM_CONFIGURATIONS.ContainsKey(key) && SAM_CONFIGURATIONS[key].Equals("true")){
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
                    samProperty.Value = value.Trim();
                }
                else
                {
                    SamProperty samProperty = new SamProperty(ConfigurationType.CD, key.Trim(), value.Trim());
                    samProperties.Add(key.Trim(), samProperty);
                }
            }

            public void WriteSamPbPropertiesToPb()
            {
                // Current CD
                string[] lines = samPb.CustomData.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                // New CD
                string newSamPbCustomData = "SAM. \n";

                foreach(var samPropery in this.samProperties)
                {
                    // Exclusive tags
                    if ("true".Equals(samPropery.Value.Value))
                    {
                        newSamPbCustomData += SAM_CD_TAG + samPropery.Key + "\n";
                        continue;
                    }
                    // Key Value Tag
                    if (! "false".Equals(samPropery.Value.Value))
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

