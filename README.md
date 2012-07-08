# VSHaskell - Visual Studio 2010 Haskell addin

This project provides a Visual Studio 2010 Haskell addin. The project was started by [Well Typed](http://www.well-typed.com/) with funding from Alexis Suzat. The code is all open-source under the BSD license.

## Installation

To install VSHaskell follow these steps:

1. Install Visual Studio 2010, any edition except Express. There are free trails available from [the Visual Studio website](http://www.microsoft.com/visualstudio/en-us/products/2010-editions/professional/overview).

1. Install [GHC 7.4.1](http://www.haskell.org/ghc/dist/7.4.1/ghc-7.4.1-i386-windows.exe). Later or earlier versions will not work. Make sure you leave "Add bin directories to PATH" checked.

1. Install the [VSHaskell addin](https://github.com/downloads/ndmitchell/VSHaskell/VSHaskell.vsix).

To test the addin, open a Haskell file (`.hs` extension) in Visual Studio 2010. There are two main features:

1. The Haskell will have syntax coloring. Keywords such as `module` should appear in blue.

1. The module will be type-checked as you type, and any errors will appear both in the Error List (display it from under the View menu) and with red wiggly lines in the editor.
