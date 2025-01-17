﻿using GoodAI.Core.Configuration;
using GoodAI.Core.Execution;
using GoodAI.Core.Memory;
using GoodAI.Core.Nodes;
using GoodAI.Core.Observers;
using GoodAI.Modules.Transforms;
using GoodAI.Core.Utils;
using GoodAI.BrainSimulator.Nodes;
using GoodAI.BrainSimulator.NodeView;
using GoodAI.BrainSimulator.Utils;
using Graph;
using ManagedCuda.BasicTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using WeifenLuo.WinFormsUI.Docking;
using YAXLib;

namespace GoodAI.BrainSimulator.Forms
{
    public partial class MainForm : Form
    {
        private static string TITLE_TEXT = "Brain Simulator";

        public MyConfiguration Configuration { get; private set; }
        public MySimulationHandler SimulationHandler { get; private set; }
        public MyDocProvider Documentation { get; private set; } 

        private MyFlatOrdering m_topologyOps = new MyFlatOrdering();

        #region Project

        private MyProject m_project;

        public MyProject Project 
        {
            get { return m_project; }
            private set
            {
                if (m_project != null)
                {
                    m_project.Dispose();
                }
                m_project = value;
                SimulationHandler.Project = value;
            }
        }

        private string m_savedProjectRepresentation = null;

        //internal Uri ProjectLocation { get; private set; }

        private void CreateNewProject()
        {
            Project = new MyProject();
            Project.Network = Project.CreateNode<MyNetwork>();
            Project.Network.Name = "Network";            

            worldList.SelectedIndex = -1;
            worldList.SelectedItem = MyConfiguration.KnownWorlds.Values.First();

            Text = TITLE_TEXT + " - New Project";

            exportStateButton.Enabled = false;
            clearDataButton.Enabled = false;
        }

        private void SaveProject(string fileName)
        {
            MyLog.INFO.WriteLine("Saving project: " + fileName);
            try
            {
                string fileContent = GetSerializedProject(fileName);

                TextWriter writer = new StreamWriter(fileName);                
                writer.Write(fileContent);
                writer.Close();

                m_savedProjectRepresentation = fileContent;

                Properties.Settings.Default.LastProject = fileName;

                Text = TITLE_TEXT + " - " + Project.Name;
            }
            catch (Exception e)
            {
                MyLog.ERROR.WriteLine("Project saving failed: " + e.Message);
            }
        }

        private bool OpenProject(string fileName)
        {
            MyLog.INFO.WriteLine("--------------");
            MyLog.INFO.WriteLine("Loading project: " + fileName);
            try
            {
                TextReader reader = new StreamReader(fileName);
                string content = reader.ReadToEnd();
                reader.Close();

                Project = MyProject.Deserialize(content, Path.GetDirectoryName(fileName));
                
                Properties.Settings.Default.LastProject = fileName;
                saveFileDialog.FileName = fileName;
                m_savedProjectRepresentation = content;

                CloseAllGraphLayouts();
                CloseAllObservers();

                CreateNetworkView();
                OpenGraphLayout(Project.Network);

                if (Project.World != null)
                {
                    SelectWorldInWorldList(Project.World);
                }

                if (Project.Observers != null)
                {
                    foreach (MyAbstractObserver observer in Project.Observers)
                    {
                        observer.RestoreTargetFromIdentifier(Project);

                        if (observer.GenericTarget != null)
                        {
                            ShowObserverView(observer);
                        }
                    }
                }
                Project.Observers = null;

                exportStateButton.Enabled = MyMemoryBlockSerializer.TempDataExists(Project);
                clearDataButton.Enabled = exportStateButton.Enabled;

                Text = TITLE_TEXT + " - " + Project.Name;
                return true;
            }
            catch (Exception e)
            {
                MyLog.ERROR.WriteLine("Project loading failed: " + e.Message);
                return false;
            }
        }

