﻿using GoodAI.Core.Configuration;
using GoodAI.Core.Nodes;
using GoodAI.Core.Utils;
using Graph;
using Graph.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoodAI.BrainSimulator.NodeView
{
    internal class MyUserInputView : MyNodeView
    {
        private List<NodeSliderItem> sliders = new List<NodeSliderItem>();

        private float minValue;
        private float maxValue;

        public MyUserInputView(MyNodeConfig nodeInfo, GraphControl owner) : base(nodeInfo, owner) { }

        public override void UpdateView()
        {
            base.UpdateView();

            MyUserInput userInputNode = Node as MyUserInput;
            int newSlidersCount = userInputNode.ConvertToBinary ? 1 : userInputNode.OutputSize;

            if (newSlidersCount != sliders.Count)
            {
                sliders.ForEach(s => RemoveItem(s));

                for (int i = 0; i < newSlidersCount; i++)
                {
                    NodeSliderItem slider = new NodeSliderItem(null, 0, 0, 0, 1, 0, false, false);
                    slider.Tag = i;
                    slider.ValueChanged += slider_ValueChanged;

                    sliders.Add(slider);
                    AddItem(slider);
                }                
                SetSlidersToDefault();
            }
            else if (minValue != userInputNode.MinValue || maxValue != userInputNode.MaxValue)
            {
                SetSlidersToDefault();
            }
        }

        private void SetSlidersToDefault()
        {
            MyUserInput userInputNode = Node as MyUserInput;

            maxValue = userInputNode.MaxValue;
            minValue = userInputNode.MinValue;

            for (int i = 0; i < sliders.Count; i++)
            {
                NodeSliderItem slider = sliders[i];
                slider.Value = (userInputNode.GetUserInput(i) - minValue) / (maxValue - minValue);
            }
        }

        void slider_ValueChanged(object sender, NodeItemEventArgs e)
        {
            NodeSliderItem slider = (NodeSliderItem)sender;
            int index = (int)slider.Tag;

            if (Node is MyUserInput)
            {
                (Node as MyUserInput).SetUserInput(index, (e.Item as NodeSliderItem).Value);
            }
        }
    }
}
