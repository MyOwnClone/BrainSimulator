﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoodAI.Core.Nodes;
using GoodAI.Core.Task;
using GoodAI.Core.Utils;
using GoodAI.Core.Memory;
using YAXLib;
using ManagedCuda;
using GoodAI.Modules.Transforms;

namespace GoodAI.Modules.SparseCoding
{
    /// <author>GoodAI</author>
    /// <meta>js</meta>
    /// <status>WIP</status>
    /// <summary>Encodes scalar values into Sparse Distributed Representation
    /// with the required properties for the CLA Node</summary>
    /// <description>n-tuple of scalar values is encoded into [n x LENGTH] binary matrix, where each
    /// row corresponds to its respective scalar value.
    /// The input scalar values have to be inside the interval [MIN, MAX], otherwise they are cropped
    /// to this interval (lower value than MIN is set to MIN, larger value than MAX is set to MAX).
    /// Parameters:<br />
    /// <ul>
    /// <li>MIN: minimal value of 1 input scalar value in the input vector</li>
    /// <li>MAX: maximal value of 1 input scalar value in the input vector</li>
    /// <li>LENGTH: length of the binary vector that encodes one scalar value</li>
    /// <li>ON_BITS_LENGTH: the number of bits equal to 1; should be around 2% of the LENGTH value</li>
    /// <li>RESOLUTION: read-only parameter - the smallest quantization step of input value that is preserved by the encoding</li>
    /// </ul>
    /// </description>
    [YAXSerializeAs("ScalarToSDR")]
    public class MyScalarToSDRNode : MyWorkingNode
    {
        [MyInputBlock]
        public MyMemoryBlock<float> Input
        {
            get { return GetInput(0); }
        }

        [MyOutputBlock]
        public MyMemoryBlock<float> Output
        {
            get { return GetOutput(0); }
            set { SetOutput(0, value); }
        }

        [MyBrowsable, Category("Params")]
        [YAXSerializableField(DefaultValue = -1), YAXElementFor("Structure")]
        public float MIN { get; set; }

        [MyBrowsable, Category("Params")]
        [YAXSerializableField(DefaultValue = 1), YAXElementFor("Structure")]
        public float MAX { get; set; }

        [MyBrowsable, Category("Params")]
        [YAXSerializableField(DefaultValue = 1024), YAXElementFor("Structure")]
        public int LENGTH { get; set; }

        [MyBrowsable, Category("Params")]
        [YAXSerializableField(DefaultValue = 20), YAXElementFor("Structure")]
        public int ON_BITS_LENGTH { get; set; }

        [MyBrowsable, Category("Params"), ReadOnly(true)]
        [YAXSerializableField(DefaultValue = 0.02f), YAXElementFor("Structure")]
        public float RESOLUTION { get; set; }

        public MyEncodeTask EncodeTask { get; protected set; }
        public MyDecodeTask DecodeTask { get; protected set; }

        [Description("Encode"), MyTaskInfo(OneShot = false)]
        public class MyEncodeTask : MyTask<MyScalarToSDRNode>
        {
            public override void Init(int nGPU)
            {
            }

            public override void Execute()
            {
                if (Owner.Input != null)
                {
                    Owner.Input.SafeCopyToHost();
                    for (int i = 0; i < Owner.Input.Count; i++)
                    {
                        float input = Owner.Input.Host[i];
                        // crop input into the <MIN, MAX> interval
                        if (input < Owner.MIN)
                        {
                            input = Owner.MIN;
                        }
                        else if (input > Owner.MAX)
                        {
                            input = Owner.MAX;
                        }

                        int onBitsBlockStartIndex = (int)Math.Round((input - Owner.MIN) / Owner.RESOLUTION);
                        for (int j = 0; j < Owner.LENGTH; j++)
                        {
                            int id = i * Owner.LENGTH + j;
                            // set continuous block of ON_BITS_LENGTH bits to 1
                            if ((j >= onBitsBlockStartIndex) && (j < onBitsBlockStartIndex + Owner.ON_BITS_LENGTH))
                            {
                                Owner.Output.Host[id] = 1;
                            }
                            else
                            {
                                Owner.Output.Host[id] = 0;
                            }
                        }

                    }
                    Owner.Output.SafeCopyToDevice();
                }
            }
        }

        // Decodes the most probable value from several ORed encodings together
        // uses sliding window of length ON_BITS_LENGTH to find the most dense cluster of on bits
        [Description("Decode"), MyTaskInfo(OneShot = false, Disabled = true)]
        public class MyDecodeTask : MyTask<MyScalarToSDRNode>
        {
            public override void Init(int nGPU)
            {
            }

            public override void Execute()
            {
                if (Owner.Input != null)
                {
                    int numberOfRows = Owner.Input.Count / Owner.LENGTH;
                    Owner.Input.SafeCopyToHost();
                    for (int row = 0; row < numberOfRows; row++)
                    {
                        Owner.Output.Host[row] = 0;
                        int slidingWindowMax = 0;
                        int slidingWindowMaxPos = 0;
                        for (int slidingWindowPos = 0; slidingWindowPos < Owner.LENGTH - Owner.ON_BITS_LENGTH; slidingWindowPos++)
                        {
                            int slidingWindowCurrent = 0;

                            // integrate ower the sliding window
                            for (int i = 0; i < Owner.ON_BITS_LENGTH; i++)
                            {
                                // check if the bit is 1, it is stored as a float value, 
                                // lets consider value above 0.5 as an "on" bit
                                if (Owner.Input.Host[row * Owner.LENGTH + slidingWindowPos + i] > 0.5)
                                {
                                    slidingWindowCurrent++;
                                }
                                if (slidingWindowCurrent > slidingWindowMax)
                                {
                                    slidingWindowMax = slidingWindowCurrent;
                                    slidingWindowMaxPos = slidingWindowPos;
                                }

                            }
                        }

                        Owner.Output.Host[row] = Owner.MIN + slidingWindowMaxPos * Owner.RESOLUTION;

                    }
                    Owner.Output.SafeCopyToDevice();
                }
            }
        }


        public override void UpdateMemoryBlocks()
        {
            if (Input != null)
            {
                if (EncodeTask.Enabled)
                {
                    Output.Count = LENGTH * Input.Count;
                    Output.ColumnHint = LENGTH;
                }
                else if (DecodeTask.Enabled)
                {
                    Output.Count = Input.Count / LENGTH;
                }
            }
            RESOLUTION = (MAX - MIN) / (LENGTH - ON_BITS_LENGTH);
        }

        public override void Validate(MyValidator validator)
        {
            base.Validate(validator);
            validator.AssertError(Input != null, this, "Input connection missing!");
            validator.AssertError(LENGTH > 0, this, "The length of encoded output have to be larger than 0!");
            validator.AssertError(ON_BITS_LENGTH < LENGTH, this, "The value ON_BITS_LENGTH should be smaller than LENGTH (around 2% of the LENGTH value is recomended)!");
        }
    }
}
