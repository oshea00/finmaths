# Finmaths Project

## Approach
This is a beginning on creating C# tools similar to those that can be found in Python for financial engineering, machine learning and scientific computing. The python ecosystem is rich with machine learning and scientific computing libraries, such as SciKit, SciPy, Pandas, Matplotlib, and Pylab, while the prevalence of open source equivalents for C# developers is fairly small. There are, however, some alternatives that look promising.


### C# Tools 
This project is using some open source tools for C#, Namely: 
- Accord.Math (a scipy and numpy-like tools)
- Accord.MachineLearning (a sklearn-like tool)
- Accord.Controls (a matplotlib-like tool)
- Deedle (A pandas-like dataframe tool)

## Goals
Current the goal is to be able to explore financial topics of interest and be able to implement algorithms, visualize and manipulate data, train ML models, do regression and optimization in a style that closely matches the experience found in Jupyter notebooks.

## Financial Data
In this initial project, I've chosen to use the IEX Exchange's cloud API for retrieving current equity and price data. (see iexcloud.io). This will likely expand into a more general .Net Standard library to interface with the entire IEX cloud api.

## Initial Steps
In order to put a baseline set of tools/calcs together, I've chosen to implement an MVO (Mean Variance Optimization) solver, along with some ideas from Black-Litterman that shortcomings of vanilla-MVO.

Challenges so far:
- SciPy SQSLP optimizer is hard to beat. Accord has COBLYA derivative-free, constraints-based, nonlinear solver that comes close, but there's an obvious lack of open-source C# solvers in this area. There are many promising C++ ones that could be ported/interfaced. This is an area I'm currently researching and looking at various open source projects to collaborate with.


