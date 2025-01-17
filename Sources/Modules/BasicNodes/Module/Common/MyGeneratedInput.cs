﻿using GoodAI.Core.Memory;
using GoodAI.Core.Nodes;
using GoodAI.Core.Utils;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YAXLib;
using GoodAI.Core.Task;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using GoodAI.Core;

namespace GoodAI.Modules.Common
{
    public enum MyGenerateType
    {
        Linear,
        Sine,
        Cosine,
        UserData,
        SimulationStep,
        SimulationStepFce,
    }

    /// <author>GoodAI</author>
    /// <meta>mb</meta>
    /// <status>Working</status>
    /// <summary>Samples a linear function values to the output array. 
    /// The output is shifted each step by ShiftSpeed parameter.
    /// </summary>
    /// <description></description>
    public class MyGenerateInput : MyWorkingNode
    {
        [MyOutputBlock]
        public MyMemoryBlock<float> Output
        {
            get { return GetOutput(0); }
            set { SetOutput(0, value); }
        }

        [YAXSerializableField(DefaultValue = 1), YAXElementFor("IO")]
        [MyBrowsable, Category("I/O")]
        public int OutputSize { get; set; }

        [YAXSerializableField(DefaultValue = 1)]
        [MyBrowsable, Category("I/O")]
        public int ColumnHint { get; set; }

        [YAXSerializableField(DefaultValue = "")]
        [MyBrowsable, Category("User Data Input")]
        public string UserInput
        {
            get { return m_userInput; }
            set
            {
                if (value.Length > 0)
                {
                    UserInput_parsed = value.Trim().Split(',', ' ').Select(a => float.Parse(a, CultureInfo.InvariantCulture)).ToList();
                    Output.Count = UserInput_parsed.Count;
                }
                m_userInput = value;
            }
        }
        private string m_userInput;
        List<float> UserInput_parsed;// = new List<float>();

        [YAXSerializableField(DefaultValue = MyGenerateType.Linear)]
        [MyBrowsable, Category("User Data Input")]
        public MyGenerateType GenerateType { get; set; }

        public override string Description
        {
            get
            {
                if (GenerateType == MyGenerateType.Linear)
                {
                    return base.Description;
                }
                else if (GenerateType == MyGenerateType.UserData)
                {
                    if (UserInput.Length > 10)
                    {
                        return UserInput.Substring(0, 10) + " ...";
                    }
                    else
                        return UserInput;
                }
                else if (GenerateType == MyGenerateType.SimulationStep)
                {
                    return "SimulStep";
                }
                else
                {
                    return "SimulStepFce";
                }
            }
        }

        public override void UpdateMemoryBlocks()
        {
            Output.ColumnHint = ColumnHint > 0 ? ColumnHint : 1;
            if (GenerateType == MyGenerateType.SimulationStepFce)
            {
                OutputSize = 1;
            }
            Output.Count = OutputSize;
            if (GenerateType == MyGenerateType.UserData)
            {
                Output.Count = UserInput_parsed != null ? UserInput_parsed.Count : OutputSize;
            }
        }

        public override void Validate(MyValidator validator)
        {
            validator.AssertError(OutputSize > 0, this, "Invalid OutputSize, must be at least 1");
            validator.AssertError(!(GenerateType == MyGenerateType.UserData && UserInput.Length == 0), this, "You need to enter some values to UserData");
        }

        public MyTransferTask GenerateInput { get; private set; }

        /// <summary></summary>
        [Description("Generate input")]
        public class MyTransferTask : MyTask<MyGenerateInput>
        {
            private MyCudaKernel m_kernel;

            [YAXSerializableField(DefaultValue = 0)]
            [MyBrowsable, Category("Interval"), DisplayName("M\tinValue")]
            public float MinValue { get; set; }

            [YAXSerializableField(DefaultValue = 1)]
            [MyBrowsable, Category("Interval")]
            public float MaxValue { get; set; }

            [YAXSerializableField(DefaultValue = 0)]
            [MyBrowsable, Category("Interval")]
            public int ShiftSpeed { get; set; }


            public MyTransferTask()
            {
            }

            public override void Init(Int32 nGPU)
            {
                switch (Owner.GenerateType)
                {
                    case MyGenerateType.Linear:
                        m_kernel = MyKernelFactory.Instance.Kernel(nGPU, @"Transforms\TransformKernels", "LinearValuesKernel");
                        break;
                }
            }

            public override void Execute()
            {

                switch (Owner.GenerateType)
                {
                    case MyGenerateType.Linear:
                        m_kernel.SetupExecution(Owner.OutputSize);
                        m_kernel.Run(MinValue, MaxValue, Owner.Output, Owner.OutputSize, ShiftSpeed * SimulationStep);
                        break;
                    case MyGenerateType.Sine:
                        Owner.Output.Host[0] = (float)Math.Sin(this.SimulationStep * 2 * Math.PI * Owner.UserInput_parsed[0]);
                        Owner.Output.SafeCopyToDevice();
                        break;
                    case MyGenerateType.Cosine:
                        Owner.Output.Host[0] = (float)Math.Cos(this.SimulationStep * 2 * Math.PI * Owner.UserInput_parsed[0]);
                        Owner.Output.SafeCopyToDevice();
                        break;
                    case MyGenerateType.UserData:
                        for (int a = 0; a < Owner.UserInput_parsed.Count; a++)
                        {
                            Owner.Output.Host[a] = Owner.UserInput_parsed[a];
                        }
                        Owner.Output.SafeCopyToDevice();
                        break;
                    case MyGenerateType.SimulationStep:
                        Owner.Output.Host[0] = SimulationStep;
                        Owner.Output.SafeCopyToDevice();
                        break;
                    case MyGenerateType.SimulationStepFce:
                        int stepMod = (int)SimulationStep % Owner.UserInput_parsed.Count;
                        Owner.Output.Host[0] = Owner.UserInput_parsed[stepMod];
                        Owner.Output.SafeCopyToDevice();
                        break;
                }
            }

        }

    }
}