        private bool ImportProject(string fileName, bool showObservers = false)
        {
            MyLog.INFO.WriteLine("Importing project: " + fileName);
            try
            {
                TextReader reader = new StreamReader(fileName);
                string content = reader.ReadToEnd();
                reader.Close();

                MyProject importedProject = MyProject.Deserialize(content, Path.GetDirectoryName(fileName));                
                
                //offset all imported nodes
                float maxY = NetworkView.Desktop.GetContentBounds().Bottom;                               
                foreach (var node in importedProject.Network.Children)
                {
                    node.Location.Y += maxY + 10.0f;                                                                  
                }

                if (showObservers && importedProject.Observers != null)
                {
                    foreach (MyAbstractObserver observer in importedProject.Observers)
                    {
                        observer.RestoreTargetFromIdentifier(importedProject);

                        if (observer.GenericTarget != null)
                        {
                            ShowObserverView(observer);
                        }
                    }
                }

                Project.Network.AppendGroupContent(importedProject.Network);

                if (showObservers && importedProject.Observers != null)
                {
                    foreach (MyAbstractObserver observer in importedProject.Observers)
                    {
                        observer.UpdateTargetIdentifier();
                    }
                }
     
                NetworkView.ReloadContent();
                NetworkView.Desktop.ZoomToBounds();
                
                return true;
            }
            catch (Exception e)
            {
                MyLog.ERROR.WriteLine("Project import failed: " + e.Message);
                return false;
            }
        }
        
        private bool IsProjectSaved(string fileName)
        {
            if (m_savedProjectRepresentation == null)
                return false;

            string currentRepresentation = null;

            try
            {
                currentRepresentation = GetSerializedProject(fileName);
            }
            catch
            {
                return false;
            }

            return m_savedProjectRepresentation.Equals(currentRepresentation);
        }

        private string GetSerializedProject(string fileName)
        {
            Project.Observers = new List<MyAbstractObserver>();  // potential sideffect
            ObserverViews.ForEach(ov => { ov.StoreWindowInfo(); Project.Observers.Add(ov.Observer); });

            Project.Name = Path.GetFileNameWithoutExtension(fileName);  // a little sideeffect (should be harmless)

            string serializedProject = Project.Serialize(Path.GetDirectoryName(fileName));
            Project.Observers = null;

            return serializedProject;
        }
        #endregion        

        #region Views

        public NodePropertyForm NodePropertyView { get; private set; }
        public MemoryBlocksForm MemoryBlocksView { get; private set; }
        
        public TaskForm TaskView { get; private set; }
        public TaskPropertyForm TaskPropertyView { get; private set; }

        private GraphLayoutForm NetworkView { get; set; }
        public ConsoleForm ConsoleView { get; private set; }
        public ValidationForm ValidationView { get; private set; }
        public NodeHelpForm HelpView { get; private set; }

        public DebugForm DebugView { get; private set; }

        protected List<DockContent> m_views;
        public Dictionary<MyNodeGroup, GraphLayoutForm> GraphViews { get; private set; }
        public List<ObserverForm> ObserverViews { get; private set; }     

        private void CreateNetworkView()
        {
            NetworkView = new GraphLayoutForm(this, Project.Network);
            NetworkView.FormClosed += GraphLayoutForm_FormClosed;

            GraphViews[Project.Network] = NetworkView;
            NetworkView.CloseButton = false;
            NetworkView.CloseButtonVisible = false;
        }

        public void CreateAndShowObserverView(MyWorkingNode node, Type observerType)
        {
            try
            {
                MyAbstractObserver observer = (MyAbstractObserver)Activator.CreateInstance(observerType);
                observer.GenericTarget = node;

                ObserverForm newView = new ObserverForm(this, observer, node);
                ObserverViews.Add(newView);

                newView.Show(dockPanel, DockState.Float);
            }
            catch (Exception e)
            {
                MyLog.ERROR.WriteLine("Error creating observer: " + e.Message);
            }
        }

