//Includes for IntelliSense 
#define _SIZE_T_DEFINED

#include <cuda.h>
#include <device_launch_parameters.h>
#include <texture_fetch_functions.h>
#include "float.h"
#include <builtin_types.h>
#include <vector_functions.h>
#include <math.h>

#include "..\Activation\ActivationFunction.cu"

extern "C"
{
	__global__ void FullyConnectedForwardKernel(
		ActivationFunctionEnum activationFunction,
		float *inputPtr,
		float *outputPtr,
		float *weightPtr,
		float *neuronInputPtr,
		float *biasPtr,
		float *dropoutMaskPtr,
		float dropout,
		int prevLayerSize,
		int thisLayerSize
		)
	{
		// i: prev. layer neuron id
		// j: current layer neuron id
		int i;
		int j = blockDim.x * blockIdx.y * gridDim.x	//rows preceeding current row in grid
			+ blockDim.x * blockIdx.x				//blocks preceeding current block
			+ threadIdx.x;

		if (j < thisLayerSize)
		{
			if (dropoutMaskPtr[j])
			{
				neuronInputPtr[j] = 0;
				outputPtr[j] = 0;
			}
			else
			{
				float sum = 0.0;
				int index = j;
				for (i = 0; i < prevLayerSize; i++) {
					sum += weightPtr[index] * inputPtr[i];
					index += thisLayerSize;
				}
				// add bias
				sum += biasPtr[j];

				// sum neuron input
				neuronInputPtr[j] = sum;

				// set output value
				outputPtr[j] = Evaluate(activationFunction, sum) / (1.0f - dropout);
			}
		}
	}

	__global__ void OneToOneForwardKernel(
		ActivationFunctionEnum activationFunction,
		float *inputPtr,
		float *outputPtr,
		int layerSize
		)
	{
		// i: neuron id
		int i = blockDim.x * blockIdx.y * gridDim.x	//rows preceeding current row in grid
			+ blockDim.x * blockIdx.x				//blocks preceeding current block
			+ threadIdx.x;

		if (i < layerSize)
			outputPtr[i] = Evaluate(activationFunction, inputPtr[i]);
	}
}