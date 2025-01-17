﻿# GoodAI Brain Simulator

Brain Simulator is a collaborative platform for researchers, developers and high-tech companies to prototype and simulate artificial brain architecture, share knowledge, and exchange feedback.

The platform is designed to simplify collaboration, testing, and the implementation of new theories, and to easily visualize experiments and data. No mathematical or programming background is required to experiment with Brain Simulator modules.

Please keep in mind that Brain Simulator is still in the PROTOTYPE STAGE OF DEVELOPMENT. GoodAI will continuously improve the platform based on its own research advancement and user feedback.


## VS Solution Structure

Basic info for researches / node developers:

* **BasicNodes** – A place where you put your C# code and implement a wrapper class for your model
* **BasicNodesCuda** – A place to store your CUDA kernels which are needed for your model execution
* **BrainSimulator** – Simulation front-end, you will alter only configuration of here (hopefully, we can get rid this in future as well)
* **Core** – Core project, you need not to modify it at all
* **MNIST** - Module with MNIST world
* **XmlFeedForwardNet** - Module with feed-forward nets
* **XmlFeedForwardNetCuda** - CUDA kernels for feed-forward nets

