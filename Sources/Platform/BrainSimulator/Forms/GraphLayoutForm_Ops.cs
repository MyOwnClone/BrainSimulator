﻿using GoodAI.Core;
using GoodAI.Core.Configuration;
using GoodAI.Core.Nodes;
using GoodAI.Modules.Transforms;
using GoodAI.Core.Utils;
using GoodAI.BrainSimulator.Nodes;
using GoodAI.BrainSimulator.NodeView;
using Graph;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodAI.BrainSimulator.Forms
{
    public partial class GraphLayoutForm
    {
        private void AddNodeButton(MyNodeConfig nodeInfo, bool isTransform)
        {            
            ToolStripItem newButton = isTransform ? new ToolStripMenuItem() : newButton = new ToolStripButton();
            ToolStripItemCollection items;

            newButton.Image = nodeInfo.SmallImage;
            newButton.Name = nodeInfo.NodeType.Name;
            newButton.ToolTipText = nodeInfo.NodeType.Name.Substring(2);
            newButton.MouseDown += addNodeButton_MouseDown;
            newButton.Tag = nodeInfo.NodeType;

            newButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            newButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            newButton.ImageTransparentColor = System.Drawing.Color.Magenta;

            if (isTransform)
            {
                newButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                newButton.Text = newButton.ToolTipText;
                items = transformMenu.DropDownItems;                
            }
            else
            {
                items = toolStrip1.Items;
                newButton.MouseUp += newButton_MouseUp;          
            }

            if (items.Count > 0 && (items[items.Count - 1].Tag as Type).Namespace != nodeInfo.NodeType.Namespace)
            {
                items.Add(new ToolStripSeparator());
            }
            items.Add(newButton);
        }

        void newButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                contextMenuStrip.Tag = sender;
                ToolStripItem button = sender as ToolStripItem;
                contextMenuStrip.Show(toolStrip1, button.Bounds.Left + e.Location.X + 2, button.Bounds.Top + e.Location.Y + 2);                
            }
        }

        private void RemoveNodeButton(ToolStripItem nodeButton)
        {
            StringCollection toolBarNodes = Properties.Settings.Default.ToolBarNodes;
            string typeName = (nodeButton.Tag as Type).Name;
            if (toolBarNodes != null && toolBarNodes.Contains(typeName))
            {
                toolBarNodes.Remove(typeName);
                toolStrip1.Items.Remove(nodeButton);
            }
        }

        private void LoadContentIntoDesktop()
        {
            Dictionary<MyNode, MyNodeView> nodeViewTable = new Dictionary<MyNode, MyNodeView>();

            //Global i/o

            for(int i = 0; i < Target.GroupInputNodes.Length; i++)
            {
                MyParentInput inputNode = Target.GroupInputNodes[i];

                if (inputNode.Location == null)
                {
                    inputNode.Location = new MyLocation() { X = 50, Y = 150 * i + 100 };
                }

                MyNodeView inputView = MyNodeView.CreateNodeView(inputNode, Desktop);
                inputView.UpdateView();
                Desktop.AddNode(inputView);
                nodeViewTable[inputNode] = inputView;
            }


            for (int i = 0; i < Target.GroupOutputNodes.Length; i++)
            {
                MyOutput outputNode = Target.GroupOutputNodes[i];

                if (outputNode.Location == null)
                {
                    outputNode.Location = new MyLocation() { X = 800, Y = 150 * i + 100 };
                }

                MyNodeView outputView = MyNodeView.CreateNodeView(outputNode, Desktop);
                outputView.UpdateView();
                Desktop.AddNode(outputView);
                nodeViewTable[outputNode] = outputView;
            }                       

            //other nodes
            foreach (MyNode node in Target.Children)
            {
                MyNodeView newNodeView = MyNodeView.CreateNodeView(node, Desktop);
                newNodeView.UpdateView();

                Desktop.AddNode(newNodeView);
                nodeViewTable[node] = newNodeView;
            }

            foreach (MyNode outputNode in Target.GroupOutputNodes)
            {             
                RestoreConnections(outputNode, nodeViewTable);
            }

            //other connections
            foreach (MyNode node in Target.Children)
            {
                RestoreConnections(node, nodeViewTable);
            }         
        }

        private void RestoreConnections(MyNode node, Dictionary<MyNode, MyNodeView> nodeViewTable) 
        {
            MyNodeView toNodeView = nodeViewTable[node];
   
            for (int i = 0; i < node.InputBranches; i++)
            {
                MyConnection connection = node.InputConnections[i];

                if (connection != null)
                {
                    MyNodeView fromNodeView = nodeViewTable[connection.From];
                    NodeItem fromNodeViewItem = fromNodeView.GetOuputBranchItem(connection.FromIndex);                    

                    NodeConnection c = Desktop.Connect(fromNodeViewItem, toNodeView.GetInputBranchItem(connection.ToIndex));
                    c.Tag = connection;
                }
            }       
        }

        private void StoreLayoutProperties() 
        {
            Target.LayoutProperties = new MyLayout();
            Target.LayoutProperties.Zoom = Desktop.Zoom;
            Target.LayoutProperties.Translation.X = Desktop.Translation.X;
            Target.LayoutProperties.Translation.Y = Desktop.Translation.Y;
        }

        public void SelectNodeView(MyNode node)
        {
            Node nodeView = Desktop.Nodes.First(nw => (nw as MyNodeView).Node == node);

            if (nodeView != null)
            {
                Desktop.FocusElement = nodeView;
            }
        }
    }
}