        public void CreateAndShowObserverView(MyAbstractMemoryBlock memoryBlock, MyNode declaredOwner, Type mbObserverType)
        {
            bool isPlot = mbObserverType == typeof(MyTimePlotObserver);

            if (isPlot && !(memoryBlock is MyMemoryBlock<float>))
            {
                MyLog.ERROR.WriteLine("Plot observers are not allowed for non-float memory blocks");
                return;
            }

            try
            {
                MyAbstractObserver observer;

                if (isPlot)
                {
                    MyTimePlotObserver plotObserver = new MyTimePlotObserver();
                    plotObserver.Target = (MyMemoryBlock<float>)memoryBlock;

                    observer = plotObserver;
                }
                else
                {
                    MyAbstractMemoryBlockObserver memObserver = (MyAbstractMemoryBlockObserver)Activator.CreateInstance(mbObserverType);
                    memObserver.Target = memoryBlock;

                    observer = memObserver;
                }

                ObserverForm newView = new ObserverForm(this, observer, declaredOwner);
                ObserverViews.Add(newView);

                newView.Show(dockPanel, DockState.Float);
            }
            catch (Exception e)
            {
                MyLog.ERROR.WriteLine("Error creating observer: " + e.Message);
            }
        }

        public void ShowObserverView(MyAbstractObserver observer)
        {
            MyNode owner = null;

            if (observer is MyAbstractMemoryBlockObserver)
            {
                owner = (observer as MyAbstractMemoryBlockObserver).Target.Owner;
            }
            else if (observer is MyTimePlotObserver)
            {
                owner = (observer as MyTimePlotObserver).Target.Owner;
            }
            else
            {
                owner = observer.GenericTarget as MyNode;
            }

            ObserverForm newView = new ObserverForm(this, observer, owner);
            ObserverViews.Add(newView);

            newView.Show(dockPanel, DockState.Float);
            newView.FloatPane.FloatWindow.Size = new Size((int)observer.WindowSize.Width, (int)observer.WindowSize.Height);
            newView.FloatPane.FloatWindow.Location = new Point((int)observer.WindowLocation.X, (int)observer.WindowLocation.Y);

            if (!SystemInformation.VirtualScreen.Contains(newView.FloatPane.FloatWindow.Bounds))
            {
                newView.FloatPane.FloatWindow.Location = new Point(0, 0);
            }
        }

        public void ResetObservers()
        {
            foreach (ObserverForm ov in ObserverViews.ToList())
            {
                if (ov.Observer != null)
                {
                    ov.Observer.TriggerReset();
                }
            }
        }

        public void UpdateObserverView(MyAbstractObserver observer)
        {
            foreach (ObserverForm ov in ObserverViews.ToList())
            {
                if (ov.Observer == observer)
                {
                    ov.UpdateView(SimulationHandler.SimulationStep);
                }
            }
        }

        public void UpdateObservers()
        {
            foreach (ObserverForm ov in ObserverViews.ToList())
            {
                if (ov.Observer != null)
                {
                    ov.UpdateView(SimulationHandler.SimulationStep);
                }
            }
        }

        public void CloseObservers(MyNode node)
        {
            HashSet<ObserverForm> viewsToClose = new HashSet<ObserverForm>();

            MyNodeGroup.IteratorAction checkTarget = delegate(MyNode target)
            {
                foreach (ObserverForm ov in ObserverViews)
                {
                    if (ov.Observer.GenericTarget is MyAbstractMemoryBlock)
                    {
                        if ((ov.Observer.GenericTarget as MyAbstractMemoryBlock).Owner == target)
                        {
                            viewsToClose.Add(ov);
                        }
                    }
                    else if (ov.Observer.GenericTarget == target)
                    {
                        viewsToClose.Add(ov);
                    }
                }
            };

            checkTarget(node);

            if (node is MyNodeGroup) 
            {
                (node as MyNodeGroup).Iterate(true, checkTarget);
            }

            foreach (ObserverForm ov in viewsToClose)
            {
                ov.Close();
            }
        }

        public void RemoveObserverView(ObserverForm view)
        {
            ObserverViews.Remove(view);
        }

        public GraphLayoutForm OpenGraphLayout(MyNodeGroup target)
        {
            GraphLayoutForm graphForm;

            if (GraphViews.ContainsKey(target))
            {
                graphForm = GraphViews[target];
            }
            else
            {
                graphForm = new GraphLayoutForm(this, target);
                graphForm.FormClosed += GraphLayoutForm_FormClosed;
                GraphViews.Add(target, graphForm);
            }

            graphForm.Show(dockPanel, DockState.Document);
            return graphForm;
        }

