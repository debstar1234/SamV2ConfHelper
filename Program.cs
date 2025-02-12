using EmptyKeys.UserInterface.Generated.StoreBlockView_Bindings;
using Microsoft.Build.Utilities;
using Microsoft.IO;
using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
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
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Configuration;
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
        #region mdk preserve
        // Version
        static string VERSION = "1.0.0";

        //===== TAG Configurations =====//
        // ME TAG
        static string SAM_V2_CONF_HELPER_TAG_NAME = "XSamHelper";
        static string SAM_V2_CONF_HELPER_CD_TAG = SAM_V2_CONF_HELPER_TAG_NAME + ".";

        // SAM Values
        static string SAM_TAG_NAME = "SAM";
        static string SAM_CD_TAG = SAM_TAG_NAME + ".";

        //===== SHIP Configurations =====//
        // SHIP NAME
        static string SHIP_NAME = "";

        // Auto Update Sam Configuration from profile 
        static Boolean AutoUpdateSamFromProfile = true;

        //===== LOG Configurations =====//
        // MAX Log entries
        static int MAX_LOG_ENTRIES = 15;

        //===== HELPER Configurations =====//
        // Auto Scan Block
        static bool AutoRescanBlocks = true;
         
        // Extra Ticck per update (1 tick = 160 ms)
        static int ExtraTickPerUpdate = 1 ;

        // Unknown Signal 
        static string US_DROP_POSITION = "US_DROP_POSITION";

        //===== SPECIFIC MERGE STUFF Configurations =====//
        // Toggle Remove SAM RC when Merged
        Boolean AutoRemoveSamRCWhenMerged = true;

        // Toggle Ship Rename
        Boolean AutoRenameShipWhenUnmerged = true;


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
                { "Wait", new [] { "wait", "false", "20","1" } },
                { "NODAMPENERS", new [] { "nd", "true", "false","1" } },
                { "IGNOREGRAVITY", new [] { "ig", "true", "false","1" } }
            };


        // DO NOT EDIT AFTER THIS :)
        #endregion

        // Init
        MyCommandLine _commandLine = new MyCommandLine();
        Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        MyIni _ini = new MyIni();

        static SamConfHelperBlocks samConfHelperBlocks = new SamConfHelperBlocks();
        static LogPrinter logPrinter;
        static DestinationProfileManager destinationProfileManager = null;


        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        int tickCounter = 0;

        public Program()
        {
            // 

            samConfHelperBlocks.PPPPP = this;

            // Command Lists
            _commands["setUp"] = SetUp;
            _commands["reload"] = LoadBlocks;

            _commands["setSamMainConnector"] = SetSamMainConnector;
            _commands["con"] = SetSamMainConnector;

            _commands["toggleSamMainRc"] = SetSamMainRc;
            _commands["rc"] = SetSamMainRc;

            _commands["refresh"] = Refresh;

            _commands["loadProfile"] = LoadProfile;
            _commands["profile"] = LoadProfile;

            _commands["SetSorterItemType"] = SetSorterItemType;

            _commands["run"] = AddWaypoints;
            _commands["runUs"] = RunUnknownSignal;
            _commands["autopilot"] = Autopilot;
            _commands["rtb"] = ReturnToBase;

            _commands["dumpItems"] = TestGetAllItemDefinitions;
            _commands["checkIIMSpecial"] = TestIIMSpecialContainer;
            _commands["land"] = TestLand;




            // Value modifier
            foreach (var entry in SAM_CONFIGURATIONS)
            {
                _commands[entry.Key] = () => SetSamValue(entry.Key);
                _commands[entry.Value[0]] = () => SetSamValue(entry.Key);
            }

            _commands["mode"] = SetSamMode;

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

            // TIck counter
          
            if ((updateType & UpdateType.Update100) == UpdateType.Update100)
            {
                tickCounter++;
                if(tickCounter > ExtraTickPerUpdate)
                {
                    Refresh();
                    tickCounter = 0;
                }
            }
        }

        public void SetUp()
        {
            Logger.Info("SetUp ... ");
            LoadBlocks();
            Logger.Info("SetUp ... Done");

        }

        public void Refresh()
        {
            // Re Load Blocs ??
            // if(AutoRescanBlocks) LoadBlocks();

            // Load profile
            LoadProfile();

            // Check Merged Blocks
            CheckMergeBlocks();

            // Check Connected Connectors
            CheckConnectors();

            // Output settings to LCD
            DisplaySamPbConf();

            // Autopilot
            Autopilot();

            // Output log
            logPrinter.print(Logger.PrintBuffer());

        }

        // Command 
        public void LoadBlocks()
        {
            // Clear lists
            blocks.Clear();
            logPrinter = new LogPrinter(this);

            this.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => b.IsSameConstructAs(Me));

            samConfHelperBlocks.ScanBlocks(blocks);
            logPrinter.clear();

            Boolean result = samConfHelperBlocks.CheckBlocks();
            foreach(var textSurface in samConfHelperBlocks.samConfHelperLcdLogList)
            {
                logPrinter.AddTextSurface(textSurface);
            }

            // Init objects
            destinationProfileManager = new DestinationProfileManager(Me);

            // Ship name
            if (SHIP_NAME == "") SHIP_NAME = Me.CubeGrid.CustomName;

        }

        // Command  
        public void SetSamMainRc()
        {
            Logger.Info("SetSamMainRc : ");
            // Command
            string rcName = _commandLine.Argument(1);
            ChangeMainSamRc(rcName);
        }

        // Command  
        public void SetSamMainConnector()
        {
            Logger.Info("SetSamMainConnector : ");
            // Command
            string connectorName = _commandLine.Argument(1);
            ConnectorManager.ChangeMainSamConnector(connectorName);
        }

        
        public void UnknownSignalRetriever()
        {

        }

        public void ReturnToBase()
        {
            var destinationProfile = destinationProfileManager.LoadDestinationProfile("DEFAULT");
            if (destinationProfile.properties.ContainsKey("HomeConnector"))
            {
                string homeConnector = destinationProfile.properties["HomeConnector"];
                if(homeConnector != "")
                {
                    samConfHelperBlocks.samPb.samPb.TryRun("GO " + homeConnector);
                    return;
                }
            }
            Logger.Info("No Home Connector was set");
        }

        public void ChangeMainSamRc(string rcName)
        {
            if (rcName == null) return;

            // Get Configured Conenctor in that direction
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            if (samConfHelperBlocks.samRc.CustomName.ToLower().StartsWith(rcName.ToLower()))
            {
                return;
            }

            foreach (IMyRemoteControl rc in samConfHelperBlocks.remotControllers)
            {
                if (rc.CustomName.ToLower().StartsWith(rcName.ToLower()) || rc.CustomName.ToLower().Trim().Contains(SAM_V2_CONF_HELPER_TAG_NAME + " " + rcName.ToLower()))
                {
                    if (rc != samConfHelperBlocks.samRc)
                    {
                        Logger.Info("Switch to RC " + rc.CustomName);
                        Logger.Info("From RC : " + samConfHelperBlocks.samRc.CustomName);

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
        public static bool IsBlockWithNameOrHasTagNameOrCdName(IMyTerminalBlock block, string tagName, string cdName, string blockName)
        {
            return block.CustomName.ToUpper().StartsWith(blockName.ToUpper())  // Name
                 || block.CustomName.ToUpper().Trim().Contains(tagName.ToUpper() + " NAME=" + blockName.ToUpper()) // TAG
                 || block.CustomData.ToUpper().Trim().Contains(cdName.ToUpper() + "NAME=" + blockName.ToUpper()); // CD 
        }
        
        public void TriggerTimer(string timerName)
        {

            if (timerName == null) return;

            foreach (IMyTimerBlock timer in samConfHelperBlocks.samConfHelperTimers)
            {

                if (IsBlockWithNameOrHasTagNameOrCdName(timer, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, timerName))
                {
                    if (destinationProfileManager.CurrentDestination.properties.ContainsKey("timer") && destinationProfileManager.CurrentDestination.properties.ContainsKey("connector"))
                    {

                        // Check Profile
                        string currentDestinationTimer = destinationProfileManager.CurrentDestination.properties["timer"];
                        string currentDestinationConnector = destinationProfileManager.CurrentDestination.properties["connector"];

                        Logger.Info("Start timer " + timer.CustomName);
                        if (!destinationProfileManager.CurrentDestination.timerTriggered)
                        {
                            timer.StartCountdown();
                            destinationProfileManager.CurrentDestination.timerTriggered = true;
                        }
                    }


                }
            }
        }

        // 

        public void CheckConnectors()
        {
            if (samConfHelperBlocks.samMainConnector.IsConnected)
            {
                // Other connector
                IMyShipConnector otherConnector = samConfHelperBlocks.samMainConnector.OtherConnector;

                // Current destination
                string destination = destinationProfileManager.CurrentDestination.destination;

                // timer
                bool timerTriggered = destinationProfileManager.CurrentDestination.timerTriggered;


                // Check if current destination = other connector sam name
                if (!timerTriggered && otherConnector.CustomName.ToLower().Contains(destination.ToLower())
                    || otherConnector.CustomData.ToLower().Contains(destination.ToLower()))
                {

                    if (destinationProfileManager.CurrentDestination.properties.ContainsKey("timer"))
                    {
                        string timerName = destinationProfileManager.CurrentDestination.properties["timer"];

                        TriggerTimer(timerName);
                    }

                }
            }
            else
            {
                destinationProfileManager.CurrentDestination.timerTriggered = false;
            }

        }

        public void CheckMergeBlocks()
        {

            if (!AutoRemoveSamRCWhenMerged || samConfHelperBlocks.samConfHelperMergeBLocks.Count() < 1) return;

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
                    if (AutoRenameShipWhenUnmerged && !Me.CubeGrid.CustomName.Equals(SHIP_NAME)) Me.CubeGrid.CustomName = SHIP_NAME;

                    samConfHelperBlocks.samRc.CustomData = SAM_CD_TAG;
                    samConfHelperBlocks.samPb.samPb.Enabled = true;
                }
            }
        }

        public void DisplaySamPbConf()
        {
            var _textToOutput = "";
            Animation.Run();
            _textToOutput += "SamV2 Helper " + VERSION + " " + Animation.Rotator() + '\n';

            // SAM Settings
            var samSettingsText = samConfHelperBlocks.samPb.samPb.CustomData.Replace(SAM_CD_TAG, "");        

            // Lcd Pretty printing
            StringBuilder sb = new StringBuilder();
            sb.Append("X");
            if (samConfHelperBlocks.samConfHelperLcdList != null && samConfHelperBlocks.samConfHelperLcdList.Count > 0)
            {
                foreach (var textSurface in samConfHelperBlocks.samConfHelperLcdList)
                {
                    var _textToOutputFormated = PrettyPrint(samSettingsText);

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

            // SAM MODE
            _textToOutput += "SAM Mode : " + samConfHelperBlocks.samPb.getMode() + "\n";

            // Main Connector
            if (samConfHelperBlocks.samMainConnector != null)
            {
                _textToOutput += "[C] : " + samConfHelperBlocks.samMainConnector.CustomName + "\n";
            }

            // RC
            if (samConfHelperBlocks.samRc != null)
            {
                _textToOutput += "RC : " + samConfHelperBlocks.samRc.CustomName + "\n";
            }

            // Timer
            if (destinationProfileManager.CurrentDestination.properties.ContainsKey("timer"))
            {
                _textToOutput += "[T] : " + destinationProfileManager.CurrentDestination.properties["timer"] + "\n";
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
                Echo(_textToOutput);
            }

        }

        private string PrettyPrint(string buffer)
        {
            string[] lines = buffer.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string output = "";
            var maxLineLength = 0;

            // Get all keys
            // Iterate through the key-value pairs and print them
            foreach (var line in lines)
            {
                if (String.IsNullOrEmpty(line)) continue;
                string key = line;
                string value = "";
                if (line.Contains("="))
                {
                    string[] keyValue = line.Split(new[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                    key = keyValue[0].Trim();
                    if (keyValue.Length > 1) value = keyValue[1].Trim();

                    if (maxLineLength < key.Length + value.Length + 5)
                    {
                        maxLineLength = key.Length + value.Length + 5;
                    }
                }
            }

                // Iterate through the key-value pairs and print them
                foreach (var line in lines)
            {
                if (String.IsNullOrEmpty(line)) continue;
                string key = line;
                string value = "";
                string formattedLine = "";
                if (line.Contains("="))
                {
                    string[] keyValue = line.Split(new[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                    key = keyValue[0].Trim();
                    if (keyValue.Length > 1) value = keyValue[1].Trim();
                    int currentLength = key.Length + value.Length;
                    int spacesCounter = maxLineLength - currentLength;

                    string spaces = new string('.', spacesCounter);
                      formattedLine = ($"{key} {spaces} {value}");
                }
                else
                {
                      formattedLine = ($"{line}");
                }

               
                // Create a formatted string with left-aligned keys and right-aligned values

                
                output += formattedLine + "\n";
            }
            return output;
        }

        public void SetSamMode()
        {
            int argumentCount = _commandLine.ArgumentCount;
            string mode = "";
            if (argumentCount > 1)
            {
                mode = _commandLine.Argument(1).Trim();
            }
            if (mode.ToUpper().Equals("CYCLE"))
            {
                mode = SamModeCycle.Cycle();
            }
            samConfHelperBlocks.samPb.SetMode(mode);
        }

        List<string> waypoints = new List<string>();
        int currentWaypointIndex = 0;
        bool autopilotIsRunning = false;


        public void RunUnknownSignal()
        {
            autopilotIsRunning = false;
            waypoints.Clear();
            currentWaypointIndex = 0;

            int argumentCount = _commandLine.ArgumentCount;
            if (argumentCount > 0)
            {
                // First waypoint
                waypoints.Add(US_DROP_POSITION);

                // Second waypoint
                string waypoint = _commandLine.Argument(1).Trim();
                waypoints.Add(waypoint);

            }
        }

        public void AddWaypoints()
        {
            autopilotIsRunning = false;
            waypoints.Clear();
            currentWaypointIndex = 0;

            int argumentCount = _commandLine.ArgumentCount;
            if (argumentCount > 0)
            {
                for (int argCounter = 1; argCounter < argumentCount; argCounter++)
                {
                    string waypoint = _commandLine.Argument(argCounter).Trim();
                    // samConfHelperBlocks.samPb.samPb.TryRun("add " + waypoint);
                    // waypoints.Add(waypoint.Split(':')[1]);
                    waypoints.Add(waypoint);
                }
            }
        }

        int autopilotTickDelay = 0;
        public void Autopilot()
        {
            autopilotTickDelay++;

            if (waypoints.Count == 0)
            {
                return;
            }

            // If not running
            if (!autopilotIsRunning)
            {
                autopilotIsRunning = true;
                string waypoint = waypoints[currentWaypointIndex];

                if (waypoint.StartsWith("GPS:"))
                {
                    samConfHelperBlocks.samPb.samPb.TryRun("start " + waypoints[currentWaypointIndex]);
                }
                else
                {
                    samConfHelperBlocks.samPb.samPb.TryRun("go " + waypoints[currentWaypointIndex]);
                }

                currentWaypointIndex++;
                autopilotTickDelay = 0;
                return;

            }
            // Next waypoint
            if (autopilotTickDelay > 10 && autopilotIsRunning && samConfHelperBlocks.logLcd.IsNavigationSuccessful())
            {
                autopilotIsRunning = false;

                // Autopilot finished
                if (currentWaypointIndex >= waypoints.Count())
                {
                    currentWaypointIndex = 0;
                    waypoints.Clear();
                }
            }


        }

        public void SetSamValue(string key)
        {
            string targetValue = _commandLine.Argument(1);
            if (targetValue != null)
            {
                samConfHelperBlocks.samPb.SetSamTargetValue(key, targetValue,destinationProfileManager.CurrentDestination);
            }
        }

        public void SetSorterItemType()
        {
            string item = null;
            string type = null;

            if (_commandLine.ArgumentCount > 0)
                item = _commandLine.Argument(1);
            if (_commandLine.ArgumentCount > 1)
                type = _commandLine.Argument(2);

            SetSorterWhiteList(item, type);
        }


        public void LoadProfile()
        {
            // Get destination / profile From command line
            string destination = _commandLine.Argument(1);

            // Not a Manual Command
            if (!AutoUpdateSamFromProfile && destination == null) return;

            // Or else Get from SAM Log
            if (destination == null)
            {
                destination = samConfHelperBlocks.logLcd.GetDestination();
            }

            // Check if profile exists
            DestinationProfile destinationProfile = destinationProfileManager.LoadDestinationProfile(destination);

            destinationProfileManager.applyCurrentDestinationProfileToSamPb(samConfHelperBlocks.samPb);

            // Change connector if needed
            if (destinationProfile.properties.ContainsKey("connector"))
            {
                ConnectorManager.ChangeMainSamConnector(destinationProfile.properties["connector"]);
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

        public void SetSorterWhiteList(string item, string type)
        {
            var mode = MyConveyorSorterMode.Whitelist;
            List<MyInventoryItemFilter> filterlist = new List<MyInventoryItemFilter>();
            if (type != null && item != null)
            {
                MyItemType iType = new MyItemType("MyObjectBuilder_" + item, type);
                MyInventoryItemFilter UI = new MyInventoryItemFilter(iType);
                filterlist.Add(UI);
            }
            else if (item != null && type == null)
            {
                MyItemType iType = new MyItemType("MyObjectBuilder_" + item, "");
                MyInventoryItemFilter UI = new MyInventoryItemFilter(iType, true);
                filterlist.Add(UI);
            }
            else
            {
                mode = MyConveyorSorterMode.Blacklist;
            }

            foreach (IMyConveyorSorter sorter in samConfHelperBlocks.samConfHelperSorters)
            {
                if (sorter.CustomData == null || sorter.CustomData == "")
                {

                    sorter.SetFilter(mode, filterlist);
                }
            }
        }

        //     var DUMP_LCD = GridTerminalSystem.GetBlockWithName("DUMP_LCD") as IMyTextPanel;


        public void TestGetAllItemDefinitions()
        {
            ///////
            var DUMP_LCD = GridTerminalSystem.GetBlockWithName("DUMP_LCD") as IMyTextPanel;

            var output = new StringBuilder();
            var itemDefinitions = GetAllItemDefinitions();

            foreach (var item in itemDefinitions)
            {
                output.AppendLine($"{item}");
            }
            DUMP_LCD.WriteText(output.ToString(), false);

        }

        public List<string> GetAllItemDefinitions()
        {
            var itemDefinitions = new List<string>();

            List<IMyCargoContainer> blocks = new List<IMyCargoContainer>();

            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(blocks); 


            foreach (var block in blocks)
            {
                var inventory = block.GetInventory();
                if (inventory == null) continue;

                var listItems = new List<MyItemType>();
                inventory.GetAcceptedItems(listItems);

                foreach (var item in listItems)
                {
                    var itemDefinition = item.ToString().Replace("MyObjectBuilder_", "");
                    if (!itemDefinitions.Contains(itemDefinition))
                    {
                        itemDefinitions.Add(itemDefinition);
                    }
                }

            }
            return itemDefinitions;
        }


        public void TestIIMSpecialContainer()
        {
            IMyCargoContainer iimSpecialContainer = samConfHelperBlocks.iimSpecialCargoContainer.First();

            //Init
            var requiredItems = new Dictionary<string, int>();
            var currentInventory = new Dictionary<string, int>();
            var inventory = iimSpecialContainer.GetInventory();
            var items = new List<MyInventoryItem>();


            // build current inventory needed items
            string iimContainerCD = iimSpecialContainer.CustomData;
            string[] lines = iimContainerCD.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                // Only some lines are required
                if (!line.Contains("=") || !line.StartsWith("Component") && !line.StartsWith("Ore") && !line.StartsWith("Ingot"))
                {
                    continue;
                }
                // Count
                string[] requiredItemDefinition = line.Split('=');
                int amount = 0;
                int.TryParse(requiredItemDefinition[1], out amount);

                if (amount > 0)
                {
                    requiredItems[requiredItemDefinition[0]] = amount;
                }
            }


            // items
            inventory.GetItems(items);

            // Count
            foreach (var item in items)
            {
                var currentItemName = ((MyDefinitionId)item.Type).ToString().Replace("MyObjectBuilder_", "");
                var currentItemAmount = (int)item.Amount;

                if (currentInventory.ContainsKey(currentItemName))
                {
                    currentInventory[currentItemName] += currentItemAmount;
                }
                else
                {
                    currentInventory[currentItemName] = currentItemAmount;
                }
            }
            StringBuilder output = new StringBuilder();



            // Check if the current inventory meets the required amounts
            int totalRequiredItem = 0;
            foreach (var requiredItem in requiredItems)
            {
                var itemName = requiredItem.Key;
                var requiredAmount = requiredItem.Value;
                int currentAmount;
                if (currentInventory.TryGetValue(itemName, out currentAmount))
                {
                    if (currentAmount >= requiredAmount)
                    {
                        output.AppendLine($"{itemName}: Sufficient ({currentAmount}/{requiredAmount})");
                        totalRequiredItem++;
                    }
                    else
                    {
                        output.AppendLine($"{itemName}: Insufficient ({currentAmount}/{requiredAmount})");
                    }
                }
                else
                {
                    output.AppendLine($"{itemName}: Missing (0/{requiredAmount})");
                }
            }
            if (totalRequiredItem == requiredItems.Count())
            {
                output.AppendLine("All items OK");
            }

            Echo(output.ToString());           
        }

        public void TestLand()
        {
            var gps = CalculateOrbit("1");
            samConfHelperBlocks.samPb.samPb.TryRun("start " + gps);
        }

        public  string CalculateOrbit(string arg)
        {
            double altitude; if (!double.TryParse(arg, out altitude))
            {
                Echo(
        "Unable to parse altitude in meters. Example: \"ORBIT 11000\""); return null ;
            }
            Vector3D orbit; try { orbit = GetOrbit(this, altitude); }
            catch (Exception e)
            {
                Echo("Unable to calculate orbit: " +
        e.Message); return null ;
            }
            Echo("Orbit set in Custom Data");
            return GenerateGPS("orbit", orbit);
        }


        static Vector3D GetOrbit(Program p, double
                altitude)
        {
            List<IMyShipController> shipControllers = new List<IMyShipController>(); p.GridTerminalSystem.GetBlocksOfType<
                    IMyShipController>(shipControllers); foreach (IMyShipController shipController in shipControllers)
            {
                if (!shipController.IsSameConstructAs(p.
                    Me)) { continue; }
                Vector3D planetCenter; if (!shipController.TryGetPlanetPosition(out planetCenter))
                {
                    throw new Exception(
                    "Not in gravity?");
                }
                double currentAltitude; if (!shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out currentAltitude))
                {
                    throw
                    new Exception("Not in gravity?");
                }
                Vector3D currentPosition = shipController.GetPosition(); return (altitude - currentAltitude) *
                    Vector3D.Normalize(currentPosition - planetCenter) + currentPosition;
            }
            throw new Exception("No RCs or Cockpits?");
        }

        static string GenerateGPS(string name, Vector3D v, string color = "FF00FF")
        {
            bool gpsfull = true;
            return string.Format("GPS:{0}:{1}:{2}:{3}:#{4}:", name, gpsfull ? v.X : Math.Round(v.X, 4), gpsfull ? v.Y : Math.Round(v.Y, 4), gpsfull ? v.Z : Math.Round(v.Z, 4), color); }

        /// <summary>
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        /// 

        internal class SAMController
        {
            public void Run(string command)
            {
                samConfHelperBlocks.samPb.samPb.TryRun(command);
            }

        }

        internal class ConnectorManager
        {
            public static void ChangeMainSamConnector(string connectorName)
            {
                if (connectorName == null) return;

                if (samConfHelperBlocks.samConnectors.Count == 1
                    && (samConfHelperBlocks.samMainConnector.CustomName.ToLower().StartsWith(connectorName.ToLower())
                    || samConfHelperBlocks.samMainConnector.CustomName.ToLower().Contains(connectorName.ToLower()))
                    )
                {
                    return;
                }
                // Don't change connector if ship is connected with actual main
                if (samConfHelperBlocks.samMainConnector.IsConnected) return;

                foreach (IMyShipConnector connector in samConfHelperBlocks.samConnectors)
                {
                    if (IsBlockWithNameOrHasTagNameOrCdName(connector, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, connectorName))
                    {
                        if (connector != samConfHelperBlocks.samMainConnector || !connector.CustomData.ToUpper().Contains(SAM_CD_TAG + "MAIN"))
                        {
                            Logger.Info("-->[C] " + connector.CustomName);
                            Logger.Info("<--[C] " + samConfHelperBlocks.samMainConnector.CustomName);

                            // Change current main connector
                            samConfHelperBlocks.samMainConnector.CustomName = samConfHelperBlocks.samMainConnector.CustomName.Replace(SAM_TAG_NAME + " MAIN", SAM_TAG_NAME);
                            samConfHelperBlocks.samMainConnector.CustomData = samConfHelperBlocks.samMainConnector.CustomData.ToUpper().Replace(SAM_CD_TAG + "MAIN", "");
                            samConfHelperBlocks.samMainConnector = connector;
                            samConfHelperBlocks.samMainConnector.CustomData += SAM_CD_TAG + "\n" + SAM_CD_TAG + "MAIN";

                            // Change profile
                            destinationProfileManager.CurrentDestination.AddProperty("connector", connectorName);
                        }
                    }
                }
            }
        }
        class SamConfHelperBlocks
        {
            // SAM CONF HELPER 
            public List<IMyTextSurface> samConfHelperLcdList = new List<IMyTextSurface>();
            public List<IMyTextSurface> samConfHelperLcdLogList = new List<IMyTextSurface>();
            public List<IMyShipConnector> samConnectors = new List<IMyShipConnector>();
            public List<IMyRemoteControl> remotControllers = new List<IMyRemoteControl>();
            public List<IMyShipMergeBlock> samConfHelperMergeBLocks = new List<IMyShipMergeBlock>();
            public List<IMyConveyorSorter> samConfHelperSorters = new List<IMyConveyorSorter>();
            public List<IMyTimerBlock> samConfHelperTimers = new List<IMyTimerBlock>();
            public List<IMyCargoContainer> iimSpecialCargoContainer = new List<IMyCargoContainer>();
            public Program PPPPP { set; get; }

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


                // Iterate through all blocks and find the ones with the specified tag
                foreach (var block in blocks)
                {
                    if (IsBlockMatchesTagOrCd(block, SAM_TAG_NAME, SAM_CD_TAG))
                    {
                        if (block is IMyRemoteControl)
                        {
                            samRc = block as IMyRemoteControl;
                            Logger.Info("Found SAM RC ... ");
                        }
                        else if (block is IMyTextPanel)
                        {
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_TAG_NAME, SAM_CD_TAG, "LOG"))
                            {
                                samLoglcd = block as IMyTextPanel;
                                Logger.Info(  "Found SAM LOG LCD ... ");
                            }
                        }
                        else if (block is IMyCockpit)
                        {
                            Logger.Info( "Found COCKPIT ... ");
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_TAG_NAME, SAM_CD_TAG, "LOG"))
                            {
                                samCockpit = new Cockpit(SAM_TAG_NAME,SAM_CD_TAG, block as IMyCockpit);
                                Logger.Info( "Found SAM LOG COCKPIT ... ");
                            }
                        }
                        else if (block is IMyProgrammableBlock && block.IsWorking)
                        {
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_TAG_NAME, SAM_CD_TAG, ""))
                            {
                                samPb = new SamPb(block as IMyProgrammableBlock);
                                Logger.Info("Found SAM PB ... ");
                            }
                                
                        }
                        else if (block is IMyShipConnector)
                        {
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_TAG_NAME, SAM_CD_TAG, "MAIN"))
                            {
                                samMainConnector = block as IMyShipConnector;
                                samConnectors.Add(block as IMyShipConnector);
                                Logger.Info( "Found SAM MAIN Connector ... " );
                            }
                            else
                            {
                                Logger.Info(  "Found Connector ... ");
                                samConnectors.Add(block as IMyShipConnector);
                            }

                            if (samMainConnector == null)
                            {
                                samMainConnector = block as IMyShipConnector;
                                Logger.Info(  "Found Connector ... ");
                            }


                        }
                    }

                    // RC
                    if (block is IMyRemoteControl)
                    {
                        remotControllers.Add(block as IMyRemoteControl);
                        Logger.Info("Found  RC ... ");
                    }

                    // Sorters
                    if (block is IMyConveyorSorter)
                    {
                        samConfHelperSorters.Add(block as IMyConveyorSorter);
                        Logger.Info("Found  Sorter ... ") ;
                    }

                    // Timers
                    if (block is IMyTimerBlock)
                    {
                        samConfHelperTimers.Add(block as IMyTimerBlock);
                        Logger.Info("Found  Timer ... ");
                    }

                    //
                    if (IsBlockMatchesTagOrCd(block, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG))
                    {
                        if (block is IMyTextPanel)
                        {
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, "LOG"))
                            {
                                IMyTextSurface textSurface = block as IMyTextSurface;

                                textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
                                textSurface.Font = "Monospace";

                                samConfHelperLcdLogList.Add(block as IMyTextSurface);
                                Logger.Info( "Found " + SAM_V2_CONF_HELPER_TAG_NAME + "LOG LCD ... ");
                            }
                            else
                            {
                                if (IsBlockMatchTagOrCdAttributes(block, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, ""))
                                {
                                    IMyTextSurface textSurface = block as IMyTextSurface;
                                    textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
                                    textSurface.Font = "Monospace";
     
                                    samConfHelperLcdList.Add(block as IMyTextPanel);
                                    Logger.Info( "Found " + SAM_V2_CONF_HELPER_TAG_NAME + " LCD ... ");
                                }
                            }

                        }
                        if (block is IMyCockpit)
                        {
                            Logger.Info($"Found COCKPIT for {SAM_V2_CONF_HELPER_TAG_NAME}... ") ;
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, "PANEL"))
                            {
                                Cockpit cockpit = new Cockpit(SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, block as IMyCockpit);
                                if (cockpit.getLogTextSurface() != null)
                                {
                                    cockpit.getLogTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                                    cockpit.getLogTextSurface().Font = "Monospace";

                                    samConfHelperLcdLogList.Add(cockpit.getLogTextSurface());

                                    Logger.Info("Found " + SAM_V2_CONF_HELPER_TAG_NAME + " LOG COCKPIT LCD ... ");
                                }
                                if (cockpit.getConfTextSurface() != null)
                                {
                                    cockpit.getConfTextSurface().ContentType = ContentType.TEXT_AND_IMAGE;
                                    samConfHelperLcdList.Add(cockpit.getConfTextSurface());
                                    cockpit.getConfTextSurface().Font = "Monospace";
                                    Logger.Info("Found " + SAM_V2_CONF_HELPER_TAG_NAME + " COCKPIT LCD ... ");
                                }
                            }
                                

                        }

                        if (block is IMyShipMergeBlock)
                        {
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, ""))
                            {
                                samConfHelperMergeBLocks.Add(block as IMyShipMergeBlock);
                                Logger.Info("Found " + SAM_V2_CONF_HELPER_TAG_NAME + " Merge Blocks ... ");
                            }
                        }

                        if (block is IMyCargoContainer)
                        {
                            if (IsBlockMatchTagOrCdAttributes(block, SAM_V2_CONF_HELPER_TAG_NAME, SAM_V2_CONF_HELPER_CD_TAG, "SPECIAL"))
                            {
                                    iimSpecialCargoContainer.Add(block as IMyCargoContainer);                              
                            }
                        }

                    }
                }
                logLcd = new LogLcd(samLoglcd, samCockpit);

                Logger.Info("LoadBlocks ... Done");

            }

            public Boolean CheckBlocks()
            {
                // Minimum
                if (samPb == null) { Logger.Err("No SAM PB found"); return false; }
                if (samLoglcd == null && samCockpit == null) { Logger.Err("No SAM LOG LCD Or Cockpit found"); return false; }

                if (samRc == null) { Logger.Err("No SAM RC found"); return false; }
                if (samMainConnector == null) { Logger.Err("No SAM MAIN Connector found"); return false; }

                //
                if (samConfHelperLcdLogList == null || samConfHelperLcdLogList.Count < 1) Logger.Warn("No " + SAM_V2_CONF_HELPER_TAG_NAME + "LOG LCD found");
                if (samConfHelperLcdList == null || samConfHelperLcdList.Count < 1) Logger.Warn("No " + SAM_V2_CONF_HELPER_TAG_NAME + " LCD found");

                return true;
            }

            public void Clear()
            {
                samConfHelperLcdLogList.Clear();
                samConfHelperLcdList.Clear();
                samConnectors.Clear();
                remotControllers.Clear();
                samConfHelperMergeBLocks.Clear();
                samConfHelperTimers.Clear();
                samConfHelperSorters.Clear();
                samConfProperties.Clear();
                iimSpecialCargoContainer.Clear();
            }

            public bool IsBlockMatchTagOrCdAttributes(IMyTerminalBlock block, string tag, string cd , string attribute)
            {
                 // Check TAG
                var customName = block.CustomName.ToUpper();
                var tagPattern = "[" + tag;
                tagPattern = tagPattern.ToUpper();
                var attributePattern = attribute.ToUpper();

                // Find the start of the tag
                int startIndex = customName.IndexOf(tagPattern);
                if (startIndex != -1)
                {
                    // Find the end of the tag
                    startIndex += tagPattern.Length;
                    int endIndex = customName.IndexOf(']', startIndex);
                    if (endIndex != -1)
                    {
                        // Extract the tag
                        var tagText = customName.Substring(startIndex, endIndex - startIndex);

                        // Test attribute 
                        if (String.IsNullOrEmpty(attributePattern) || tagText.Contains(attributePattern)) return true;
                    }
                }

                // Check CD
                var customData = block.CustomData.ToUpper();
                var cdPattern = cd.ToUpper();

              
                string[] lines = customData.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (line.StartsWith(cdPattern))
                    {
                        if(String.IsNullOrEmpty(attributePattern) || line.Contains(attributePattern))
                        {
                            return true;
                        }
                    }
                }
               
                return false;
            }

            public static Boolean IsBlockMatchesTagOrCd(IMyTerminalBlock block, string tag, string cd)
            {
                return block != null && (block.CustomName.ToLower().Contains("[" + tag.ToLower()) 
                    || block.CustomData.ToLower().Contains(cd.ToLower())) ;
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
            private IMyShipConnector connector;
  
            public SamConnector(IMyShipConnector connector)
            {
                this.connector = connector;
            }
 

            public bool IsMain()
            {
                return connector.CustomName.ToUpper().Trim().Contains("SAM MAIN") || connector.CustomData.ToUpper().Trim().Contains("SAM.MAIN");
            }
            
            public string getShortName()
            {
                return "";
            }

        }

        internal class CustomName
        {
            string tag;
            string pattern;
            public string tagText { set; get; }
            public string realName { set; get; }

            public Dictionary<string, string> properties = new Dictionary<string, string>();

            public CustomName(string tag, string customName)
            {
                this.tag = tag.ToUpper();
                this.realName = customName;
                this.pattern = $"[{tag} ".ToUpper();
                this.tagText = "";
            }

            public void parseCustonName()
            {
                // Clear Properties
                properties.Clear();

                // Find the start of the tag
                int startIndex = realName.ToUpper().IndexOf(pattern);
                if (startIndex == -1) return;

                // Find the end of the tag
                startIndex += pattern.Length;
                int endIndex = realName.IndexOf(']', startIndex);
                if (endIndex == -1) return;

                // Extract the tag
                this.tagText = realName.Substring(startIndex, endIndex - startIndex);

                // Retrieve only the content
                this.tagText = tagText.Replace(pattern, "").Replace("]", "").Trim();

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
            public bool TagContains(string text)
            {
                return tagText.Contains(text);
            }

            public void updateCustomName()
            {
                string newTagText = $"[{tag}";
                foreach (var property in properties)
                {
                    newTagText += $" {property.Key}={property.Value}";
                }

                // Define the pattern to search for
                int startIndex = realName.ToUpper().IndexOf(pattern.ToUpper());
                if (startIndex != -1)
                {
                    int endIndex = realName.IndexOf(']', startIndex);
                    if (endIndex != -1)
                    {
                        // Extract the value
                        string tagText = realName.Substring(startIndex, endIndex - startIndex).ToUpper();

                        tagText = tagText.Replace(pattern, "").Replace("]", "");
                        string[] tagtextKeyValues = tagText.Trim().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                        newTagText += "]";

                        // Replace the existing value
                        realName = realName.Remove(startIndex, endIndex - startIndex + 1);
                        realName = realName.Insert(startIndex, newTagText.ToUpper());
                    }
                }
                else
                {
                    // If the key does not exist, append it
                    realName += " " + newTagText;
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


        static partial class Logger
        {
            public enum LogType{I,W,E,D}
            private class LogEntry
            {
                public string entry;
                public int count;
                public LogType logType;
            }
            private static List<LogEntry> logger = new List<LogEntry>();
            public static void Log(string line, LogType logType)
            {
                if (logger.Count >= 1 && line == logger[0].entry && logType == logger[0].logType)
                {
                    ++logger[0].count;
                    return;
                }
                logger.Insert(0, new LogEntry
                {
                    entry = line,
                    count = 1,
                    logType = logType
                });
                if (logger.Count() > MAX_LOG_ENTRIES)
                {
                    logger.RemoveAt(logger.Count()-1);
                }
            }
            public static void Clear()
            {
                logger.Clear();
            }
            public static void D(string line)
            {
                Log(line, LogType.D);
            }
            public static void Info(string line)
            {
                Log(line, LogType.I);
            }
            public static
            void Warn(string line)
            {
                Log(line, LogType.W);
            }
            public static void Err(string line)
            {
                Log(line, LogType.E);
            }
            public static string PrintBuffer()
            {
                var str = "";
                foreach (var logEntry in logger)
                {
                    str += logEntry.logType.ToString() + ": " + (logEntry.count != 1 ? "(" + logEntry.count.ToString() + ") " : "") + logEntry.entry + "\n";
                }
                return str;
            }
        }

        internal class LogPrinter
        {
            List<IMyTextSurface> textSurfaceList = new List<IMyTextSurface>();
            Program program;

            public LogPrinter(Program program)
            {
                this.program = program;
            }

            public void AddTextSurface(IMyTextSurface textSurface)
            {
                textSurfaceList.Add(textSurface);
            }

            public void clear()
            {
                this.textSurfaceList.Clear();
            }

            public  void print(String outputString)
            {
                if (textSurfaceList != null && textSurfaceList.Count > 0)
                {
                    foreach (IMyTextSurface textSurface in textSurfaceList)
                    {
                        textSurface.WriteText(outputString, false);
                    }
                }else
                {
                    program.Echo(outputString);
                }
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

            public Boolean IsNavigationSuccessful()
            {
                string lcdText = null;

                if (samLogLcd != null) lcdText = samLogLcd.GetText();
                if (samLogCockpit != null) lcdText = samLogCockpit.getLogTextSurface().GetText();

                if (lcdText == null)
                    return false;

                string[] lines = lcdText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                string navigationSucessText = "I: Navigation successful!";
                string navigatingText = "I: Navigating to ";
                int depth = 5;


                for (int i = 0; i < depth; i++)
                {
                    // false if navigation was started recently
                    if (lines[i].Contains(navigatingText))
                    {
                        return false;
                    }
                    if (lines[i].Contains(navigationSucessText))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        internal class DestinationProfile
        {
            public string destination { get; set; }
            public Dictionary<string, string> properties { get; }

            public bool timerTriggered = false;

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
                // Current SAM Attributes
                if (samV2HelperPb.CustomData == null || samV2HelperPb.CustomData == "")
                {
                    // generate default
                    samV2HelperPb.CustomData += $"[{profileName}] \n";
                    foreach (var samProperty in SAM_CONFIGURATIONS)
                    {
                        samV2HelperPb.CustomData += $"{samProperty.Key}={samProperty.Value[2]} \n";
                    }

                    // Connector
                    samV2HelperPb.CustomData += "Connector=" + samConfHelperBlocks.samMainConnector.CustomName + "\n";

                    // Remote controller
                    samV2HelperPb.CustomData += "Rc=" + samConfHelperBlocks.samRc.CustomName + "\n";

                }
                //PB TAG
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
                if (this.samV2HelperPb.CustomData == null || destination == null)
                {
                    destination = "DEFAULT";
                }

                if (HasChanged())
                {
                    parseDestinationProfile();
                }
                CurrentDestination = this.GetDestinationProfile(destination);
         

                // Merge with default profile
                DestinationProfile defaultDestinationProfile = this.GetDestinationProfile("DEFAULT");
                if (CurrentDestination == null)
                {
                    CurrentDestination = defaultDestinationProfile;
                }else
                {
                    foreach (var key in defaultDestinationProfile.properties.Keys.ToList())
                    {
                        if (! CurrentDestination.properties.ContainsKey(key)){
                            CurrentDestination.properties.Add(key, defaultDestinationProfile.properties[key]);
                        }
                    }
                }

                return CurrentDestination;
            }

            public void applyCurrentDestinationProfileToSamPb(SamPb samPb)
            {
                foreach (var key in CurrentDestination.properties.Keys.ToList())
                {
                    if (SAM_CONFIGURATIONS.ContainsKey(key))
                    {
                        samPb.SetSamPbProperty(key, CurrentDestination.properties[key]);
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
                    if (destination.ToLower().Trim().EndsWith(key.ToLower().Trim()))
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

            public void SetMode(String mode)
            {
                CustomName samPbCustomName = new CustomName(SAM_TAG_NAME, this.samPb.CustomName);
                samPbCustomName.parseCustonName();


                if (String.IsNullOrEmpty(mode))
                {

                    if (samPbCustomName.TagContains("LOOP"))
                    {
                        this.samPb.CustomName = this.samPb.CustomName.Replace("LOOP", "");
                        return;
                    }
                    if (samPbCustomName.TagContains("LIST"))
                    {
                        this.samPb.CustomName = this.samPb.CustomName.Replace("LIST", "");
                        return;
                    }
                }

                if (mode.ToUpper().Equals("LIST"))
                {

                    if (samPbCustomName.TagContains("LOOP"))
                    {
                        this.samPb.CustomName = this.samPb.CustomName.Replace("LOOP", "LIST");
                        return;
                    }
                    if (samPbCustomName.TagContains("LIST"))
                    {
                        return;
                    }
                    this.samPb.CustomName = this.samPb.CustomName.Replace("[" + SAM_TAG_NAME, "[" + SAM_TAG_NAME + " LIST");
                    return;
                }
                if (mode.ToUpper().Equals("LOOP"))
                {

                    if (samPbCustomName.TagContains("LIST"))
                    {
                        this.samPb.CustomName = this.samPb.CustomName.Replace("LIST", "LOOP");
                        return;
                    }
                    if (samPbCustomName.TagContains("LOOP"))
                    {
                        return;
                    }
                    this.samPb.CustomName = this.samPb.CustomName.Replace("[" + SAM_TAG_NAME, "[" + SAM_TAG_NAME + " LOOP");
                    return;
                }
            }

            public string getMode()
            {
                var mode = this.samPb.CustomName.Contains(" LOOP") ? "LOOP" : "";
                mode = this.samPb.CustomName.Contains(" LIST") ? "LIST" : mode;
                return mode;
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

                this.samPb.CustomData = newSamPbCustomData;
            }

            public void SetSamTargetValue(string key, string targetValue ,DestinationProfile currentDestination )
            {
                Logger.Info("Changing " + key + " to " + targetValue);

                if (currentDestination != null)
                {
                    // Increement +
                    if (targetValue != null)
                    {
                        string value = currentDestination.properties[key];
                        double doubleValue;

                        if (double.TryParse(value, out doubleValue))
                        {
                            targetValue = targetValue.Trim();
                            double increement;

                            if (targetValue.StartsWith("+"))
                            {
                                double.TryParse(targetValue.Substring(1), out increement);
                                doubleValue += increement;

                                targetValue = "" + doubleValue;
                            }
                            if (targetValue.StartsWith("-"))
                            {
                                double.TryParse(targetValue.Substring(1), out increement);
                                doubleValue -= increement;

                                targetValue = "" + doubleValue;
                            }
                        }
                    }

                    currentDestination.AddProperty(key, targetValue);
                }
                else
                {
                     SetSamPbProperty(key, targetValue);
                }
                WriteSamPbPropertiesToPb();
            }
        }

        internal class Cockpit
        {
            IMyCockpit myCockpit;
            int screenLogIndex = -1;
            int screenConfIndex = -1;
            public CustomName customName;
            string tag;
            string cd;

            public Cockpit(string tag, string cd , IMyCockpit myCockpit)
            {
                this.myCockpit = myCockpit;
                this.tag = tag.ToUpper();
                this.cd = cd.ToUpper();

                // Tag 
                customName = new CustomName(tag.ToUpper(), myCockpit.CustomName.ToUpper());
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

                // CD 
                // Check CD
                var customData = myCockpit.CustomData.ToUpper();
                var cdPattern = cd.ToUpper() + "PANEL";
                string[] lines = customData.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (line.Contains(cdPattern) )
                    {
                        if (line.Contains("LOG"))
                        {
                            int.TryParse(line.Replace(cdPattern, "").Replace("=LOG", ""), out screenLogIndex);
                        }else
                        {
                            if (line.EndsWith("="))
                            {
                                int.TryParse(line.Replace(cdPattern, "").Replace("=", ""), out screenConfIndex);
                            }
                        }
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


        internal static class SamModeCycle
        { // Animation
            private static string[] ROTATOR = new string[] { "", "LIST", "LOOP" };
            private static int rotatorCount = 0;

            public static void Run()
            {
                if (++rotatorCount > ROTATOR.Length - 1)
                {
                    rotatorCount = 0;
                }
            }

            public static string Cycle()
            {
                SamModeCycle.Run();
                return ROTATOR[rotatorCount];
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

