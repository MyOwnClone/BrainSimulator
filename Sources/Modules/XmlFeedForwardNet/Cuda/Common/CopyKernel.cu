#define _SIZE_T_DEFINED 
#ifndef __CUDACC__ 
#define __CUDACC__ 
#endif 
#ifndef __cplusplus 
#define __cplusplus 
#endif

#include <cuda.h> 
#include <device_launch_parameters.h> 
#include <texture_fetch_functions.h> 
#include <builtin_types.h> 
#include <vector_functions.h> 
#include <float.h>


extern "C"  
{
	//kernel code
	__global__ void CopyKernel(float *from, int fromOffset, float *to, int toOffset, int count)
	{
		int threadId = blockDim.x*blockIdx.y*gridDim.x	//rows preceeding current row in grid
				+ blockDim.x*blockIdx.x				//blocks preceeding current block
				+ threadIdx.x;
		
		if(threadId < count)
		{
			to[threadId + toOffset] = from[threadId + fromOffset];
		}

	}
}