        internal void CloseGraphLayout(MyNodeGroup target)
        {
            if (GraphViews.ContainsKey(target))
            {
                GraphViews[target].Close();
            }
        }

        internal void ReloadGraphLayout(MyNodeGroup target)
        {
            if (GraphViews.ContainsKey(target))
            {
                GraphViews[target].ReloadContent();
            }
        }   

        private void CloseAllGraphLayouts()
        {
            GraphViews.Values.ToList().ForEach(view => view.Close());
            GraphViews.Clear();
            NetworkView = null;
        }

        private void CloseAllObservers()
        {
            ObserverViews.ToList().ForEach(view => view.Close());
        }

        private void GraphLayoutForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphViews.Remove((sender as GraphLayoutForm).Target);
        }

        private void RestoreViewsLayout(string layoutFileName)
        {
                //TODO: change PersistString in WinFormsUI to be accessible publicly (or make our own DockContent common superclass for all forms)
                Dictionary<string, DockContent> viewTable = new Dictionary<string, DockContent>();

                foreach (DockContent view in m_views)
                {
                    if (!(view is GraphLayoutForm))
                    {
                        viewTable[view.GetType().ToString()] = view;
                    }
                }

                dockPanel.LoadFromXml(layoutFileName,
                    (string persistString) =>
                    {
                        return viewTable.ContainsKey(persistString) ? viewTable[persistString] : null;
                    });            
        }

        private void ResetViewsLayout()
        {
            foreach (DockContent view in m_views)
            {
                view.Hide();
                view.DockPanel = null;
            }

            ConsoleView.Show(dockPanel, DockState.DockBottom);
            ConsoleView.DockPanel.DockBottomPortion = 0.3;

            NodePropertyView.Show(dockPanel, DockState.DockRight);
            NodePropertyView.DockPanel.DockRightPortion = 0.15;

            TaskView.Show(dockPanel, DockState.DockLeft);
            TaskView.DockPanel.DockLeftPortion = 0.15;

            MemoryBlocksView.Show(dockPanel, DockState.Float);
            MemoryBlocksView.DockHandler.FloatPane.DockTo(dockPanel, DockStyle.Right);

            TaskPropertyView.Show(dockPanel, DockState.Float);
            TaskPropertyView.DockHandler.FloatPane.DockTo(dockPanel, DockStyle.Left);

            ValidationView.Show(dockPanel, DockState.Float);
            ValidationView.DockHandler.FloatPane.DockTo(dockPanel, DockStyle.Bottom);
        }

        private void StoreViewsLayout(string layoutFileName)
        {            
            dockPanel.SaveAsXml(layoutFileName);                            
        }

        private string UserLayoutFileName
        {
            get
            {
                return Application.LocalUserAppDataPath + "\\user.layout";
            }
        } 

        #endregion

