﻿
using GoodAI.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace GoodAI.Core.Versioning
{
    public class MyConversion : MyBaseConversion
    {
        public override int CurrentVersion
        {
            get { return 9; }
        }

        public static string Convert1To2(string xml)
        {
            //            dom = XDocument.Parse(xml);
            return xml;
        }

        /// <summary>
        /// Pascal's refactoring of MultiLayer Network Node (new namespace and location of tasks).
        /// </summary>  
        public static string Convert2To3(string xml)
        {
            string result = xml;

            result = result.Replace(
                "MyAbstractMultiLayerNetworkNode+MyInitTask",
                "Task.MyInitTask");

            result = result.Replace(
                "MyAbstractMultiLayerNetworkNode+MyForwardPropagationTask",
                "Task.MyForwardPropagationTask");

            result = result.Replace(
                "MyAbstractMultiLayerNetworkNode+MyBackwardPropagationTask",
                "Task.MyBackwardPropagationTask");

            result = result.Replace(
                "MyAbstractMultiLayerNetworkNode+MyComputeEnergyTask",
                "Task.MyComputeEnergyTask");

            return result;
        }

        /// <summary>
        /// Permutation class' member name changes
        /// Author: Martin Milota
        /// </summary>  
        public static string Convert3To4(string xml)
        {
            string result = xml;

            result = result.Replace(
                "VSA.MyCombinationBook+MyCodeVectorsTask",
                "VSA.MyCombinationBook+MyCombinationTask");

            result = result.Replace(
                "<Mode>Perm</Mode>",
                "<Mode>Permute</Mode>");

            result = result.Replace(
                "<Mode>Perm</Mode>",
                "<Mode>Permute</Mode>");

            return result;
        }

        /// <summary>
        /// Moving gameboy to a standalone module
        /// </summary>  
        public static string Convert4To5(string xml)
        {
            string result = xml;

            result = result.Replace(
                "Brainsimulator.GameBoy",
                "GameBoy");

            result = result.Replace(
                "BrainSimulator.MNIST",
                "MNIST");

            return result;
        }

        /// <summary>
        /// MyHiddenlayer and MyOutputLayer rename
        /// </summary>        
        public static string Convert5To6(string xml)
        {
            string result = xml;

            result = result.Replace(
                "BrainSimulator.NeuralNetwork.Layers.CommonTasks.",
                "BrainSimulator.NeuralNetwork.Layers.");

            result = result.Replace(
                "BrainSimulator.NeuralNetwork.Layers.MyHiddenLayerDeltaTask",
                "BrainSimulator.NeuralNetwork.Layers.MyCalcDeltaTask");

            result = result.Replace(
                "BrainSimulator.NeuralNetwork.Layers.MyOutputLayerDeltaTask",
                "BrainSimulator.NeuralNetwork.Layers.MyCalcDeltaTask");

            XDocument document = XDocument.Parse(result);

            XNamespace yaxlib = "http://www.sinairv.com/yaxlib/";
            XName realType = yaxlib + "realtype";

            IEnumerable<XElement> elements = document.XPathSelectElements("//MyOutputLayer");

            foreach (XElement e in elements)
            {
                e.Add(new XElement("ProvideTarget", "true"));
                e.SetAttributeValue(realType, "BrainSimulator.NeuralNetwork.Layers.MyLayer");
                e.Name = "MyLayer";
            }

            elements = document.XPathSelectElements("//MyHiddenLayer");

            foreach (XElement e in elements)
            {
                e.SetAttributeValue(realType, "BrainSimulator.NeuralNetwork.Layers.MyLayer");
                e.Name = "MyLayer";
            }

            return document.ToString();
        }

        /// <summary>
        /// convert BrainSimulator.FeedForward namespace to XmlFeedForwardNet
        /// </summary>        
        public static string Convert6To7(string xml)
        {
            string result = xml;

            result = result.Replace(
                "BrainSimulator.FeedForward.",
                "XmlFeedForwardNet.");

            return result;
        }

        /// <summary>
        /// convert QMatrix observer name
        /// </summary>        
        public static string Convert7To8(string xml)
        {
            string result = xml;

            result = result.Replace(".MyQMatrixObserver", ".MySRPObserver");

            return result;
        }

        /// <summary>
        /// ControllerNode renamed to PIDController
        /// </summary>        
        public static string Convert8To9(string xml)
        {
            string result = xml;

            result = result.Replace(".MyControllerNode", ".MyPIDController");

            return result;
        }
    }
}