        public MainForm()
        {
            this.Font = SystemFonts.MessageBoxFont;
            InitializeComponent();            

            SimulationHandler = new MySimulationHandler(backgroundWorker);
            SimulationHandler.StateChanged += SimulationHandler_StateChanged;
            SimulationHandler.ProgressChanged += SimulationHandler_ProgressChanged;

            // must be created in advance to grab possible error logs
            ConsoleView = new ConsoleForm(this);

            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            MyLog.INFO.WriteLine(assemblyName.Name + " version " + assemblyName.Version);

            try
            {
                SimulationHandler.Simulation = new MyLocalSimulation();
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occured when initializing simulation. Please make sure you have a supported CUDA-enabled graphics card and apropriate drivers." +
                        "Technical details: " + e.Message, "Simulation Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // this way you do not have to tweak form Close and Closing events and it works even with any worker threads still running
                Environment.Exit(1);
            }

            MyConfiguration.SetupModuleSearchPath();
            MyConfiguration.ProcessCommandParams();

            try
            {
                MyConfiguration.LoadModules();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Fatal error occured during initialization", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            Documentation = new MyDocProvider();

            foreach (MyModuleConfig module in MyConfiguration.Modules)
            {
                Documentation.LoadXMLDoc(module.Assembly);
            }
            
            NodePropertyView = new NodePropertyForm(this);
            MemoryBlocksView = new MemoryBlocksForm(this);

            TaskView = new TaskForm(this);
            TaskPropertyView = new TaskPropertyForm(this);

            GraphViews = new Dictionary<MyNodeGroup, GraphLayoutForm>();
            ObserverViews = new List<ObserverForm>();
            
            ValidationView = new ValidationForm(this);
            HelpView = new NodeHelpForm(this);
            HelpView.StartPosition = FormStartPosition.CenterScreen;

            DebugView = new DebugForm(this);

            PopulateWorldList();
            CreateNewProject();                 
            CreateNetworkView();

            m_views = new List<DockContent>() { NetworkView, NodePropertyView, MemoryBlocksView, TaskView, TaskPropertyView, ConsoleView, ValidationView, DebugView, HelpView };

            foreach (DockContent view in m_views)
            {
                ToolStripMenuItem viewMenuItem = new ToolStripMenuItem(view.Text);
                viewMenuItem.Click += viewToolStripMenuItem_Click;
                viewMenuItem.Tag = view;
                viewMenuItem.Name = view.Text;

                viewToolStripMenuItem.DropDownItems.Add(viewMenuItem);
            }

            ((ToolStripMenuItem)viewToolStripMenuItem.DropDownItems.Find(HelpView.Text, false).First()).ShortcutKeys = Keys.F1;
            viewToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem resetViewsMenuItem = new ToolStripMenuItem("Reset Views Layout");
            resetViewsMenuItem.ShortcutKeys = Keys.Control | Keys.W;
            resetViewsMenuItem.Click += resetViewsMenuItem_Click;

            viewToolStripMenuItem.DropDownItems.Add(resetViewsMenuItem);

            ToolStripMenuItem nodeSettingsMenuItem = new ToolStripMenuItem("Configure node selection...");
            nodeSettingsMenuItem.ShortcutKeys = Keys.Control | Keys.L;
            nodeSettingsMenuItem.Click += nodeSettingsMenuItem_Click;

            viewToolStripMenuItem.DropDownItems.Add(nodeSettingsMenuItem);

            modeDropDownList.SelectedIndex = 0;

            AddTimerMenuItem(timerToolStripSplitButton, timerItem_Click, 0);
            AddTimerMenuItem(timerToolStripSplitButton, timerItem_Click, 10);
            AddTimerMenuItem(timerToolStripSplitButton, timerItem_Click, 20);
            AddTimerMenuItem(timerToolStripSplitButton, timerItem_Click, 50);
            AddTimerMenuItem(timerToolStripSplitButton, timerItem_Click, 100);
            AddTimerMenuItem(timerToolStripSplitButton, timerItem_Click, 500);

            timerItem_Click(timerToolStripSplitButton.DropDownItems[Properties.Settings.Default.StepDelay], EventArgs.Empty);

            AddTimerMenuItem(observerTimerToolButton, observerTimerItem_Click, 0);
            AddTimerMenuItem(observerTimerToolButton, observerTimerItem_Click, 20);
            AddTimerMenuItem(observerTimerToolButton, observerTimerItem_Click, 100);
            AddTimerMenuItem(observerTimerToolButton, observerTimerItem_Click, 500);
            AddTimerMenuItem(observerTimerToolButton, observerTimerItem_Click, 1000);
            AddTimerMenuItem(observerTimerToolButton, observerTimerItem_Click, 5000);

            observerTimerItem_Click(observerTimerToolButton.DropDownItems[Properties.Settings.Default.ObserverPeriod], EventArgs.Empty);
            
            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(typeof(MyWorkingNode))["DataFolder"];
            EditorAttribute editor = (EditorAttribute)descriptor.Attributes[typeof(EditorAttribute)];

            editor.GetType().GetField("typeName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(editor,
                typeof(MyFolderDialog).AssemblyQualifiedName);                

            editor.GetType().GetField("baseTypeName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(editor,
                typeof(UITypeEditor).AssemblyQualifiedName);

            autosaveTextBox.Text = Properties.Settings.Default.AutosaveInterval.ToString();
            autosaveTextBox_Validating(this, new CancelEventArgs());

            autosaveButton.Checked = Properties.Settings.Default.AutosaveEnabled;
        }

        public void PopulateWorldList()
        {
            Type selectedWorldType = null;

            if (worldList.SelectedIndex != -1)
            {
                selectedWorldType = ((MyWorldConfig)worldList.SelectedItem).NodeType;
            }

            worldList.Items.Clear();

            foreach (MyWorldConfig wc in MyConfiguration.KnownWorlds.Values)
            {
                if ((Properties.Settings.Default.ToolBarNodes != null &&
                    Properties.Settings.Default.ToolBarNodes.Contains(wc.NodeType.Name)) ||
                    wc.IsBasicNode)
                {
                    worldList.Items.Add(wc);

                    if (wc.NodeType == selectedWorldType)
                    {
                        worldList.SelectedItem = wc;
                    }
                }
            }
        }

        private void SelectWorldInWorldList(MyWorld world)
        {
            if (Properties.Settings.Default.ToolBarNodes == null)
            {
                Properties.Settings.Default.ToolBarNodes = new System.Collections.Specialized.StringCollection();
            }

            // if the world is not present in the combo box, add it first
            if (!Properties.Settings.Default.ToolBarNodes.Contains(world.GetType().Name))
            {
                Properties.Settings.Default.ToolBarNodes.Add(world.GetType().Name);
                worldList.Items.Add(MyConfiguration.KnownWorlds[Project.World.GetType()]);
            }

            worldList.SelectedItem = MyConfiguration.KnownWorlds[Project.World.GetType()];
        }

        private void AddTimerMenuItem(ToolStripSplitButton splitButton, EventHandler clickHandler, int ms) 
        {
            string title;

            if (ms < 1000)
            {
                title = ms + " ms";
            }
            else
            {
                title = (ms / 1000.0f).ToString("0.##" + " s");
            }

            ToolStripMenuItem mi = new ToolStripMenuItem(title);
            mi.Tag = ms;
            mi.Click += clickHandler;
             
            splitButton.DropDownItems.Add(mi);
        }

        void resetViewsMenuItem_Click(object sender, EventArgs e)
        {
            ResetViewsLayout();
        }

        void timerItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < timerToolStripSplitButton.DropDownItems.Count; i++)
            {
                ToolStripMenuItem item = (ToolStripMenuItem)timerToolStripSplitButton.DropDownItems[i];                

                if (item == sender)
                {
                    Properties.Settings.Default.StepDelay = i;
                    SimulationHandler.SleepInterval = (int)item.Tag;
                    item.Checked = true;
                }
                else
                {
                    item.Checked = false;
                }
            }            
        }

        void observerTimerItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < observerTimerToolButton.DropDownItems.Count; i++)
            {
                ToolStripMenuItem item = (ToolStripMenuItem)observerTimerToolButton.DropDownItems[i];

                if (item == sender)
                {
                    Properties.Settings.Default.ObserverPeriod = i;
                    SimulationHandler.ReportInterval = (int)item.Tag;
                    item.Checked = true;
                }
                else
                {
                    item.Checked = false;
                }
            }
        }
                     
        #region Simulation               

        private bool UpdateAndCheckChange(MyNode node)
        {
            node.PushOutputBlockSizes();
            node.UpdateMemoryBlocks();
            return node.AnyOutputSizeChanged();
        }

        private static int MAX_BLOCKS_UPDATE_ATTEMPTS = 20;

        public bool UpdateMemoryModel()
        {            
            MyLog.INFO.WriteLine("Updating memory blocks...");

            IMyOrderingAlgorithm topoOps = new MyHierarchicalOrdering();
            List<MyNode> orderedNodes = topoOps.EvaluateOrder(Project.Network);

            if (!orderedNodes.Any())
            {
                return true;
            }

            int attempts = 0;
            bool anyOutputChanged = false;

            try
            {

                while (attempts < MAX_BLOCKS_UPDATE_ATTEMPTS)
                {
                    attempts++;
                    anyOutputChanged = false;

                    anyOutputChanged |= UpdateAndCheckChange(Project.World);
                    orderedNodes.ForEach(node => anyOutputChanged |= UpdateAndCheckChange(node));

                    if (!anyOutputChanged)
                    {
                        MyLog.INFO.WriteLine("Successful update after " + attempts + " cycle(s).");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.ERROR.WriteLine("Exception occured while updating memory model: " + e.Message);
                return true;
            }

            return anyOutputChanged;                        
        }

        private void StartSimulation(bool oneStepOnly) 
        {            
            if (SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED)
            {
                MyLog.INFO.WriteLine("--------------");
                bool anyOutputChanged = UpdateMemoryModel();

                MyValidator validator = ValidationView.Validator;
                validator.Simulation = SimulationHandler.Simulation;

                validator.ClearValidation();

                Project.World.ValidateWorld(validator);                
                Project.Network.Validate(validator);

                validator.AssertError(!anyOutputChanged, Project.Network, "Possible infinite loop in memory block sizes.");

                ValidationView.UpdateListView();
                validator.Simulation = null;                               

                ResetObservers();

                if (validator.ValidationSucessfull)
                {
                    try
                    {
                        SimulationHandler.StartSimulation(oneStepOnly);                        
                    }
                    catch (Exception e)
                    {
                        MyLog.ERROR.WriteLine("Simulation cannot be started! Exception occured: " + e.Message);
                    }
                }
                else
                {                
                    MyLog.ERROR.WriteLine("Simulation cannot be started! Validation failed.");
                    OpenFloatingOrActivate(ValidationView);
                }
            }
            else
            {
                try
                {
                    SimulationHandler.StartSimulation(oneStepOnly);
                }
                catch (Exception e)
                {
                    MyLog.ERROR.WriteLine("Simulation cannot be started! Exception occured: " + e.Message);
                }
            }           
        }

        void SimulationHandler_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            if (SimulationHandler.State != MySimulationHandler.SimulationState.STOPPED)
            {
                statusStrip.BeginInvoke((MethodInvoker)(() => stepStatusLabel.Text = "(" + SimulationHandler.SimulationStep + ", " + SimulationHandler.SimulationSpeed + "/s)"));

                GraphLayoutForm activeLayout = dockPanel.ActiveDocument as GraphLayoutForm;
                activeLayout.Desktop.Invalidate();                
            }
            else
            {
                statusStrip.Invoke((MethodInvoker)(() => stepStatusLabel.Text = String.Empty));                                                       
            }
        }

        #endregion

        #region Global Shortcuts        

        public bool PerformMainMenuClick(Keys shortCut)
        {           
            foreach (ToolStripItem item in mainMenuStrip.Items)
            {
                if (PerformMenuClick(item, shortCut))
                {
                    return true;
                }
            }
            return false;
        }

        private bool PerformMenuClick(ToolStripItem menuItem, Keys shortCut)
        {
            if (menuItem is ToolStripMenuItem)
            {
                if ((menuItem as ToolStripMenuItem).ShortcutKeys == shortCut)
                {
                    menuItem.PerformClick();
                    return true;
                }
            }

            if (menuItem is ToolStripDropDownItem)
            {
                foreach (ToolStripItem item in (menuItem as ToolStripDropDownItem).DropDownItems)
                {
                    if (PerformMenuClick(item, shortCut))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region Clipboard Copy/Paste

        public void CopySelectedNodesToClipboard()
        {
            if (dockPanel.ActiveContent is ConsoleForm)
            {
                Clipboard.SetText((dockPanel.ActiveContent as ConsoleForm).textBox.SelectedText);
                return;
            }

            if (dockPanel.ActiveDocument is GraphLayoutForm)
            {
                GraphLayoutForm activeLayout = dockPanel.ActiveDocument as GraphLayoutForm;

                NodeSelection selection = null;

                if (activeLayout.Desktop.FocusElement is NodeSelection)
                {
                    selection = activeLayout.Desktop.FocusElement as NodeSelection;
                }
                else if (activeLayout.Desktop.FocusElement is Node)
                {
                    selection = new NodeSelection(new Node[] { activeLayout.Desktop.FocusElement as Node });
                }

                if (selection != null)
                {
                    HashSet<int> approvedNodes = new HashSet<int>();
                    MyNetwork clipboardNetwork = Project.CreateNode<MyNetwork>();
                    clipboardNetwork.Name = "Clipboard";

                    foreach (MyNodeView nodeView in selection.Nodes)
                    {
                        MyNode selectedNode = nodeView.Node;

                        if (selectedNode is MyWorkingNode)
                        {
                            clipboardNetwork.Children.Add(nodeView.Node);
                            approvedNodes.Add(selectedNode.Id);
                        }
                        
                        if (selectedNode is MyNodeGroup)
                        {
                            (selectedNode as MyNodeGroup).Iterate(true, true, node => approvedNodes.Add(node.Id));
                        }
                    }

                    if (approvedNodes.Count > 0)
                    {
                        clipboardNetwork.PrepareConnections();
                        clipboardNetwork.FilterPreparedCollection(approvedNodes);

                        YAXSerializer networkSerializer = new YAXSerializer(typeof(MyNetwork), YAXExceptionHandlingPolicies.ThrowErrorsOnly, YAXExceptionTypes.Warning, YAXSerializationOptions.DontSerializeNullObjects);
                        string xml = networkSerializer.Serialize(clipboardNetwork);

                        Clipboard.SetText(xml);
                    }
                    else
                    {
                        MyLog.WARNING.WriteLine("Copying is not allowed");
                    }
                }
                else
                {
                    MyLog.WARNING.WriteLine("Selection is empty");
                }
            }
        }

        public void PasteNodesFromClipboard()
        {
            if (Clipboard.ContainsText() && dockPanel.ActiveDocument is GraphLayoutForm)
            {
                string xml = Clipboard.GetText();

                try
                {
                    YAXSerializer networkSerializer = new YAXSerializer(typeof(MyNetwork), YAXExceptionHandlingPolicies.ThrowErrorsOnly, YAXExceptionTypes.Error, YAXSerializationOptions.DontSerializeNullObjects);

                    MyNetwork networkToPaste = (MyNetwork)networkSerializer.Deserialize(xml);
                    networkToPaste.UpdateAfterDeserialization(0, Project);

                    GraphLayoutForm activeLayout = dockPanel.ActiveDocument as GraphLayoutForm;

                    activeLayout.Target.AppendGroupContent(networkToPaste);
                    activeLayout.ReloadContent();
                    
                    HashSet<int> pastedNodes = new HashSet<int>();
                    networkToPaste.Children.ForEach(node => pastedNodes.Add(node.Id));

                    List<MyNodeView> pastedNodeViews = new List<MyNodeView>();
                    RectangleF? pastedBounds = null;
                    Graphics context = activeLayout.Desktop.CreateGraphics();
                    
                    foreach (MyNodeView nodeView in activeLayout.Desktop.Nodes)
                    {
                        if (pastedNodes.Contains(nodeView.Node.Id))
                        {
                            pastedNodeViews.Add(nodeView);

                            SizeF size = GraphRenderer.Measure(context, nodeView);
                            RectangleF bounds = new RectangleF(nodeView.Location, size);

                            if (pastedBounds.HasValue)
                            {
                                pastedBounds = RectangleF.Union(pastedBounds.Value, bounds);
                            }
                            else
                            {
                                pastedBounds = bounds;
                            }
                        }
                    }
                   
                    PointF pasteLocation = activeLayout.Desktop.UnprojectPoint(new PointF(20, 20));

                    if (pastedBounds.HasValue)
                    {
                        foreach (MyNodeView nodeView in pastedNodeViews)
                        {
                            nodeView.Node.Location = new MyLocation()
                            {
                                X = nodeView.Location.X - pastedBounds.Value.Left + pasteLocation.X,
                                Y = nodeView.Location.Y - pastedBounds.Value.Top + pasteLocation.Y
                            };
                            nodeView.UpdateView();
                        }
                    }


                    //select pasted nodes
                    activeLayout.Desktop.FocusElement = new NodeSelection(pastedNodeViews);
                }
                catch (Exception e)
                {
                    MyLog.ERROR.WriteLine("Paste failed: " + e.Message);
                }
            }
        }

        #endregion
    }
}
